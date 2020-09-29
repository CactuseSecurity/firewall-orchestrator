# how to debug using vscode or vs

## setting up central backend host
using devservffm as a central server for all services that are not debugged via visual studio (code)

### connect via ssh
this needs to be done as root to be able to forward low port 636

    sudo ssh -i <your-private-key> -p 60333 developer@cactus.de -L 9443:localhost:9443 -L 636:localhost:636 [-L 60443:localhost:443]

for user tim:

    sudo ssh -i /home/tim/.ssh/id_rsa -p 60333 developer@cactus.de -L 9443:localhost:9443 -L 636:localhost:636
    
note: your public key needs to be added to /home/authorized_keys on devservffm 

### automatic updates of test server
The testserver has a webhook receiver running at https://cactus.de:60344/fwo which is triggered by changes to the cactus master.

todo: exclude changes to documentation

### add local config on development client

In order to allow locally running components to read the necessary config you have to create the following config files on your client:
    /etc/fworch/   (in Windows <current drive>:\etc\fworch\)
        fworch.yaml
        secrets/
          jwt_private_key.pem
          jwt_public_key.pem

### /etc/fworch/fworch.yaml
```yaml
fworch_home: "/usr/local/fworch"
dotnet_mode: "Release"

# api
api_uri: "https://127.0.0.1:9443/api/v1/graphql"
api_hasura_jwt_alg: "RS256"

# auth
auth_uri: "http://127.0.0.1:8888"
auth_hostname: "127.0.0.1"
auth_server_port: "8888"
```

### configure port forwarding per ssh

- central server (to be built): cactus.de
  - 9443:localhost:9443
  - 636:localhost:636


### debugging starts the folloging local listeners on client

- 5001 - blazor
- 8888 - auth-server
