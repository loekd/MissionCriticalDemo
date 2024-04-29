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
Run `kubectl run bb --image=busybox -i --tty --restart=Never -n default-radius` inside a new terminal, to run an interactive container inside K8s for debugging.