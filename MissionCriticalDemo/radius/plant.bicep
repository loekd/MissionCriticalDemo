import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

@description('The container registry name (leave empty for local deployments).')
param containerRegistry string = 'acrradius.azurecr.io'

@description('The k8s namespace name.')
var kubernetesNamespace = '${split(environment, '/')[9]}-radius'

var plantApiPort = 8082


module shared 'shared.bicep' = {
  name: 'shared'
  params: {
    environment: environment
    application: application
  }
}

import kubernetes as localKubernetes {
  kubeConfig: ''
  namespace: kubernetesNamespace
}


resource daprConfig 'dapr.io/Configuration@v1alpha1' = {
  metadata: {
    name: 'plantdaprconfig'
    namespace: kubernetesNamespace
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

// plant API
resource plant_api 'Applications.Core/containers@2023-10-01-preview' = {
  name: 'plantapi'
  properties: {
    application: application
    environment: environment
    container: {
      image: empty(containerRegistry) ? 'missioncriticaldemo.plantapi:latest' : '${containerRegistry}/missioncriticaldemo.plantapi:latest'
      imagePullPolicy: empty(containerRegistry) ? 'Never' : 'IfNotPresent'
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
        source: plantStateStore.id
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
        appId: 'plantapi'
        appPort: plantApiPort
        config: daprConfig.metadata.name
      }
      {
        kind: 'kubernetesMetadata'
        annotations: {
          'dapr.io/app-max-concurrency': '1'
          'dapr.io/log-level': 'debug'
        }
      }
    ]
  }
}

// state store for plant API (managed by Radius)
// resource plant_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
//   name: 'plantstate'
//   properties: {
//     environment: environment
//     application: application
//     resourceProvisioning: 'manual'
//     type: 'state.mongodb'
//     version: 'v1'
//     metadata: {
//       host: '${plantStateStore.properties.host}:${plantStateStore.properties.port}'
//       databaseName: plantStateStore.properties.database
//       collectionName: plantStateStore.name
//       username: null
//       password: null
//     }
//   }
// }

resource plantStateStore 'Applications.Datastores/mongoDatabases@2023-10-01-preview' = {
  name: 'plantstate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'recipe'
    recipe: {
      name: 'stateStoreRecipe'
      parameters: {
        databaseName: 'plant'
        appId: 'plantapi'
      }
    }
  }
}
