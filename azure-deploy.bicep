// azure-deploy.bicep
// Deploys: Consumption Plan (Linux, Y1/Dynamic — cheapest) + Function App (.NET 8 isolated) + Azure SQL
// Usage: az deployment group create --resource-group <rg> --template-file azure-deploy.bicep --parameters @azure-deploy.params.json

@description('Base name used for all resources')
param baseName string = 'birthregistry'

@description('Azure region')
param location string = resourceGroup().location

@description('SQL Server admin login')
param sqlAdminLogin string

@secure()
@description('SQL Server admin password')
param sqlAdminPassword string

@description('Datadog site (e.g. datadoghq.com or datadoghq.eu)')
param datadogSite string = 'datadoghq.com'

// ── Storage account (required by Azure Functions) ────────────────────────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: take('br${uniqueString(resourceGroup().id)}', 24)
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}

// ── App Service Plan — Linux Consumption (Y1/Dynamic, cheapest option) ──────
// Cost: ~$0 for the first 1M executions/month, then ~$0.20 per million after that.
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${baseName}-plan'
  location: location
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true   // required for Linux
  }
}

// ── Azure SQL Server + Database ──────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2023-02-01-preview' = {
  name: '${baseName}-sql-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
  }
}

resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-02-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-02-01-preview' = {
  parent: sqlServer
  name: '${baseName}-db'
  location: location
  sku: {
    name: 'GP_S_Gen5_1'   // Serverless tier — cost-efficient
    tier: 'GeneralPurpose'
  }
  properties: {
    autoPauseDelay: 60     // auto-pause after 1 hour idle
    minCapacity: 1
  }
}

// ── Function App ─────────────────────────────────────────────────────────────
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${baseName}-func'
  location: location
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: appServicePlan.id
    reserved: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        { name: 'AzureWebJobsStorage',              value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value}' }
        { name: 'FUNCTIONS_EXTENSION_VERSION',      value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME',         value: 'dotnet-isolated' }
        { name: 'DatabaseProvider',                 value: 'SqlServer' }
        { name: 'ConnectionStrings__BirthRegistry', value: 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${sqlDatabase.name};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;' }

        // ── Datadog: core settings ────────────────────────────────────────────
        { name: 'DD_API_KEY',                       value: '<YOUR_DATADOG_API_KEY>' }
        { name: 'DD_SITE',                          value: datadogSite }
        { name: 'DD_ENV',                           value: 'dev' }
        { name: 'DD_SERVICE',                       value: 'ociofunctionone' }
        { name: 'DD_VERSION',                       value: '1.0.11' }
        { name: 'DD_LOGS_INJECTION',                value: 'true' }
        { name: 'DD_RUNTIME_METRICS_ENABLED',       value: 'true' }
        { name: 'DD_TRACE_SAMPLE_RATE',             value: '1.0' }

        // ── Datadog: CLR auto-instrumentation (required for traces/spans) ────
        // The Datadog.AzureFunctions NuGet package extracts the native profiler
        // to /home/site/wwwroot/datadog at deploy time.
        { name: 'CORECLR_ENABLE_PROFILING',         value: '1' }
        { name: 'CORECLR_PROFILER',                 value: '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}' }
        { name: 'CORECLR_PROFILER_PATH',            value: '/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so' }
        { name: 'DD_DOTNET_TRACER_HOME',            value: '/home/site/wwwroot/datadog' }

        // ── Datadog: log forwarding ───────────────────────────────────────────
        // Logs flow stdout → Azure Log Stream → Datadog via the Azure integration.
        // Configure the Datadog Azure integration at app.datadoghq.com > Integrations > Azure.
      ]
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
    }
    httpsOnly: true
  }
}

output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
