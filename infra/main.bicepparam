using './main.bicep'

param environmentName = 'dev'

// param location = 'australiaeast'  // Uncomment and set to your preferred region

// SQL Server admin password - REPLACE with a strong password
param sqlAdminPassword = '<SQL_ADMIN_PASSWORD>'

// Auth0 configuration
param auth0Authority = '<AUTH0_AUTHORITY>'  // e.g., https://your-tenant.auth0.com/
param auth0Audience = 'https://api.claudenest.com'
param auth0Domain = '<AUTH0_DOMAIN>'  // e.g., your-tenant.auth0.com
param auth0ClientId = '<AUTH0_CLIENT_ID>'

// Stripe configuration - REPLACE with actual keys
param stripeSecretKey = '<STRIPE_SECRET_KEY>'
param stripeWebhookSecret = '<STRIPE_WEBHOOK_SECRET>'
param stripePublishableKey = '<STRIPE_PUBLISHABLE_KEY>'
param stripeCurrency = 'aud'

// Container image tags
param backendImageTag = 'latest'
param frontendImageTag = 'latest'

// Agent version
param agentLatestVersion = '1.0.0'
