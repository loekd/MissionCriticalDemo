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
docker pull redis/redisinsight:latest