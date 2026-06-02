# PatchMindAI Azure Deployment Checklist

Use this checklist to move PatchMindAI from local to Azure staging/production safely.

## 1. Prerequisites

- [ ] Azure subscription with required quota for App Service, Azure AI Search, Azure OpenAI, Service Bus, Redis.
- [ ] Azure CLI logged in (`az login`) and correct subscription selected.
- [ ] Resource naming convention agreed (for `stg` and `prod`).
- [ ] Git repository configured with CI/CD secrets or federated credentials.

## 2. Resource Provisioning

- [ ] Create resource group for staging.
- [ ] Create resource group for production.
- [ ] Provision Azure OpenAI resource and model deployment.
- [ ] Provision Azure AI Search service and index.
- [ ] Provision Azure Service Bus namespace and queue (`cve-analysis-jobs`).
- [ ] Provision Azure Cache for Redis.
- [ ] Provision App Service Plan and two Web Apps:
  - [ ] API app (PatchMindAI.API)
  - [ ] Web app (PatchMindAI.Web)
- [ ] Provision Key Vault for secrets.
- [ ] Provision Application Insights (recommended).

## 3. Identity and Access (Managed Identity)

For each app (API and Web):

- [ ] Enable system-assigned managed identity.
- [ ] Grant API app managed identity:
  - [ ] Cognitive Services OpenAI User (Azure OpenAI resource).
  - [ ] Search Index Data Reader (Azure AI Search service).
  - [ ] Azure Service Bus Data Sender/Receiver (namespace as needed).
  - [ ] Key Vault Secrets User (if using Key Vault references).
- [ ] Grant Web app managed identity:
  - [ ] Key Vault Secrets User (if using Key Vault references).

## 4. Data and Index Readiness

- [ ] Create Azure Search index schema aligned to:
  - `id` (key)
  - `cveId`
  - `title`
  - `content`
  - `severity`
  - `baseScore`
  - `affectedProducts`
  - `publishedAtUtc`
  - `lastModifiedAtUtc`
  - `references`
- [ ] Use sample shape from `src/PatchMindAI.Infrastructure/SeedData/azuresearch-cve-sample-document.json`.
- [ ] Load preloaded NVD rows into the index.
- [ ] Verify search by CVE id and plain-language query.

## 5. App Configuration (Per Environment)

API app settings (App Service Configuration):

- [ ] `ASPNETCORE_ENVIRONMENT` = `Staging` or `Production`
- [ ] `ConnectionStrings__PatchMindAIDb` set to target DB path/connection.
- [ ] `AzureOpenAI__Endpoint`
- [ ] `AzureOpenAI__DeploymentName`
- [ ] `AzureOpenAI__Model`
- [ ] `AzureOpenAI__UseManagedIdentity` = `true`
- [ ] `AzureOpenAI__ApiKey` empty or omitted
- [ ] `AzureSearch__Endpoint`
- [ ] `AzureSearch__IndexName`
- [ ] `AzureSearch__UseManagedIdentity` = `true`
- [ ] `AzureSearch__ApiKey` empty or omitted
- [ ] `AzureSearch__SourceIdField` = `id`
- [ ] `AzureSearch__ContentField` = `content`
- [ ] `AzureSearch__TitleField` = `title`
- [ ] `ServiceBus__Provider` = `AzureServiceBus`
- [ ] `ServiceBus__FullyQualifiedNamespace` set
- [ ] `ServiceBus__ConnectionString` empty when using MI
- [ ] `ServiceBus__QueueName` = `cve-analysis-jobs`
- [ ] `Redis__Provider` = `Redis`
- [ ] `Redis__ConnectionString` set

Web app settings:

- [ ] `ASPNETCORE_ENVIRONMENT` = `Staging` or `Production`
- [ ] `PatchMindApi__BaseUrl` = deployed API URL

## 6. CI/CD Pipeline

- [ ] Build on pull request.
- [ ] Run unit and integration tests on main branch.
- [ ] Publish artifacts for API and Web.
- [ ] Deploy API first, then Web.
- [ ] Use deployment slots for staging/production swap (recommended).

## 7. Smoke Tests After Deploy

- [ ] `GET /health/live` returns healthy.
- [ ] `GET /health/ready` returns healthy.
- [ ] Prompt flow works:
  - [ ] Create prompt analysis job.
  - [ ] Job transitions to completed.
  - [ ] Result contains citation chunks.
  - [ ] Citations tab renders in UI.
- [ ] Validate logs show retrieval hit count and no auth failures.

## 8. Hardening Before Production

- [ ] Turn on App Service authentication if required.
- [ ] Restrict network access (private endpoints/IP restrictions) if required.
- [ ] Enable diagnostic logs and alerts (failed jobs, 5xx, queue depth).
- [ ] Define backup/restore strategy for persistent data.
- [ ] Run rollback drill using previous deployment slot/artifact.
