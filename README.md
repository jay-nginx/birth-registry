# Birth Registry — Azure Functions + Datadog Observability

A .NET 8 isolated Azure Function app for registering newborn births, with full Datadog APM tracing and log forwarding.

**Live demo:** https://birthregistry-func.azurewebsites.net/api/register

---

## Table of Contents

1. [Architecture](#architecture)
2. [Prerequisites](#prerequisites)
3. [Deploy to Azure](#deploy-to-azure)
4. [Configure Datadog Observability](#configure-datadog-observability)
   - [Step 1 — Enable the Azure Integration](#step-1--enable-the-azure-integration)
   - [Step 2 — Enable Log Forwarding](#step-2--enable-log-forwarding)
   - [Step 3 — APM Traces](#step-3--apm-traces)
   - [Step 4 — Verify in Datadog](#step-4--verify-in-datadog)
5. [Run Locally](#run-locally)
6. [Endpoints](#endpoints)
7. [Un-deploy from Azure](#un-deploy-from-azure)

---

## Architecture

```
Browser → Azure Function App (.NET 8 Isolated, Linux Consumption)
               ↓
         Azure SQL (Serverless Gen5)
               ↓
         Datadog APM  ←  CLR Profiler (Datadog.AzureFunctions NuGet)
         Datadog Logs ←  stdout JSON → Azure Log Stream → DD Forwarder
```

**Stack:**
- Runtime: .NET 8 Isolated Worker
- Hosting: Azure Functions v4, Linux Consumption (Y1/Dynamic) — cheapest tier
- Database: Azure SQL Serverless Gen5
- Observability: Datadog APM + Log Forwarding via Azure Integration

---

## Prerequisites

Install these tools once:

```bash
# Azure CLI
brew install azure-cli                        # macOS
# Windows: https://aka.ms/installazurecliwindows

# Azure Functions Core Tools v4
brew tap azure/functions
brew install azure-functions-core-tools@4

# .NET 8 SDK (no sudo required — installs to ~/.dotnet)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"             # add to ~/.zshrc or ~/.bashrc permanently

# GitHub CLI (for pushing to GitHub)
brew install gh

# Verify all tools
az --version        # ≥ 2.50
func --version      # 4.x
dotnet --version    # 8.x
```

Log in:

```bash
az login            # opens browser
gh auth login       # follow prompts
```

---

## Deploy to Azure

### 1. Create a Resource Group

```bash
az group create \
  --name rg-birthregistry \
  --location westeurope
```

### 2. Deploy Infrastructure (Bicep)

The Bicep template provisions: Storage Account, Consumption Plan (Linux), Function App, SQL Server, SQL Database.

```bash
az deployment group create \
  --resource-group rg-birthregistry \
  --template-file azure-deploy.bicep \
  --parameters @azure-deploy.params.json \
  --parameters sqlAdminPassword="<STRONG_PASSWORD>"
```

> This takes ~3–5 minutes. On completion the outputs show `functionAppName` and `functionAppUrl`.

### 3. Publish the Function App

```bash
export PATH="$HOME/.dotnet:$PATH"

func azure functionapp publish birthregistry-func --dotnet-isolated
```

Expected output:
```
Deployment completed successfully.
Functions in birthregistry-func:
    GetRegistrationForm    - [httpTrigger]  /api/register
    SubmitRegistrationForm - [httpTrigger]  /api/register
    SearchForm             - [httpTrigger]  /api/search
    GetRecord              - [httpTrigger]  /api/records/{id}
    HealthCheck            - [httpTrigger]  /api/health
    UpdateStatus           - [httpTrigger]  /api/admin/records/{id}/status
    DeleteRecord           - [httpTrigger]  /api/admin/records/{id}
```

### 4. Verify

```bash
curl https://birthregistry-func.azurewebsites.net/api/health
# → {"status":"healthy","timestamp":"...","service":"birth-registry"}
```

Open the registration form:
```
https://birthregistry-func.azurewebsites.net/api/register
```

---

## Configure Datadog Observability

### Step 1 — Enable the Azure Integration

This is the foundation — it lets Datadog pull metrics and route logs from your Azure subscription.

1. Go to **Datadog → Integrations → Azure**
2. Click **Add New App Registration**
3. In the Azure portal, create an App Registration:
   ```bash
   # Create the service principal Datadog will use
   az ad sp create-for-rbac \
     --name "datadog-integration" \
     --role "Monitoring Reader" \
     --scopes "/subscriptions/<YOUR_SUBSCRIPTION_ID>"
   ```
   Note down: `appId` (Client ID), `password` (Client Secret), `tenant` (Tenant ID)
4. Back in Datadog, fill in:
   - **Tenant ID** — from step above
   - **Client ID** — `appId` from step above
   - **Client Secret** — `password` from step above
   - **Subscription ID** — your Azure subscription ID
5. Click **Submit**

---

### Step 2 — Enable Log Forwarding

Logs from the Function App (written to stdout as JSON by Serilog) need to be forwarded to Datadog. There are two ways — use Option A for the simplest setup.

#### Option A — Datadog Forwarder (recommended for Functions)

1. Deploy the Datadog Forwarder ARM template into your Azure subscription:
   ```bash
   az deployment group create \
     --resource-group rg-birthregistry \
     --template-uri "https://raw.githubusercontent.com/DataDog/datadog-serverless-functions/master/azure/deploy.json" \
     --parameters apiKey="<YOUR_DATADOG_API_KEY>" \
     --parameters datadogSite="datadoghq.com"
   ```

2. In the Azure portal, go to your Function App → **Diagnostic settings → + Add diagnostic setting**:
   - Check: **Function Application Logs** and **All Metrics**
   - Destination: **Send to Log Analytics workspace** OR **Stream to an event hub**
   - If using Event Hub, configure the Datadog Forwarder to consume from it

3. Alternatively, use **Azure Monitor → Diagnostic Settings** on the Function App and route to the Event Hub that the Datadog Forwarder is listening on.

#### Option B — Azure Integration Log Collection (simpler, slight delay)

1. In Datadog → **Integrations → Azure** → click your integration
2. Go to the **Log Collection** tab
3. Enable **Collect logs from all defined resources** or add a filter for `rg-birthregistry`
4. Logs appear in Datadog within ~5 minutes

---

### Step 3 — APM Traces

APM tracing is handled by the `Datadog.AzureFunctions` NuGet package combined with the CLR profiler environment variables. These are already set in `azure-deploy.bicep` and were applied during the Bicep deployment. No manual portal steps needed.

The 4 required CLR profiler app settings (already configured):

| Setting | Value |
|---|---|
| `CORECLR_ENABLE_PROFILING` | `1` |
| `CORECLR_PROFILER` | `{846F5F1C-F9AE-4B07-969E-05C26BC060D8}` |
| `CORECLR_PROFILER_PATH` | `/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so` |
| `DD_DOTNET_TRACER_HOME` | `/home/site/wwwroot/datadog` |

Plus the service identification settings (already configured):

| Setting | Value |
|---|---|
| `DD_API_KEY` | `<YOUR_DATADOG_API_KEY>` |
| `DD_SITE` | `datadoghq.com` |
| `DD_SERVICE` | `ociofunctionone` |
| `DD_ENV` | `dev` |
| `DD_VERSION` | `1.0.11` |
| `DD_LOGS_INJECTION` | `true` |
| `DD_RUNTIME_METRICS_ENABLED` | `true` |

If you ever need to add these manually:
```bash
az functionapp config appsettings set \
  --name birthregistry-func \
  --resource-group rg-birthregistry \
  --settings \
    "CORECLR_ENABLE_PROFILING=1" \
    "CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}" \
    "CORECLR_PROFILER_PATH=/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so" \
    "DD_DOTNET_TRACER_HOME=/home/site/wwwroot/datadog" \
    "DD_API_KEY=<YOUR_DATADOG_API_KEY>" \
    "DD_SITE=datadoghq.com" \
    "DD_SERVICE=ociofunctionone" \
    "DD_ENV=dev" \
    "DD_VERSION=1.0.11" \
    "DD_LOGS_INJECTION=true" \
    "DD_RUNTIME_METRICS_ENABLED=true"
```

> **Important:** Do NOT set `DD_TRACE_AGENT_URL` or `DD_AGENT_HOST` for serverless — traces go direct to Datadog, not via a local agent.

---

### Step 4 — Verify in Datadog

After hitting the app a few times:

| What to check | Where in Datadog |
|---|---|
| APM traces | **APM → Services → ociofunctionone** |
| Individual traces/spans | **APM → Traces** → filter `service:ociofunctionone` |
| Logs | **Logs** → filter `service:ociofunctionone env:dev` |
| Runtime metrics (GC, threads) | **APM → Services → ociofunctionone → JVM/Runtime** tab |
| Infrastructure metrics | **Infrastructure → Azure** |

Traces should appear within ~30 seconds of the first request. Logs may take 1–5 minutes depending on the forwarding method used.

---

## Run Locally

```bash
# Clone and enter the project
git clone https://github.com/jay-nginx/birth-registry.git
cd birth-registry

# Add .dotnet to PATH if installed via install script
export PATH="$HOME/.dotnet:$PATH"

# Install Azurite (local Azure Storage emulator)
npm install -g azurite
azurite --silent &

# Run (uses SQLite locally — no SQL Server needed)
func start
```

Open http://localhost:7071/api/register

> Local tracing requires a Datadog Agent running locally (`docker run -d -p 8126:8126 datadog/agent`). Without it, the app runs fine but traces won't be sent anywhere.

---

## Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/api/register` | Anonymous | Registration form |
| `POST` | `/api/register` | Anonymous | Submit registration |
| `GET` | `/api/search` | Anonymous | Search form + results |
| `GET` | `/api/records/{id}` | Anonymous | View record by GUID or `BR-YYYY-XXXXXX` |
| `GET` | `/api/health` | Anonymous | Health check (JSON) |
| `PATCH` | `/api/admin/records/{id}/status` | Function key | Update status |
| `DELETE` | `/api/admin/records/{id}` | Function key | Delete record |

Admin endpoints require the Function host key in the `x-functions-key` header:
```bash
# Get the host key
az functionapp keys list \
  --name birthregistry-func \
  --resource-group rg-birthregistry \
  --query "functionKeys"

# Use it
curl -X PATCH https://birthregistry-func.azurewebsites.net/api/admin/records/<id>/status \
  -H "x-functions-key: <HOST_KEY>" \
  -H "Content-Type: application/json" \
  -d '{"status":"Verified"}'
```

---

## Un-deploy from Azure

### Option A — Delete everything (recommended after testing)

One command removes all resources: Function App, SQL Server, Storage Account, App Service Plan.

```bash
# Preview what will be deleted
az resource list --resource-group rg-birthregistry --output table

# Delete the resource group and ALL resources inside it
az group delete \
  --name rg-birthregistry \
  --yes \
  --no-wait

# Confirm deletion (returns "not found" when complete, ~2–3 min)
az group show --name rg-birthregistry
```

### Option B — Delete individual resources (keep the resource group)

```bash
# 1. Function App
az functionapp delete \
  --name birthregistry-func \
  --resource-group rg-birthregistry

# 2. App Service Plan
az appservice plan delete \
  --name birthregistry-plan \
  --resource-group rg-birthregistry --yes

# 3. SQL Server (drops all databases under it too)
az sql server delete \
  --name birthregistry-sql-<uniqueid> \
  --resource-group rg-birthregistry --yes

# 4. Storage Account
az storage account delete \
  --name br<uniqueid> \
  --resource-group rg-birthregistry --yes
```

### Clean up the Datadog Azure Integration (optional)

1. Go to **Datadog → Integrations → Azure**
2. Click the integration tile → **Delete**
3. In Azure, remove the App Registration created for Datadog:
   ```bash
   az ad app delete --id <datadog-app-registration-client-id>
   ```

---

## Cost

| Resource | Tier | Estimated cost |
|---|---|---|
| Function App | Consumption (Y1/Dynamic) | ~$0 (first 1M executions free/month) |
| Azure SQL | Serverless Gen5, 1 vCore | ~$0.30/day active; auto-pauses after 1hr idle |
| Storage Account | Standard LRS | ~$0.02/GB/month |
| **Total (testing)** | | **< $1 for a short test session** |

> Run `az group delete` as soon as you finish testing to stop all billing.
