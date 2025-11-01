extension radius
// import the kubernetes module
extension kubernetes with {
  kubeConfig: ''
  namespace: kubernetesNamespace
} as kubernetes

@description('Specifies the Environment Name.')
param environmentName string = 'test'

@description('The Radius Application Name.')
param applicationName string = 'demo04'

@description('The container registry name (leave empty for local deployments).')
param containerRegistry string = 'acrradius.azurecr.io'



@description('The k8s namespace name.')
var kubernetesNamespace = '${environmentName}-${applicationName}'

var plantApiPort = 8082


module shared 'shared.bicep' = {
  name: 'shared'
  params: {
    environmentName: environmentName
    applicationName: applicationName
  }
}

#disable-next-line BCP081
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
    application: shared.outputs.application.id
    environment: shared.outputs.environment.id
    container: {
      image: empty(containerRegistry) ? 'missioncriticaldemo.plantapi:latest' : '${containerRegistry}/missioncriticaldemo.plantapi:2.0.0'
      imagePullPolicy: empty(containerRegistry) ? 'Never' : 'IfNotPresent'
      env: {
        ASPNETCORE_URLS: {value: 'http://+:${plantApiPort}' }
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
resource plant_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'plantstate'
  properties: {
    environment: shared.outputs.environment.id
    application: shared.outputs.application.id
    resourceProvisioning: 'recipe'
    recipe: {
      name: shared.outputs.stateStoreRecipeName      
      parameters: {
        databaseName: 'plant'
        appId: 'plantapi'
      }
    }
  }
}
