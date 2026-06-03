# Azure SQL Database Migration Guide

## ✅ What Was Updated

Your infrastructure has been successfully migrated from SQLite to Azure SQL Server:

### 1. **Packages Updated**
- ✅ Added `Microsoft.EntityFrameworkCore.SqlServer` (9.0.8)
- ✅ Added `Microsoft.Data.SqlClient` (6.0.2)  
- ✅ Added `Azure.Identity` (1.13.1) for Managed Identity support
- ✅ Added `Azure.Core` (1.45.0) to resolve version conflicts
- ✅ Downgraded `Azure.Search.Documents` to 11.6.0 for compatibility

### 2. **Code Updated**
- ✅ [PatchMindDbContextFactory.cs](src/PatchMindAI.Infrastructure/PatchMindDbContextFactory.cs) - Uses SQL Server for migrations
- ✅ [InfrastructureServiceCollectionExtensions.cs](src/PatchMindAI.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs) - Uses SQL Server DbContext
- ✅ [appsettings.json](src/PatchMindAI.API/appsettings.json) - Local SQL Server LocalDB
- ✅ [appsettings.Production.json](src/PatchMindAI.API/appsettings.Production.json) - Azure SQL connection string

---

## 🚀 Deployment Steps

### **Step 1: Create EF Core Migration**

Generate a migration for Azure SQL Server:

```bash
cd src/PatchMindAI.Infrastructure

# Create initial migration
dotnet ef migrations add InitialAzureSqlMigration --startup-project ../PatchMindAI.API
```

This creates migration files in `/Migrations` folder.

---

### **Step 2: Configure Azure SQL Connection String**

You have **two options** for authentication:

#### **Option A: Username/Password (Development/Testing)**

Update [appsettings.Production.json](src/PatchMindAI.API/appsettings.Production.json):

```json
{
  "ConnectionStrings": {
    "PatchMindAIDb": "Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patchmindai;User ID=<your-username>;Password=<your-password>;Persist Security Info=False;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

**Replace:**
- `<your-username>` with your SQL admin username
- `<your-password>` with your SQL admin password

⚠️ **Security Warning:** Never commit passwords to source control!

---

#### **Option B: Managed Identity (Production - Recommended)**

This is the **secure, passwordless** option for production.

**Connection String (appsettings.Production.json):**
```json
{
  "ConnectionStrings": {
    "PatchMindAIDb": "Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patchmindai;Authentication=Active Directory Default;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

**Azure Configuration:**

1. **Enable Managed Identity on your App Service:**
   ```bash
   az webapp identity assign \
     --name <your-app-name> \
     --resource-group <your-resource-group>
   ```

2. **Grant Database Access:**
   
   Connect to your Azure SQL database using Azure Data Studio or SSMS, then run:
   
   ```sql
   -- Create user for Managed Identity
   CREATE USER [<your-app-name>] FROM EXTERNAL PROVIDER;
   
   -- Grant permissions
   ALTER ROLE db_datareader ADD MEMBER [<your-app-name>];
   ALTER ROLE db_datawriter ADD MEMBER [<your-app-name>];
   ALTER ROLE db_ddladmin ADD MEMBER [<your-app-name>];
   GO
   ```

   Replace `<your-app-name>` with your App Service name.

---

### **Step 3: Run Database Migration**

#### **Option 1: From Development Machine (Quick Test)**

Set environment variable with your connection string:

```bash
# PowerShell (Windows)
$env:PATCHMINDAI_DB_CONNECTION="Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patchmindai;User ID=<your-username>;Password=<your-password>;Encrypt=True;"

# Bash (Mac/Linux)
export PATCHMINDAI_DB_CONNECTION="Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patchmindai;User ID=<your-username>;Password=<your-password>;Encrypt=True;"

# Run migration
cd src/PatchMindAI.Infrastructure
dotnet ef database update --startup-project ../PatchMindAI.API
```

---

#### **Option 2: Automatic Migration on App Startup (Recommended for Production)**

The application already runs migrations automatically on startup:

```csharp
// In Program.cs (already configured)
var context = scope.ServiceProvider.GetRequiredService<PatchMindDbContext>();
context.Database.Migrate();  // ← Runs migrations automatically
```

**To use this:**
1. Deploy your application to Azure App Service
2. Configure connection string in App Service Configuration
3. Migrations run automatically on first startup

**Azure CLI:**
```bash
az webapp config connection-string set \
  --name <your-app-name> \
  --resource-group <your-resource-group> \
  --connection-string-type SQLAzure \
  --settings PatchMindAIDb="Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patchmindai;Authentication=Active Directory Default;MultipleActiveResultSets=True;Encrypt=True;"
```

---

### **Step 4: Verify Database Schema**

After migration, verify tables were created:

```bash
# Azure CLI
az sql db show-connection-string \
  --client ado.net \
  --server patchmindai \
  --name patchmindai
```

**Connect with Azure Data Studio and verify tables:**
- ✅ `Cves`
- ✅ `Assets`
- ✅ `PatchStatuses`
- ✅ `AnalysisJobs`
- ✅ `AnalysisResults`
- ✅ `__EFMigrationsHistory`

---

### **Step 5: Test Application Locally with Azure SQL**

Update your [appsettings.Development.json](src/PatchMindAI.API/appsettings.Development.json):

```json
{
  "ConnectionStrings": {
    "PatchMindAIDb": "Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patchmindai;User ID=<your-username>;Password=<your-password>;Encrypt=True;"
  }
}
```

Run locally:
```bash
cd src/PatchMindAI.API
dotnet run
```

**Expected Output:**
```
info: Database seeding completed. Added 20 CVEs, 15 assets, 23 patch statuses
info: Azure Search seeding completed. Uploaded 20 CVE documents
```

---

## 🔧 Configuration Files Reference

### **Local Development** (appsettings.json)
```json
{
  "ConnectionStrings": {
    "PatchMindAIDb": "Server=(localdb)\\mssqllocaldb;Database=PatchMindAI;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```
Uses SQL Server LocalDB on Windows, or can connect to Azure SQL for testing.

---

### **Azure Production** (appsettings.Production.json)
```json
{
  "ConnectionStrings": {
    "PatchMindAIDb": "Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patchmindai;Authentication=Active Directory Default;MultipleActiveResultSets=True;Encrypt=True;"
  }
}
```
Uses Managed Identity (passwordless authentication).

---

## 🔐 Security Best Practices

### ✅ DO:
- ✅ Use Managed Identity in production
- ✅ Store connection strings in Azure Key Vault
- ✅ Use Azure App Configuration for centralized config
- ✅ Enable Azure SQL firewall rules (allow Azure services)
- ✅ Use User Secrets for local development

### ❌ DON'T:
- ❌ Commit passwords to source control
- ❌ Use SQL authentication in production
- ❌ Store connection strings in appsettings.Production.json in the repo
- ❌ Disable SSL/encryption

---

### **Using User Secrets (Recommended for Local Development)**

```bash
cd src/PatchMindAI.API

# Initialize user secrets
dotnet user-secrets init

# Set connection string securely
dotnet user-secrets set "ConnectionStrings:PatchMindAIDb" "Server=tcp:patchmindai.database.windows.net,1433;Initial Catalog=patchmindai;User ID=<your-username>;Password=<your-password>;Encrypt=True;"
```

This stores the connection string **outside your source code** in:
- Windows: `%APPDATA%\Microsoft\UserSecrets\`
- Mac/Linux: `~/.microsoft/usersecrets/`

---

## 📊 Migration Commands Reference

```bash
# Create new migration
dotnet ef migrations add <MigrationName> --startup-project ../PatchMindAI.API

# Apply migrations to database
dotnet ef database update --startup-project ../PatchMindAI.API

# Rollback to specific migration
dotnet ef database update <MigrationName> --startup-project ../PatchMindAI.API

# Remove last migration (if not applied)
dotnet ef migrations remove --startup-project ../PatchMindAI.API

# Generate SQL script (for manual execution)
dotnet ef migrations script --startup-project ../PatchMindAI.API --output migration.sql
```

---

## 🚨 Troubleshooting

### Issue: "Cannot open server 'patchmindai' requested by the login"
**Solution:** Add your IP to Azure SQL firewall rules:
```bash
az sql server firewall-rule create \
  --resource-group <your-resource-group> \
  --server patchmindai \
  --name AllowMyIP \
  --start-ip-address <your-ip> \
  --end-ip-address <your-ip>
```

---

### Issue: "Login failed for user 'NT AUTHORITY\\SYSTEM'"
**Solution:** Managed Identity not configured. Run:
```bash
az webapp identity assign --name <your-app-name> --resource-group <your-resource-group>
```
Then grant database permissions (Step 2, Option B above).

---

### Issue: "A network-related or instance-specific error occurred"
**Solutions:**
1. ✅ Check firewall rules allow Azure services
2. ✅ Verify connection string format
3. ✅ Ensure port 1433 is open
4. ✅ Check database name is correct

---

### Issue: "The term 'dotnet' is not recognized"
**Solution:** Install .NET SDK:
- https://dotnet.microsoft.com/download

---

## 📝 Next Steps After Migration

1. ✅ **Create Migration:** `dotnet ef migrations add InitialAzureSqlMigration`
2. ✅ **Configure Connection String:** Update appsettings with your Azure SQL credentials
3. ✅ **Run Migration:** `dotnet ef database update` or deploy (auto-migrates)
4. ✅ **Test Locally:** Run app with Azure SQL connection
5. ✅ **Deploy to Azure:** App Service with Managed Identity
6. ✅ **Verify Azure Search:** Index gets seeded with CVE data

---

## 🎯 Summary

**Status:** ✅ Code migration complete, ready for Azure SQL deployment

**What Changed:**
- SQLite → Azure SQL Server
- UseSqlite() → UseSqlServer()
- Local file database → Cloud database
- No authentication → Managed Identity support

**What's Next:**
- Create EF Core migration
- Configure Azure SQL connection string
- Run migration
- Deploy to Azure

Your infrastructure is now production-ready for Azure deployment! 🚀
