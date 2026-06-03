# PatchMindAI - Complete Development & Deployment Plan

## 🔍 Current State Analysis

### ✅ What's Already Deployed
1. **PatchMindAI.API** (Backend REST API)
   - ✅ Deployed to: `https://patchmindai-app.azurewebsites.net`
   - ✅ Azure SQL Database connected (20 CVEs seeded)
   - ✅ Azure AI Search configured (20 documents indexed)
   - ✅ Background worker running (AnalysisJobWorker)
   - ✅ CVE CRUD endpoints working
   - ⚠️  Using InMemory Queue and Cache

### ❌ What's Missing

#### 1. **Frontend Application (PatchMindAI.Web)**
- **Status**: Not deployed
- **Type**: ASP.NET Core MVC with Views
- **Features**:
  - Home page with analysis form
  - JavaScript UI for CVE analysis
  - Calls API backend via HttpClient
  - Located in: `src/PatchMindAI.Web/`

#### 2. **Production-Grade Message Queue**
- **Current**: InMemory (lost on restart)
- **Needed**: Azure Service Bus
- **Purpose**: Reliable job queuing for analysis requests

#### 3. **Production-Grade Cache**
- **Current**: InMemory (not shared across instances)
- **Needed**: Azure Cache for Redis
- **Purpose**: Job status caching, distributed state

#### 4. **Azure OpenAI Permissions**
- **Current**: API using Managed Identity but missing role assignment
- **Needed**: "Cognitive Services OpenAI User" role

---

## 📋 Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Azure Cloud                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐         ┌──────────────────┐         │
│  │ PatchMindAI.Web  │────────>│ PatchMindAI.API  │         │
│  │  (Frontend MVC)  │ HTTP    │ (REST Backend)   │         │
│  │  App Service     │         │  App Service     │         │
│  └──────────────────┘         └──────────────────┘         │
│                                        │                     │
│                    ┌───────────────────┼──────────────────┐ │
│                    │                   │                  │ │
│                    ▼                   ▼                  ▼ │
│          ┌─────────────────┐  ┌──────────────┐  ┌────────────┐
│          │ Azure SQL       │  │ Service Bus  │  │   Redis    │
│          │ Database        │  │   Queue      │  │   Cache    │
│          └─────────────────┘  └──────────────┘  └────────────┘
│                    │                   │                  │ │
│                    └───────────────────┼──────────────────┘ │
│                                        │                     │
│                                        ▼                     │
│                            ┌────────────────────┐            │
│                            │ Background Worker  │            │
│                            │ (AnalysisJobWorker)│            │
│                            │ - Dequeues jobs    │            │
│                            │ - Runs AI agents   │            │
│                            │ - Saves results    │            │
│                            └────────────────────┘            │
│                                        │                     │
│                    ┌───────────────────┼──────────────────┐ │
│                    │                   │                  │ │
│                    ▼                   ▼                  ▼ │
│          ┌─────────────────┐  ┌──────────────┐  ┌────────────┐
│          │ Azure OpenAI    │  │ Azure Search │  │ Azure SQL  │
│          │ (GPT-4o)        │  │ (Knowledge)  │  │ (CVE Data) │
│          └─────────────────┘  └──────────────┘  └────────────┘
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 🎯 Complete Deployment Plan

### Phase 1: Enable Production Services (Azure Resources)

#### Task 1.1: Create Azure Service Bus
```bash
# Create namespace
az servicebus namespace create \
  --name patchmindai-servicebus \
  --resource-group AIAgent \
  --location westus2 \
  --sku Standard

# Create queue
az servicebus queue create \
  --name cve-analysis-jobs \
  --namespace-name patchmindai-servicebus \
  --resource-group AIAgent \
  --max-delivery-count 10 \
  --lock-duration PT5M

# Get connection details
SERVICEBUS_NAMESPACE=$(az servicebus namespace show \
  --name patchmindai-servicebus \
  --resource-group AIAgent \
  --query "serviceBusEndpoint" -o tsv)
```

#### Task 1.2: Create Azure Cache for Redis
```bash
# Create Redis cache (Basic C1 = 1GB)
az redis create \
  --name patchmindai-redis \
  --resource-group AIAgent \
  --location westus2 \
  --sku Basic \
  --vm-size c1

# Get connection string
REDIS_HOST=$(az redis show \
  --name patchmindai-redis \
  --resource-group AIAgent \
  --query "hostName" -o tsv)

REDIS_KEY=$(az redis list-keys \
  --name patchmindai-redis \
  --resource-group AIAgent \
  --query "primaryKey" -o tsv)
```

#### Task 1.3: Grant Managed Identity Permissions

**For API App Service:**
```bash
# Get API App Service principal ID
API_PRINCIPAL_ID=$(az webapp identity show \
  --name PatchMindAI-app \
  --resource-group AIAgent \
  --query principalId -o tsv)

# Grant Service Bus Data Sender role
az role assignment create \
  --assignee $API_PRINCIPAL_ID \
  --role "Azure Service Bus Data Sender" \
  --scope /subscriptions/ccb4555d-96a5-4438-9ac3-47df0b506143/resourceGroups/AIAgent/providers/Microsoft.ServiceBus/namespaces/patchmindai-servicebus

# Grant Service Bus Data Receiver role
az role assignment create \
  --assignee $API_PRINCIPAL_ID \
  --role "Azure Service Bus Data Receiver" \
  --scope /subscriptions/ccb4555d-96a5-4438-9ac3-47df0b506143/resourceGroups/AIAgent/providers/Microsoft.ServiceBus/namespaces/patchmindai-servicebus

# Grant Azure OpenAI role
OPENAI_RESOURCE_ID=$(az cognitiveservices account show \
  --name PatchMindAI \
  --resource-group AIAgent \
  --query id -o tsv)

az role assignment create \
  --assignee $API_PRINCIPAL_ID \
  --role "Cognitive Services OpenAI User" \
  --scope $OPENAI_RESOURCE_ID
```

---

### Phase 2: Update API Configuration

#### Task 2.1: Update appsettings.Production.json
```json
{
  "ServiceBus": {
    "Provider": "AzureServiceBus",
    "FullyQualifiedNamespace": "patchmindai-servicebus.servicebus.windows.net",
    "ConnectionString": "",
    "QueueName": "cve-analysis-jobs"
  },
  "Redis": {
    "Provider": "Redis",
    "ConnectionString": "patchmindai-redis.redis.cache.windows.net:6380,password=YOUR_KEY,ssl=True,abortConnect=False",
    "KeyPrefix": "patchmindai",
    "JobStatusTtlMinutes": 60,
    "ResultTtlMinutes": 240
  }
}
```

#### Task 2.2: Configure App Settings in Azure Portal
```bash
# Update Service Bus settings
az webapp config appsettings set \
  --name PatchMindAI-app \
  --resource-group AIAgent \
  --settings \
    ServiceBus__Provider="AzureServiceBus" \
    ServiceBus__FullyQualifiedNamespace="patchmindai-servicebus.servicebus.windows.net" \
    ServiceBus__QueueName="cve-analysis-jobs" \
    Redis__Provider="Redis" \
    Redis__ConnectionString="patchmindai-redis.redis.cache.windows.net:6380,password=$REDIS_KEY,ssl=True,abortConnect=False"
```

---

### Phase 3: Deploy Frontend Application

#### Task 3.1: Create App Service for Frontend
```bash
# Create App Service for Web frontend
az webapp create \
  --name PatchMindAI-web \
  --resource-group AIAgent \
  --plan $(az appservice plan list --resource-group AIAgent --query "[0].name" -o tsv) \
  --runtime "DOTNET:9.0"

# Enable managed identity
az webapp identity assign \
  --name PatchMindAI-web \
  --resource-group AIAgent
```

#### Task 3.2: Update Frontend Configuration
Update `src/PatchMindAI.Web/appsettings.Production.json`:
```json
{
  "PatchMindApi": {
    "BaseUrl": "https://patchmindai-app.azurewebsites.net"
  }
}
```

#### Task 3.3: Build and Deploy Frontend
```bash
# Build frontend
cd /Users/seeni/Repository/PatchMindAI
dotnet publish src/PatchMindAI.Web/PatchMindAI.Web.csproj -c Release -o ./publish-web

# Create deployment package
cd publish-web && zip -r ../deploy-web.zip .

# Deploy to Azure
az webapp deploy \
  --resource-group AIAgent \
  --name PatchMindAI-web \
  --src-path ../deploy-web.zip \
  --type zip
```

#### Task 3.4: Configure App Settings
```bash
az webapp config appsettings set \
  --name PatchMindAI-web \
  --resource-group AIAgent \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    PatchMindApi__BaseUrl="https://patchmindai-app.azurewebsites.net"
```

---

### Phase 4: Redeploy API with Updated Configuration

#### Task 4.1: Update Code Files
1. Update `src/PatchMindAI.API/appsettings.Production.json` with real Redis/ServiceBus settings
2. Commit changes

#### Task 4.2: Rebuild and Redeploy
```bash
# Clean and rebuild
cd /Users/seeni/Repository/PatchMindAI
rm -rf publish deploy.zip

dotnet publish src/PatchMindAI.API/PatchMindAI.API.csproj -c Release -o ./publish
cd publish && zip -r ../deploy.zip .

# Redeploy
az webapp deploy \
  --resource-group AIAgent \
  --name PatchMindAI-app \
  --src-path ../deploy.zip \
  --type zip
```

---

### Phase 5: Enable CORS for Frontend-API Communication

#### Task 5.1: Update API Program.cs
Add CORS policy:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://patchmindai-web.azurewebsites.net")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Before app.UseRouting()
app.UseCors("AllowFrontend");
```

---

### Phase 6: Configure Custom Domains (Optional)

#### Task 6.1: Add Custom Domain to Frontend
```bash
# Map custom domain (if you have one)
az webapp config hostname add \
  --webapp-name PatchMindAI-web \
  --resource-group AIAgent \
  --hostname www.patchmindai.com
```

---

## 🧪 Testing Plan

### Test 1: API Backend (Already Working)
```bash
curl https://patchmindai-app.azurewebsites.net/api/cves
```

### Test 2: Create Analysis Job via API
```bash
curl -X POST https://patchmindai-app.azurewebsites.net/api/analysis \
  -H "Content-Type: application/json" \
  -d '{"cveId": "CVE-2021-44228", "query": "Tell me about Log4Shell"}'
```

### Test 3: Check Queue (Service Bus)
```bash
az servicebus queue show \
  --name cve-analysis-jobs \
  --namespace-name patchmindai-servicebus \
  --resource-group AIAgent \
  --query "countDetails"
```

### Test 4: Frontend Application
```bash
# Visit in browser
https://patchmindai-web.azurewebsites.net
```

### Test 5: End-to-End Flow
1. Open frontend: `https://patchmindai-web.azurewebsites.net`
2. Enter CVE ID and query
3. Submit analysis request
4. Check job status via polling
5. View results when complete

---

## 📊 Resource Summary

### Azure Resources Needed

| Resource | Name | SKU | Monthly Cost (USD) | Purpose |
|----------|------|-----|-------------------|---------|
| App Service Plan | Existing | B1 Basic | ~$13 | Hosts both Web and API |
| App Service (API) | PatchMindAI-app | - | Included | Backend REST API |
| App Service (Web) | PatchMindAI-web | - | Included | Frontend MVC |
| Azure SQL Database | patch-mindai-db | Basic | ~$5 | CVE data storage |
| Azure AI Search | patchmindaisearchdb | Free | $0 | Knowledge retrieval |
| Azure OpenAI | PatchMindAI | Existing | Pay-per-use | GPT-4o agents |
| Service Bus | patchmindai-servicebus | Standard | ~$10 | Message queue |
| Redis Cache | patchmindai-redis | Basic C1 (1GB) | ~$17 | Job status cache |
| **Total** | | | **~$45/month** | |

---

## 🔐 Security Checklist

- [x] API uses Managed Identity for Azure SQL
- [ ] API uses Managed Identity for Azure OpenAI
- [x] API uses API key for Azure Search (temporary)
- [ ] API uses Managed Identity for Service Bus
- [ ] Redis connection uses SSL
- [ ] All secrets stored in App Settings (not committed)
- [ ] CORS configured to allow only frontend domain
- [ ] HTTPS enforced on all endpoints
- [ ] SQL firewall allows only Azure services

---

## 📝 Configuration Files to Update

### 1. `/src/PatchMindAI.API/appsettings.Production.json`
- Update `ServiceBus.Provider` to `"AzureServiceBus"`
- Update `ServiceBus.FullyQualifiedNamespace`
- Update `Redis.Provider` to `"Redis"`
- Update `Redis.ConnectionString`

### 2. `/src/PatchMindAI.Web/appsettings.Production.json`
- Update `PatchMindApi.BaseUrl` to point to API App Service

### 3. `/src/PatchMindAI.API/Program.cs`
- Add CORS policy for frontend domain

---

## 🚀 Deployment Order

1. ✅ **DONE**: Deploy API (Backend)
2. ✅ **DONE**: Seed SQL Database
3. ✅ **DONE**: Seed Azure Search Index
4. **NEXT**: Create Service Bus + Redis
5. **NEXT**: Update API configuration
6. **NEXT**: Redeploy API with new config
7. **NEXT**: Deploy Frontend (Web)
8. **NEXT**: Test end-to-end flow

---

## 📖 Usage After Deployment

### For End Users:
1. Navigate to: `https://patchmindai-web.azurewebsites.net`
2. Enter CVE ID (e.g., CVE-2021-44228)
3. Enter analysis question (e.g., "What is the severity?")
4. Click "Analyze"
5. Wait for results (background job processes)

### For API Consumers:
```bash
# List all CVEs
GET https://patchmindai-app.azurewebsites.net/api/cves

# Get specific CVE
GET https://patchmindai-app.azurewebsites.net/api/cves/CVE-2021-44228

# Submit analysis
POST https://patchmindai-app.azurewebsites.net/api/analysis
{
  "cveId": "CVE-2021-44228",
  "query": "What is Log4Shell?"
}

# Check job status
GET https://patchmindai-app.azurewebsites.net/api/analysis/{jobId}

# Get results
GET https://patchmindai-app.azurewebsites.net/api/analysis/{jobId}/result
```

---

## 🐛 Troubleshooting

### Issue: Background Worker Not Processing Jobs
- Check Service Bus queue has messages
- Check API logs: `az webapp log tail --name PatchMindAI-app`
- Verify Managed Identity has Service Bus roles

### Issue: Frontend Can't Reach API
- Check CORS policy includes frontend URL
- Verify `PatchMindApi.BaseUrl` in Web app settings
- Check API is responding: `curl https://patchmindai-app.azurewebsites.net/api/cves`

### Issue: Jobs Stuck in "Processing"
- Check Redis connection in API logs
- Verify cache TTL settings
- Check background worker logs

---

## ✅ Success Criteria

- [ ] Frontend accessible at custom URL
- [ ] Users can submit CVE analysis requests
- [ ] Jobs queued to Service Bus successfully
- [ ] Background worker processes jobs
- [ ] AI agents run with Azure OpenAI
- [ ] Knowledge retrieval uses Azure Search
- [ ] Results cached in Redis
- [ ] Users see analysis results in UI
- [ ] System scales with multiple requests
- [ ] No data loss on app restart

---

## 📚 Next Steps After Deployment

1. **Monitoring**: Set up Application Insights
2. **CI/CD**: Configure GitHub Actions for automatic deployments
3. **Custom Domain**: Configure custom domain and SSL
4. **Scaling**: Enable auto-scaling for App Services
5. **Backup**: Configure SQL database backup policy
6. **Alerts**: Set up alerts for failed jobs, API errors
7. **Documentation**: Create user guide and API documentation
8. **Testing**: Set up automated integration tests

---

**Created**: June 3, 2026
**Last Updated**: June 3, 2026
**Status**: Ready for Phase 1 implementation
