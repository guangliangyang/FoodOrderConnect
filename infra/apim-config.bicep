@description('API Management service name')
param apimServiceName string

@description('External Order API backend URL')
param externalOrderApiUrl string

@description('Internal System API backend URL')
param internalSystemApiUrl string

@description('Environment name')
param environmentName string = 'dev'

@description('Location for resources')
param location string = resourceGroup().location

// Variables
var tags = {
  Environment: environmentName
  Project: 'BidOne-Integration-Demo'
  Component: 'API-Management'
}

// Reference existing API Management service
resource apiManagementService 'Microsoft.ApiManagement/service@2023-05-01-preview' existing = {
  name: apimServiceName
}

// Create Backend for External Order API
resource externalOrderApiBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'external-order-api-backend'
  properties: {
    description: 'External Order API Backend'
    url: externalOrderApiUrl
    protocol: 'http'
    circuitBreaker: {
      rules: [
        {
          failureCondition: {
            count: 5
            percentage: 50
            statusCodeRanges: [
              {
                min: 500
                max: 599
              }
            ]
          }
          name: 'external-api-circuit-breaker'
          tripDuration: 'PT60S'
        }
      ]
    }
  }
}

// Create Backend for Internal System API (for Logic App access)
resource internalSystemApiBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'internal-system-api-backend'
  properties: {
    description: 'Internal System API Backend'
    url: internalSystemApiUrl
    protocol: 'http'
    circuitBreaker: {
      rules: [
        {
          failureCondition: {
            count: 3
            percentage: 30
            statusCodeRanges: [
              {
                min: 500
                max: 599
              }
            ]
          }
          name: 'internal-api-circuit-breaker'
          tripDuration: 'PT30S'
        }
      ]
    }
  }
}

// External Order API Definition
resource externalOrderApi 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'external-order-api'
  properties: {
    displayName: 'External Order API'
    description: 'API for receiving orders from external systems'
    path: 'external'
    protocols: ['https']
    serviceUrl: externalOrderApiUrl
    subscriptionRequired: true
    apiVersion: 'v1'
    apiVersionSetId: externalOrderApiVersionSet.id
    subscriptionKeyParameterNames: {
      header: 'Ocp-Apim-Subscription-Key'
      query: 'subscription-key'
    }
  }
}

// Internal System API Definition (for Logic App access)
resource internalSystemApi 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'internal-system-api'
  properties: {
    displayName: 'Internal System API'
    description: 'Internal API for order processing'
    path: 'internal'
    protocols: ['https']
    serviceUrl: internalSystemApiUrl
    subscriptionRequired: false // Logic App will use this
    apiVersion: 'v1'
    apiVersionSetId: internalSystemApiVersionSet.id
  }
}

// API Version Sets
resource externalOrderApiVersionSet 'Microsoft.ApiManagement/service/apiVersionSets@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'external-order-api-versions'
  properties: {
    displayName: 'External Order API Versions'
    versioningScheme: 'Segment'
    versionHeaderName: 'Api-Version'
  }
}

resource internalSystemApiVersionSet 'Microsoft.ApiManagement/service/apiVersionSets@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'internal-system-api-versions'
  properties: {
    displayName: 'Internal System API Versions'
    versioningScheme: 'Segment'
    versionHeaderName: 'Api-Version'
  }
}

// External Order API Operations
resource createOrderOperation 'Microsoft.ApiManagement/service/apis/operations@2023-05-01-preview' = {
  parent: externalOrderApi
  name: 'create-order'
  properties: {
    displayName: 'Create Order'
    method: 'POST'
    urlTemplate: '/api/orders'
    description: 'Create a new order'
    request: {
      description: 'Order creation request'
      headers: [
        {
          name: 'Content-Type'
          type: 'string'
          required: true
          values: ['application/json']
        }
      ]
      representations: [
        {
          contentType: 'application/json'
          schemaId: 'create-order-schema'
          typeName: 'CreateOrderRequest'
        }
      ]
    }
    responses: [
      {
        statusCode: 201
        description: 'Order created successfully'
        representations: [
          {
            contentType: 'application/json'
            schemaId: 'order-response-schema'
            typeName: 'OrderResponse'
          }
        ]
      }
      {
        statusCode: 400
        description: 'Bad request'
      }
      {
        statusCode: 500
        description: 'Internal server error'
      }
    ]
  }
}

resource getOrderOperation 'Microsoft.ApiManagement/service/apis/operations@2023-05-01-preview' = {
  parent: externalOrderApi
  name: 'get-order'
  properties: {
    displayName: 'Get Order'
    method: 'GET'
    urlTemplate: '/api/orders/{orderId}'
    description: 'Get order by ID'
    templateParameters: [
      {
        name: 'orderId'
        type: 'string'
        required: true
        description: 'Order identifier'
      }
    ]
    responses: [
      {
        statusCode: 200
        description: 'Order found'
        representations: [
          {
            contentType: 'application/json'
            schemaId: 'order-response-schema'
            typeName: 'OrderResponse'
          }
        ]
      }
      {
        statusCode: 404
        description: 'Order not found'
      }
    ]
  }
}

// Internal System API Operations
resource processOrderOperation 'Microsoft.ApiManagement/service/apis/operations@2023-05-01-preview' = {
  parent: internalSystemApi
  name: 'process-order'
  properties: {
    displayName: 'Process Order'
    method: 'POST'
    urlTemplate: '/api/orders'
    description: 'Process order internally'
    request: {
      description: 'Internal order processing request'
      representations: [
        {
          contentType: 'application/json'
        }
      ]
    }
    responses: [
      {
        statusCode: 200
        description: 'Order processed successfully'
      }
      {
        statusCode: 500
        description: 'Processing failed'
      }
    ]
  }
}

// Products
resource externalApiProduct 'Microsoft.ApiManagement/service/products@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'external-api-product'
  properties: {
    displayName: 'External Integration APIs'
    description: 'APIs for external systems to integrate with BidOne platform'
    state: 'published'
    subscriptionRequired: true
    approvalRequired: environmentName == 'prod'
    subscriptionsLimit: environmentName == 'prod' ? 100 : 10
    terms: 'Terms and conditions for using BidOne Integration APIs'
  }
}

resource internalApiProduct 'Microsoft.ApiManagement/service/products@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'internal-api-product'
  properties: {
    displayName: 'Internal System APIs'
    description: 'Internal APIs for system integration'
    state: 'published'
    subscriptionRequired: false
    approvalRequired: false
  }
}

// Associate APIs with Products
resource externalApiProductApi 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: externalApiProduct
  name: externalOrderApi.name
}

resource internalApiProductApi 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: internalApiProduct
  name: internalSystemApi.name
}

// Named Values (for configuration)
resource serviceBusConnectionNamedValue 'Microsoft.ApiManagement/service/namedValues@2023-05-01-preview' = {
  parent: apiManagementService
  name: 'service-bus-connection'
  properties: {
    displayName: 'Service Bus Connection String'
    value: 'placeholder-will-be-updated-by-deployment'
    secret: true
  }
}

// Outputs
output externalApiUrl string = '${apiManagementService.properties.gatewayUrl}/external'
output internalApiUrl string = '${apiManagementService.properties.gatewayUrl}/internal'
output apiManagementUrl string = apiManagementService.properties.gatewayUrl