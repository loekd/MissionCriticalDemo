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

@description('Indicates whether to use HTTPS for the Dispatch API. (default: true for prod)')
param useHttps string = contains(environmentName, 'prod') ? 'true' : 'false'

@description('The host name of the application.')
param hostName string = contains(environmentName, 'prod') ? 'demo.loekd.com' : 'localhost'

@description('The k8s namespace name.')
var kubernetesNamespace = '${environmentName}-${applicationName}'

@description('The port on which the frontend is exposed internally.')
var frontendPort = 8080

@description('The name of the frontend container.')
var frontendContainerName = 'frontend'

var certPrivateKey = contains(environmentName, 'prod')
  ? loadTextContent('./certificates/privkey.pem')
  : loadTextContent('./certificates/localhost.key')
var certPublicKey = contains(environmentName, 'prod')
  ? loadTextContent('./certificates/fullchain.pem')
  : loadTextContent('./certificates/localhost.crt')

//Deploy shared resources like Jaeger and PubSub
module shared 'shared.bicep' = {
  name: 'shared'
  params: {
    environmentName: environmentName
    applicationName: applicationName
  }
}

// Blazor WASM Frontend on Nginx
resource frontend 'Applications.Core/containers@2023-10-01-preview' = {
  name: frontendContainerName
  properties: {
    application: shared.outputs.application.id
    environment: shared.outputs.environment.id

    container: {
      image: empty(containerRegistry) ? 'missioncriticaldemo.frontend:2.0.1' : '${containerRegistry}/missioncriticaldemo.frontend:2.0.1'
      imagePullPolicy: empty(containerRegistry) ? 'Never' : 'Always'
      ports: {
        web: {
          containerPort: frontendPort
          port:          frontendPort
          protocol: 'TCP'
        }
      }
      env: {
        ASPNETCORE_URLS: { 
          value: 'http://+:${frontendPort}' 
        }
        AzureAdB2C__ClientSecret: {
          value: loadTextContent('./secrets/clientsecret.txt')
        }
      }      
    }
    connections: {
      zipkin: {
        source: shared.outputs.jaeger.id
      }
    }
    // runtimes: {
    //   kubernetes: {
    //     pod: {
    //       containers: [
    //       { 
    //         name: frontendContainerName
    //         securityContext: {
    //           runAsUser: 0         // switch to root
    //           runAsGroup: 0
    //           capabilities: {
    //             add: [
    //               'NET_BIND_SERVICE'  // allow binding to ports < 1024
    //             ]
    //           }
    //         }
    //       }]
    //     }
    //   }
    //}
  }
}


// Get existing Dispatch API to locate its internal port
resource dispatch_api 'Applications.Core/containers@2023-10-01-preview' existing = {
  name: 'dispatchapi'
}


//Application gateway for ingress and TLS offloading
resource gateway 'Applications.Core/gateways@2023-10-01-preview' = {
  name: 'gateway'
  properties: {
    application: shared.outputs.application.id
    environment: shared.outputs.environment.id
    hostname: {
      fullyQualifiedHostname: hostName
    }
    tls: useHttps == 'true' ? {
      sslPassthrough: false
      certificateFrom: appCert.id
    } : {}
    routes: [
      {
        path: '/api' //Dispatch REST API
        destination: 'http://${dispatch_api.name}:${dispatch_api.properties.container.ports.web.containerPort}'
        enableWebsockets: true
      }
      {
        path: '/dispatchhub' //Dispatch websocket
        destination: 'http://${dispatch_api.name}:${dispatch_api.properties.container.ports.web.containerPort}'
        enableWebsockets: true
      }
      {
        path: '/' //frontend index.html
        destination: 'http://${frontend.name}:${frontend.properties.container.ports.web.containerPort}'
        enableWebsockets: true
      }
    ]
  }
}

// Secret store for TLS certificate, used by the ingress controller
resource appCert 'Applications.Core/secretStores@2023-10-01-preview' = {
  name: 'appcert'
  properties:{
    application: shared.outputs.application.id
    environment: shared.outputs.environment.id
    type: 'certificate'
    data: {
      'tls.key': {
        value: certPrivateKey
      }
      'tls.crt': {
        value: certPublicKey
      }
      'clientsecret.txt': {
        value: loadTextContent('./secrets/clientsecret.txt') 
      }
    }
  }
}
