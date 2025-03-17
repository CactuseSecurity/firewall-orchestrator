# Integrating Check Point R8x via API

## initial CP API connect
First connect to api should result in the following:
```console
tim@acantha:~$ wget --no-check-certificate https://192.168.100.110/web_api/ 
Connecting to 192.168.100.110:443... connected.
WARNING: cannot verify 192.168.100.110's certificate, issued by ‘unstructuredName=An optional company name,emailAddress=Email Address,CN=192.168.100.110,L=Locality Name (eg\\, city)’:
  Self-signed certificate encountered.
HTTP request sent, awaiting response... 401 Unauthorized
Username/Password Authentication Failed.
```

if you get the following:
```console
tim@acantha:~$ wget --no-check-certificate https://192.168.9.99/web_api/ 
HTTP request sent, awaiting response... 403 Forbidden
2020-06-03 12:56:12 ERROR 403: Forbidden.
``` 
make sure the api server is up and running and accepting connections from your ip address:
(taken from <https://community.checkpoint.com/t5/API-CLI-Discussion-and-Samples/Enabling-web-api/td-p/32641>)
```console
mgmt_cli -r true --domain MDS set api-settings accepted-api-calls-from "All IP addresses"`
api restart
```
### create api user profile

```console
```

### Create read-only api user
```console
```

### Create full access api user
```console
```

## login
```console
curl --insecure --request POST --url https://192.168.100.88/web_api/login --header 'Content-Type: application/json' --data '{"user": "apiuser", "password" : "xxx"}'
```

gives the sid (session id) which can then be used to authenticate for further api calls
```json
{
  "sid" : "PhTmI9SD02MTtCWCcTHpc8FsIlX63icc9CvF19PB3qo",
  "url" : "https://192.168.100.88:443/web_api",
  "session-timeout" : 600,
  "last-login-was-at" : {
    "posix" : 1631716365111,
    "iso-8601" : "2021-09-15T16:32+0200"
  },
  "read-only" : true,
  "api-server-version" : "1.8",
  "user-name" : "apiuser",
  "user-uid" : "ba2038a1-437f-45ef-8ea5-c8785cdad9a7"
}```

## login to a domain of an MDS
```console
curl --insecure --request POST --url https://192.168.100.88/web_api/login --header 'Content-Type: application/json' --data '{"user": "apiuser", "password" : "xxx", "domain": "yyy"}'
```

gives the sid (session id) which can then be used to authenticate for further api calls
```json
{
  "sid" : "PhTmI9SD02MTtCWCcTHpc8FsIlX63icc9CvF19PB3qo",
  "url" : "https://192.168.100.88:443/web_api",
  "session-timeout" : 600,
  "last-login-was-at" : {
    "posix" : 1631716365111,
    "iso-8601" : "2021-09-15T16:32+0200"
  },
  "read-only" : true,
  "api-server-version" : "1.8",
  "user-name" : "apiuser",
  "user-uid" : "ba2038a1-437f-45ef-8ea5-c8785cdad9a7"
}
```


## logout

```console
```

## get a rulebase (part)

```console
curl --insecure --request POST \
  --url https://192.168.100.88/web_api/show-access-rulebase \
  --header 'Content-Type: application/json' \
  --header 'X-chkp-sid: PhTmI9SD02MTtCWCcTHpc8FsIlX63icc9CvF19PB3qo' \
  --data '{"name": "FirstLayer shared with inline layer"}'
```

## get an arbitrary object by UID

```console
curl --insecure --request POST   --url https://192.168.100.88/web_api/show-object   --header 'Content-Type: application/json'   --header 'X-chkp-sid: KJC5pzFMSRINoVTSByVhUq1xdEE33WD0uy9iXl-cG-4'   --data '{"uid": "dd699ecd-1420-41a0-931f-de7f55f799b6", "details-level": "full"}'
```
results in 
```console
{
  "object" : {
    "uid" : "d699ecd-1420-41a0-931f-de7f55f799b6",
    "type" : "access-section",
    "domain" : {
      "uid" : "3981ee76-52c3-1744-bf5b-75fe309b1ed9",
      "name" : "dom-name1",
      "domain-type" : "domain"
    },
    "tags" : [ ],
    "meta-info" : {
      "lock" : "unlocked",
      "validation-state" : "ok",
      "last-modify-time" : {
        "posix" : 1668506934927,
        "iso-8601" : "2022-11-15T11:08+0100"
      },
      "last-modifier" : "admin-user-1234",
      "creation-time" : {
        "posix" : 1668506934927,
        "iso-8601" : "2022-11-15T11:08+0100"
      },
      "creator" : "admin-user-3433"
    },
    "read-only" : false
  }
}
```
