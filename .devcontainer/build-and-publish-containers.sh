#!/bin/sh

#pre-requisites:
#docker buildx create --name multibuilder --use

#build local images
docker build -f ../MissionCriticalDemo/MissionCriticalDemo.DispatchApi/Dockerfile ../MissionCriticalDemo -t missioncriticaldemo.dispatchapi:2.0.0
docker build -f ../MissionCriticalDemo/MissionCriticalDemo.Frontend/MissionCriticalDemo.Frontend/Dockerfile ../MissionCriticalDemo -t missioncriticaldemo.frontend:2.0.0
docker build -f ../MissionCriticalDemo/MissionCriticalDemo.PlantApi/Dockerfile ../MissionCriticalDemo -t missioncriticaldemo.plantapi:2.0.0

az acr login --name acrradius

## Build the containers
docker buildx build --platform linux/amd64,linux/arm64 -f ../MissionCriticalDemo/MissionCriticalDemo.DispatchApi/Dockerfile ../MissionCriticalDemo -t acrradius.azurecr.io/missioncriticaldemo.dispatchapi:2.0.0 --push
docker buildx build --platform linux/amd64,linux/arm64 -f ../MissionCriticalDemo/MissionCriticalDemo.Frontend/MissionCriticalDemo.Frontend/Dockerfile ../MissionCriticalDemo -t acrradius.azurecr.io/missioncriticaldemo.frontend:2.0.0 --push
docker buildx build --platform linux/amd64,linux/arm64 -f ../MissionCriticalDemo/MissionCriticalDemo.PlantApi/Dockerfile ../MissionCriticalDemo -t acrradius.azurecr.io/missioncriticaldemo.plantapi:2.0.0 --push

docker tag acrradius.azurecr.io/missioncriticaldemo.dispatchapi:2.0.0 missioncriticaldemo.dispatchapi:2.0.0
docker tag acrradius.azurecr.io/missioncriticaldemo.frontend:2.0.0 missioncriticaldemo.frontend:2.0.0
docker tag acrradius.azurecr.io/missioncriticaldemo.plantapi:2.0.0 missioncriticaldemo.plantapi:2.0.0

## Push the containers to ACR

# docker push acrradius.azurecr.io/missioncriticaldemo.dispatchapi:2.0.0
# docker push acrradius.azurecr.io/missioncriticaldemo.frontend:2.0.0
# docker push acrradius.azurecr.io/missioncriticaldemo.plantapi:2.0.0

## Import locally built images to k3d
# k3d image import missioncriticaldemo.frontend:2.0.0
# k3d image import missioncriticaldemo.dispatchapi:2.0.0
# k3d image import missioncriticaldemo.plantapi:2.0.0