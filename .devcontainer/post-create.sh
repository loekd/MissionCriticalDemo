#!/bin/sh

## Create a k3d cluster
while (! kubectl cluster-info ); do
  # Docker takes a few seconds to initialize
  echo "Waiting for Docker to launch..."
  k3d cluster delete
  k3d cluster create -p '8081:80@loadbalancer' --k3s-arg '--disable=traefik@server:0'
  sleep 1
done

## Install Dapr and init
wget -q https://raw.githubusercontent.com/dapr/cli/master/install/install.sh -O - | /bin/bash
dapr uninstall # clean if needed
dapr init -k




docker pull mongo:7.0
docker pull bitnami/mongodb:6.0.2
docker pull mongo-express
docker pull redis
docker pull redis
docker pull redis/redisinsight:latest
docker pull daprio/dashboard:latest
docker pull daprio/daprd:1.13.1
docker pull jaegertracing/all-in-one:1.6
#docker pull openzipkin/zipkin:2.23.4


## turn dispatch port to public (to allow CORS)
gh codespace ports visibility 8080:public -c $CODESPACE_NAME

## configure the dispatch api endpoint
export URL="https://$CODESPACE_NAME-8080.app.github.dev"
echo "Editing the file '/workspaces/MissionCriticalDemo/MissionCriticalDemo/MissionCriticalDemo.FrontEnd/wwwroot/appsettings.json' and put $URL as value for DispatchApi:Endpoint"
jq --arg newEndpoint "$URL" '.DispatchApi.Endpoint = $newEndpoint' /workspaces/MissionCriticalDemo/MissionCriticalDemo/MissionCriticalDemo.FrontEnd/wwwroot/appsettings.json > temp.json && mv temp.json /workspaces/MissionCriticalDemo/MissionCriticalDemo/MissionCriticalDemo.FrontEnd/wwwroot/appsettings.json

## notify user about the frontend redirect url
export URL="https://$CODESPACE_NAME-80.app.github.dev/authentication/login-callback"
echo "Make sure to add this redirect uri \"$URL\" to Azure AD as well!"

## run MongoDb replicaset
helm install --set service.nameOverride=mongo --set replicaCount=1 --set architecture=replicaset --set auth.enabled=false mongo oci://registry-1.docker.io/bitnamicharts/mongodb

## echo hint about running the containers
echo "Run \" sh ../../.devcontainer/build-and-publish-containers.sh\" to build and publish the container images locally"
## hint to run the containers
echo "Run \"docker-compose -f docker-compose.yml -f docker-compose.override.yml up\" to run the containers"

## hint to run the containers using radius
echo "Run \"rad init\" or \"rad run\" to run the containers using radius, from the folder '/workspaces/MissionCriticalDemo/MissionCriticalDemo/radius'"

## mark repo as safe
git config --global --add safe.directory /workspaces/MissionCriticalDemo

## add dotnet dev cert
dotnet dev-certs https