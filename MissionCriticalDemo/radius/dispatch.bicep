import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

@description('The container registry name (leave empty for local deployments).')
param containerRegistry string = 'acrradius.azurecr.io'

var dispatchApiPort = 8080

module shared 'shared.bicep' = {
  name: 'shared'
  params: {
    environment: environment
    application: application
  }
}

// Dispatch API
resource dispatch_api 'Applications.Core/containers@2023-10-01-preview' = {
  name: 'dispatchapi'
  properties: {
    application: application
    environment: environment
    container: {
      image: empty(containerRegistry) ? 'missioncriticaldemo.dispatchapi:latest' : '${containerRegistry}/missioncriticaldemo.dispatchapi:latest'
      imagePullPolicy: empty(containerRegistry) ? 'Never' : 'IfNotPresent'
      env: {
      }
      ports: {
        web: {
          containerPort: dispatchApiPort
          port: dispatchApiPort
        }
      }
    }
    connections: {
      dispatchinboxstate: {
        source: dispatch_inbox_state.id
      }
      dispatchoutboxstate: {
        source: dispatch_outbox_state.id
      }
      gasinstorestate: {
        source: gas_in_store_state.id
      }
      dispatchpubsub: {
        source: shared.outputs.pubsub.id
      }
      zipkin: {
        source: shared.outputs.jaeger.id
      }
    }
    extensions: [
      {
        kind: 'daprSidecar'
        appId: 'dispatchapi'
        appPort: dispatchApiPort
        config: daprConfig.metadata.name
      }
      {
        kind: 'kubernetesMetadata'
        annotations: {
          'dapr.io/log-level': 'debug'
        }
      }
    ]
  }
}

import kubernetes as kubernetes {
  kubeConfig: ''
  namespace: 'azure-radius'
}

resource daprConfig 'dapr.io/Configuration@v1alpha1' = {
  metadata: {
    name: 'dispatchdaprconfig'
    namespace: 'azure-radius'
  }
  spec: {
    tracing: {
      samplingRate: '1'
      zipkin: {
        endpointAddress: shared.outputs.jaeger.properties.zipkinEndpoint
      }
    }
    metric: {
      enabled: true
    }
  }
}

// outbox for dispatch API (not managed by Radius)
resource dispatch_outbox_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'outboxstate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'manual'
    type: 'state.mongodb'
    version: 'v1'
    metadata: {
      host: outboxStateStore.properties.host
      databaseName: outboxStateStore.properties.database
      collectionName: outboxStateStore.name
      username: null
      password: null
      operationTimeout: '30s'
      replicaset: true
      params: '?replicaSet=rs0'
    }
  }
}

resource outboxStateStore 'Applications.Datastores/mongoDatabases@2023-10-01-preview' = {
  name: 'outboxcollection'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'recipe'
    recipe: {
      name: 'stateStoreRecipe'
      parameters: {
        databaseName: 'dispatch'
        replicaset: true
      }
    }
  }
  dependsOn: [
    inboxStateStore
    gisStateStore
  ]
}

// inbox for dispatch API (not managed by Radius)
resource dispatch_inbox_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'inboxstate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'manual'
    type: 'state.mongodb'
    version: 'v1'
    metadata: {
      host: inboxStateStore.properties.host
      databaseName: inboxStateStore.properties.database
      collectionName: inboxStateStore.name
      username: null
      password: null
      operationTimeout: '30s'
      params: '?replicaSet=rs0'
    }
  }
}

resource inboxStateStore 'Applications.Datastores/mongoDatabases@2023-10-01-preview' = {
  name: 'inboxcollection'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'recipe'
    recipe: {
      name: 'stateStoreRecipe'
      parameters: {
        databaseName: 'dispatch'
        replicaset: true
      }
    }
  }
  dependsOn: [
    gisStateStore
  ]
}

// gas in store state for dispatch API (not managed by Radius)
resource gas_in_store_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'gasinstorestate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'manual'
    type: 'state.mongodb'
    version: 'v1'
    metadata: {
      host: gisStateStore.properties.host
      databaseName: gisStateStore.properties.database
      collectionName: gisStateStore.name
      username: null
      password: null
      operationTimeout: '30s'
    }
  }
}

resource gisStateStore 'Applications.Datastores/mongoDatabases@2023-10-01-preview' = {
  name: 'giscollection'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'recipe'
    recipe: {
      name: 'stateStoreRecipe'
      parameters: {
        databaseName: 'dispatch'
        replicaset: false
      }
    }
  }
}


//Run the frontend module from here, so we can test it against the dispatch api.
module frontend 'frontend.bicep' = {
  name: 'frontend'
  params: {
    environment: environment
    application: application
    containerRegistry: containerRegistry
  }
}
