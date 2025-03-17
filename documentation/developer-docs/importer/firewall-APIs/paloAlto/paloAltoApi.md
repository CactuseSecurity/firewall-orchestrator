# Integrating Palo Alto firewall

All examples here are given for PanOS 9.0. PAN-OS has two APIs: XML and REST. There is also an extra REST API for the central management server Panorama. The XML-API can be used to get the whole config in one go.

## Create api user
see <https://docs.paloaltonetworks.com/pan-os/9-0/pan-os-panorama-api/get-started-with-the-pan-os-xml-api/enable-api-access.html>

## login
```console
curl --insecure --request GET --url 'https://PAN-IP/api/?type=keygen&user=fwo&password=xxx'
```
gets us a session key in XML format which seems to be valid indefinetly?!:
```xml
<response status = 'success'>
  <result>
    <key>LUFRPT1Tb2xDZnk0R25WbDJONWJNMmlEMHNpS0Y2d1U9T3ZLZFhydER6SDZKYk9OQit2cmVTVUtEb2MyMVBDUkdBOGY3UzlDS0VrTT0=</key>
  </result>
</response>
```

More secure:
see <https://docs.paloaltonetworks.com/pan-os/9-0/pan-os-panorama-api/about-the-pan-os-xml-api/structure-of-a-pan-os-xml-api-request/api-authentication-and-security.html>, but note: You cannot use basic authentication when you Get Your API Key.


## get API version

`curl -X GET "https://<firewall>/api/?type=version&key=<apikey>"' 

## Get all network objects
The session key can be used to get objects as follows (for single fw, the name of the vsys seems to be vsys1):
```console
curl --insecure --request GET \
  --url 'https://PAN-IP/restapi/v9.1/Objects/Addresses?location=vsys&vsys=vsys1' \
  --header 'X-PAN-KEY: LUFRPT1JdHF6SnVndXNEU2VxVFIvNnZ1bG1yeFk0S2c9clVWeGhkdnNQNTBRK1BzNXBCeEMvNzdTSks1NWVDdzJLSmZXa1JsUkYzdW9OUnJSb1pDREdseitlVUtNc1VKSw==' 
```
Gives us the network objects in JSON format:

```json
{
  "@status": "success",
  "@code": "19",
  "result": {
    "@total-count": "45",
    "@count": "45",
    "entry": [
      {
        "@name": "ext-interface-ip-10.9.8.2",
        "@location": "vsys",
        "@vsys": "vsys1",
        "ip-netmask": "10.9.8.2"
      },
      {
        "@name": "cactus-da",
        "@location": "vsys",
        "@vsys": "vsys1",
        "ip-netmask": "85.182.155.96\/27",
        "tag": {
          "member": [
            "cactus-DA"
          ]
        }
      },
      {
        "@name": "fb_inet_10.9.8.1",
        "@location": "vsys",
        "@vsys": "vsys1",
        "ip-netmask": "10.9.8.1"
      },
      {
        "@name": "gware.cactus.de_85.182.155.108",
        "@location": "vsys",
        "@vsys": "vsys1",
        "ip-netmask": "85.182.155.108\/32",
        "tag": {
          "member": [
            "cactus-DA"
          ]
        }
      }
    ]
  }
}
```
To get address groups:

```console
curl --request GET \
  --url 'https://10.8.6.3/restapi/v9.1/Objects/AddressGroups?location=vsys&vsys=vsys1' \
  --header 'X-PAN-KEY: LUFRPT1zUmdXTlZjUFZPaWxmc0R2eHRPa1FvdmtlV009T3ZLZFhydER6SDZKYk9OQit2cmVTZHNYWDJrdHREWDVyN1VnZG01VXNKWT0=' \
```

Retrieves tag-based filters:
```json
{
  "@status": "success",
  "@code": "19",
  "result": {
    "@total-count": "3",
    "@count": "3",
    "entry": [
      {
        "@name": "GRP_tims-ip-adressen",
        "@location": "vsys",
        "@vsys": "vsys1",
        "dynamic": {
          "filter": "'tims-clients'"
        }
      },
      {
        "@name": "GRP_guest-ips",
        "@location": "vsys",
        "@vsys": "vsys1",
        "dynamic": {
          "filter": "'guests' "
        }
      },
      {
        "@name": "GRP_kids_ips",
        "@location": "vsys",
        "@vsys": "vsys1",
        "dynamic": {
          "filter": "'kids-ips'"
        }
      }
    ]
  }
}
```
## get service objects

first predefined services:

```console
curl --request GET \
  --url 'https://10.8.6.3/restapi/v9.1/Objects/Services?location=predefined' \
  --header 'X-PAN-KEY: LUFRPT1JdHF6SnVndXNEU2VxVFIvNnZ1bG1yeFk0S2c9clVWeGhkdnNQNTBRK1BzNXBCeEMvNzdTSks1NWVDdzJLSmZXa1JsUkYzdW9OUnJSb1pDREdseitlVUtNc1VKSw==' \
```
  
yields:

```json
{
  "@status": "success",
  "@code": "19",
  "result": {
    "@total-count": "2",
    "@count": "2",
    "entry": [
      {
        "@name": "service-http",
        "@location": "predefined",
        "protocol": {
          "tcp": {
            "port": "80,8080"
          }
        }
      },
      {
        "@name": "service-https",
        "@location": "predefined",
        "protocol": {
          "tcp": {
            "port": "443"
          }
        }
      }
    ]
  }
}
```

Then self-defined:

```console
curl --insecure --request GET \
  --url 'https://PAN-IP/restapi/v9.1/Objects/Services?location=vsys&vsys=vsys1' \
  --header 'X-PAN-KEY: LUFRPT1JdHF6SnVndXNEU2VxVFIvNnZ1bG1yeFk0S2c9clVWeGhkdnNQNTBRK1BzNXBCeEMvNzdTSks1NWVDdzJLSmZXa1JsUkYzdW9OUnJSb1pDREdseitlVUtNc1VKSw=='
```

give us:

```json
{
  "@status": "success",
  "@code": "19",
  "result": {
    "@total-count": "42",
    "@count": "42",
    "entry": [
      {
        "@name": "tcp_64285",
        "@location": "vsys",
        "@vsys": "vsys1",
        "protocol": {
          "tcp": {
            "port": "64285",
            "override": {
              "no": []
            }
          }
        }
      },
      {
        "@name": "steam_27000-27100",
        "@location": "vsys",
        "@vsys": "vsys1",
        "protocol": {
          "udp": {
            "port": "27000-27100",
            "override": {
              "no": []
            }
          }
        }
      },
      {
        "@name": "svc_3000_tcp_hbci",
        "@location": "vsys",
        "@vsys": "vsys1",
        "protocol": {
          "tcp": {
            "port": "3000",
            "override": {
              "no": []
            }
          }
        }
      },
      {
        "@name": "fritzbox_tcp_14013",
        "@location": "vsys",
        "@vsys": "vsys1",
        "protocol": {
          "tcp": {
            "port": "14013",
            "override": {
              "no": []
            }
          }
        }
      }
    ]
  }
}
```


## get (predefined) applications

in order to get the application names we need API v9.1!

with version 9.0:
```console
curl --insecure --request GET \
  --url 'https://10.8.6.3/restapi/9.0/Objects/Applications?location=predefined' \
  --header 'X-PAN-KEY: LUFRPT1zUmdXTlZjUFZPaWxmc0R2eHRPa1FvdmtlV009T3ZLZFhydER6SDZKYk9OQit2cmVTZHNYWDJrdHREWDVyN1VnZG01VXNKWT0=' \
```
```json
{
  "@status": "success",
  "@code": "19",
  "result": {
    "@total-count": "3566",
    "@count": "3566",
    "entry": [
      {
        "default": {
          "port": {
            "member": [
              "tcp\/3468,6346,11300"
            ]
          }
        },
        "category": "general-internet",
        "subcategory": "file-sharing",
        "technology": "peer-to-peer",
        "risk": "5",
        "evasive-behavior": "yes",
        "consume-big-bandwidth": "yes",
        "used-by-malware": "yes",
        "able-to-transfer-file": "yes",
        "has-known-vulnerability": "yes",
        "tunnel-other-application": "no",
        "prone-to-misuse": "yes",
        "pervasive-use": "yes"
      }
    ]
  }
}
```
With v9.1:

```console
curl --request GET \
  --url 'https://10.8.6.3/restapi/v9.1/Objects/Applications?location=predefined' \
  --header 'X-PAN-KEY: LUFRPT1zUmdXTlZjUFZPaWxmc0R2eHRPa1FvdmtlV009T3ZLZFhydER6SDZKYk9OQit2cmVTZHNYWDJrdHREWDVyN1VnZG01VXNKWT0='
```

```json
{
  "@status": "success",
  "@code": "19",
  "result": {
    "@total-count": "3566",
    "@count": "3566",
    "entry": [
      {
        "@name": "100bao",
        "@location": "predefined",
        "default": {
          "port": {
            "member": [
              "tcp\/3468,6346,11300"
            ]
          }
        },
        "category": "general-internet",
        "subcategory": "file-sharing",
        "technology": "peer-to-peer",
        "risk": "5",
        "evasive-behavior": "yes",
        "consume-big-bandwidth": "yes",
        "used-by-malware": "yes",
        "able-to-transfer-file": "yes",
        "has-known-vulnerability": "yes",
        "tunnel-other-application": "no",
        "prone-to-misuse": "yes",
        "pervasive-use": "yes"
      },
      {
        "@name": "open-vpn",
        "@location": "predefined",
        "default": {
          "port": {
            "member": [
              "tcp\/1194",
              "tcp\/443",
              "udp\/1194"
            ]
          }
        },
        "category": "networking",
        "subcategory": "encrypted-tunnel",
        "technology": "client-server",
        "timeout": "3600",
        "risk": "3",
        "evasive-behavior": "no",
        "consume-big-bandwidth": "no",
        "used-by-malware": "no",
        "able-to-transfer-file": "yes",
        "has-known-vulnerability": "yes",
        "tunnel-other-application": "yes",
        "tunnel-applications": {
          "member": [
            "cyberghost-vpn",
            "frozenway",
            "hotspot-shield",
            "ipvanish",
            "spotflux"
          ]
        },
        "prone-to-misuse": "no",
        "pervasive-use": "yes"
      }
    ]
  }
}      
```

## get rules

```console
curl --insecure --request GET \
  --url 'https://PAN-IP/restapi/v9.1/Policies/SecurityRules?location=vsys&vsys=vsys1' \
  --header 'X-PAN-KEY: LUFRPT1JdHF6SnVndXNEU2VxVFIvNnZ1bG1yeFk0S2c9clVWeGhkdnNQNTBRK1BzNXBCeEMvNzdTSks1NWVDdzJLSmZXa1JsUkYzdW9OUnJSb1pDREdseitlVUtNc1VKSw==' \
```

gives us:

```json
{
  "@status": "success",
  "@code": "19",
  "result": {
    "@total-count": "85",
    "@count": "85",
    "entry": [
      {
        "@name": "special access tim-1",
        "@uuid": "ca58af60-05c3-4806-b7c6-aea7a1ddc70c",
        "@location": "vsys",
        "@vsys": "vsys1",
        "to": {
          "member": [
            "untrust"
          ]
        },
        "from": {
          "member": [
            "trust"
          ]
        },
        "source": {
          "member": [
            "GRP_tims-ip-adressen"
          ]
        },
        "destination": {
          "member": [
            "any"
          ]
        },
        "source-user": {
          "member": [
            "any"
          ]
        },
        "application": {
          "member": [
            "open-vpn",
            "ssh",
            "ssh-tunnel",
            "ssl",
            "web-browsing"
          ]
        },
        "service": {
          "member": [
            "any"
          ]
        },
        "hip-profiles": {
          "member": [
            "any"
          ]
        },
        "action": "allow",
        "rule-type": "interzone",
        "profile-setting": {
          "profiles": {
            "url-filtering": {
              "member": [
                "default"
              ]
            },
            "file-blocking": {
              "member": [
                "strict file blocking"
              ]
            },
            "virus": {
              "member": [
                "test-av-profile"
              ]
            },
            "spyware": {
              "member": [
                "strict"
              ]
            },
            "vulnerability": {
              "member": [
                "default"
              ]
            },
            "wildfire-analysis": {
              "member": [
                "default"
              ]
            }
          }
        },
        "tag": {
          "member": [
            "tims-clients"
          ]
        },
        "log-start": "yes",
        "category": {
          "member": [
            "any"
          ]
        },
        "disabled": "no",
        "log-setting": "forwarder-traffic-log",
        "group-tag": "tims-clients"
      },
      {
        "@name": "DMZ minecraft",
        "@uuid": "ac91834b-0ac3-4d9a-abcd-3ad69075bed7",
        "@location": "vsys",
        "@vsys": "vsys1",
        "to": {
          "member": [
            "any"
          ]
        },
        "from": {
          "member": [
            "untrust"
          ]
        },
        "source": {
          "member": [
            "any"
          ]
        },
        "destination": {
          "member": [
            "ext-interface-ip-10.9.8.2"
          ]
        },
        "source-user": {
          "member": [
            "any"
          ]
        },
        "category": {
          "member": [
            "any"
          ]
        },
        "application": {
          "member": [
            "any"
          ]
        },
        "service": {
          "member": [
            "tcp_60999"
          ]
        },
        "hip-profiles": {
          "member": [
            "any"
          ]
        },
        "action": "allow",
        "log-start": "yes",
        "rule-type": "universal",
        "log-setting": "forwarder-traffic-log",
        "profile-setting": {
          "profiles": {
            "vulnerability": {
              "member": [
                "strict"
              ]
            }
          }
        },
        "disabled": "no"
      }
    ]
  }
}
```