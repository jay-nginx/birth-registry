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

        // ── Logging: Serilog → stdout (JSON) ─────────────────────────────────
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(new CompactJsonFormatter())
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

Log.CloseAndFlush();
