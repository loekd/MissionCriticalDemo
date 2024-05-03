// Shared services like Zipkin and Pub/Sub are defined here. Used from Dispatch and Plant APIs.

import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

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
      parameters: {
        location: 'northeurope'
      }
    }
  }
}

output pubsub object = dispatch_pubsub
output jaeger object = jaegerExtender
