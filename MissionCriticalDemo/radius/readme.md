# Getting started

- Dispatch API requires query support. This is not present in the default state store.
- Run this helm command to install a mongodb in replicaset mode:
    - `helm install --set service.nameOverride=mongo --set replicaCount=1 --set architecture=replicaset --set auth.enabled=false mongo oci://registry-1.docker.io/bitnamicharts/mongodb`