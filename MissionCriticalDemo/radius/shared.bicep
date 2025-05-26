// Shared services like Zipkin and Pub/Sub are defined here. Used from Dispatch and Plant APIs.

extension radius

@description('Specifies the Environment Name.')
param environmentName string = 'test'

@description('The Radius Application Name.')
param applicationName string = 'demo04'

@description('The k8s namespace name.')
var kubernetesNamespace = '${environmentName}-${applicationName}'

var pubSubRecipeName = environmentName == 'prod' ? 'cloudPubsubRecipe' : 'localPubsubRecipe'
var stateStoreRecipeName = environmentName == 'prod' ? 'cloudStateStoreRecipe' : 'localStateStoreRecipe'
var otelRecipeName = environmentName == 'prod' ? 'otlpCollectorRecipe' : 'jaegerRecipe'
var parameters = contains(environmentName, 'azure') ? {
  location: 'northeurope'
} : {}


resource app 'Applications.Core/applications@2023-10-01-preview' = {
  name: 'demo04'
  properties: {
    environment: env.id
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
          'environment.name': env.name
        }
      }
    ]
  }
}

resource env 'Applications.Core/environments@2023-10-01-preview' existing = {
  name: environmentName
}

// In prod, this will deploy an OTLP collector that forwards to Azure Monitor
// In test, this will deploy a Jaeger container that stores telemetry in memory.
resource otelExtender 'Applications.Core/extenders@2023-10-01-preview' = {
  name: 'otel'
  properties: {
    environment: env.id
    application: app.id
    recipe: {
      name: otelRecipeName
    }
  }
}

// pub/sub messaging using 'sb_pubsub_recipe' 
// in test, this will deploy a Redis pub/sub broker locally
// in prod, this will deploy an Azure Service Bus pub/sub broker
resource dispatch_pubsub 'Applications.Dapr/pubSubBrokers@2023-10-01-preview' = {
  name: 'dispatchpubsub'
  properties: {
    environment: env.id
    application: app.id
    resourceProvisioning: 'recipe'
    recipe: {
      name: pubSubRecipeName      
      parameters: parameters
    }
  }
}

//return shared resources
output pubsub object = dispatch_pubsub
output jaeger object = otelExtender
output environment object = env
output application object = app

//return environment-specific recipe names
output stateStoreRecipeName string = stateStoreRecipeName
output pubSubRecipeName string = pubSubRecipeName
