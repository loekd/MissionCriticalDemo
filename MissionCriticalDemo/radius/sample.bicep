import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The ID of your Radius Application. Automatically injected by the rad CLI.')
param application string

var namespace = 'default'

// The backend container that is connected to the Dapr state store
resource backend 'Applications.Core/containers@2023-10-01-preview' = {
  name: 'backend'
  properties: {
    application: application
    environment: environment
    container: {
      // This image is where the app's backend code lives
      image: 'ghcr.io/radius-project/samples/dapr-backend:latest'
      ports: {
        orders: {
          containerPort: 3000
        }
      }
    }
    connections: {
      orders: {
        source: stateStore.id
      }
    }
    extensions: [
      {
        kind: 'daprSidecar'
        appId: 'backend'
        appPort: 3000
      }
    ]
  }
}

// // The Dapr state store that is connected to the backend container
// resource stateStore 'Applications.Dapr/stateStores@2023-10-01-preview' = {
//   name: 'statestore'
//   properties: {
//     // Provision Redis Dapr state store automatically via the default Radius Recipe
//     environment: environment
//     application: application
//   }
// }

resource stateStore 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'statestore'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'manual'
    type: 'state.mongodb'
    version: 'v1'
    metadata: {
      host: '${service.metadata.name}.${namespace}.svc.cluster.local:${service.spec.ports[0].port}'
      // redisPassword: ''
    }
  }
}

import kubernetes as kubernetes{
  kubeConfig: ''
  namespace: namespace
}

resource statefulset 'apps/StatefulSet@v1' = {
  metadata: {
    name: 'redis'
    namespace: namespace 
    labels: {
      app: 'redis'
    }
  }
  spec: {
    replicas: 1
    serviceName: service.metadata.name
    selector: {
      matchLabels: {
        app: 'redis'
      }
    }
    template: {
      metadata: {
        labels: {
          app: 'redis'
        }
      }
      spec: {
        automountServiceAccountToken: true
        terminationGracePeriodSeconds: 10
        containers: [
          {
            name: 'redis'
            image: 'mongo:7.0'
            securityContext: {
              allowPrivilegeEscalation: false
            }
            ports: [
              {
                containerPort: 27017
              }
            ]
          }
        ]
      }
    }
  }
}

resource service 'core/Service@v1' = {
  metadata: {
    name: 'redis'
    namespace: namespace
    labels: {
      app: 'redis'
    }
  }
  spec: {
    clusterIP: 'None'
    ports: [
      {
        port: 27017
      }
    ]
    selector: {
      app: 'redis'
    }
  }
}
