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

@description('The resource ID of the SQL private DNS zone')
param sqlDnsZoneId string

var sqlServerName = 'sql-claudenest-${environmentName}'
var sqlDatabaseName = 'sqldb-claudenest-${environmentName}'
var identityName = 'id-claudenest-${environmentName}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    primaryUserAssignedIdentityId: managedIdentityId
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Application'
      login: identityName
      sid: identityPrincipalId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'S0'
    tier: 'Standard'
    capacity: 10
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 268435456000 // 250 GB
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: 'pe-sql-${environmentName}'
  location: location
  properties: {
    subnet: {
      id: peSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'pe-sql-${environmentName}'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: privateEndpoint
  name: 'sqlDnsZoneGroup'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql'
        properties: {
          privateDnsZoneId: sqlDnsZoneId
        }
      }
    ]
  }
}

var connectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Default;User Id=${identityClientId};Connection Timeout=120;'

@description('The fully qualified domain name of the SQL Server')
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The connection string for the SQL Database')
output connectionString string = connectionString
