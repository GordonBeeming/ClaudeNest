@description('The environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string

@description('The principal ID of the managed identity to grant Key Vault access')
param identityPrincipalId string

@description('The SQL connection string to store as a secret')
@secure()
param sqlConnectionString string

@description('The Auth0 authority URL to store as a secret')
@secure()
param auth0Authority string

@description('The Stripe secret key to store as a secret')
@secure()
param stripeSecretKey string

@description('The Stripe webhook secret to store as a secret')
@secure()
param stripeWebhookSecret string

@description('The Cloudflare Tunnel token to store as a secret')
@secure()
param cloudflareTunnelToken string

@description('The Azure SignalR connection string to store as a secret')
@secure()
param signalrConnectionString string

@description('The resource ID of the Private Endpoints subnet')
param peSubnetId string

@description('The resource ID of the Key Vault private DNS zone')
param kvDnsZoneId string

var kvName = 'kv-claudenest-${environmentName}'

// Key Vault Secrets User role definition ID
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
  }
}

// Assign Key Vault Secrets User role to the managed identity
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, identityPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: identityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'sql-connection-string'
  properties: {
    value: sqlConnectionString
  }
}

resource auth0AuthoritySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'auth0-authority'
  properties: {
    value: auth0Authority
  }
}

resource stripeSecretKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'stripe-secret-key'
  properties: {
    value: stripeSecretKey
  }
}

resource stripeWebhookSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'stripe-webhook-secret'
  properties: {
    value: stripeWebhookSecret
  }
}

resource cloudflareTunnelTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'cloudflare-tunnel-token'
  properties: {
    value: cloudflareTunnelToken
  }
}

resource signalrConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'azure-signalr-connection-string'
  properties: {
    value: signalrConnectionString
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: 'pe-kv-${environmentName}'
  location: location
  properties: {
    subnet: {
      id: peSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'pe-kv-${environmentName}'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: privateEndpoint
  name: 'kvDnsZoneGroup'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'kv'
        properties: {
          privateDnsZoneId: kvDnsZoneId
        }
      }
    ]
  }
}

@description('The name of the Key Vault')
output kvName string = keyVault.name

@description('The URI of the Key Vault')
output kvUri string = keyVault.properties.vaultUri
