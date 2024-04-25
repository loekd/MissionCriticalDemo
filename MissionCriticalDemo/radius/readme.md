# Getting started

Most data stores can be built by using Dapr defaults, except one.

## Prepare queryable external database
- Dispatch API requires query support. This is not present in the default state store.
- This helm command was used to install a mongodb in replicaset mode:
    - `helm install --set service.nameOverride=mongo --set replicaCount=1 --set architecture=replicaset --set auth.enabled=false mongo oci://registry-1.docker.io/bitnamicharts/mongodb`

## Prepare Radius

- Run this command to prepare radius:
    - `/workspaces/MissionCriticalDemo/MissionCriticalDemo/radius# rad init`

## Usage


Open the user interface.
- `https://fuzzy-yodel-g4495xr5xw736r7-80.app.github.dev/dispatch`
- Log in using Azure AD B2C
- Inject some gas 
- When everything is working, you will get a notification about processing, and the numbers should increase or decrease.

Check Gas In Store at Plant level
`https://fuzzy-yodel-g4495xr5xw736r7-8082.app.github.dev/api/gasinstore/gasinstore`

