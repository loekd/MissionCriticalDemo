import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The ID of your Radius Application. Automatically injected by the rad CLI.')
param application string

var namespace = 'default'

// The frontend container that serves the application UI
resource frontend 'Applications.Core/containers@2023-10-01-preview' = {
  name: 'frontend'
  properties: {
    application: application
    container: {
      // This image is where the app's frontend code lives
      image: 'ghcr.io/radius-project/samples/dapr-frontend:latest'
      env: {
        // An environment variable to tell the frontend container where to find the backend
        CONNECTION_BACKEND_APPID: 'backend'
        // An environment variable to override the default port that .NET Core listens on
        ASPNETCORE_URLS: 'http://*:8080'
      }
      // The frontend container exposes port 8080, which is used to serve the UI
      ports: {
        ui: {
          containerPort: 8080
        }
      }
    }
    // The extension to configure Dapr on the container, which is used to invoke the backend
    extensions: [
      {
        kind: 'daprSidecar'
        appId: 'frontend'
      }
    ]
  }
}

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
      params: '?replicaSet=rs0'      
    }
  }
}

import kubernetes as kubernetes{
  kubeConfig: ''
  namespace: namespace
}

// resource statefulset 'apps/StatefulSet@v1' = {
//   metadata: {
//     name: 'redis'
//     namespace: namespace 
//     labels: {
//       app: 'redis'
//     }
//   }
//   spec: {
//     replicas: 1
//     serviceName: service.metadata.name
//     selector: {
//       matchLabels: {
//         app: 'redis'
//       }
//     }
//     template: {
//       metadata: {
//         labels: {
//           app: 'redis'
//         }
//       }
//       spec: {
//         automountServiceAccountToken: true
//         terminationGracePeriodSeconds: 30
//         containers: [
//           {
//             name: 'redis'
//             image: 'mongo:7.0'
//             env: [
//               {
//                 name: 'MONGODB_REPLICA_SET_MODE'
//                 value: 'primary'
//               }
//             ]
//             command: ['/bin/sh', '-c']
//             args: ['mongod --replSet rs0 --bind_ip_all --port 27017']
//             securityContext: {
//               allowPrivilegeEscalation: true
//             }
//             ports: [
//               {
//                 containerPort: 27017
//               }
//             ]
//           }
//         ]
//         initContainers: [{
//           name: 'init'
//           image: 'mongo:7.0'
//           command: ['mongosh']
//           args:['<<rs.initiate({_id:\'rs0\',members:[{_id:0,host:\'localhost:27017\'}]})']
//         }]
//       }
//     }
//   }
// }

resource service 'core/Service@v1' existing = {
  metadata: {
    name: 'redis'
    namespace: namespace    
  }
}
