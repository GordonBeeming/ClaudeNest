@description('The environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string

@description('The resource ID of the Container Apps Environment')
param containerAppsEnvironmentId string

@description('The resource ID of the user-assigned managed identity')
param managedIdentityId string

@description('The login server URL of the container registry')
param acrLoginServer string

@description('The name of the Key Vault (for secret references)')
param kvName string

@description('The Auth0 audience')
param auth0Audience string

@description('The Stripe publishable key')
param stripePublishableKey string

@description('The Stripe currency')
param stripeCurrency string = 'aud'

@description('The container image tag for the backend')
param imageTag string = 'latest'

@description('The latest agent version')
param agentLatestVersion string = '1.0.0'

@description('The Application Insights connection string')
param appInsightsConnectionString string

@description('Email address to promote to admin on startup')
param adminSeedEmail string = ''

@description('The client ID of the managed identity')
param managedIdentityClientId string

var appName = 'ca-claudenest-api-${environmentName}'
var imageName = '${acrLoginServer}/claudenest-backend:${imageTag}'

resource backendApp 'Microsoft.App/containerApps@2024-03-01' = {
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
      ingress: {
        external: false
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: acrLoginServer
          identity: managedIdentityId
        }
      ]
      secrets: [
        {
          name: 'sql-connection-string'
          keyVaultUrl: 'https://${kvName}.vault.azure.net/secrets/sql-connection-string'
          identity: managedIdentityId
        }
        {
          name: 'auth0-authority'
          keyVaultUrl: 'https://${kvName}.vault.azure.net/secrets/auth0-authority'
          identity: managedIdentityId
        }
        {
          name: 'stripe-secret-key'
          keyVaultUrl: 'https://${kvName}.vault.azure.net/secrets/stripe-secret-key'
          identity: managedIdentityId
        }
        {
          name: 'stripe-webhook-secret'
          keyVaultUrl: 'https://${kvName}.vault.azure.net/secrets/stripe-webhook-secret'
          identity: managedIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'backend'
          image: imageName
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__nestdb'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'Auth0__Authority'
              secretRef: 'auth0-authority'
            }
            {
              name: 'Auth0__Audience'
              value: auth0Audience
            }
            {
              name: 'Stripe__SecretKey'
              secretRef: 'stripe-secret-key'
            }
            {
              name: 'Stripe__WebhookSecret'
              secretRef: 'stripe-webhook-secret'
            }
            {
              name: 'Stripe__PublishableKey'
              value: stripePublishableKey
            }
            {
              name: 'Stripe__Currency'
              value: stripeCurrency
            }
            {
              name: 'Cors__Origins__0'
              value: 'https://claudenest.app'
            }
            {
              name: 'Stripe__SuccessUrl'
              value: 'https://claudenest.app/plans?success=true'
            }
            {
              name: 'Stripe__CancelUrl'
              value: 'https://claudenest.app/plans?cancelled=true'
            }
            {
              name: 'Stripe__BillingPortalReturnUrl'
              value: 'https://claudenest.app/account'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: managedIdentityClientId
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: '8080'
            }
            {
              name: 'Agent__LatestVersion'
              value: agentLatestVersion
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsightsConnectionString
            }
            {
              name: 'AdminSeedEmail'
              value: adminSeedEmail
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

@description('The FQDN of the backend Container App')
output fqdn string = backendApp.properties.configuration.ingress.fqdn

@description('The URL of the backend Container App')
output url string = 'https://${backendApp.properties.configuration.ingress.fqdn}'
