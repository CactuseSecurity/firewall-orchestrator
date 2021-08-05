# Integrating Fortinet FortiManager 7.x

## Create api user
```console
create api user
config system admin user
   edit "apiuser"
       set password xxx
       set adom "all_adoms"             
       set rpc-permit read-write
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
