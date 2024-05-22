# Getting started

Most data stores can be built by using Dapr defaults, except one.
When resuming a Codespace, you may need to install mongo and dapr again, and rebuild & publish the local container images.

## Build and publish local container images
- Run this script first:
    - `sh /workspaces/MissionCriticalDemo/.devcontainer/build-and-publish-containers.sh`

## Prepare queryable external database
- Dispatch API requires query support. This is not present in the default state store, so we need to bring in our own MongoDb.
- This helm command was used to install a mongodb in replicaset mode:
    - `helm install --set service.nameOverride=mongo --set replicaCount=1 --set architecture=replicaset --set auth.enabled=false mongo oci://registry-1.docker.io/bitnamicharts/mongodb`

## Prepare Dapr
- This Dapr CLI command was used to prepare Dapr in the Kubernetes cluster:
    - `dapr init -k`

## Prepare Radius

- Run this command to prepare radius:
    - `/workspaces/MissionCriticalDemo/MissionCriticalDemo/radius# rad init`

- Run the project
    - `rad run app.bicep`

- Make sure Port 8080 is made public. It sometimes reverts to private.

## Usage


Open the user interface.
- `https://fuzzy-yodel-g4495xr5xw736r7-80.app.github.dev/dispatch`
- Log in using Azure AD B2C
- Inject some gas 
- When everything is working, you will get a notification about processing, and the numbers should increase or decrease.

Check Gas In Store at Plant level:
- Browse:
    `https://fuzzy-yodel-g4495xr5xw736r7-8082.app.github.dev/api/gasinstore/gasinstore`
- Adjust:
    - Port forward to plant api
    - `curl -v -X POST http://localhost:8082/api/gasinstore/20`


## Redis

### Kubernetes
Create a Port Forward to Redis. (change the pod name to the actual value)
`kubectl port-forward pods/daprpubsub-fd6yvbjatqj5a-577df9c456-cbfbb 6379:6379 -n default-radius`
Use the VS Code extension to connect to it on `localhost`, without credentials.

### Docker compose
Use the VS Code extension to connect to it on `localhost:6378`, without credentials. 

## MongoDb
Create a Port Forward to MongoDb in `default` namespace
`kubectl port-forward pods/mongo-mongodb-0 27017:27017 -n default`
Connect using extension: `mongodb://localhost:27017/?directConnection=true&replicaSet=rs0`


## Debugging
Run `kubectl run bb --image=busybox -i --tty --restart=Never -n default` inside a new terminal, to run an interactive container inside K8s for debugging.

# Local runs

## Prepare
- Create a cluster using k3d
    - `winget install k3d`
        - `k3d cluster create mycluster`
    - Register it inside `config.yaml` in profile \.rad\ as `local`
    - Select it: `rad workspace switch local` (or whatever workspace name you used)
- Install Radius control plane
    - `rad install kubernetes` or `rad install kubernetes --reinstall`
- Install Dapr
    - `dapr init -k`
- Create k8s namespace
    - `kubectl create ns local-radius`
- Create Radius resource group
    - `rad group create default`

        Make sure this group name corresponds with the `scope` element inside the config.yaml file, for the current workspace:
        - `scope: /planes/radius/local/resourceGroups/default`
- Create local environment inside group
    - `rad env create local -g default`
- Deploy local recipes
    - `rad recipe register pubsubRecipe --environment local --resource-type 'Applications.Dapr/pubSubBrokers' --template-kind bicep --template-path acrradius.azurecr.io/recipes/redispubsub:0.1.0 --group default`
    - `rad recipe register stateStoreRecipe --environment local --resource-type 'Applications.Datastores/mongoDatabases' --template-kind bicep --template-path acrradius.azurecr.io/recipes/localstatestore:0.1.0 --group default`
    - `rad recipe register stateStoreRecipe --environment local --resource-type 'Applications.Datastores/mongoDatabases' --template-kind bicep --template-path acrradius.azurecr.io/recipes/localstatestore:0.1.0 --group default`
    - `rad recipe register jaegerRecipe --environment local --resource-type 'Applications.Core/extenders' --template-kind bicep --template-path acrradius.azurecr.io/recipes/jaeger:0.1.0 --group default`
## Run

- Set kubectl context if needed:
    - `kubectl config use-context docker-desktop`    
- Deploy the Plant API:
    - `rad deploy ./plant.bicep -e local -g default`
- Deploy the Dispatch api:
    - `rad deploy ./dispatch.bicep -e local -g default`
    - If you get `"message": "Container state is 'Terminated' Reason: Error, Message: "` errors, try run & deploy again until it works
- Run the Frontend and Gateway:
    - Codespaces:
        - `rad run ./frontend.bicep -e local -g default --parameters dispatchApiHostAndPort=https://$CODESPACE_NAME-8080.app.github.dev`
        - Turn dispatch port to public (to allow CORS)
            `gh codespace ports visibility 8080:public -c $CODESPACE_NAME`
    - Localhost:        
        - `rad run ./frontend.bicep -e local -g default --parameters hostName=localhost` (access trough gateway)
    - Please note that the Gateway breaks signalR after 15s
        - fix: `kubectl patch httpproxy dispatchapi -n local-radius --type='json' -p='[{"op": "add", "path": "/spec/routes/0/enableWebsockets", "value": true}]'`
        - This can block redeployments, so delete the custom resource if you see any errors about 'patch httpproxy'

# Azure

## Prepare

- Create an SPN with Owner access on Azure Resource Group
    - Create a secret
    - Capture Client ID
    - Capture Tenant ID
- Create a radius environment: `rad init full`
    - Configure it with the SPN details
    - Name the environment `azure`
- Register the recipes
    - Service Bus (replaces Redis Pub/Sub with Cloud PaaS) 
        - `rad bicep publish --file sb_pubsub_recipe.bicep --target br:acrradius.azurecr.io/recipes/sbpubsub:0.1.0`
        - `rad recipe register pubsubRecipe --environment azure --resource-type 'Applications.Dapr/pubSubBrokers' --template-kind bicep --template-path acrradius.azurecr.io/recipes/sbpubsub:0.1.0`
    - Local MongoDb (because Cosmos doesn't support query Dapr API)
        - `rad bicep publish --file local_statestore_recipe.bicep --target br:acrradius.azurecr.io/recipes/localstatestore:0.1.0`
        - `rad recipe register stateStoreRecipe --environment azure --resource-type 'Applications.Datastores/mongoDatabases' --template-kind bicep --template-path acrradius.azurecr.io/recipes/localstatestore:0.1.0`
    - Local Jaeger (can be replaced with OTEL forwarder)
        - `rad bicep publish --file jaeger_recipe.bicep --target br:acrradius.azurecr.io/recipes/jaeger:0.1.0`
        - `rad recipe register jaegerRecipe --environment azure --resource-type 'Applications.Core/extenders' --template-kind bicep --template-path acrradius.azurecr.io/recipes/jaeger:0.1.0`

## Run

- Deploy plant API
    - `kubectl config use-context aksradius-admin`
    - `rad workspace switch aks`
    - `rad deploy .\plant.bicep -e azure -g azure`
- Deploy dispatch api
    - `rad deploy .\dispatch.bicep -e azure -g azure`

- Run frontend:        
    - `rad run ./frontend.bicep -e azure -g azure` (access trough gateway)       
    - Please note that the Gateway currently breaks signalR after 15s
        - fix: `kubectl patch httpproxy dispatchapi -n azure-radius --type='json' -p='[{"op": "add", "path": "/spec/routes/0/enableWebsockets", "value": true}]'`