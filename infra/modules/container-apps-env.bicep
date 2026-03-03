@description('The environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string

@description('The resource ID of the Container Apps Environment infrastructure subnet')
param infrastructureSubnetId string

var envName = 'cae-claudenest-${environmentName}'
var logAnalyticsName = 'log-claudenest-${environmentName}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: envName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
    vnetConfiguration: {
      infrastructureSubnetId: infrastructureSubnetId
      internal: true
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'ai-claudenest-${environmentName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    RetentionInDays: 30
  }
}

@description('The resource ID of the Container Apps Environment')
output environmentId string = containerAppsEnvironment.id

@description('The default domain of the Container Apps Environment')
output defaultDomain string = containerAppsEnvironment.properties.defaultDomain

@description('The static IP of the Container Apps Environment')
output staticIp string = containerAppsEnvironment.properties.staticIp

@description('The Application Insights connection string')
output appInsightsConnectionString string = appInsights.properties.ConnectionString
