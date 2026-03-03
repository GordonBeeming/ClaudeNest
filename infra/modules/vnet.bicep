@description('The environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string

var vnetName = 'vnet-claudenest-${environmentName}'
var caeSubnetName = 'snet-cae-${environmentName}'
var peSubnetName = 'snet-pe-${environmentName}'

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: caeSubnetName
        properties: {
          addressPrefix: '10.0.0.0/23'
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: peSubnetName
        properties: {
          addressPrefix: '10.0.2.0/24'
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

@description('The resource ID of the VNet')
output vnetId string = vnet.id

@description('The resource ID of the Container Apps Environment subnet')
output caeSubnetId string = vnet.properties.subnets[0].id

@description('The resource ID of the Private Endpoints subnet')
output peSubnetId string = vnet.properties.subnets[1].id
