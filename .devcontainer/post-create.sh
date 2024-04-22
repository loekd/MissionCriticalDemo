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


## Build the containers
cd /workspaces/MissionCriticalDemo/MissionCriticalDemo
docker-compose -f docker-compose.yml -f docker-compose.override.yml build

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


# turn dispatch port to public (to allow CORS)
gh codespace ports visibility 5133:public -c $CODESPACE_NAME


echo "Editing the file '/workspaces/MissionCriticalDemo/MissionCriticalDemo/MissionCriticalDemo.FrontEnd/wwwroot/appsettings.json' and put https://$CODESPACE_NAME-5133.app.github.dev as value for DispatchApi:Endpoint"
export URL="https://$CODESPACE_NAME-5133.app.github.dev"
jq --arg newEndpoint "$URL" '.DispatchApi.Endpoint = $newEndpoint' /workspaces/MissionCriticalDemo/MissionCriticalDemo/MissionCriticalDemo.FrontEnd/wwwroot/appsettings.json > temp.json && mv temp.json /workspaces/MissionCriticalDemo/MissionCriticalDemo/MissionCriticalDemo.FrontEnd/wwwroot/appsettings.json
echo "Make sure to add the proper redirect uri to Azure AD as well!"

echo "Run `docker-compose -f docker-compose.yml -f docker-compose.override.yml up` to run the containers"