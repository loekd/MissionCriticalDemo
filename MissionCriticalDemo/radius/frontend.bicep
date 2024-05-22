import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

@description('The container registry name (leave empty for local deployments).')
param containerRegistry string = 'acrradius.azurecr.io'

@description('Indicates whether to use HTTPS for the Dispatch API. (default: true)')
param useHttps bool = true

@description('The host name of the application.')
param hostName string = 'demo.loekd.com'

@description('The name of the environment.')
var environmentName = split(environment, '/')[9]

@description('The name of the application.')
var applicationName = split(application, '/')[9]

@description('The k8s namespace name.')
var kubernetesNamespace = '${environmentName}-${applicationName}'

@description('The host and port on which the Dispatch API is exposed (through the gateway).')
var dispatchApiHostAndPort = useHttps ? 'https://${hostName}' : 'http://${hostName}'

@description('The port on which the frontend is exposed internally.')
var frontendPort = 80

@description('The name of the volume to mount the ConfigMap to.')
var volumeName = 'scripts'

@description('The name of the frontend container.')
var frontendContainerName = 'frontend'

// import the kubernetes module
import kubernetes as kubernetes {
  kubeConfig: ''
  namespace: kubernetesNamespace
}

// Create a ConfigMap with the frontend appsettings.json. This is mounted to the frontend container.
// It points to the public endpoint of the Dispatch API.
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
        "ClientId": "34b20d4c-6028-4a32-b78a-4f9778197acb",
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
    application: application
    environment: environment
    container: {
      image: empty(containerRegistry) ? 'missioncriticaldemo.frontend:latest' : '${containerRegistry}/missioncriticaldemo.frontend:latest'
      imagePullPolicy: empty(containerRegistry) ? 'Never' : 'IfNotPresent'
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
    application: application 
    environment: environment
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
      }
      {
        path: '/dispatchhub' //Dispatch websocket
        destination: 'http://${dispatch_api.name}:${dispatch_api.properties.container.ports.web.containerPort}'
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
    application: application
    environment: environment
    type: 'certificate'
    data: {
      'tls.key': {
        value: loadTextContent('privkey.pem')
      }
      'tls.crt': {
        value: loadTextContent('fullchain.pem')
      }
    }
  }
}


