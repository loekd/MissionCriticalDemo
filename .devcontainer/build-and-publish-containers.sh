#!/bin/sh

## Build the containers
cd /workspaces/MissionCriticalDemo/MissionCriticalDemo
docker-compose -f docker-compose.yml -f docker-compose.override.yml build


## Import locally built images to k3d
if [ -n "$CODESPACE_NAME" ]; then
    k3d image import missioncriticaldemo.frontend:latest
    k3d image import missioncriticaldemo.dispatchapi:latest
    k3d image import missioncriticaldemo.plantapi:latest
fi
## else push to acrradius in Azure
else
    ## Define the container registry name
    CONTAINER_REGISTRY=acrradius.azurecr.io
    
    ## log in
    echo "make sure to log in first using `az acr login --name $CONTAINER_REGISTRY`"
    
    ## Push images to the container registry
    docker tag missioncriticaldemo.frontend:latest $CONTAINER_REGISTRY/missioncriticaldemo.frontend:latest
    docker tag missioncriticaldemo.dispatchapi:latest $CONTAINER_REGISTRY/missioncriticaldemo.dispatchapi:latest
    docker tag missioncriticaldemo.plantapi:latest $CONTAINER_REGISTRY/missioncriticaldemo.plantapi:latest
    docker push $CONTAINER_REGISTRY/missioncriticaldemo.frontend:latest
    docker push $CONTAINER_REGISTRY/missioncriticaldemo.dispatchapi:latest
    docker push $CONTAINER_REGISTRY/missioncriticaldemo.plantapi:latest
fi
