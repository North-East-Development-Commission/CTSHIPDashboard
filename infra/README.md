# Azure deployment setup

This project deploys to the ctship-app Azure App Service and uses an existing
Azure SQL database. Never commit the production connection string.

## Azure SQL connection string

Use an encrypted Azure SQL connection string in this form:

~~~text
Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;MultipleActiveResultSets=True;
~~~

The Azure SQL firewall must allow connections from the App Service. Add the App
Service outbound IP addresses to the SQL server firewall, or use private
networking when the app and database are integrated with a virtual network.

## GitHub Actions

Create the AZURE_SQL_CONNECTION_STRING repository secret containing the complete
Azure SQL connection string.

The workflow already expects the Azure OIDC client, tenant, and subscription
secrets generated for the App Service. On deployment it stores the SQL value as
the App Service DefaultConnection connection string and enables
Database__ApplyMigrationsOnStartup.

## Azure DevOps

Create a secret pipeline variable named AzureSqlConnectionString. Confirm that
the azureSubscription variable in azure-pipelines.yml is the name of an Azure
Resource Manager service connection with permission to configure and deploy
ctship-app.

## Infrastructure template

The Bicep template configures a Windows App Service with HTTPS-only access,
TLS 1.2, health checks, a system-assigned identity, the SQL connection string,
and production migration settings. Supply the connection string as a secure
parameter:

~~~powershell
az deployment group create --resource-group <resource-group> --template-file infra/azurewebapp.bicep --parameters webAppName=ctship-app sqlConnectionString='<connection-string>'
~~~

The template configures an existing Azure SQL database; it does not create or
delete a SQL server or database.

## Migrations and health

Production deployments set Database__ApplyMigrationsOnStartup=true. The app
runs pending Entity Framework migrations before startup data initialization.
SQL transient errors are retried. The /health endpoint returns HTTP 200 only
when the database is reachable and no EF migrations are pending, and both
deployment workflows wait for that result.

Local development keeps startup migrations disabled. Run migrations explicitly
when needed:

~~~powershell
dotnet ef database update
~~~

The migration process does not invoke the demo-data methods in Data/SeedData.cs.

