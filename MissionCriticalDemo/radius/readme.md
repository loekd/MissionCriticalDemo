# Getting started

## Certs
- Use CertBot to get a certificate:
  - `docker run -it certbot/certbot certonly --manual --preferred-challenges dns -d demo.loekd.com`

# Local runs

## Prepare 

Select the 'local' workspace.
  - `rad workspace switch local`

## Run

- Set kubectl context if needed:
    - `kubectl config use-context docker-desktop`    
- Deploy the Plant API:
    - `rad deploy ./plant.bicep -g test`
- Deploy the Dispatch api:
    - `rad deploy ./dispatch.bicep -g test`
    - If you get `"message": "Container state is 'Terminated' Reason: Error, Message: "` errors, try run & deploy again until it works
- Run the Frontend and Gateway:
    - **Localhost + Docker-Desktop**:        
        - `rad run ./frontend.bicep --parameters hostName=localhost --parameters useHttps=true` 
    - **Codespaces**:
        - `rad run ./frontend.bicep --parameters hostName=$CODESPACE_NAME-8080.app.github.dev`
        - Turn dispatch port to public (to allow CORS)
            `gh codespace ports visibility 8080:public -c $CODESPACE_NAME`
    - **Dev Container + K3d** 
        - If you get 405 Errors about SignalR, it could be due to the Gateway not working on K3d.
        - Workaround for this is to connect the frontend directly to the Dispatch API Service instead of passing through the Gateway:
        - `rad run ./frontend.bicep --parameters hostName=localhost --parameters overrideDispatchApiHostAndPort=http://localhost:8080`
    - **Bug** - Please note that the Gateway breaks signalR after 15s
        - fix: `kubectl patch httpproxy dispatchapi -n test-demo04 --type='json' -p='[{"op": "add", "path": "/spec/routes/0/enableWebsockets", "value": true}]'`
        - This can block redeployments, so delete the custom resource if you see any errors about 'patch httpproxy' during deloyment:
        - `kubectl delete httpproxy dispatchapi -n test-demo04`
- Explore the Frontend:
    - `explorer http://localhost`
- Explore the Radius Dashboard:
    - `explorer http://localhost:7007`
- Explore telemetry in Jaeger:
    - `kubectl port-forward services/jaeger-dxvmbpocxv4js 16686:16686 9411:9411 14250:14250 -n prod-demo04`
    - `explorer http://localhost:16686`

# Azure

## Prepare
- Connect a DNS Entry to the kubernetes IP address of your AKS Cluster
- Optionally link it as a CName to your own domain.
- Get a TLS certificate
- Or comment out ln 158-165 from 'frontend.bicep'.
  ```yaml
  data: {
      'tls.key': {
        value: loadTextContent('privkey.pem')
      }
      'tls.crt': {
        value: loadTextContent('fullchain.pem')
      }
    }
  ```

- List and show workspaces:
    - `rad workspace list`
    - `rad workspace show`

- Create a new workspace for Azure AKS if it does not yet exist:
    - `rad workspace create kubernetes aks --context aksradius`

- Or switch to the aks workspace:
  - `rad workspace switch aks`

- Get credentials for an AKS cluster
    - `az aks get-credentials -g rg-radius -n aksradius` (replace with your details)

- Create an SPN with Owner access on Azure Resource Group
    - Create a Client Secret
    - Capture Client ID
    - Capture Tenant ID

- Create a radius environment: `rad init --full`
    - Use the AKS context
    - Use 'prod' as environment and namespace.
    - Configure it for Azure, with the SPN details
    - Do not scaffold / setup an application
    
- Register the recipes
    - `cd azure`
    - Service Bus (replaces Redis Pub/Sub with Cloud PaaS) 
        - `rad bicep publish --file sb_pubsub_recipe.bicep --target br:acrradius.azurecr.io/recipes/sbpubsub:0.1.0`
        - `rad recipe register pubsubRecipe --environment prod --resource-type 'Applications.Dapr/pubSubBrokers' --template-kind bicep --template-path acrradius.azurecr.io/recipes/sbpubsub:0.1.0`
    - Local MongoDb (because Cosmos doesn't support query Dapr API)
        - `rad bicep publish --file local_statestore_recipe.bicep --target br:acrradius.azurecr.io/recipes/localstatestore:0.1.0`
        - `rad recipe register stateStoreRecipe --environment prod --resource-type 'Applications.Dapr/stateStores' --template-kind bicep --template-path acrradius.azurecr.io/recipes/localstatestore:0.1.0`
    - Local Jaeger (can be replaced with OTEL forwarder)
        - `rad bicep publish --file jaeger_recipe.bicep --target br:acrradius.azurecr.io/recipes/jaeger:0.1.0`
        - `rad recipe register jaegerRecipe --environment prod --resource-type 'Applications.Core/extenders' --template-kind bicep --template-path acrradius.azurecr.io/recipes/jaeger:0.1.0`
    - `cd ..`

## Run

- Deploy plant API
    - `kubectl config use-context aksradius` (if needed)
    - `rad workspace switch aks` (if needed)
    - `rad deploy ./plant.bicep --parameters environmentName=prod`
- Deploy dispatch api
    - `rad deploy ./dispatch.bicep --parameters environmentName=prod`

- Run frontend:        
    - Public AKS IP Address:        
        - `rad run ./frontend.bicep --parameters environmentName=prod --parameters hostName=demo.loekd.com` (access trough gateway)     
    - Please note that the Gateway currently breaks signalR after 15s
        - fix: `kubectl patch httpproxy dispatchapi -n prod-demo05 --type='json' -p='[{"op": "add", "path": "/spec/routes/0/enableWebsockets", "value": true}]'`
        - This can block redeployments, so delete the custom resource if you see any errors about 'patch httpproxy':
        - `kubectl delete httpproxy dispatchapi -n prod-demo05`

## Usage

Open the user interface.
- `https://demo.loekd.com`
- Log in using Azure AD B2C
- Inject some gas 
- When everything is working, you will get a notification about processing, and the numbers should increase or decrease.

Check Gas In Store at Plant level:
- Browse:
    `https://demo.loekd.com/api/gasinstore/gasinstore`
- Adjust:
    - Port forward to plant api
    - `curl -v -X POST http://localhost:8082/api/gasinstore/20`


## Developer tools

### Redis
- Create a Port Forward to Redis. (change the pod name to the actual value)
    - `kubectl port-forward pods/daprpubsub-fd6yvbjatqj5a-577df9c456-cbfbb 6379:6379 -n default-radius`
- Use the VS Code extension to connect to it on `localhost`, without credentials.


## MongoDb
- Create a Port Forward to MongoDb in `default` namespace
    - `kubectl port-forward pods/mongo-mongodb-0 27017:27017 -n default`
- Connect using extension: `mongodb://localhost:27017/?directConnection=true&replicaSet=rs0`

## Debugging
- Run busybox inside a new terminal, to run an interactive container inside K8s for debugging:
    - `kubectl run bb --image=busybox -i --tty --restart=Never -n default` 