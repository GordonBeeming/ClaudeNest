@description('The environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string

@description('The resource ID of the Application Insights instance')
param appInsightsId string

@description('The public base URL of the site (e.g., https://claudenest.app)')
param siteBaseUrl string

var tests = [
  {
    name: 'avail-web-${environmentName}'
    displayName: 'Web Frontend Health'
    url: '${siteBaseUrl}/health'
  }
  {
    name: 'avail-api-health-${environmentName}'
    displayName: 'API Health'
    url: '${siteBaseUrl}/api/health'
  }
  {
    name: 'avail-api-alive-${environmentName}'
    displayName: 'API Liveness'
    url: '${siteBaseUrl}/api/alive'
  }
  {
    name: 'avail-hub-${environmentName}'
    displayName: 'SignalR Hub'
    url: '${siteBaseUrl}/api/hubs/nest'
  }
]

resource availabilityTests 'Microsoft.Insights/webtests@2022-06-15' = [
  for test in tests: {
    name: test.name
    location: location
    tags: {
      'hidden-link:${appInsightsId}': 'Resource'
    }
    kind: 'standard'
    properties: {
      SyntheticMonitorId: test.name
      Name: test.displayName
      Enabled: true
      Frequency: 300
      Timeout: 30
      Kind: 'standard'
      RetryEnabled: true
      Locations: [
        { Id: 'us-va-ash-azr' }
        { Id: 'emea-au-syd-edge' }
        { Id: 'emea-gb-db3-azr' }
      ]
      Request: {
        RequestUrl: test.url
        HttpVerb: 'GET'
        ParseDependentRequests: false
      }
      ValidationRules: {
        ExpectedHttpStatusCode: 200
        SSLCheck: true
        SSLCertRemainingLifetimeCheck: 7
      }
    }
  }
]
