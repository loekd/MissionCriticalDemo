//Azure Cosmos DB with SQL or MongoDB API
//Don't use this, because multi-partition queries are not supported using the GO SDK.

@description('Radius-provided object containing information about the resouce calling the Recipe')
param context object

@description('The geo-location where the resource lives.')
param location string = resourceGroup().location

@description('Sets this Dapr State Store as the actor state store. Only one Dapr State Store can be set as the actor state store. Defaults to false.')
param actorStateStore bool = false

@description('The name of the account to create.')
param accountName string = context.resource.name

@description('The name of the database to create within the account.')
param databaseName string = context.resource.name

// @description('The shard key for the collection.')
// param collectionIndexes array = [
//   {
//     key: {
//       keys: [
//         '_id'
//       ]
//     }
//   }
//   {
//     key: {
//       keys: [
//         '$**'
//       ]
//     }
//   }
// ]

@description('Maximum autoscale throughput for the database shared with up to 25 collections')
@minValue(1000)
@maxValue(1000000)
param sharedAutoscaleMaxThroughput int = 1000

@description('The user-defined tags that will be applied to the resource. Default is null')
param tags object = {}

@description('The Radius specific tags that will be applied to the resource')
var radiusTags = {
  'radapp.io-environment': context.environment.id
  'radapp.io-application': context.application == null ? '' : context.application.id
  'radapp.io-resource': context.resource.id
}

@description('The name of the container to create within the database and to reference within the Dapr component.')
var containerName = context.resource.name

var daprType = 'state.azure.cosmosdb'
var daprVersion = 'v1'

// Cosmos DB Account
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2021-06-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    locations: [ {
        locationName: location
        failoverPriority: 0
      } ]
    databaseAccountOfferType: 'Standard'
    enableFreeTier: false
    capabilities: []
  }
  tags: union(tags, radiusTags)
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2022-05-15' = {
  parent: cosmosDbAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
    options: {
      autoscaleSettings: {
        maxThroughput: sharedAutoscaleMaxThroughput
      }
    }
  }
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2022-05-15' = {
  parent: database
  name: containerName
  properties: {
    resource: {
      id: containerName
      partitionKey: {
        paths: [
          '/partitionKey'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
      defaultTtl: 86400      
    }
  }
}

// // Database within the account
// resource cosmosDbDatabase 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2022-05-15' = {
//   parent: cosmosDbAccount
//   name: databaseName
//   properties: {
//     resource: {
//       id: databaseName
//     }
//     options: {
//       autoscaleSettings: {
//         maxThroughput: sharedAutoscaleMaxThroughput
//       }
//     }
//   }
//   tags: union(tags, radiusTags)
// }

// // Collection within the database
// resource cosmosDbCollection 'Microsoft.DocumentDb/databaseAccounts/mongodbDatabases/collections@2022-05-15' = {
//   parent: cosmosDbDatabase
//   name: containerName
//   properties: {
//     resource: {
//       id: containerName
//       shardKey: {
//         _id: 'Hash'
//       }

//       indexes: collectionIndexes
//     }
//   }
//   tags: union(tags, radiusTags)
// }

resource daprComponent 'dapr.io/Component@v1alpha1' = {
  metadata: {
    name: context.resource.name
  }
  spec: {
    type: daprType
    version: daprVersion
    metadata: [
      {
        name: 'url'
        value: cosmosDbAccount.properties.documentEndpoint
      }
      // Temporarily setting raw secret value until richer secret store support in Radius
      {
        name: 'masterKey'
        value: cosmosDbAccount.listKeys().primaryMasterKey
      }
      {
        name: 'database'
        value: databaseName
      }
      {
        name: 'collection'
        value: containerName
      }
      {
        name: 'actorStateStore'
        value: actorStateStore ? 'true' : 'false'
      }
    ]
  }
}

import kubernetes as k8s {
  kubeConfig: ''
  namespace: context.runtime.kubernetes.namespace
}

output result object = {
  // This workaround is needed because the deployment engine omits Kubernetes resources from its output.
  // This allows Kubernetes resources to be cleaned up when the resource is deleted.
  // Once this gap is addressed, users won't need to do this.
  resources: [
    '/planes/kubernetes/local/namespaces/${daprComponent.metadata.namespace}/providers/dapr.io/Component/${daprComponent.metadata.name}'
  ]
  values: {
    type: daprType
    version: daprVersion
    metadata: daprComponent.spec.metadata
    server: cosmosDbAccount.properties.documentEndpoint
    database: databaseName    
    collection: containerName
    port: 443
  }
  secrets: {
    // Temporarily disable linter until secret outputs are added
    #disable-next-line outputs-should-not-contain-secrets    
    password: cosmosDbAccount.listKeys().primaryMasterKey
  }
}

//deploying the recipe can be done by this command:
//rad bicep publish --file cosmos_statestore_recipe.bicep --target br:acrradius.azurecr.io/recipes/cosmosstatestore:0.1.0
//rad recipe register stateStoreRecipe --environment azure --resource-type 'Applications.Datastores/mongoDatabases' --template-kind bicep --template-path acrradius.azurecr.io/recipes/cosmosstatestore:0.1.0



