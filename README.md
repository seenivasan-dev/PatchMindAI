# PatchMindAI 🛡️

**Intelligent Vulnerability Management Platform Powered by Multi-Agent AI System**

PatchMindAI is an enterprise-grade vulnerability intelligence platform that leverages Azure OpenAI and a sophisticated multi-agent architecture to help security teams prioritize, analyze, and respond to CVE (Common Vulnerabilities and Exposures) threats efficiently.

## 🌟 Why PatchMindAI?

In today's threat landscape, security teams face thousands of vulnerabilities daily. PatchMindAI solves this challenge by:

- **🤖 Intelligent Prioritization**: Uses AI to analyze CVEs based on CVSS scores, asset criticality, and exposure levels
- **💬 Natural Language Queries**: Ask questions like "Tell me about Log4Shell" or "What should I patch first?"
- **🔍 Knowledge-Augmented Responses**: Combines real-time CVE data with AI-powered analysis using RAG (Retrieval-Augmented Generation)
- **⚡ Automated Workflows**: Background job processing with Azure Service Bus for scalable analysis
- **📊 Comprehensive Reporting**: Generates prioritized vulnerability reports with actionable recommendations

## 🏗️ Multi-Agent AI Architecture

PatchMindAI implements a **specialized multi-agent system** where each agent handles a specific aspect of vulnerability analysis:

```
┌─────────────────────────────────────────────────────────────────┐
│                     User Query (Natural Language)                │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌───────────────────────────────────────────────────────────────────┐
│                  🧠 MultiAgentOrchestrator                        │
│              (Routes queries to specialized agents)               │
└───────────────────────────┬───────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ▼                   ▼                   ▼
┌──────────────┐   ┌──────────────┐   ┌──────────────┐
│PromptParser  │   │  CVE Search  │   │Prioritization│
│    Agent     │   │    Agent     │   │    Agent     │
│              │   │              │   │              │
│ Classifies   │   │ Retrieves &  │   │ Calculates   │
│ user intent  │   │ analyzes CVE │   │ risk scores  │
│ & extracts   │   │ data with    │   │ based on     │
│ keywords     │   │ RAG          │   │ multiple     │
│              │   │              │   │ factors      │
└──────┬───────┘   └──────┬───────┘   └──────┬───────┘
       │                   │                   │
       └───────────────────┼───────────────────┘
                           │
                           ▼
                  ┌──────────────┐
                  │   Report     │
                  │   Agent      │
                  │              │
                  │ Generates    │
                  │ final        │
                  │ analysis     │
                  └──────┬───────┘
                         │
                         ▼
                  ┌──────────────┐
                  │   Response   │
                  └──────────────┘
```

### Agent System Components

#### 1. **PromptParserAgent** 🎯
**Purpose**: Intent classification and query understanding

**Workflow**:
- Receives natural language user queries
- Uses Azure OpenAI GPT-4o to classify intent into categories:
  - `CveSearch`: Specific CVE lookup (e.g., "What is CVE-2021-44228?")
  - `PriorityReport`: Prioritized vulnerability list (e.g., "What should I patch first?")
  - `WeeklySummary`: Trend analysis and summaries
  - `AssetInventory`: Asset management queries
- Extracts structured data: CVE IDs, keywords, top-N counts
- Returns parsed intent with confidence scores

**Key Features**:
- Regex-based CVE ID extraction (CVE-YYYY-NNNNN pattern)
- JSON schema validation for structured responses
- Fallback handling for unknown intents

#### 2. **AzureOpenAiAnalysisOrchestrator** 🔍
**Purpose**: CVE data retrieval and AI-powered analysis

**Workflow**:
1. **Data Retrieval**:
   - Queries Azure SQL database for CVE records
   - Falls back to NVD (National Vulnerability Database) API if not found
   - Syncs new CVEs to local database

2. **RAG (Retrieval-Augmented Generation)**:
   - Searches Azure AI Search index for relevant knowledge
   - Retrieves contextual documents using keyword/semantic search
   - Augments AI prompts with retrieved knowledge for accurate responses

3. **AI Analysis**:
   - Uses Semantic Kernel with Azure OpenAI
   - Generates comprehensive vulnerability analysis
   - Includes: Description, Impact, Affected Products, Mitigation Steps, References

**Key Features**:
- Managed Identity authentication (no API keys in code)
- Automatic CVE syncing with Azure Search
- Multi-turn conversational context
- Configurable temperature and token limits

#### 3. **PrioritizationAgent** 📊
**Purpose**: Risk-based vulnerability prioritization

**Workflow**:
1. Queries all vulnerable assets from database
2. Calculates **composite risk scores** using:
   - **CVSS Base Score** (0-10): Industry standard severity rating
   - **Asset Criticality** (1-5): Business impact of affected systems
   - **Exposure Level** (1-3): Network exposure (internal/DMZ/public)
   - **Time Factor**: Days since publication (urgency multiplier)

3. **Scoring Formula**:
   ```
   Risk Score = (CVSS × 40) + (Asset Criticality × 20) + (Exposure × 15) + (Time Factor × 5)
   ```

4. Returns ranked list with:
   - Prioritized vulnerabilities
   - Risk scores
   - Recommended actions
   - Patching deadlines

**Key Features**:
- Multi-dimensional risk assessment
- Configurable scoring weights
- Asset-aware prioritization
- SLA-based deadline calculation

#### 4. **ReportAgent** 📝
**Purpose**: Report generation and formatting

**Workflow**:
- Aggregates analysis results from other agents
- Formats output for different audiences:
  - Executive summaries (high-level risk overview)
  - Technical reports (detailed CVE analysis)
  - Compliance reports (audit-ready documentation)
- Generates markdown/JSON/HTML output
- Tracks analysis history

**Key Features**:
- Multi-format output support
- Template-based generation
- Historical trend analysis
- Audit trail logging

#### 5. **MultiAgentOrchestrator** 🎭
**Purpose**: Coordination and workflow management

**Workflow**:
1. Receives analysis jobs from queue
2. Routes to PromptParserAgent for intent classification
3. Based on intent, delegates to appropriate agent:
   - `CveSearch` → AzureOpenAiAnalysisOrchestrator
   - `PriorityReport` → PrioritizationAgent
   - `WeeklySummary` → ReportAgent with aggregation
4. Collects results and sends to ReportAgent
5. Returns final structured response
6. Logs all actions to audit trail

**Key Features**:
- Intent-based routing logic
- Error handling and retry mechanisms
- Distributed tracing with correlation IDs
- Performance metrics collection

## 🔄 AI Agent Workflows

### Workflow 1: CVE Search Query
```
User: "Tell me about Log4Shell"
    ↓
PromptParserAgent
    → Intent: CveSearch
    → Extracted: "Log4Shell" → Resolves to CVE-2021-44228
    ↓
AzureOpenAiAnalysisOrchestrator
    → Query Azure SQL: SELECT * FROM Cves WHERE Id = 'CVE-2021-44228'
    → RAG Search: Query Azure Search for "Log4Shell Apache Log4j"
    → AI Analysis: Generate comprehensive explanation with context
    ↓
ReportAgent
    → Format response with:
      • Description & Impact
      • CVSS Score: 10.0 (Critical)
      • Affected Products
      • Mitigation Steps
      • External References
    ↓
Response: Detailed CVE analysis with actionable guidance
```

### Workflow 2: Priority Report
```
User: "What should I patch first?"
    ↓
PromptParserAgent
    → Intent: PriorityReport
    → TopN: 10
    ↓
PrioritizationAgent
    → Query vulnerable assets
    → Calculate risk scores for each:
      CVE-2021-44228 (Log4Shell):
        • CVSS: 10.0 × 40 = 400
        • Asset Criticality: 5 × 20 = 100
        • Exposure: Public (3) × 15 = 45
        • Time Factor: 1900 days × 5 = 250
        • Total Risk Score: 795
    → Rank by score descending
    ↓
ReportAgent
    → Generate prioritized list with:
      1. CVE-2021-44228 (Score: 795) - Patch by 2026-06-05
      2. CVE-2024-3094 (Score: 720) - Patch by 2026-06-04
      ...
    ↓
Response: Top 10 vulnerabilities with deadlines and actions
```

### Workflow 3: Background Job Processing
```
User submits analysis request via Web UI
    ↓
API Controller creates AnalysisJob
    ↓
Job enqueued to Azure Service Bus (cve-analysis-jobs)
    ↓
AnalysisJobWorker (Background Service)
    → Dequeues job from Service Bus
    → Invokes MultiAgentOrchestrator.RunAsync()
    → Updates job status: Processing → Completed
    → Caches result in Redis (InMemory for now)
    → Saves result to Azure SQL
    ↓
User polls /api/analysis/{jobId}/status
    ↓
Response: Analysis complete with results
```

## 🛠️ Technology Stack

### AI & Machine Learning
- **Azure OpenAI (GPT-4o)**: Primary LLM for analysis and generation
- **Semantic Kernel**: AI orchestration framework
- **Azure AI Search**: Vector/keyword search for RAG
- **Retrieval-Augmented Generation (RAG)**: Knowledge-enhanced responses

### Backend (.NET 9.0)
- **ASP.NET Core 9.0**: Web API framework
- **Entity Framework Core 9.0**: ORM with Code-First migrations
- **Azure Service Bus**: Distributed message queue (Standard SKU)
- **Azure SQL Database**: Relational data storage
- **Managed Identity**: Secure service-to-service authentication

### Infrastructure (Azure)
- **Azure App Service**: API and Web hosting (Linux, .NET 9.0)
- **Azure SQL Server**: Production database
- **Azure Service Bus**: Job queue (cve-analysis-jobs)
- **Azure AI Search**: Knowledge index (search-1780455352788)
- **Azure OpenAI Service**: LLM endpoint (gpt-4o deployment)

### Architecture Patterns
- **Multi-Agent System**: Specialized agents with orchestration
- **CQRS**: Separate read/write models
- **Repository Pattern**: Data access abstraction
- **Background Workers**: Long-running job processing
- **RAG Pattern**: Knowledge retrieval + generation
- **Circuit Breaker**: Resilient external API calls

## 📦 Project Structure

```
PatchMindAI/
├── src/
│   ├── PatchMindAI.API/              # REST API & Controllers
│   │   ├── Controllers/              # CVE, Analysis endpoints
│   │   ├── Background/               # AnalysisJobWorker
│   │   ├── Middleware/               # CORS, Error handling
│   │   └── Health/                   # Health checks
│   │
│   ├── PatchMindAI.Web/              # MVC Frontend
│   │   ├── Controllers/              # UI Controllers
│   │   ├── Views/                    # Razor views
│   │   └── wwwroot/                  # Static assets
│   │
│   ├── PatchMindAI.Agents/           # 🤖 AI Agent System
│   │   ├── MultiAgentOrchestrator.cs       # Master orchestrator
│   │   ├── PromptParserAgent.cs            # Intent classification
│   │   ├── AzureOpenAiAnalysisOrchestrator.cs # CVE analysis
│   │   ├── PrioritizationAgent.cs          # Risk scoring
│   │   ├── ReportAgent.cs                  # Report generation
│   │   └── AuditLogger.cs                  # Activity tracking
│   │
│   ├── PatchMindAI.Core/             # Domain Models & Interfaces
│   │   ├── Domain/                   # Cve, Asset, PatchStatus
│   │   ├── Interfaces/               # Agent & Repository contracts
│   │   ├── Configuration/            # Settings models
│   │   └── Enums/                    # PatchingStatus, Severity
│   │
│   └── PatchMindAI.Infrastructure/   # Data & External Services
│       ├── Data/                     # EF Core DbContext
│       ├── SeedData/                 # Database & Search seeders
│       ├── KnowledgeRetrieval/       # Azure Search integration
│       ├── Messaging/                # Service Bus queue
│       ├── Caching/                  # Redis cache
│       └── ExternalClients/          # NVD API client
│
├── DEPLOYMENT_PLAN.md                # Deployment guide
└── README.md                         # This file
```

## 🚀 Getting Started

### Prerequisites
- .NET 9.0 SDK
- Azure subscription
- Azure CLI
- SQL Server (local or Azure)

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/PatchMindAI.git
   cd PatchMindAI
   ```

2. **Configure Azure resources**
   
   Create required Azure resources:
   ```bash
   # Create resource group
   az group create --name AIAgent --location westus2

   # Create Azure OpenAI
   az cognitiveservices account create \
     --name PatchMindAI \
     --resource-group AIAgent \
     --kind OpenAI \
     --sku S0 \
     --location eastus

   # Create Azure AI Search
   az search service create \
     --name patchmindaisearchdb \
     --resource-group AIAgent \
     --sku basic \
     --location westus2

   # Create Azure SQL Database
   az sql server create \
     --name patchmindai \
     --resource-group AIAgent \
     --location westus2 \
     --admin-user sqladmin

   az sql db create \
     --server patchmindai \
     --resource-group AIAgent \
     --name patch-mindai-db \
     --service-objective S0
   ```

3. **Configure appsettings**
   
   Update `src/PatchMindAI.API/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "PatchMindAIDb": "Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patch-mindai-db;Authentication=Active Directory Default;..."
     },
     "AzureOpenAI": {
       "Endpoint": "https://patchmindai.openai.azure.com",
       "DeploymentName": "PATCHMINDAI-DEPLOYMENT",
       "Model": "gpt-4o",
       "UseManagedIdentity": true
     },
     "AzureSearch": {
       "Endpoint": "https://patchmindaisearchdb.search.windows.net",
       "IndexName": "search-1780455352788",
       "UseManagedIdentity": true
     }
   }
   ```

4. **Run database migrations**
   ```bash
   cd src/PatchMindAI.API
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   # Terminal 1: API
   cd src/PatchMindAI.API
   dotnet run

   # Terminal 2: Web UI
   cd src/PatchMindAI.Web
   dotnet run
   ```

6. **Access the application**
   - API: http://localhost:5293
   - Web UI: http://localhost:5000
   - Swagger: http://localhost:5293/openapi/v1.json

## 🌐 Production Deployment

### Azure Resources Created
- **Frontend**: https://patchmindai-web.azurewebsites.net
- **API**: https://patchmindai-app.azurewebsites.net
- **Service Bus Namespace**: patchmindai-servicebus
- **Service Bus Queue**: cve-analysis-jobs
- **SQL Server**: patchmindai.database.windows.net
- **Database**: patch-mindai-db
- **Azure OpenAI**: patchmindai.openai.azure.com
- **Azure AI Search**: patchmindaisearchdb.search.windows.net

### Deployment Steps

1. **Build and publish API**
   ```bash
   dotnet publish src/PatchMindAI.API/PatchMindAI.API.csproj -c Release -o ./publish
   cd publish && zip -r ../deploy.zip .
   
   az webapp deploy \
     --resource-group AIAgent \
     --name PatchMindAI-app \
     --src-path deploy.zip \
     --type zip
   ```

2. **Build and publish Web**
   ```bash
   dotnet publish src/PatchMindAI.Web/PatchMindAI.Web.csproj -c Release -o ./publish-web
   cd publish-web && zip -r ../deploy-web.zip .
   
   az webapp deploy \
     --resource-group AIAgent \
     --name PatchMindAI-web \
     --src-path deploy-web.zip \
     --type zip
   ```

3. **Configure Managed Identity permissions**
   ```bash
   # Get API Managed Identity principal ID
   PRINCIPAL_ID=$(az webapp identity show \
     --name PatchMindAI-app \
     --resource-group AIAgent \
     --query principalId -o tsv)

   # Grant permissions
   az role assignment create \
     --assignee $PRINCIPAL_ID \
     --role "Azure Service Bus Data Sender" \
     --scope /subscriptions/{subscription-id}/resourceGroups/AIAgent/providers/Microsoft.ServiceBus/namespaces/patchmindai-servicebus

   az role assignment create \
     --assignee $PRINCIPAL_ID \
     --role "Cognitive Services OpenAI User" \
     --scope /subscriptions/{subscription-id}/resourceGroups/AIAgent/providers/Microsoft.CognitiveServices/accounts/PatchMindAI
   ```

See [DEPLOYMENT_PLAN.md](DEPLOYMENT_PLAN.md) for detailed deployment guide.

## 📊 API Endpoints

### CVE Management
- `GET /api/cves` - List all CVEs
- `GET /api/cves/{id}` - Get specific CVE
- `POST /api/cves/{id}/sync` - Sync CVE from NVD

### Analysis
- `POST /api/analysis` - Create analysis job
- `GET /api/analysis/{jobId}/status` - Check job status
- `GET /api/analysis/{jobId}/result` - Get analysis result

### Health
- `GET /health/live` - Liveness check
- `GET /health/ready` - Readiness check (validates provider config)

## 🔒 Security

- **Managed Identity**: No credentials in code or config
- **Azure RBAC**: Role-based access control for all services
- **HTTPS Only**: TLS 1.2+ enforced
- **CORS**: Configured for frontend domain only
- **SQL Injection Protection**: EF Core parameterized queries
- **Audit Logging**: All queries tracked with correlation IDs

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## 📈 Monitoring & Observability

- **Application Insights**: Telemetry and performance monitoring
- **Structured Logging**: JSON logs with correlation IDs
- **Health Checks**: Liveness and readiness probes
- **Distributed Tracing**: Request tracking across services
- **Service Bus Metrics**: Queue depth and processing rates

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Azure OpenAI** for powerful language models
- **Semantic Kernel** for AI orchestration framework
- **NIST NVD** for CVE data
- **.NET Team** for excellent framework and tooling

## 📞 Support

For issues and questions:
- 🐛 Bug Reports: [GitHub Issues](https://github.com/yourusername/PatchMindAI/issues)
- 💬 Discussions: [GitHub Discussions](https://github.com/yourusername/PatchMindAI/discussions)
- 📧 Email: support@patchmindai.com

---

**Built with ❤️ using .NET 9.0, Azure OpenAI, and Multi-Agent AI Architecture**
