# CTSHIPDashboard Docker deployment to Azure App Service

This deployment path uses the existing CTSHIPDashboard .NET 9 MVC/Razor/Identity project, current `Program.cs`, current EF Core `ApplicationDbContext`, the Azure SQL connection string, and the GitHub repository setup.

The goal is:

```text
CTSHIPDashboard source code
  -> Dockerfile
  -> GitHub Actions
  -> Build Docker image
  -> Push image to Azure Container Registry
  -> Azure App Service pulls image
  -> Container starts CTSHIPDashboard.dll
  -> CTSHIPDashboard connects to Azure SQL
  -> https://ctship-app.azurewebsites.net
```

## Files in this repo

- `Dockerfile`
- `.dockerignore`
- `.github/workflows/docker_ctship-app.yml`

## Dockerfile

The app is built with the .NET 9 SDK image and runs on the .NET 9 ASP.NET runtime image.

Runtime command:

```text
dotnet CTSHIPDashboard.dll
```

Container port:

```text
8080
```

App Service must have:

```text
WEBSITES_PORT=8080
ASPNETCORE_URLS=http://+:8080
```

## GitHub repository settings

### Required secrets

```text
CTSHIPDASHBOARD_SPN
AZURE_SQL_CONNECTION_STRING
```

`CTSHIPDASHBOARD_SPN` is the Azure service principal JSON used by `azure/login@v2`.

`AZURE_SQL_CONNECTION_STRING` should be:

```text
Server=tcp:ctship-app-server.database.windows.net,1433;Initial Catalog=ctship-app-database;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;
```

The workflow converts this as needed:

- App Service container runtime uses:

```text
Authentication=Active Directory Managed Identity
```

- Optional GitHub Actions EF migration step uses:

```text
Authentication=Active Directory Default
```

That allows the already logged-in Azure service principal to apply migrations from the GitHub runner.

### Required repository variable

If your Azure Container Registry is not named `ctshipacr`, set:

```text
ACR_NAME
```

Example:

```text
ctshipappacr
```

## One-time Azure resources

### 1. Create Azure Container Registry

```powershell
az acr create `
  --resource-group <resource-group-name> `
  --name <acr-name> `
  --sku Basic
```

### 2. Use a Linux App Service plan

Container deployment should use Linux App Service. Do not try to run this Linux container on the old Windows zip-deploy App Service.

```powershell
az appservice plan create `
  --resource-group <resource-group-name> `
  --name ctship-container-plan `
  --is-linux `
  --sku B1
```

### 3. Create the Linux container Web App

```powershell
az webapp create `
  --resource-group <resource-group-name> `
  --plan ctship-container-plan `
  --name ctship-app `
  --deployment-container-image-name mcr.microsoft.com/dotnet/aspnet:9.0
```

If `ctship-app` already exists as a Windows App Service, create a new Linux app name, for example:

```text
ctship-app-container
```

Then update `WEB_APP_NAME` in `.github/workflows/docker_ctship-app.yml`.

## Azure SQL identity setup

Your application uses:

```text
Authentication=Active Directory Managed Identity
```

So the App Service managed identity must be created as a user inside Azure SQL and granted permissions.

Run this against `ctship-app-database` as an Azure SQL Entra admin:

```sql
CREATE USER [ctship-app] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [ctship-app];
ALTER ROLE db_datawriter ADD MEMBER [ctship-app];
ALTER ROLE db_ddladmin ADD MEMBER [ctship-app];
```

If your App Service name is different, use that managed identity name instead.

For GitHub Actions EF migrations, the Azure service principal also needs SQL permissions. Create a user for the service principal display name or app registration name, then grant migration permissions:

```sql
CREATE USER [<github-actions-service-principal-name>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [<github-actions-service-principal-name>];
ALTER ROLE db_datawriter ADD MEMBER [<github-actions-service-principal-name>];
ALTER ROLE db_ddladmin ADD MEMBER [<github-actions-service-principal-name>];
```

## GitHub Actions workflow behavior

Workflow file:

```text
.github/workflows/docker_ctship-app.yml
```

It does the following:

1. Logs in to Azure using `CTSHIPDASHBOARD_SPN`.
2. Finds the resource group for `ctship-app`.
3. Finds the ACR login server.
4. Builds Docker image:

```text
<acr>.azurecr.io/ctshipdashboard:<commit-sha>
<acr>.azurecr.io/ctshipdashboard:latest
```

5. Pushes both tags to ACR.
6. Enables managed identity on the Web App.
7. Grants the Web App identity `AcrPull` on ACR.
8. Sets App Service settings:

```text
WEBSITES_PORT=8080
ASPNETCORE_URLS=http://+:8080
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
Database__ApplyMigrationsOnStartup=false
ConnectionStrings__DefaultConnection=<Azure SQL connection string>
```

9. Sets App Service connection string:

```text
DefaultConnection=<Azure SQL connection string>
```

10. Optionally applies EF Core migrations if manually triggered with `apply_migrations=true`.
11. Points App Service to the new image.
12. Restarts App Service.
13. Verifies:

```text
https://<app-hostname>/alive
```

Expected response:

```json
{"status":"alive","application":"CTSHIPDashboard"}
```

## EF Core migrations deployment

Migrations are not run automatically on every push.

To apply migrations:

1. Go to GitHub Actions.
2. Select `Build and deploy CTSHIPDashboard Docker image`.
3. Click `Run workflow`.
4. Set:

```text
apply_migrations = true
```

The workflow runs:

```bash
dotnet ef database update \
  --project CTSHIPDashboard.csproj \
  --startup-project CTSHIPDashboard.csproj \
  --connection "$MIGRATION_CONNECTION_STRING" \
  --verbose
```

For normal app deployments, leave migrations off.

## Troubleshooting

### `/alive` does not return JSON

Check container logs:

```powershell
az webapp log tail `
  --resource-group <resource-group-name> `
  --name ctship-app
```

### ACR pull fails

Confirm the Web App managed identity has `AcrPull` on the registry:

```powershell
az role assignment list `
  --assignee <web-app-principal-id> `
  --scope <acr-resource-id> `
  --output table
```

### SQL login fails

Confirm:

- Azure SQL Entra admin is configured.
- App Service managed identity has a SQL user.
- GitHub service principal has a SQL user if using workflow migrations.
- Connection string uses `Authentication=Active Directory Managed Identity` for the running app.

### App starts but login fails

Confirm forwarded headers and HTTPS settings:

```text
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ASPNETCORE_ENVIRONMENT=Production
```
