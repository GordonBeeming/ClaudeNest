@description('The environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string

@description('The principal ID (object ID) of the managed identity')
param identityPrincipalId string

@description('The client ID of the managed identity')
param identityClientId string

@description('The resource ID of the user-assigned managed identity')
param managedIdentityId string

@description('The resource ID of the Private Endpoints subnet')
param peSubnetId string

@description('The resource ID of the SignalR private DNS zone')
param signalrDnsZoneId string

var signalrName = 'signalr-claudenest-${environmentName}'

// SignalR App Server role definition ID
var signalrAppServerRoleId = '420fcaa2-552c-430f-98ca-3264be4806c7'

resource signalr 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: signalrName
  location: location
  sku: {
    name: 'Standard_S1'
    capacity: 1
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
      {
        flag: 'EnableConnectivityLogs'
        value: 'True'
      }
    ]
    publicNetworkAccess: 'Disabled'
  }
}

// Assign SignalR App Server role to the managed identity
resource signalrAppServerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(signalr.id, identityPrincipalId, signalrAppServerRoleId)
  scope: signalr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', signalrAppServerRoleId)
    principalId: identityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: 'pe-signalr-${environmentName}'
  location: location
  properties: {
    subnet: {
      id: peSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'pe-signalr-${environmentName}'
        properties: {
          privateLinkServiceId: signalr.id
          groupIds: [
            'signalr'
          ]
        }
      }
    ]
  }
}

resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: privateEndpoint
  name: 'signalrDnsZoneGroup'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'signalr'
        properties: {
          privateDnsZoneId: signalrDnsZoneId
        }
      }
    ]
  }
}

var connectionString = 'Endpoint=https://${signalr.properties.hostName};AuthType=azure.msi;ClientId=${identityClientId};Version=1.0;'

@description('The connection string for Azure SignalR Service')
output connectionString string = connectionString
