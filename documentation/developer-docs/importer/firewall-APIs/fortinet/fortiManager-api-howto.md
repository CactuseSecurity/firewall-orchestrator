# Integrating Fortinet FortiManager 7.x

## API Docs

(need account)

https://fndn.fortinet.net/index.php?/fortiapi/5-fortimanager/&_fromLogin=1



## user setup

connect to FM via ssh (admin/empty password) and add the following config

### make existing admin api ready

```console
config system admin user
  edit admin
  set rpc-permit read-write
end
```

### create api user profile

```console
config system admin profile
    edit "apiuserprofile"
       set super-user-profile enable
    next
```
enter yes here!

NB: the user will have full rw access via UI but can be restricted to read-only via API as shown below.
Need to find out if there is a more secure way to create an all-read-only api user

### Create read-only api user
```console
config system admin user
   edit "apiuser"
       set password xxx
       set adom "all_adoms"             
       set profileid "apiuserprofile"
       set rpc-permit read
   end
```

### Create full access api user
```console
config system admin user
   edit "apiuser"
       set password xxx
       set adom "all_adoms"             
       set profileid "apiuserprofile"
       set rpc-permit read-write
end       
```

## login
```console
curl --request POST \
  --url https://10.5.1.55/jsonrpc \
  --header 'Content-Type: application/json' \
  --data '{
  "id": 1,
  "method": "exec",
  "params": [
    {
      "data": [
        {
          "passwd": "xxx", 
          "user": "apiuser"
        }
      ], 
      "url": "sys/login/user"
    }
  ]
}'
```

gives
```json
{
  "id": 1,
  "result": [
    {
      "status": {
        "code": 0,
        "message": "OK"
      },
      "url": "sys\/login\/user"
    }
  ],
  "session": "KCOuhqKTFt3ISXKntpIVO2kA5GJ+QcorMoxm8xLGru0HxrwwpgWuTtRcU8P9XCpbIRlDjjSv2+lzTYYIt1bSzw=="
}
```

## logout

```console
curl --request POST \
  --url https://10.5.1.55/jsonrpc \
  --header 'Content-Type: application/json' \
  --data '{
  "id": 1,
  "jsonrpc": "1.0", 
  "method": "exec",
  "params": [
    {
      "url": "sys/logout"
    }
  ],
  "verbose": 1,
	"session": "BJG0kh4qBopxgjJ+DwEyxJSWCl3MzHdeeympX4GJqw50EoLIjXoLH3+7W3e4N9EqtWb5IhGKBJugGKS6HQrDUg=="
}'
```

## get a list of all ADOMs

```console
curl --request POST \
  --url https://10.5.1.55/jsonrpc \
  --header 'Content-Type: application/json' \
  --data '{
  "method": "get",
  "params": [
    {
	  "fields": ["name", "oid", "uuid"],
      "url": "/dvmdb/adom"
    }
  ],
  "session": "lieHUJqiA0VldI45nVh8K2o0kP2XRm7NrrayIL1t977BG78\/wukwCgnFnpClbH9A6rAQbCPVjcGVFOw1VwULLQ=="
}
'
```

## get a list of fw rules

```console
curl --request POST \
  --url https://10.5.1.55/jsonrpc \
  --header 'Content-Type: application/json' \
  --data '{
  "method": "get",
  "params": [
    {
      "url": "/pm/config/adom/my_adom/pkg/mypkg/firewall/policy"
    }
  ],
	"verbose": 1,
  "id": 2,
  "session": "++7z161rod0cGMaStLUWohDpyUnsyT030tNuLPyVYvIhd0GCLXwp9vCJRKnYV4I0Q\/di1bSL3Wf7o8oNnWu6cw=="
}'
```

## get nat rules

### get snat rules
```console
{
  "method": "get",
  "params": [
    {
      "url": "/pm/config/adom/my_adom/pkg/mypkg/firewall/central-snat-map"
    }
  ],
	"verbose": 1,
  "id": 2,
  "session": "++7z161rod0cGMaStLUWohDpyUnsyT030tNuLPyVYvIhd0GCLXwp9vCJRKnYV4I0Q\/di1bSL3Wf7o8oNnWu6cw=="
}'
```
### get dnat rules

```console
curl --request POST \
  --url https://10.5.1.55/jsonrpc \
  --header 'Content-Type: application/json' \
  --data '{
  "method": "get",
  "params": [
    {
      "url": "/pm/config/adom/my_adom/pkg/mypkg/firewall/central/dnat"
    }
  ],
	"verbose": 1,
  "id": 2,
  "session": "++7z161rod0cGMaStLUWohDpyUnsyT030tNuLPyVYvIhd0GCLXwp9vCJRKnYV4I0Q\/di1bSL3Wf7o8oNnWu6cw=="
}'
```