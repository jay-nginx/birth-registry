# Datadog Integration Reference

All Datadog code has been removed from the app. This file contains everything
needed to add it back.

---

## 1. NuGet Packages

Add to `BirthRegistry.Functions.csproj`:

```xml
<!-- Datadog APM — bundles the native CLR profiler for auto-instrumentation -->
<PackageReference Include="Datadog.AzureFunctions" Version="2.57.0" />

<!-- Datadog Log sink — ships logs directly to Datadog HTTP intake, no agent needed -->
<PackageReference Include="Serilog.Sinks.Datadog.Logs" Version="0.5.2" />
```

---

## 2. Program.cs

Add the using statements:

```csharp
using Serilog.Sinks.Datadog.Logs;
```

Call the compatibility layer **before** `HostBuilder` is built:

```csharp
try { Datadog.Serverless.CompatibilityLayer.Start(); }
catch (Exception ex) { Console.WriteLine($"[Datadog] CompatibilityLayer.Start skipped: {ex.Message}"); }
```

Replace the Serilog configuration inside `ConfigureServices`:

```csharp
var ddApiKey  = config["DD_API_KEY"]  ?? "";
var ddService = config["DD_SERVICE"]  ?? "my-service";
var ddEnv     = config["DD_ENV"]      ?? "dev";
var ddVersion = config["DD_VERSION"]  ?? "1.0.0";

var ddConfig = new DatadogConfiguration
{
    Url    = "http-intake.logs.datadoghq.com",
    Port   = 443,
    UseSSL = true,
    UseTCP = false
};

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service",  ddService)
    .Enrich.WithProperty("env",      ddEnv)
    .Enrich.WithProperty("version",  ddVersion)
    .Enrich.WithProperty("ddsource", "csharp")
    .WriteTo.Console(new CompactJsonFormatter())
    .WriteTo.DatadogLogs(
        apiKey:        ddApiKey,
        service:       ddService,
        source:        "csharp",
        host:          Environment.MachineName,
        tags:          new[] { $"env:{ddEnv}", $"version:{ddVersion}" },
        configuration: ddConfig
    )
    .CreateLogger();
```

Add `Log.CloseAndFlush()` at the very end of `Program.cs` (after `host.RunAsync()`):

```csharp
Log.CloseAndFlush();
```

---

## 3. Manual Spans (Service Layer)

Add using to any service file:

```csharp
using Datadog.Trace;
```

Wrap operations in a custom span:

```csharp
using var scope = Tracer.Instance.StartActive("operation-name");
scope.Span.SetTag("my.tag", someValue);

// ... your code ...

scope.Span.SetTag("result.count", results.Count.ToString());
```

Example from `BirthRegistryService.cs`:

```csharp
public async Task<BirthRecord> RegisterBirthAsync(BirthRecordDto dto)
{
    using var scope = Tracer.Instance.StartActive("birth-registry.register");
    scope.Span.SetTag("child.name", $"{dto.ChildFirstName} {dto.ChildLastName}");
    scope.Span.SetTag("hospital", dto.HospitalName ?? "unknown");

    // ... save logic ...

    scope.Span.SetTag("registration.number", record.RegistrationNumber);
    return record;
}
```

---

## 4. Azure App Settings

### local.settings.json (local dev)

```json
"DD_API_KEY":                  "<your-api-key>",
"DD_SITE":                     "datadoghq.com",
"DD_ENV":                      "dev",
"DD_SERVICE":                  "my-service",
"DD_VERSION":                  "1.0.0",
"DD_LOGS_INJECTION":           "true",
"DD_RUNTIME_METRICS_ENABLED":  "true",
"DD_TRACE_SAMPLE_RATE":        "1.0",
"CORECLR_ENABLE_PROFILING":    "1",
"CORECLR_PROFILER":            "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}",
"CORECLR_PROFILER_PATH":       "/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
"DD_DOTNET_TRACER_HOME":       "/home/site/wwwroot/datadog"
```

### azure-deploy.bicep — add parameter + app settings

Add parameter:

```bicep
@description('Datadog site (e.g. datadoghq.com or datadoghq.eu)')
param datadogSite string = 'datadoghq.com'
```

Add to the `appSettings` array in the Function App resource:

```bicep
{ name: 'DD_API_KEY',                value: '<YOUR_DATADOG_API_KEY>' }
{ name: 'DD_SITE',                   value: datadogSite }
{ name: 'DD_ENV',                    value: 'dev' }
{ name: 'DD_SERVICE',                value: 'my-service' }
{ name: 'DD_VERSION',                value: '1.0.0' }
{ name: 'DD_LOGS_INJECTION',         value: 'true' }
{ name: 'DD_RUNTIME_METRICS_ENABLED',value: 'true' }
{ name: 'DD_TRACE_SAMPLE_RATE',      value: '1.0' }
{ name: 'CORECLR_ENABLE_PROFILING',  value: '1' }
{ name: 'CORECLR_PROFILER',          value: '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}' }
{ name: 'CORECLR_PROFILER_PATH',     value: '/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so' }
{ name: 'DD_DOTNET_TRACER_HOME',     value: '/home/site/wwwroot/datadog' }
```

---

## 5. What each setting does

| Setting | Purpose |
|---|---|
| `DD_API_KEY` | Authenticates with Datadog |
| `DD_SITE` | Datadog region (`datadoghq.com` = US, `datadoghq.eu` = EU) |
| `DD_SERVICE` | Service name shown in APM and logs |
| `DD_ENV` | Environment tag (`dev`, `staging`, `prod`) |
| `DD_VERSION` | Version tag for deployment tracking |
| `DD_LOGS_INJECTION` | Injects trace/span IDs into log lines for log↔trace correlation |
| `DD_RUNTIME_METRICS_ENABLED` | Sends .NET CLR metrics (GC, heap, threads) automatically |
| `DD_TRACE_SAMPLE_RATE` | `1.0` = trace 100% of requests |
| `CORECLR_ENABLE_PROFILING` | Activates the native CLR profiler |
| `CORECLR_PROFILER` | CLSID of the Datadog profiler |
| `CORECLR_PROFILER_PATH` | Path to the native `.so` bundled by `Datadog.AzureFunctions` |
| `DD_DOTNET_TRACER_HOME` | Directory where the tracer home files live |
