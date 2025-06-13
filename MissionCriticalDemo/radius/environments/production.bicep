//Separate environment definition 
//To be deployed first.
//Links to recipes for Cloud services (Service Bus and CosmosDb) and containerized service for state.
extension radius

resource prodEnv 'Applications.Core/environments@2023-10-01-preview' = {
  name: 'prod'
  properties: {
    compute: {
      kind: 'kubernetes'
      namespace: 'prod' //Radius will append the application name here.
    }
    //add Azure providers for production
    providers: {
      azure: {
        scope: '/subscriptions/6eb94a2c-34ac-45db-911f-c21438b4939c/resourceGroups/rg-radius'
      }
    }

    //register recipes using Bicep
    recipes: {
      'Applications.Dapr/pubSubBrokers': {
        cloudPubsubRecipe: {
          templateKind: 'bicep'
          templatePath: 'acrradius.azurecr.io/recipes/sbpubsub:0.1.0'
        }
      }

      'Applications.Dapr/stateStores': {
        cloudStateStoreRecipe: {
          templateKind: 'bicep'
          templatePath: 'acrradius.azurecr.io/recipes/cosmosstatestore:0.1.0'
        }

        //you can still use containers for some services and SaaS for others
        localStateStoreRecipe: {
          templateKind: 'bicep'
          templatePath: 'acrradius.azurecr.io/recipes/localstatestore:0.1.2'
        }
      }

      'Applications.Core/extenders': {
        otlpCollectorRecipe: {
          templateKind: 'bicep'
          templatePath: 'acrradius.azurecr.io/recipes/otlp:0.1.1'
        }
      }
    }
  }
}
