// Azure Service Bus PubSub Recipe

extension radius

@description('Radius-provided object containing information about the resouce calling the Recipe')
param context object


@description('The Azure region where the resources will be deployed.')
param location string = 'northeurope'

@description('The unique seed used to generate resource names.')
param uniqueSeed string = uniqueString('${resourceGroup().id}')

@description('The user-defined tags that will be applied to the resource. Default is null')
param tags object = {}

@description('The Radius specific tags that will be applied to the resource')
var radiusTags = {
  'radapp.io-environment': context.environment.id
  'radapp.io-application': context.application == null ? '' : context.application.id
  'radapp.io-resource': context.resource.id
}

var daprType = 'pubsub.azure.servicebus.topics'
var daprVersion = 'v1'

//-----------------------------------------------------------------------------
// Create the Service Bus
//-----------------------------------------------------------------------------

resource serviceBus 'Microsoft.ServiceBus/namespaces@2021-06-01-preview' = {
  name: 'sb-${uniqueSeed}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }

  resource authorizationRule 'AuthorizationRules' = {
    name: 'authRule'
    properties: {
      rights: [
        'Listen'
        'Send'
        'Manage'
      ]
    }
  }
  tags: union(tags, radiusTags)
}

extension kubernetes with {
  kubeConfig: ''
  namespace: context.runtime.kubernetes.namespace
} as kubernetes

resource daprComponent 'dapr.io/Component@v1alpha1' = {
  metadata: {
    name: context.resource.name    
  }
  spec: {
    type: daprType
    version: daprVersion
    metadata: [
      {
        name: 'connectionString'
        value: serviceBus::authorizationRule.listKeys().primaryConnectionString
      }      
    ]
  }
}

//-----------------------------------------------------------------------------
// Output
//-----------------------------------------------------------------------------


output result object = {
  // This workaround is needed because the deployment engine omits Kubernetes resources from its output.
  // This allows Kubernetes resources to be cleaned up when the resource is deleted.
  // Once this gap is addressed, users won't need to do this.
  resources: [
    '/planes/kubernetes/local/namespaces/${context.namespace}/providers/dapr.io/Component/${daprComponent.metadata.name}'
  ]
  values: {
    type: daprType
    version: daprVersion
    metadata: daprComponent.metadata
    host: serviceBus.properties.serviceBusEndpoint
    component: daprComponent
  }
  secrets: {
    // Temporarily disable linter until secret outputs are added
    #disable-next-line outputs-should-not-contain-secrets
    connectionString: serviceBus::authorizationRule.listKeys().primaryConnectionString
  }
}
//deploying the recipe can be done by this command:
//rad bicep publish --file sb_pubsub_recipe.bicep --target br:acrradius.azurecr.io/recipes/sbpubsub:0.1.0
//rad recipe register pubsubRecipe --environment prod --resource-type 'Applications.Dapr/pubSubBrokers' --template-kind bicep --template-path acrradius.azurecr.io/recipes/sbpubsub:0.1.0 --group prod
