using BirthRegistry.Data;
using BirthRegistry.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Datadog.Logs;

// Datadog: must be called BEFORE the host is built.
// Initialises the CLR profiler compatibility layer for auto-instrumentation.
// Wrapped in try/catch so a missing native profiler never crashes the app.
try { Datadog.Serverless.CompatibilityLayer.Start(); }
catch (Exception ex) { Console.WriteLine($"[Datadog] CompatibilityLayer.Start skipped: {ex.Message}"); }

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;

        // ── Database ─────────────────────────────────────────────────────────
        var provider = config["DatabaseProvider"] ?? "Sqlite";
        var connStr  = config.GetConnectionString("BirthRegistry")
                       ?? "Data Source=birthregistry.db";

        services.AddDbContext<BirthRegistryDbContext>(opts =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                opts.UseSqlServer(connStr);
            else
                opts.UseSqlite(connStr);
        });

        // ── Application services ──────────────────────────────────────────────
        services.AddScoped<IBirthRegistryService, BirthRegistryService>();

        // ── Logging: Serilog → Console (stdout) + Datadog HTTP intake ────────
        // The Datadog.Logs sink sends directly to https://http-intake.logs.datadoghq.com
        // No agent, no Azure integration, no forwarder required.
        var ddApiKey = config["DD_API_KEY"] ?? "";
        var ddService = config["DD_SERVICE"] ?? "ociofunctionone";
        var ddEnv     = config["DD_ENV"]     ?? "dev";
        var ddVersion = config["DD_VERSION"] ?? "1.0.11";

        // Url = host only — the sink prepends "https://" and appends "/api/v2/logs" internally
        var ddConfig  = new DatadogConfiguration
        {
            Url    = "http-intake.logs.datadoghq.com",
            Port   = 443,
            UseSSL = true,
            UseTCP = false
        };

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", ddService)
            .Enrich.WithProperty("env",     ddEnv)
            .Enrich.WithProperty("version", ddVersion)
            .Enrich.WithProperty("ddsource", "csharp")
            // Keep stdout for Azure log stream / local dev
            .WriteTo.Console(new CompactJsonFormatter())
            // Send directly to Datadog Logs HTTP intake — no agent needed
            .WriteTo.DatadogLogs(
                apiKey:  ddApiKey,
                service: ddService,
                source:  "csharp",
                host:    Environment.MachineName,
                tags:    new[] { $"env:{ddEnv}", $"version:{ddVersion}" },
                configuration: ddConfig
            )
            .CreateLogger();

        services.AddLogging(lb =>
        {
            lb.ClearProviders();
            lb.AddSerilog(Log.Logger, dispose: true);
        });
    })
    .Build();

// ── Create / migrate schema on startup ───────────────────────────────────────
try
{
    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BirthRegistryDbContext>();
    db.Database.EnsureCreated();
    try
    {
        db.GetInfrastructure()
          .GetRequiredService<IRelationalDatabaseCreator>()
          .CreateTables();
    }
    catch { /* tables already exist — safe to ignore */ }
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] DB schema init failed (will retry on first request): {ex.Message}");
}

await host.RunAsync();

// Flush any buffered logs before the process exits (important on Consumption plan)
Log.CloseAndFlush();
