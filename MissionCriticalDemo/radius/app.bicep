extension radius

@description('Specifies the Environment Name.')
param environmentName string = 'test'

@description('The Radius Application Name.')
param applicationName string = 'demo04'

@description('Indicates whether to use HTTPS for the Gateway.')
param useHttps string

@description('The host name of the application.')
param hostName string

//Deploy shared resources like Jaeger and PubSub
module shared 'shared.bicep' = {
  name: 'shared'
  params: {
    environmentName: environmentName
    applicationName: applicationName
  }
}
//deploy the frontend (Blazor WebAssembly app)
module frontend 'frontend.bicep' = {
  name: 'frontend'
  params: {
    environmentName: environmentName
    applicationName: applicationName
    useHttps: useHttps
    hostName: hostName
  }
  dependsOn: [
    shared
    dispatchApi
    plantApi
  ]
}

//deploy the plant API
module plantApi 'plant.bicep' = {
  name: 'plant'
  params: {
    environmentName: environmentName
    applicationName: applicationName    
  }
  dependsOn: [
    shared
  ]
}

//deploy the dispatch API
module dispatchApi 'dispatch.bicep' = {
  name: 'dispatch'
  params: {
    environmentName: environmentName
    applicationName: applicationName    
  }
  dependsOn: [
    shared
  ]
}
