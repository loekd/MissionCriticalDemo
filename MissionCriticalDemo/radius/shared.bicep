// Shared services like Zipkin and Pub/Sub are defined here. Used from Dispatch and Plant APIs.

import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

var parameters = contains(environment, 'azure') ? {
  location: 'northeurope'
} : {}

@description('The name of the environment.')
var environmentName = split(environment, '/')[9]

@description('The name of the application.')
var applicationName = split(application, '/')[9]

@description('The k8s namespace name.')
var kubernetesNamespace = '${environmentName}-${applicationName}'

resource app 'Applications.Core/applications@2023-10-01-preview' = {
  name: 'radius'
  properties: {
    environment: environment
    extensions: [
      {
        kind: 'kubernetesNamespace'
        namespace: kubernetesNamespace
      }
      {
        kind: 'kubernetesMetadata'
        labels: {
          'team.name': 'StorageControl'
          'team.costcenter': 'Netherlands'
          'team.contact': 'storage_at_control.com'
          'product.docs': 'readme.md'
        }
      }
    ]
  }
}

// Zipkin telemetry collection endpoint using 'jaeger_recipe' 
// No resource for OTEL collectors in Radius at this time, so we are using an extender
resource jaegerExtender 'Applications.Core/extenders@2023-10-01-preview' = {
  name: 'jaeger'
  properties: {
    environment: environment
    application: application
    recipe: {
      name: 'jaegerRecipe'
    }
  }
}

// pub/sub messaging using 'sb_pubsub_recipe' 
resource dispatch_pubsub 'Applications.Dapr/pubSubBrokers@2023-10-01-preview' = {
  name: 'dispatchpubsub'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'recipe'
    recipe: {
      name: 'pubsubRecipe'
      parameters: parameters
    }
  }
}

output pubsub object = dispatch_pubsub
output jaeger object = jaegerExtender
