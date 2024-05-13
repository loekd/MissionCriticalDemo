import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

@description('The container registry name (leave empty for local deployments).')
param containerRegistry string = 'acrradius.azurecr.io'

@description('The k8s namespace name (leave empty for local deployments).')
param kubernetesNamespace string

import kubernetes as kubernetes {
  kubeConfig: ''
  namespace: kubernetesNamespace
}

var dispatchApiPort = 8080

//Deploy shared resources like Jaeger and PubSub
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
        source: inboxStateStore.id
      }
      dispatchoutboxstate: {
        source: outboxStateStore.id
      }
      gasinstorestate: {
        source: gisStateStore.id
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


// Dapr configuration for telemetry through Jaeger (zipkin endpoint)
resource daprConfig 'dapr.io/Configuration@v1alpha1' = {
  metadata: {
    name: 'dispatchdaprconfig'    
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

// Dapr state store for outbox (queryable)
resource outboxStateStore 'Applications.Datastores/mongoDatabases@2023-10-01-preview' = {
  name: 'outboxstate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'recipe'
    recipe: {
      name: 'stateStoreRecipe'
      parameters: {
        databaseName: 'dispatch'
        replicaset: true
        appId: 'dispatchapi'
      }
    }
  }
  dependsOn: [
    inboxStateStore
    gisStateStore
  ]
}

// Dapr state store for inbox (queryable)
resource inboxStateStore 'Applications.Datastores/mongoDatabases@2023-10-01-preview' = {
  name: 'inboxstate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'recipe'
    recipe: {
      name: 'stateStoreRecipe'
      parameters: {
        databaseName: 'dispatch'
        replicaset: true
        appId: 'dispatchapi'
      }
    }
  }
  dependsOn: [
    gisStateStore
  ]
}

// Dapr state store for gas in store (not queryable)
resource gisStateStore 'Applications.Datastores/mongoDatabases@2023-10-01-preview' = {
  name: 'gasinstorestate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'recipe'
    recipe: {
      name: 'stateStoreRecipe'
      parameters: {
        databaseName: 'dispatch'
        replicaset: false
        appId: 'dispatchapi'
      }
    }
  }
}


//Frontend module, so we can test it against the dispatch api.
module frontend 'frontend.bicep' = {
  name: 'frontend'
  params: {
    environment: environment
    application: application
    containerRegistry: containerRegistry
    kubernetesNamespace: kubernetesNamespace
  }
}


//Application gateway 
resource gateway 'Applications.Core/gateways@2023-10-01-preview' = {
  name: 'gateway'
  properties: {
    application: application 
    environment: environment
    hostname: {
      // Omitting hostname properties results in gatewayname.appname.PUBLIC_HOSTNAME_OR_IP.nip.io

      // Results in prefix.appname.PUBLIC_HOSTNAME_OR_IP.nip.io
      prefix: ''
      // Alternately you can specify your own hostname that you've configured externally
      //fullyQualifiedHostname: 'hostname.radapp.io'
    }
    routes: [
      {
        path: '/api'
        destination: 'http://${dispatch_api.name}:${dispatchApiPort}'
      }
      {
        path: '/'
        destination: 'http://${frontend.outputs.frontendName}:${frontend.outputs.frontendPort}'
      }
    ]
  }
}
