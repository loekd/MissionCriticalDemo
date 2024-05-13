import radius as radius

@description('Specifies the environment for resources.')
param environment string

@description('The Radius Application ID. Injected automatically by the rad CLI.')
param application string

@description('The container registry name (leave empty for local deployments).')
param containerRegistry string = 'acrradius.azurecr.io'

@description('The host and port on which the Dispatch API is exposed.')
param dispatchApiHostAndPort string = 'http://localhost:8080'

@description('The k8s namespace name (leave empty for local deployments).')
param kubernetesNamespace string

var frontendPort = 80
var volumeName = 'scripts'
var frontendContainerName = 'frontend'

import kubernetes as kubernetes {
  kubeConfig: ''
  namespace: kubernetesNamespace
}

resource configMap 'core/ConfigMap@v1' = {
  metadata: {
    name: 'frontend-scripts'
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


output frontendPort int = frontendPort
output frontendName string = frontend.name

