@description('Name of the App Service plan')
param appServicePlanName string = 'ctship-app'

@description('Name of the Web App')
param webAppName string = 'ctship-app'

@description('Location for all resources')
param location string = resourceGroup().location

@description('SKU for App Service plan')
param skuName string = 'P1v2'

@secure()
@description('Azure SQL connection string. Pass this from a secure parameter or pipeline secret.')
param sqlConnectionString string

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: skuName
    tier: 'PremiumV2'
  }
  properties: {
    reserved: false
  }
}

resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    httpsOnly: true
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      scmType: 'None'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Database__ApplyMigrationsOnStartup'
          value: 'true'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: sqlConnectionString
        }
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
      metadata: [
        {
          name: 'CURRENT_STACK'
          value: 'dotnet'
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: sqlConnectionString
          type: 'SQLAzure'
        }
      ]
    }
  }
}

output webAppDefaultHostName string = webApp.properties.defaultHostName

// Usage:
// az deployment group create -g <rg> -f infra/azurewebapp.bicep \
//   -p webAppName=<name> appServicePlanName=<plan> sqlConnectionString='<secret>'
