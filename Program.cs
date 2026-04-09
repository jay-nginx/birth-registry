using BirthRegistry.Data;
using BirthRegistry.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;

// ── Datadog: must be called BEFORE the host is built.
// This initialises the CLR profiler compatibility layer required for
// automatic instrumentation in the Azure Functions isolated worker.
Datadog.Serverless.CompatibilityLayer.Start();

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()         // ASP.NET Core integration for isolated worker
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;

        // ── Database ──────────────────────────────────────────────────────────
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

        // ── Application services ───────────────────────────────────────────────
        services.AddScoped<IBirthRegistryService, BirthRegistryService>();

        // ── Logging (Serilog → JSON → stdout, consumed by Datadog log forwarder)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", "ociofunctionone")
            .Enrich.WithProperty("env", config["DD_ENV"] ?? "dev")
            .WriteTo.Console(new CompactJsonFormatter())   // structured JSON for Datadog
            .CreateLogger();

        services.AddLogging(lb =>
        {
            lb.ClearProviders();
            lb.AddSerilog(Log.Logger, dispose: true);
        });

        // Datadog is the sole observability provider — Application Insights not used.
    })
    .Build();

// ── Create database schema on startup ────────────────────────────────────────
// EnsureCreated lets EF Core use provider-correct types (SQL Server → uniqueidentifier/nvarchar,
// SQLite → TEXT). Using Migrate() with the hand-written SQLite migration breaks SQL Server
// because TEXT cannot be a primary key there.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BirthRegistryDbContext>();
    db.Database.EnsureCreated();
}

await host.RunAsync();
