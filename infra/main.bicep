targetScope = 'resourceGroup'

// ============================================================================
// Parameters
// ============================================================================

@description('The environment name')
@allowed(['dev', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('The Auth0 authority URL (e.g., https://your-tenant.auth0.com/)')
@secure()
param auth0Authority string

@description('The Auth0 audience (API identifier)')
param auth0Audience string

@description('The Auth0 domain (e.g., your-tenant.auth0.com)')
param auth0Domain string

@description('The Auth0 client ID for the frontend')
param auth0ClientId string

@description('The Stripe secret key')
@secure()
param stripeSecretKey string

@description('The Stripe webhook secret')
@secure()
param stripeWebhookSecret string

@description('The Stripe publishable key')
param stripePublishableKey string

@description('The Stripe currency')
param stripeCurrency string = 'aud'

@description('The container image tag for the backend')
param backendImageTag string = 'latest'

@description('The container image tag for the frontend')
param frontendImageTag string = 'latest'

@description('The latest agent version advertised to agents')
param agentLatestVersion string = '1.0.0'

@description('The Cloudflare Tunnel token')
@secure()
param cloudflareTunnelToken string

// ============================================================================
// Module 1: VNet
// ============================================================================

module vnet 'modules/vnet.bicep' = {
  name: 'vnet'
  params: {
    environmentName: environmentName
    location: location
  }
}

// ============================================================================
// Module 2: Managed Identity
// ============================================================================

module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managed-identity'
  params: {
    environmentName: environmentName
    location: location
  }
}

// ============================================================================
// Module 3: Private DNS Zones (depends on VNet)
// ============================================================================

module privateDnsZones 'modules/private-dns-zones.bicep' = {
  name: 'private-dns-zones'
  params: {
    vnetId: vnet.outputs.vnetId
  }
}

// ============================================================================
// Module 4: SQL Server (depends on Identity, VNet, DNS Zones)
// ============================================================================

module sqlServer 'modules/sql-server.bicep' = {
  name: 'sql-server'
  params: {
    environmentName: environmentName
    location: location
    identityPrincipalId: managedIdentity.outputs.identityPrincipalId
    identityClientId: managedIdentity.outputs.identityClientId
    managedIdentityId: managedIdentity.outputs.identityId
    peSubnetId: vnet.outputs.peSubnetId
    sqlDnsZoneId: privateDnsZones.outputs.sqlDnsZoneId
  }
}

// ============================================================================
// Module 5: Azure SignalR Service (depends on Identity, VNet, DNS Zones)
// ============================================================================

module signalr 'modules/signalr.bicep' = {
  name: 'signalr'
  params: {
    environmentName: environmentName
    location: location
    identityPrincipalId: managedIdentity.outputs.identityPrincipalId
    identityClientId: managedIdentity.outputs.identityClientId
    managedIdentityId: managedIdentity.outputs.identityId
    peSubnetId: vnet.outputs.peSubnetId
    signalrDnsZoneId: privateDnsZones.outputs.signalrDnsZoneId
  }
}

// ============================================================================
// Module 6: Key Vault (depends on Identity, SQL, SignalR, VNet, DNS Zones)
// ============================================================================

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    environmentName: environmentName
    location: location
    identityPrincipalId: managedIdentity.outputs.identityPrincipalId
    sqlConnectionString: sqlServer.outputs.connectionString
    auth0Authority: auth0Authority
    stripeSecretKey: stripeSecretKey
    stripeWebhookSecret: stripeWebhookSecret
    cloudflareTunnelToken: cloudflareTunnelToken
    signalrConnectionString: signalr.outputs.connectionString
    peSubnetId: vnet.outputs.peSubnetId
    kvDnsZoneId: privateDnsZones.outputs.kvDnsZoneId
  }
}

// ============================================================================
// Module 7: Container Registry (depends on Identity)
// ============================================================================

module containerRegistry 'modules/container-registry.bicep' = {
  name: 'container-registry'
  params: {
    environmentName: environmentName
    location: location
    identityPrincipalId: managedIdentity.outputs.identityPrincipalId
  }
}

// ============================================================================
// Module 8: Container Apps Environment (depends on VNet)
// ============================================================================

module containerAppsEnv 'modules/container-apps-env.bicep' = {
  name: 'container-apps-env'
  params: {
    environmentName: environmentName
    location: location
    infrastructureSubnetId: vnet.outputs.caeSubnetId
  }
}

// ============================================================================
// Module 9: Backend Container App (depends on Env + KV + ACR + Identity)
// ============================================================================

module backendApp 'modules/container-app-backend.bicep' = {
  name: 'container-app-backend'
  params: {
    environmentName: environmentName
    location: location
    containerAppsEnvironmentId: containerAppsEnv.outputs.environmentId
    managedIdentityId: managedIdentity.outputs.identityId
    managedIdentityClientId: managedIdentity.outputs.identityClientId
    acrLoginServer: containerRegistry.outputs.loginServer
    kvName: keyVault.outputs.kvName
    auth0Audience: auth0Audience
    stripePublishableKey: stripePublishableKey
    stripeCurrency: stripeCurrency
    imageTag: backendImageTag
    agentLatestVersion: agentLatestVersion
    appInsightsConnectionString: containerAppsEnv.outputs.appInsightsConnectionString
  }
}

// ============================================================================
// Module 10: Frontend Container App (depends on Env + ACR)
// ============================================================================

module frontendApp 'modules/container-app-frontend.bicep' = {
  name: 'container-app-frontend'
  params: {
    environmentName: environmentName
    location: location
    containerAppsEnvironmentId: containerAppsEnv.outputs.environmentId
    managedIdentityId: managedIdentity.outputs.identityId
    acrLoginServer: containerRegistry.outputs.loginServer
    imageTag: frontendImageTag
  }
}

// ============================================================================
// Module 11: Cloudflared Tunnel (depends on Env + KV)
// ============================================================================

module cloudflared 'modules/cloudflared.bicep' = {
  name: 'cloudflared'
  params: {
    environmentName: environmentName
    location: location
    containerAppsEnvironmentId: containerAppsEnv.outputs.environmentId
    managedIdentityId: managedIdentity.outputs.identityId
    kvName: keyVault.outputs.kvName
  }
}

// ============================================================================
// Module 12: Availability Tests (depends on Container Apps Env for App Insights)
// ============================================================================

module availabilityTests 'modules/availability-tests.bicep' = {
  name: 'availability-tests'
  params: {
    environmentName: environmentName
    location: location
    appInsightsId: containerAppsEnv.outputs.appInsightsId
    siteBaseUrl: 'https://claudenest.app'
  }
}

// ============================================================================
// Outputs
// ============================================================================

@description('The login server URL of the container registry')
output acrLoginServer string = containerRegistry.outputs.loginServer

@description('The FQDN of the backend Container App')
output backendFqdn string = backendApp.outputs.fqdn

@description('The URL of the backend Container App')
output backendUrl string = backendApp.outputs.url

@description('The FQDN of the frontend Container App')
output frontendFqdn string = frontendApp.outputs.fqdn

@description('The URL of the frontend Container App')
output frontendUrl string = frontendApp.outputs.url

@description('The fully qualified domain name of the SQL Server')
output sqlServerFqdn string = sqlServer.outputs.serverFqdn

@description('The name of the Key Vault')
output keyVaultName string = keyVault.outputs.kvName

@description('The client ID of the managed identity')
output managedIdentityClientId string = managedIdentity.outputs.identityClientId

@description('The Application Insights connection string')
output appInsightsConnectionString string = containerAppsEnv.outputs.appInsightsConnectionString
