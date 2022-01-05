# How to debug fworch using vscode or vs

## using central backend host
devsrvffm is a central backend server for all services that are not debugged locally on client via visual studio (code)

### connect as user developer via ssh on port 60333/tcp (2nd server on 60334/tcp)

(on linux this needs to be done as root to be able to forward low port 636)

    sudo ssh -i <your-private-key> -p 60333 developer@cactus.de -L 9443:localhost:9443 -L 636:localhost:636 [-L 60443:localhost:443]

example for user tim:

    sudo ssh -i /home/tim/.ssh/id_rsa -p 60333 developer@cactus.de -L 9443:localhost:9443 -L 636:localhost:636
    
note: your public key needs to be added to /home/devoloper/.ssh/authorized_keys on devsrvffm 

### automatic updates of test server
The testserver has a webhook receiver running at https://cactus.de:60355/fwo (2nd server on 60345/tcp) which is triggered by changes to the cactus repo (not the individual forks!).
Only changes to roles, inventory and site.yml will trigger a rebuild, not documentation changes.

### add local config on development client

In order to allow locally running components to read the necessary config you have to create the following config files on your client:
    /etc/fworch/   (in Windows "current drive":\etc\fworch\)
        fworch.yaml
        secrets/
          jwt_private_key.pem
          jwt_public_key.pem

For keys see below.

If you manually replace the keys on the server side (use the test keys) you need to reboot the server to reload all services depending on these keys.

#### /etc/fworch/fworch.yaml
```yaml
fworch_home: "/usr/local/fworch"
dotnet_mode: "Release"
product_version: 5.1

# api
api_uri: "https://127.0.0.1:9443/api/v1/graphql"
api_hasura_jwt_alg: "RS256"

# middleware
middleware_JWT_key_file: "/usr/local/fworch/etc/secrets/jwt_private_key.pem"
middleware_uri: "http://127.0.0.1:8880/"
middleware_native_uri: "http://127.0.0.1:8880/"

```

### test /etc/fworch/secrets/jwt_private_key.pem 
```console
-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDQgsA0KrEllrMG
iyR1Z3GbRG/JvqJi3V6J5x6nl4Idur4earg0mkmQhwtp3N3ab+fSrRWQ4DCuk3j3
1G1nFngcqCF4P1n+7OuAgoNAWad30rKBiMgC5tBfEL8IWC/v0J8b34sjIoK7ig1e
56hgmdJePNX000EXTltWPbNmwmd5mW6NAo23vExeEWju5alsDHypg/WfVGMFVPIh
opzh0MH3Rs2GlIHP8vf/T2Hj6vbrFmWhlGHN8yumVRYFVjLMfaD9vRU6NdLYKh7w
dXXRG3zN5wGvP8lht/0dVjuDexZO57J3n5d0JaQhtnLQDfKI7mMKW1tlaXzoGPNS
wXDNLXRpAgMBAAECggEAcIT+f6hCLEykk3Vm2UxXTDikMrSDIOLg734fVsU13CE4
E2X3vbs805dsm1YrCoO1EEWDC8lPvAWtE+A4oZbIfn5kgWV+hUkI1TKY7ZL0An9b
APf1J0uzSnnY/QHJ7JoiIoKBrRYkglu/V2WfwXGAyoX9175gs9j/BQ0K0Ps7p6wh
S7AaDY+TNIcVnCOj4EOoE4r/sJagBCG+INvDb6bToYnVjJOg5E6mBP1NSiA9297y
eGcGwYSE4N48ddlBlUualJJpCeQDfg6FopYqzPpbBP91MjdoTS6GTnikwl9NurH+
QM/eHr/WPwoYulSajj/J2/alPfj+T657btSRThuHeQKBgQDrcthaM5HkRO51vjxK
GKIQZ2DFjaugqjXD7AVzsJ16JGUpllILiTIe4RHuqABavWnxkPXPufp8pXsAjhSR
3mVOdqIZHcCLHeb9hXqF37nbjTZXl5UKM79Fj3TeIPZhrrfssEhR+1KQVLbXOOW/
uAyYsX+eTZoWTq4uHHBJpAS8/wKBgQDitfj+w4yoYEoun+F4qV7AdqNupHDqlSI0
UH+ynSPpNnWW3NZSQfNBW0B1mRSbxbFUaHTEQIIrqAMHufRtCjN7guoRuTHoaMjY
EYbzVjThhYaQOV2QsGesgDxnIpYxehliTNWI29lH5da9iU6kuw/4yqyfBPlAk8tP
hIXR6gwGlwKBgQDpCqQwK6j0WnH4IpID+Pu35sq23rGIddB/moYO6zoDYjCrB/kv
J91vCPC52pl3NtG84vEaaQcQBq6HbxnXA1wcXHm3CGbCi5dNSadrCHUqZUvrSMKg
9XUVQZe4IMIdD1VGtXjvhCVFbEQJJGzq5R26qL0bD8461Ce8xjMyAGEcOwKBgHPo
GS8XBvimkgaYUwv/e6Pmg9PzWo90Q5J/fWnyEQQQhbnlmeVgNl+5qZD1/KVPQ0Qm
S1xypppvQW1X0vFUf9GrssPw7OUnfVeKTnZmIo8SVyOxUHbC2Z5FyZvpAOS2yfeJ
1LotvD6X8VvhsUVjJd1KsUpznoM3jIBQ/qG9iPxtAoGAN2hcChvFPgd3SCdeb2+v
9Ja1HAChZqia3uqCmc/pVyaryyw6+RUdNs21rXdN3QrD59qO57Nxkl3jjuA13GBL
CtMKWsucknr8NZfqU0LLrJG2J3jJjbefqJtrWRdfMkJLPfOEutEag5IvTGtupg2V
O0MsC4bS3CBU5rJuC0ld584=
-----END PRIVATE KEY-----
```
### test /etc/fworch/secrets/jwt_public_key.pem 
```console
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0ILANCqxJZazBoskdWdx
m0Rvyb6iYt1eiecep5eCHbq+Hmq4NJpJkIcLadzd2m/n0q0VkOAwrpN499RtZxZ4
HKgheD9Z/uzrgIKDQFmnd9KygYjIAubQXxC/CFgv79CfG9+LIyKCu4oNXueoYJnS
XjzV9NNBF05bVj2zZsJneZlujQKNt7xMXhFo7uWpbAx8qYP1n1RjBVTyIaKc4dDB
90bNhpSBz/L3/09h4+r26xZloZRhzfMrplUWBVYyzH2g/b0VOjXS2Coe8HV10Rt8
zecBrz/JYbf9HVY7g3sWTueyd5+XdCWkIbZy0A3yiO5jCltbZWl86BjzUsFwzS10
aQIDAQAB
-----END PUBLIC KEY-----
```


### configure port forwarding via ssh

- central server: ssh://developer@cactus.de:60333
  - 9443:localhost:9443
  - 636:localhost:636


### debugging starts the following local listeners on client

- 5001 - blazor
- 8880 - middleware-server

## install webhook

simply install role webhook plus
- double-check that a) ssl is not checked if not using properly signed cert and b) Content type of the webhook call is set to application/json
- create secrets file ~/fworch-webhook.secret containing webhook secret
- copy ssh private key for deployment to ~/.ssh/id_github_deploy


## FAQ
### Errors during debugging

If you encounter the following error during vscode debugging

    The configured user limit (128) on the number of inotify instances has been reached, or the per-process limit on the number of open file descriptors has been reached.

add the following lines to the end of /etc/sysctl.conf

    fs.inotify.max_user_watches = 1638400
    fs.inotify.max_user_instances = 1638400

and run 
    
    sudo sysctl -p
