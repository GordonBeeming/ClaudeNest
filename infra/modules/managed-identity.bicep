@description('The environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string

var identityName = 'id-claudenest-${environmentName}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

@description('The resource ID of the managed identity')
output identityId string = managedIdentity.id

@description('The client ID of the managed identity')
output identityClientId string = managedIdentity.properties.clientId

@description('The principal ID of the managed identity')
output identityPrincipalId string = managedIdentity.properties.principalId
