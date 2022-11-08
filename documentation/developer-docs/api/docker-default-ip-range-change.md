# how to change the default ip range for docker

in case of a conflict (if you use the 172.16.0.0/16 range internally), you can use the following instructions taken from https://support.hyperglance.com/knowledge/changing-the-default-docker-subnet to change the network.

1. create/modify file /etc/docker/daemon.json to contain new ip address:

```json
{
"log-driver": "journald",
"log-opts": {
"tag": "{{.Name}}"
},
"bip": "172.26.0.1/16"
}
```

2. restart docker service:

```sudo systemctl restart docker```

3. restart docker container:

```sudo docker restart fworch-api```
