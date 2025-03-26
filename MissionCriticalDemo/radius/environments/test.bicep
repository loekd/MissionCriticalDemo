//Separate environment definition 
//To be deployed first.
//Links to recipes for containerized services
extension radius

resource testEnv 'Applications.Core/environments@2023-10-01-preview' = {
  name: 'test'
  properties: {
    compute: {
      kind: 'kubernetes'
      namespace: 'test' //Radius will append the application name here.
    }
    //register recipes using Bicep
    recipes: {      
      'Applications.Dapr/pubSubBrokers': {
        localPubsubRecipe: {
          templateKind: 'bicep'
          templatePath: 'acrradius.azurecr.io/recipes/redispubsub:0.1.0'
        }        
      }

      'Applications.Dapr/stateStores': {
        localStateStoreRecipe: {
          templateKind: 'bicep'
          templatePath: 'acrradius.azurecr.io/recipes/localstatestore:0.1.2'
        }
      }

      'Applications.Core/extenders': {
        jaegerRecipe: {
          templateKind: 'bicep'
          templatePath: 'acrradius.azurecr.io/recipes/jaeger:0.1.0'
        }
      }    
    }
  }
}
