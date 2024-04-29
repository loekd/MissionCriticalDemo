#!/bin/sh

## Build the containers
cd /workspaces/MissionCriticalDemo/MissionCriticalDemo
docker-compose -f docker-compose.yml -f docker-compose.override.yml build

## Import locally built images to k3d
k3d image import missioncriticaldemo.frontend:latest
k3d image import missioncriticaldemo.dispatchapi:latest
k3d image import missioncriticaldemo.plantapi:latest