# Common API Helpers

## How to convert file from json to yaml

    python -c 'import sys, yaml, json; yaml.safe_dump(json.load(sys.stdin), sys.stdout, default_flow_style=False)' < file.json > file.yaml

## How to convert a yaml file to json

    python -c 'import sys, yaml, json; json.dump(yaml.safe_load(sys.stdin), sys.stdout)' < meta.yaml >meta.json

## How to convert JSON pretty print

from pp to compact:
    python -c 'import sys, json; json.dump(json.load(sys.stdin), sys.stdout)' < file.json > file.json

from compact to pp:
    python -c 'import sys, json; json.dump(json.load(sys.stdin), sys.stdout, indent=3)' < file.json > file.json

## Troubleshooting hasura API

Display api container status

    tim@fworch-comp:~$ sudo docker ps --all
    CONTAINER ID   IMAGE                           COMMAND                  CREATED        STATUS        PORTS     NAMES
    6a4941843351   hasura/graphql-engine:v2.0.10   "graphql-engine serve"   15 hours ago   Up 15 hours             fworch-api
    tim@fworch-comp:~$ 

Restart FWORCH API

    sudo docker restart fworch-api

Note that you need to restart at least the middlware server since its subscription will break:

```console
tim@fworch-comp:~$ sudo systemctl restart fworch-middleware
tim@fworch-comp:~$ sudo systemctl status fworch-*
● fworch-importer-api.service - fworch importer pure python
     Loaded: loaded (/lib/systemd/system/fworch-importer-api.service; enabled; vendor preset: enabled)
     Active: active (running) since Thu 2021-12-09 20:07:28 CET; 14h ago
    Process: 340899 ExecStartPre=/bin/sleep 10 (code=exited, status=0/SUCCESS)
   Main PID: 341171 (import-main-loo)
      Tasks: 1 (limit: 4637)
     Memory: 16.8M
     CGroup: /system.slice/fworch-importer-api.service
             └─341171 /usr/bin/python3 /usr/local/fworch/importer/import-main-loop.py

Dez 09 20:07:18 fworch-comp systemd[1]: Starting fworch importer pure python...
Dez 09 20:07:28 fworch-comp systemd[1]: Started fworch importer pure python.

● fworch-importer.service - fworch importer
     Loaded: loaded (/lib/systemd/system/fworch-importer.service; enabled; vendor preset: enabled)
     Active: active (running) since Thu 2021-12-09 20:08:24 CET; 14h ago
    Process: 342644 ExecStartPre=/bin/sleep 10 (code=exited, status=0/SUCCESS)
   Main PID: 342651 (fworch-importer)
      Tasks: 1 (limit: 4637)
     Memory: 13.9M
     CGroup: /system.slice/fworch-importer.service
             └─342651 /usr/bin/perl -w /usr/local/fworch/importer/fworch-importer-main.pl

Dez 10 10:45:54 fworch-comp fworch-importer[396236]: version: R5x-R7x, manufacturer: check point, current_import_id=1897
Dez 10 10:45:55 fworch-comp fworch-import[396236]: Management checkpoint_demo (mgm_id=2), no changes in configuration files (MD5)
Dez 10 10:45:55 fworch-comp fworch-importer[396236]: Management checkpoint_demo (mgm_id=2), no changes in configuration files (MD5)
Dez 10 10:45:55 fworch-comp fworch-import[342651]: Import: looking at fortigate_demo ...
Dez 10 10:45:55 fworch-comp fworch-import[342651]: Import: running on responsible importer fworch-comp ...
Dez 10 10:45:55 fworch-comp fworch-importer[342651]: Import: looking at fortigate_demo ... Import: running on responsible importer fworch-comp ...
Dez 10 10:45:55 fworch-comp fworch-importer[396481]: version: 5.x-6.x, manufacturer: fortinet, current_import_id=1898
Dez 10 10:45:56 fworch-comp fworch-import[396481]: Management fortigate_demo (mgm_id=1), no changes in configuration files (MD5)
Dez 10 10:45:56 fworch-comp fworch-importer[396481]: Management fortigate_demo (mgm_id=1), no changes in configuration files (MD5)
Dez 10 10:45:56 fworch-comp fworch-import[342651]: -------- Import module: going back to sleep for 40 seconds --------

● fworch-middleware.service - FWOrch Middleware Server
     Loaded: loaded (/lib/systemd/system/fworch-middleware.service; enabled; vendor preset: enabled)
     Active: active (running) since Fri 2021-12-10 10:45:59 CET; 3s ago
    Process: 396234 ExecStartPre=/bin/sleep 10 (code=exited, status=0/SUCCESS)
   Main PID: 396568 (FWO.Middleware.)
      Tasks: 19 (limit: 4637)
     Memory: 35.2M
     CGroup: /system.slice/fworch-middleware.service
             └─396568 /usr/local/fworch/middleware/files/FWO.Middleware.Server/bin/Release/net8.0/FWO.Middleware.Server

Dez 10 10:45:59 fworch-comp fworch.middleware-server[396568]: Info - Jwt generation (JwtWriter.cs in line 87): Generated JWT eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6Im1pZGRsZXdhcmU>
Dez 10 10:45:59 fworch-comp fworch.middleware-server[396568]: Info - Found ldap connection to server (Program.cs in line 32): 127.0.0.1:636
Dez 10 10:45:59 fworch-comp fworch.middleware-server[396568]: info: Microsoft.Hosting.Lifetime[14]
Dez 10 10:45:59 fworch-comp fworch.middleware-server[396568]:       Now listening on: http://127.0.0.1:8880
tim@fworch-comp:~$ 
```