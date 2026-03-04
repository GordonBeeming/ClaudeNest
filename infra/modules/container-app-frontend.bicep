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

@description('The container image tag for the frontend')
param imageTag string = 'latest'

var appName = 'ca-claudenest-web-${environmentName}'
var imageName = '${acrLoginServer}/claudenest-frontend:${imageTag}'

resource frontendApp 'Microsoft.App/containerApps@2024-03-01' = {
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
    }
    template: {
      containers: [
        {
          name: 'frontend'
          image: imageName
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 2
              periodSeconds: 5
              failureThreshold: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
}

@description('The FQDN of the frontend Container App')
output fqdn string = frontendApp.properties.configuration.ingress.fqdn

@description('The URL of the frontend Container App')
output url string = 'https://${frontendApp.properties.configuration.ingress.fqdn}'
