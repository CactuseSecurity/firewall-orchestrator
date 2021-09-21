# Integrating Cisco ACI

Introduction on ACI reg. firewalling:
<https://www.cisco.com/c/en/us/td/docs/switches/datacenter/aci/apic/sw/5-x/security/cisco-apic-security-configuration-guide-50x/m-endpoint-security-groups.html>

API references: <https://www.cisco.com/c/en/us/td/docs/switches/datacenter/aci/apic/sw/2-x/rest_cfg/2_1_x/b_Cisco_APIC_REST_API_Configuration_Guide/b_Cisco_APIC_REST_API_Configuration_Guide_chapter_01.html#reference_7105100D869A4B0A9160FA2013D46B7B>

## online sandbox APIC

- base (UI) URL: https://sandboxapicdc.cisco.com
- doc URL: https://sandboxapicdc.cisco.com/doc/html
- API URL: https://sandboxapicdc.cisco.com/api/xxx.json
- user: admin
- password: !v3G@!4@Y https://sandboxapicdc.cisco.com

## user setup

## login
```console
curl --request POST \
  --url https://sandboxapicdc.cisco.com/api/aaaLogin.json \
  --header 'Content-Type: application/json' \
  --data '{
  "aaaUser" : {
    "attributes" : {
      "name" : "admin",
      "pwd" : "!v3G@!4@Y"
    }
  }
}'
```
gives
```json
{
  "totalCount": "1",
  "imdata": [
    {
      "aaaLogin": {
        "attributes": {
          "token": "eyJhbGciOiJSUzI1NiIsImtpZCI6ImRsY29hejNuenpud2xwNGYzaWN2ajY5bDExZnAwbXlzIiwidHlwIjoiand0In0.eyJyYmFjIjpbeyJkb21haW4iOiJhbGwiLCJyb2xlc1IiOjAsInJvbGVzVyI6MX1dLCJpc3MiOiJBQ0kgQVBJQyIsInVzZXJuYW1lIjoiYWRtaW4iLCJ1c2VyaWQiOjE1Mzc0LCJ1c2VyZmxhZ3MiOjAsImlhdCI6MTYzMTk2OTY2MiwiZXhwIjoxNjMxOTcwMjYyLCJzZXNzaW9uaWQiOiJKWmFmL0d1c1RIdTBiWEF6S1RBaG9BPT0ifQ.SA2rp64Ywr2F25mWEbwi7ADmtSJNp6N4Jb8w21WmREcY2pha_qgbzFr4_bEcV7a12jCUOmvr37RD0BCy5BBpS9jQFWoxDjVawDvhGhzThAgac9xXd8KZ0dwlW3ebNMuamHeldWJ8C2C36EqfdRPL-I27uMDrOkZEc25cBu06MTlsrlk4RerN47z0ALhSlzqCE4zL5j66cBh9JCS3p82YuNSg7sz-6M-JbjA5zGeJBU5Ss3VEv0WGIahp4h6d8__kZlxjSB6CBh2a9IJaz9nO1mAEDw8yYcnTxCo1G_upTCTiVKtvGF4GgLEUieMMoTqDq8IB27G9g7SLJvmW7fmcIw",
          "siteFingerprint": "dlcoaz3nzznwlp4f3icvj69l11fp0mys",
          "userName": "admin",
          "version": "5.2(1g)",
        }
      }
    }
  ]
}
```

## logout
From the documentation at <https://www.cisco.com/c/en/us/td/docs/switches/datacenter/aci/apic/sw/2-x/rest_cfg/2_1_x/b_Cisco_APIC_REST_API_Configuration_Guide/b_Cisco_APIC_REST_API_Configuration_Guide_chapter_01.html>:

      At this time, the aaaLogout method returns a response but does not end a session. Your session ends after a refresh timeout when you stop sending aaaRefresh messages. 

## get a list of Domains

```console
curl --request GET \
  --url https://sandboxapicdc.cisco.com/api/aaaListDomains.json \
  --header 'Content-Type: application/json' \
  --cookie APIC-cookie=eyJhbGciOiJSUzI1NiIsImtpZCI6ImRsY29hejNuenpud2xwNGYzaWN2ajY5bDExZnAwbXlzIiwidHlwIjoiand0In0.eyJyYmFjIjpbeyJkb21haW4iOiJhbGwiLCJyb2xlc1IiOjAsInJvbGVzVyI6MX1dLCJpc3MiOiJBQ0kgQVBJQyIsInVzZXJuYW1lIjoiYWRtaW4iLCJ1c2VyaWQiOjE1Mzc0LCJ1c2VyZmxhZ3MiOjAsImlhdCI6MTYzMTk2OTY2MiwiZXhwIjoxNjMxOTcwMjYyLCJzZXNzaW9uaWQiOiJKWmFmL0d1c1RIdTBiWEF6S1RBaG9BPT0ifQ.SA2rp64Ywr2F25mWEbwi7ADmtSJNp6N4Jb8w21WmREcY2pha_qgbzFr4_bEcV7a12jCUOmvr37RD0BCy5BBpS9jQFWoxDjVawDvhGhzThAgac9xXd8KZ0dwlW3ebNMuamHeldWJ8C2C36EqfdRPL-I27uMDrOkZEc25cBu06MTlsrlk4RerN47z0ALhSlzqCE4zL5j66cBh9JCS3p82YuNSg7sz-6M-JbjA5zGeJBU5Ss3VEv0WGIahp4h6d8__kZlxjSB6CBh2a9IJaz9nO1mAEDw8yYcnTxCo1G_upTCTiVKtvGF4GgLEUieMMoTqDq8IB27G9g7SLJvmW7fmcIw \
  --data '{
  "imdata": [{
    "name": "ExampleRadius"
  },
  {
    "name": "local",
    "guiBanner": "San Jose Fabric"
  }]
}'
```

## get change notifications

see <https://www.cisco.com/c/en/us/td/docs/switches/datacenter/aci/apic/sw/2-x/rest_cfg/2_1_x/b_Cisco_APIC_REST_API_Configuration_Guide/b_Cisco_APIC_REST_API_Configuration_Guide_chapter_01.html#reference_7105100D869A4B0A9160FA2013D46B7B>

Subscribing to Query Results - can be used to be informated about any changes. This needs a websocket connection.

## get a list of fw rules

## get nat rules
