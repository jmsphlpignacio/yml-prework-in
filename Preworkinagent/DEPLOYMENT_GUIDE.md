# Pre Work In Agent - Deployment Guide

## Overview

This guide covers deploying the Pre Work In Agent to Azure App Service via GitHub Actions.

## Prerequisites

- Azure subscription with App Service created
- GitHub account
- Azure CLI (optional, for manual deployments)
- .NET 9.0 SDK (for local development)

---

## Repository Structure

```
Preworkinagent/
├── .github/workflows/
│   └── deploy-azure.yml      # GitHub Actions CI/CD pipeline
├── Preworkinagent/
│   ├── Functions/            # Bot function handlers
│   ├── MainController.cs     # Main bot controller
│   ├── Program.cs            # Application entry point
│   ├── appsettings.json      # Configuration (no secrets)
│   └── Preworkinagent.csproj # Project file
├── .gitignore
└── Preworkinagent.sln
```

---

## Step 1: Configure GitHub Secrets

Before deploying, add these secrets in your GitHub repository:

1. Go to **Settings** > **Secrets and variables** > **Actions**
2. Click **New repository secret** and add:

| Secret Name | Description |
|-------------|-------------|
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Azure App Service publish profile XML |
| `AZURE_OPENAI_KEY` | Azure OpenAI API key |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint URL |
| `SQL_CONNECTION_STRING` | SQL Server connection string |

### Getting the Azure Publish Profile

1. Go to Azure Portal > Your App Service
2. Click **Download publish profile**
3. Copy the entire XML content
4. Paste as the `AZURE_WEBAPP_PUBLISH_PROFILE` secret value

---

## Step 2: Configure Azure App Service

### Application Settings

In Azure Portal > App Service > **Configuration** > **Application settings**, add:

| Name | Value |
|------|-------|
| `AzureOpenAIKey` | Your Azure OpenAI API key |
| `AzureOpenAIModel` | `gpt-4o` (or your model) |
| `AzureOpenAIEndpoint` | Your Azure OpenAI endpoint |
| `SqlConnectionString` | Your SQL connection string |

### General Settings

- **Stack**: .NET
- **Version**: .NET 9 (LTS)
- **Platform**: 64 Bit

---

## Step 3: Deploy to GitHub

### Initial Setup

```bash
# Navigate to the Preworkinagent folder
cd "Pre Work In/Preworkinagent"

# Initialize git repository
git init

# Add all files
git add .

# Initial commit
git commit -m "Initial commit - Pre Work In Agent"

# Add remote repository
git remote add origin https://github.com/YOUR_USERNAME/preworkin-agent.git

# Push to main branch
git push -u origin main
```

### Subsequent Deployments

```bash
# Stage changes
git add .

# Commit
git commit -m "Your commit message"

# Push (triggers automatic deployment)
git push
```

---

## Step 4: CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/deploy-azure.yml`) automatically:

1. **Triggers** on push to `main` branch or manual dispatch
2. **Builds** the .NET 9.0 application
3. **Publishes** the release artifacts
4. **Deploys** to Azure App Service

### Manual Deployment

To trigger manually:
1. Go to **Actions** tab in GitHub
2. Select **Deploy to Azure** workflow
3. Click **Run workflow**

---

## Step 5: Database Setup

Run the SQL scripts in order on your Azure SQL database:

```
Database/
├── 01_Schema_Tables.sql      # Tables and schema
├── 02_Views.sql              # Database views
├── 03_StoredProcedures.sql   # Stored procedures
└── 04_Alter_RemoveTeams.sql  # Alterations
```

### Using Azure Data Studio or SSMS

```bash
# Connect to your Azure SQL Server
Server: ymldbserver1.database.windows.net
Database: synchubdbxpm
Authentication: SQL Login
```

---

## Local Development

### Setup

1. Clone the repository
2. Create `appsettings.Development.json`:

```json
{
  "AzureOpenAIKey": "your-key-here",
  "AzureOpenAIModel": "gpt-4o",
  "AzureOpenAIEndpoint": "https://your-resource.cognitiveservices.azure.com/",
  "SqlConnectionString": "your-connection-string"
}
```

3. Run the application:

```bash
cd Preworkinagent/Preworkinagent
dotnet run
```

---

## Troubleshooting

### Build Failures

- Ensure .NET 9.0 SDK is installed
- Check `dotnet restore` for package issues
- Verify project path in workflow file

### Deployment Failures

- Verify publish profile is correct and not expired
- Check Azure App Service is running
- Review GitHub Actions logs

### Runtime Errors

- Check Application Insights logs in Azure
- Verify all application settings are configured
- Test database connectivity

---

## Security Notes

- Never commit secrets to the repository
- Use Azure Key Vault for production secrets
- Rotate API keys periodically
- Enable managed identity where possible

---

## Support

For issues, check:
- GitHub Actions logs
- Azure App Service logs
- Application Insights

**Last Updated:** January 2026
