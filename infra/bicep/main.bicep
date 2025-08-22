@description('Environment name (dev, staging, prod)')
param environmentName string = 'dev'

@description('Azure region for deployment')
param location string = resourceGroup().location

@description('Unique suffix for resource names')
param uniqueSuffix string = substring(uniqueString(resourceGroup().id), 0, 6)

@description('SQL Server administrator login')
param sqlAdminLogin string = 'sqladmin'

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('Service Bus namespace name')
param serviceBusNamespaceName string = 'bidone-sb-${environmentName}-${uniqueSuffix}'

@description('Container Registry name')
param containerRegistryName string = 'bidonecr${uniqueSuffix}'

@description('Key Vault name')
param keyVaultName string = 'bidone-kv-${environmentName}-${uniqueSuffix}'

@description('SQL Server name')
param sqlServerName string = 'bidone-sql-${environmentName}-${uniqueSuffix}'

@description('Cosmos DB account name')
param cosmosDbAccountName string = 'bidone-cosmos-${environmentName}-${uniqueSuffix}'

@description('Application Insights name')
param appInsightsName string = 'bidone-insights-${environmentName}-${uniqueSuffix}'

@description('Log Analytics workspace name')
param logAnalyticsName string = 'bidone-logs-${environmentName}-${uniqueSuffix}'

@description('Container Apps environment name')
param containerAppsEnvName string = 'bidone-env-${environmentName}-${uniqueSuffix}'

@description('API Management service name')
param apimServiceName string = 'bidone-apim-${environmentName}-${uniqueSuffix}'

@description('Function App name')
param functionAppName string = 'bidone-func-${environmentName}-${uniqueSuffix}'

@description('Customer Communication Function App name')
param customerCommFunctionAppName string = 'bidone-custcomm-${environmentName}-${uniqueSuffix}'

@description('Storage account name for Function App')
param storageAccountName string = 'bidonest${environmentName}${uniqueSuffix}'

@description('Logic App name')
param logicAppName string = 'bidone-logic-${environmentName}-${uniqueSuffix}'

@description('Event Grid topic name')
param eventGridTopicName string = 'bidone-events-${environmentName}-${uniqueSuffix}'

// Variables for resource configuration
var tags = {
  Environment: environmentName
  Project: 'BidOne-Integration-Demo'
  ManagedBy: 'Bicep'
}

var serviceBusQueueNames = [
  'order-received'
  'order-validated'
  'order-enriched' 
  'order-confirmed'
  'order-failed'
  'high-value-errors'
]

var eventGridEventTypes = [
  'BidOne.Dashboard.OrderMetrics'
  'BidOne.Dashboard.PerformanceAlert'
  'BidOne.Dashboard.SystemHealth'
]

var cosmosDbContainers = [
  {
    name: 'Products'
    partitionKey: '/category'
  }
  {
    name: 'CustomerProfiles'
    partitionKey: '/customerId'
  }
  {
    name: 'OrderEvents'
    partitionKey: '/orderId'
  }
]

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: environmentName == 'prod' ? 90 : 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enableRbacAuthorization: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: environmentName == 'prod' ? 'Premium' : 'Standard'
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
    networkRuleBypassOptions: 'AzureServices'
  }
}

// Service Bus Namespace
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    zoneRedundant: environmentName == 'prod'
  }
}

// Service Bus Queues
resource serviceBusQueues 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = [for queueName in serviceBusQueueNames: {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P14D'
    maxDeliveryCount: 5
    lockDuration: 'PT5M'
    enablePartitioning: false
    enableDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    deadLetteringOnMessageExpiration: true
    enableBatchedOperations: true
  }
}]

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2023-02-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-02-01-preview' = {
  parent: sqlServer
  name: 'BidOneDB'
  location: location
  tags: tags
  sku: {
    name: environmentName == 'prod' ? 'S2' : 'S1'
    tier: 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: environmentName == 'prod' ? 268435456000 : 107374182400 // 250GB for prod, 100GB for others
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: environmentName == 'prod'
    readScale: environmentName == 'prod' ? 'Enabled' : 'Disabled'
    autoPauseDelay: environmentName == 'dev' ? 60 : -1 // Auto-pause in dev environment
  }
}

// SQL Server Firewall Rule for Azure Services
resource sqlServerFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-02-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Cosmos DB Account
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosDbAccountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: environmentName == 'prod'
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    publicNetworkAccess: 'Enabled'
    enableFreeTier: environmentName == 'dev'
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: environmentName == 'prod' ? 240 : 1440
        backupRetentionIntervalInHours: environmentName == 'prod' ? 720 : 168
      }
    }
  }
}

// Cosmos DB Database
resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosDbAccount
  name: 'BidOneDB'
  properties: {
    resource: {
      id: 'BidOneDB'
    }
  }
}

// Cosmos DB Containers
resource cosmosDbContainers_resource 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = [for container in cosmosDbContainers: {
  parent: cosmosDb
  name: container.name
  properties: {
    resource: {
      id: container.name
      partitionKey: {
        paths: [
          container.partitionKey
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
    }
  }
}]

// Redis Cache
resource redisCache 'Microsoft.Cache/redis@2023-04-01' = {
  name: 'bidone-redis-${environmentName}-${uniqueSuffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: environmentName == 'prod' ? 'Standard' : 'Basic'
      family: environmentName == 'prod' ? 'C' : 'C'
      capacity: environmentName == 'prod' ? 2 : 1
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    redisConfiguration: {
      'maxmemory-policy': 'allkeys-lru'
    }
  }
}

// Event Grid Topic
resource eventGridTopic 'Microsoft.EventGrid/topics@2023-12-15-preview' = {
  name: eventGridTopicName
  location: location
  tags: tags
  properties: {
    inputSchema: 'EventGridSchema'
    publicNetworkAccess: 'Enabled'
    dataResidencyBoundary: 'WithinGeopair'
    eventTypeInfo: {
      kind: 'inline'
      inlineEventTypes: {
        'BidOne.Dashboard.OrderMetrics': {
          description: 'Triggered when order metrics need to be updated on dashboard'
          displayName: 'Dashboard Order Metrics'
          documentationUrl: 'https://docs.bidone.com/events/dashboard-metrics'
          dataSchemaUrl: 'https://docs.bidone.com/schemas/dashboard-metrics.json'
        }
        'BidOne.Dashboard.PerformanceAlert': {
          description: 'Triggered when system performance alerts need to be displayed'
          displayName: 'Dashboard Performance Alert'
        }
        'BidOne.Dashboard.SystemHealth': {
          description: 'Triggered when system health status changes'
          displayName: 'Dashboard System Health'
        }
      }
    }
  }
}

// Storage Account for Function App
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
  }
}

// App Service Plan for Function App
resource functionAppServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'bidone-func-plan-${environmentName}-${uniqueSuffix}'
  location: location
  tags: tags
  sku: {
    name: environmentName == 'prod' ? 'EP1' : 'Y1'
    tier: environmentName == 'prod' ? 'ElasticPremium' : 'Dynamic'
  }
  properties: {
    reserved: false
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp'
  properties: {
    serverFarmId: functionAppServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: functionAppName
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ServiceBusConnection'
          value: listKeys(resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', serviceBusNamespace.name, 'RootManageSharedAccessKey'), '2022-10-01-preview').primaryConnectionString
        }
        {
          name: 'SqlConnectionString'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        {
          name: 'CosmosDbConnectionString'
          value: cosmosDbAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'EventGridTopicEndpoint'
          value: eventGridTopic.properties.endpoint
        }
        {
          name: 'EventGridTopicKey'
          value: eventGridTopic.listKeys().key1
        }
      ]
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
    }
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
  }
  dependsOn: [
    appInsights
    serviceBusNamespace
    sqlDatabase
    cosmosDbAccount
    eventGridTopic
  ]
}

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppsEnvName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
    zoneRedundant: environmentName == 'prod'
  }
}

// API Management Service
resource apiManagementService 'Microsoft.ApiManagement/service@2023-05-01-preview' = {
  name: apimServiceName
  location: location
  tags: tags
  sku: {
    name: environmentName == 'prod' ? 'Standard' : 'Developer'
    capacity: environmentName == 'prod' ? 2 : 1
  }
  properties: {
    publisherEmail: 'admin@bidone.com'
    publisherName: 'BidOne Integration Team'
    notificationSenderEmail: 'noreply@bidone.com'
    hostnameConfigurations: [
      {
        type: 'Proxy'
        hostName: '${apimServiceName}.azure-api.net'
        negotiateClientCertificate: false
        defaultSslBinding: true
        certificateSource: 'BuiltIn'
      }
    ]
    customProperties: {
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls10': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls11': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Ssl30': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls10': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls11': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Ssl30': 'false'
    }
    virtualNetworkType: 'None'
    disableGateway: false
    apiVersionConstraint: {
      minApiVersion: '2019-12-01'
    }
    publicNetworkAccess: 'Enabled'
  }
}

// Logic App (Standard)
resource logicApp 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppName
  location: location
  tags: tags
  properties: {
    definition: json(loadTextContent('logic-app-definition.json'))
    parameters: {
      serviceBusConnectionString: {
        value: listKeys(resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', serviceBusNamespace.name, 'RootManageSharedAccessKey'), '2022-10-01-preview').primaryConnectionString
      }
      functionAppUrl: {
        value: 'https://${functionApp.properties.defaultHostName}'
      }
      functionAppCode: {
        value: functionApp.listKeys().functionKeys.default
      }
      internalApiUrl: {
        value: 'https://${apiManagementService.properties.gatewayUrl}/internal'
      }
    }
    state: 'Enabled'
  }
  dependsOn: [
    functionApp
    serviceBusNamespace
    apiManagementService
    keyVault
  ]
}

// Event Grid Subscriptions - Dashboard focused
resource dashboardEventSubscription 'Microsoft.EventGrid/topics/eventSubscriptions@2023-12-15-preview' = {
  parent: eventGridTopic
  name: 'dashboard-events-to-functions'
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        resourceId: '${functionApp.id}/functions/DashboardMetricsProcessor'
        maxEventsPerBatch: 20
        preferredBatchSizeInKilobytes: 128
      }
    }
    filter: {
      includedEventTypes: [
        'BidOne.Dashboard.OrderMetrics'
        'BidOne.Dashboard.PerformanceAlert'
        'BidOne.Dashboard.SystemHealth'
      ]
    }
    retryPolicy: {
      maxDeliveryAttempts: 3
      eventTimeToLiveInMinutes: 60
    }
    deadLetterDestination: {
      endpointType: 'StorageBlob'
      properties: {
        resourceId: storageAccount.id
        blobContainerName: 'dashboard-deadletter'
      }
    }
  }
}

// Service Bus System Topic for Event Grid integration
resource serviceBusSystemTopic 'Microsoft.EventGrid/systemTopics@2023-12-15-preview' = {
  name: 'servicebus-system-topic-${uniqueSuffix}'
  location: location
  tags: resourceTags
  properties: {
    source: serviceBusNamespace.id
    topicType: 'Microsoft.ServiceBus.Namespaces'
  }
}

// Event Grid Subscription for high-value errors
resource highValueErrorSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2023-12-15-preview' = {
  parent: serviceBusSystemTopic
  name: 'high-value-error-subscription'
  properties: {
    destination: {
      endpointType: 'WebHook'
      properties: {
        endpointUrl: 'https://${customerCommFunctionAppName}.azurewebsites.net/runtime/webhooks/eventgrid?functionName=CustomerCommunicationProcessor&code={FUNCTION_KEY}'
        maxEventsPerBatch: 1
        preferredBatchSizeInKilobytes: 64
      }
    }
    filter: {
      subjectBeginsWith: '/queues/high-value-errors'
      includedEventTypes: [
        'Microsoft.ServiceBus.ActiveMessagesAvailableWithNoListeners'
      ]
    }
    retryPolicy: {
      maxDeliveryAttempts: 3
      eventTimeToLiveInMinutes: 60
    }
  }
}

// Note: Logic App connections are typically created through Azure portal or ARM templates
// For this demo, we'll use direct HTTP calls with authentication parameters

// Store secrets in Key Vault
resource serviceBusConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ServiceBusConnectionString'
  properties: {
    value: listKeys(resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', serviceBusNamespace.name, 'RootManageSharedAccessKey'), '2022-10-01-preview').primaryConnectionString
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

resource cosmosConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'CosmosDbConnectionString'
  properties: {
    value: cosmosDbAccount.listConnectionStrings().connectionStrings[0].connectionString
  }
}

resource appInsightsConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'AppInsightsConnectionString'
  properties: {
    value: appInsights.properties.ConnectionString
  }
}

resource redisConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'RedisConnectionString'
  properties: {
    value: '${redisCache.properties.hostName}:${redisCache.properties.sslPort},password=${redisCache.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
}

resource eventGridTopicKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'EventGridTopicKey'
  properties: {
    value: eventGridTopic.listKeys().key1
  }
}

resource eventGridTopicEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'EventGridTopicEndpoint'
  properties: {
    value: eventGridTopic.properties.endpoint
  }
}

// Outputs
output resourceGroupName string = resourceGroup().name
output serviceBusNamespace string = serviceBusNamespace.name
output serviceBusConnectionString string = listKeys(resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', serviceBusNamespace.name, 'RootManageSharedAccessKey'), '2022-10-01-preview').primaryConnectionString
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output cosmosDbAccountName string = cosmosDbAccount.name
output cosmosDbEndpoint string = cosmosDbAccount.properties.documentEndpoint
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
output containerAppsEnvironmentName string = containerAppsEnvironment.name
output apiManagementServiceName string = apiManagementService.name
output apiManagementGatewayUrl string = 'https://${apiManagementService.properties.gatewayUrl}'
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output redisCacheName string = redisCache.name
output redisHostName string = redisCache.properties.hostName
output logicAppName string = logicApp.name
output logicAppId string = logicApp.id
output eventGridTopicName string = eventGridTopic.name
output eventGridTopicEndpoint string = eventGridTopic.properties.endpoint