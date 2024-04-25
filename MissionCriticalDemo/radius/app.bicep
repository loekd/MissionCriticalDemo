import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

// // Blazor WASM Frontend on Nginx
// resource frontend 'Applications.Core/containers@2023-10-01-preview' = {
//   name: 'frontend'
//   properties: {
//     application: application
//     container: {
//       image: 'missioncriticaldemo.frontend:latest'
//       imagePullPolicy: 'Never'
//       ports: {
//         web: {
//           containerPort: 80
//           port: 8089
//           protocol: 'TCP'
//         }
//       }
//     }
//   }
// }

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
          containerPort: 8080
          port: 5133
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
      // zipkin: {
      //   source: zipkin.id
      // }
    }
    extensions: [
      {
        kind: 'daprSidecar'
        appId: 'dispatchapi'
        appPort: 8080
      }
    ]
  }
}

// Database to serve as state store for dispatch API outbox
// resource dispatch_state_db 'Applications.Core/containers@2023-10-01-preview' = {
//   name: 'stateserver'
//   properties: {
//     application: application
//     container: {
//       image: 'mongo:7.0'
//       env: {
//         MONGODB_REPLICA_SET_MODE: 'primary'
//         ALLOW_EMPTY_PASSWORD: 'yes'
//       }
//       args: [ '--replSet', 'rs0', '--bind_ip_all', '--port', '27017' ]
//       livenessProbe: {
//         kind: 'exec'
//         command: 'echo "try { rs.status() } catch (err) { rs.initiate({_id:\'rs0\',members:[{_id:0,host:\'stateserver:27017\'}]}) }" | mongosh --port 27017 --quiet'
//         initialDelaySeconds: 5
//         failureThreshold: 30
//         timeoutSeconds: 5
//         periodSeconds: 10
//       }
//       ports: {
//         db: {
//           containerPort: 27017
//           port: 27017
//         }
//       }
//     }
//   }
// }

import kubernetes as kubernetes{
  kubeConfig: ''
  namespace: 'default-radius'
}

resource statefulset 'apps/StatefulSet@v1' = {
  metadata: {
    name: 'stateserver'
    labels: {
      app: 'stateserver'
    }
  }
  spec: {
    replicas: 1
    serviceName: service.metadata.name
    selector: {
      matchLabels: {
        app: 'stateserver'
      }
    }
    template: {
      metadata: {
        labels: {
          app: 'stateserver'
        }
      }
      spec: {
        automountServiceAccountToken: true
        terminationGracePeriodSeconds: 10
        containers: [
          {
            name: 'stateserver'
            image: 'mongo:7.0'
            env: [
              {
                name: 'ALLOW_EMPTY_PASSWORD'
                value: 'yes'
              }
              {
                name: 'MONGODB_REPLICA_SET_MODE'
                value: 'primary'
              }
            ]
            args: [ '--replSet', 'rs0', '--bind_ip_all', '--port', '27017' ]
            livenessProbe: {             
              exec: {
                command: ['echo "try { rs.status() } catch (err) { rs.initiate({_id:\'rs0\',members:[{_id:0,host:\'stateserver:27017\'}]}) }" | mongosh --port 27017 --quiet']
              }
              initialDelaySeconds: 5
              failureThreshold: 30
              timeoutSeconds: 5
              periodSeconds: 10
            }
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
    name: 'stateserver'
    labels: {
      app: 'stateserver'
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
      app: 'stateserver'
    }
  }
}

// outbox for dispatch API
resource dispatch_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
  name: 'dispatchstate'
  properties: {
    environment: environment
    application: application
    resourceProvisioning: 'manual'
    // resources: [
    //   { id: dispatch_state_db.id }
    // ]
    metadata: {
      host: '${service.metadata.name}.default-radius.svc.cluster.local:${service.spec.ports[0].port}'
      databaseName: 'dispatch'
      collectionName: 'dispatchCollection'
      params: '?replicaSet=rs0'
    }
    type: 'state.mongodb'
    version: 'v1'
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
// resource plant_api 'Applications.Core/containers@2023-10-01-preview' = {
//   name: 'plantapi'
//   properties: {
//     application: application
//     container: {
//       image: 'missioncriticaldemo.plantapi:latest'
//       imagePullPolicy: 'Never'
//       ports: {
//         web: {
//           containerPort: 8080
//           port: 5071
//         }
//       }
//     }
//     connections: {
//       plant_state: {
//         source: plant_state.id
//       }
//       dispatch_pubsub: {
//         source: dispatch_pubsub.id
//       }
//       zipkin: {
//         source: zipkin.id
//       }
//     }
//     extensions: [
//       {
//         kind: 'daprSidecar'
//         appId: 'plantapi'
//       }
//     ]
//   }
// }

// // state store for plant API 
// resource plant_state 'Applications.Dapr/stateStores@2023-10-01-preview' = {
//   name: 'plantstate'
//   properties: {
//     environment: environment
//     application: application
//   }
// }

// pub/sub messaging using default recipe
resource dispatch_pubsub 'Applications.Dapr/pubSubBrokers@2023-10-01-preview' = {
  name: 'dispatchpubsub'
  properties: {
    environment: environment
    application: application
  }
}

// jaeger container for telemetry
// resource zipkin 'Applications.Core/containers@2023-10-01-preview' = {
//   name: 'zipkin'
//   properties: {
//     application: application
//     container: {
//       image: 'jaegertracing/all-in-one:latest'
//       env: {
//         COLLECTOR_ZIPKIN_HTTP_PORT: '9411'
//         COLLECTOR_ZIPKIN_ALLOWED_ORIGINS: '*'
//         COLLECTOR_ZIPKIN_ALLOWED_HEADERS: '*'
//       }
//       ports: {
//         zipkin: {
//           containerPort: 9411
//         }
//         web: {
//           containerPort: 16686
//         }
//         grpc: {
//           containerPort: 14250
//         }
//       }
//     }
//   }
// }
