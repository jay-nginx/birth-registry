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
        { name: 'ConnectionStrings__People',        value: 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${sqlDatabase.name};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;' }
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
