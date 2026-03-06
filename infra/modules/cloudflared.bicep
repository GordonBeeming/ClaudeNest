@description('The environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string

@description('The resource ID of the Container Apps Environment')
param containerAppsEnvironmentId string

@description('The resource ID of the user-assigned managed identity')
param managedIdentityId string

@description('The name of the Key Vault (for secret references)')
param kvName string

var appName = 'ca-claudenest-tunnel-${environmentName}'

resource cloudflaredApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: [
        {
          name: 'cloudflare-tunnel-token'
          keyVaultUrl: 'https://${kvName}.vault.azure.net/secrets/cloudflare-tunnel-token'
          identity: managedIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'cloudflared'
          image: 'cloudflare/cloudflared:latest'
          command: [
            'cloudflared'
            'tunnel'
            '--no-autoupdate'
            'run'
          ]
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
          env: [
            {
              name: 'TUNNEL_TOKEN'
              secretRef: 'cloudflare-tunnel-token'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}
