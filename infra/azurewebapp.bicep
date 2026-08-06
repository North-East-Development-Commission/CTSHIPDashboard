@description('Name of the App Service plan')
param appServicePlanName string = 'ctship-asp'

@description('Name of the Web App')
param webAppName string = 'ctship-webapp'

@description('Location for all resources')
param location string = resourceGroup().location

@description('SKU for App Service plan')
param skuName string = 'P1v2'

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
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: ''
      scmType: 'None'
      alwaysOn: true
      linuxFxVersion: ''
    }
  }
}

output webAppDefaultHostName string = webApp.properties.defaultHostName

// Usage:
// az deployment group create -g <rg> -f infra/azurewebapp.bicep -p webAppName=<name> appServicePlanName=<plan>
