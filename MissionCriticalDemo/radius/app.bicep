import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

var dispatchApiPort=8080
var plantApiPort=8082
var frontendPort=80


// Blazor WASM Frontend on Nginx
resource frontend 'Applications.Core/containers@2023-10-01-preview' = {
  name: 'frontend'
  properties: {
    application: application
    container: {
      image: 'missioncriticaldemo.frontend:latest'
      imagePullPolicy: 'Never'
      ports: {
        web: {
          containerPort: frontendPort
          port: frontendPort
          protocol: 'TCP'
        }
      }
    }
  }
}

// Dispatch API
resource dispatch_api 'Applications.Core/containers@2023-10-01-preview' = {
  name: 'dispatchapi'
  properties: {
    application: application
    container: {
      image: 'missioncriticaldemo.dispatchapi:latest'
      imagePullPolicy: 'Never'
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
      dispatchstate: {
        source: dispatch_state.id
      }
      gasinstorestate: {
        source: gas_in_store_state.id
      }
      dispatchpubsub: {
        source: dispatch_pubsub.id
      }
      zipkin: {
        source: zipkin.id
      }
    }
    extensions: [
      {
        kind: 'daprSidecar'
        appId: 'dispatchapi'
        appPort: dispatchApiPort
      }
    ]
  }
}

import kubernetes as kubernetes {
  kubeConfig: ''
  namespace: 'default'
}

resource service 'core/Service@v1' existing = {
  metadata: {
    name: 'mongo'
    namespace: 'default'
  }
}

// outbox for dispatch API
resource dispatch_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'dispatchstate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'manual'
    type: 'state.mongodb'
    version: 'v1'
    metadata: {
      host: '${service.metadata.name}.${service.metadata.namespace}.svc.cluster.local:${service.spec.ports[0].port}'
      databaseName: 'dispatch'
      collectionName: 'dispatchCollection'
      params: '?replicaSet=rs0'
    }
  }
}

// gas in store state for dispatch API
resource gas_in_store_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'gasinstorestate'
  properties: {
    environment: environment
    application: application
  }
}

// plant API
resource plant_api 'Applications.Core/containers@2023-10-01-preview' = {
  name: 'plantapi'
  properties: {
    application: application
    container: {
      image: 'missioncriticaldemo.plantapi:latest'
      imagePullPolicy: 'Never'
      env: {
        ASPNETCORE_URLS: 'http://+:${plantApiPort}'
      }
      ports: {
        api: {
          containerPort: plantApiPort
          port: plantApiPort
        }
      }
    }
    connections: {
      plant_state: {
        source: plant_state.id
      }
      dispatch_pubsub: {
        source: dispatch_pubsub.id
      }
      zipkin: {
        source: zipkin.id
      }
    }
    extensions: [
      {
        kind: 'daprSidecar'
        appId: 'plantapi'
        appPort: plantApiPort
      }
    ]
  }
}

// state store for plant API 
resource plant_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'plantstate'
  properties: {
    environment: environment
    application: application
  }
}

// pub/sub messaging using default recipe
resource dispatch_pubsub 'Applications.Dapr/pubSubBrokers@2023-10-01-preview' = {
  name: 'dispatchpubsub'
  properties: {
    environment: environment
    application: application
  }
}

// jaeger container for telemetry
resource zipkin 'Applications.Core/containers@2023-10-01-preview' = {
  name: 'zipkin'
  properties: {
    application: application
    container: {
      image: 'jaegertracing/all-in-one:latest'
      env: {
        COLLECTOR_ZIPKIN_HTTP_PORT: '9411'
        COLLECTOR_ZIPKIN_ALLOWED_ORIGINS: '*'
        COLLECTOR_ZIPKIN_ALLOWED_HEADERS: '*'
      }
      ports: {
        zipkin: {
          containerPort: 9411
        }
        web: {
          containerPort: 16686
        }
        grpc: {
          containerPort: 14250
        }
      }
    }
  }
}
