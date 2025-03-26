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
param useHttps string = contains(environmentName, 'prod') ? 'true': 'false'

@description('The host name of the application.')
param hostName string = contains(environmentName, 'prod') ? 'demo.loekd.com' : 'localhost'

@description('The host name of the application.')
param overrideDispatchApiHostAndPort string = ''

@description('The k8s namespace name.')
var kubernetesNamespace = '${environmentName}-${applicationName}'

@description('The host and port on which the Dispatch API is exposed (through the gateway).')
var dispatchApiHostAndPort = empty(overrideDispatchApiHostAndPort) ? useHttps == 'true' ? 'https://${hostName}' : 'http://${hostName}' : overrideDispatchApiHostAndPort

@description('The port on which the frontend is exposed internally.')
var frontendPort = 80

@description('The name of the volume to mount the ConfigMap to.')
var volumeName = 'scripts'

@description('The name of the frontend container.')
var frontendContainerName = 'frontend'

//Deploy shared resources like Jaeger and PubSub
module shared 'shared.bicep' = {
  name: 'shared'
  params: {
    environmentName: environmentName
    applicationName: applicationName
  }
}

// Create a ConfigMap with the frontend appsettings.json. This is mounted to the frontend container.
// It points to the public endpoint of the Dispatch API.
// This is currently leaking K8s details into the Radius file.
resource configMap 'core/ConfigMap@v1' = {
  metadata: {
    name: 'frontend-scripts'
    namespace: kubernetesNamespace
  }
  data: {
    #disable-next-line prefer-interpolation
    'appsettings.json': concat('''
    {
      "AzureAdB2C": {
        "Authority": "https://loekdb2c.b2clogin.com/loekdb2c.onmicrosoft.com/B2C_1_UserFlowSuSi",
        "ClientId": "81c2fe74-bc14-4c65-b209-52f042cd3263",
        "ValidateAuthority": false
      },
      "DispatchApi": {
        "Endpoint": "''', dispatchApiHostAndPort, '''"
      }
    }
    ''')
  }
}


// Blazor WASM Frontend on Nginx
resource frontend 'Applications.Core/containers@2023-10-01-preview' = {
  name: frontendContainerName
  properties: {
    application: shared.outputs.application.id
    environment: shared.outputs.environment.id
    container: {
      image: empty(containerRegistry) ? 'missioncriticaldemo.frontend:latest' : '${containerRegistry}/missioncriticaldemo.frontend:latest'
      imagePullPolicy: empty(containerRegistry) ? 'Never' : 'Always'
      ports: {
        web: {
          containerPort: frontendPort
          protocol: 'TCP'
        }
      }
    }
    runtimes: {
      kubernetes: {
        pod: {
          containers: [
            {
              name: frontendContainerName
              volumeMounts: [
                {
                  name: volumeName
                  mountPath: '/usr/share/nginx/html/appsettings.json'
                  subPath: 'appsettings.json'
                }
              ]
            }
          ]
          volumes: [
            {
              name: volumeName
              configMap: {
                name: configMap.metadata.name
              }
            }
          ]
        }
      }
    }
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
    tls: {
      sslPassthrough: false
      certificateFrom: appCert.id
    }
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
        value: loadTextContent('./certificates/privkey.pem')
      }
      'tls.crt': {
        value: loadTextContent('./certificates/fullchain.pem')
      }
    }
  }
}


