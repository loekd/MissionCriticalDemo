#!/bin/sh

## Build the containers
docker build -f ../MissionCriticalDemo/MissionCriticalDemo.DispatchApi/Dockerfile ../MissionCriticalDemo -t missioncriticaldemo.dispatchapi:2.0.0
docker build -f ../MissionCriticalDemo/MissionCriticalDemo.Frontend/MissionCriticalDemo.Frontend/Dockerfile ../MissionCriticalDemo -t missioncriticaldemo.frontend:2.0.0
docker build -f ../MissionCriticalDemo/MissionCriticalDemo.PlantApi/Dockerfile ../MissionCriticalDemo -t missioncriticaldemo.plantapi:2.0.0

## Import locally built images to k3d
# k3d image import missioncriticaldemo.frontend:2.0.0
# k3d image import missioncriticaldemo.dispatchapi:2.0.0
# k3d image import missioncriticaldemo.plantapi:2.0.0