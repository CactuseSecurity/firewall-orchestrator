# OpenStack API

Start here: <https://docs.openstack.org/devstack/latest/>

## online sandbox 

https://www.openstack.org/passport/


## openstack test installation

on Ubuntu 20.04 (4 GB RAM is not enough, 8 GB seems ok, better 10 GB).

Make sure  simplejson ist not installed:

    sudo apt purge python3-simplejson

Prepare system:

    sudo useradd -s /bin/bash -d /opt/stack -m stack
    echo "stack ALL=(ALL) NOPASSWD: ALL" | sudo tee /etc/sudoers.d/stack
    sudo -u stack -i
    git clone https://opendev.org/openstack/devstack
    cd devstack

create local.conf file with the following content (choosing a suitable password for XXX):

    [[local|localrc]]
    ADMIN_PASSWORD=XXX
    DATABASE_PASSWORD=$ADMIN_PASSWORD
    RABBIT_PASSWORD=$ADMIN_PASSWORD
    SERVICE_PASSWORD=$ADMIN_PASSWORD


To avoid error

    Found existing installation: simplejson 3.16.0
    ERROR: Cannot uninstall 'simplejson'. It is a distutils installed project and thus we cannot accurately determine which files belong to it which would lead to only a partial uninstall.

in inc/python CHANGE FROM
    
    $cmd_pip $upgrade \

TO

    $cmd_pip $upgrade --ignore-installed \

Then run the installation

    ./stack.sh


## accessing the API

see <https://docs.openstack.org/keystone/queens/api_curl_examples.html>


### user setup

### login
```console
curl -i \
  -H "Content-Type: application/json" \
  -d '
{ "auth": {
    "identity": {
      "methods": ["password"],
      "password": {
        "user": {
          "name": "admin",
          "domain": { "id": "default" },
          "password": "adminpwd"
        }
      }
    }
  }
}' \
  "http://localhost:5000/v3/auth/tokens" ;
```
gives
```json
{
  "totalCount": "1",
  "imdata": [
    {
      "aaaLogin": {
        "attributes": {
          "token": "xxx",
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

## get various config parts (classes)

All of the below classes "CLASS" can be retrieved as follows:
```console
curl --request GET \
  --url https://sandboxapicdc.cisco.com/api/class/CLASS.json \
  --header 'Content-Type: application/json' \
  --cookie APIC-cookie=xxx \
```

### Basic config
- fvBD - bridge domains
- aaaDomain - Domains
- fvEPg - End Point Groups (EPGs)
- fvAEPg - Application endpoint groups
- aaaUser - users
- fvTenant - tenants
-  application profiles
- vzGraphCont, vzGraphDef - service graphs
-  outside connections - L3Out external EPG (l3extInstP)
-  End Point Security Groups (ESGs)

### Contract information
- vzFilter - filters?
- vzACtrct - contracts
- vzACtrctEpgDef - contracts with EPGs?
- vzBrCP - Binary Contract Profile
- vzABrCP - abstraction of Binary Contract Profile
- vzACollection - ???
- vzConsDef - Consumer EPg
- vzProvDef - Provider EPg
- vzCtrctEPgCont - Contract EPG Container
- vzCtrctEntityDef - Summary of EPg: Contains All Info to Create ProvDef/ConsDef
- vzRsDenyRule - deny rule (cleanup)
- vzRuleOwner gives an exhaustive list of what looks like fw rules
```json
{
  "totalCount": "55",
  "imdata": [
    {
      "vzRuleOwner": {
        "attributes": {
          "action": "permit",
          "childAction": "",
          "creatorDn": "topology/pod-1/node-101/sys/ctx-[vxlan-16777200]",
          "ctrctName": "",
          "direction": "uni-dir",
          "dn": "topology/pod-1/node-101/sys/actrl/scope-16777200/rule-16777200-s-any-d-any-f-implarp/own-[topology/pod-1/node-101/sys/ctx-[vxlan-16777200]]-tag-vrf",
          "intent": "install",
          "lcOwn": "local",
          "markDscp": "unspecified",
          "modTs": "2021-09-20T14:07:18.567+00:00",
          "monitorDn": "uni/tn-common/monepg-default",
          "name": "",
          "nameAlias": "",
          "prio": "any_any_filter",
          "qosGrp": "unspecified",
          "status": "",
          "tag": "vrf",
          "type": "tenant"
        }
      }
    },
```
- vzToEPg 
```json
{
  "totalCount": "12",
  "imdata": [
    {
      "vzToEPg": {
        "attributes": {
          "bdDefDn": "uni/bd-[uni/tn-Tenant_ISK/BD-BD_ISK]-isSvc-no",
          "bdDefStQual": "none",
          "bgpDomainId": "10671",
          "childAction": "",
          "ctxDefDn": "uni/ctx-[uni/tn-Tenant_ISK/ctx-VRF_ISK]",
          "ctxDefStQual": "none",
          "ctxPcTag": "49153",
          "ctxSeg": "2883584",
          "descr": "",
          "dn": "cdef-[uni/tn-common/brc-app_to_web]/epgCont-[uni/tn-Tenant_ISK/ap-AP_ISK/epg-web]/fr-[uni/tn-common/brc-app_to_web/dirass/prov-[uni/tn-Tenant_ISK/ap-AP_ISK/epg-web]-any-no]/to-[uni/tn-common/brc-app_to_web/dirass/cons-[uni/tn-Tenant_ISK/ap-AP_ISK/epg-app]-any-no]",
          "epgDefDn": "uni/tn-common/brc-app_to_web/dirass/cons-[uni/tn-Tenant_ISK/ap-AP_ISK/epg-app]-any-no",
          "epgDn": "uni/tn-Tenant_ISK/ap-AP_ISK/epg-app",
          "intent": "install",
          "isCtxPcTagInUseForRules": "no",
          "isGraph": "no",
          "l3CtxEncap": "unknown",
          "lcOwn": "local",
          "modTs": "2021-09-20T14:21:02.498+00:00",
          "monPolDn": "",
          "name": "",
          "nameAlias": "",
          "ownerKey": "",
          "ownerTag": "",
          "pcTag": "16387",
          "prefGrMemb": "exclude",
          "scopeId": "2883584",
          "servicePcTag": "any",
          "status": "",
          "txId": "17870283321406128551"
        }
      }
    },
```

## get change notifications

- vzReeval?

see <https://www.cisco.com/c/en/us/td/docs/switches/datacenter/aci/apic/sw/2-x/rest_cfg/2_1_x/b_Cisco_APIC_REST_API_Configuration_Guide/b_Cisco_APIC_REST_API_Configuration_Guide_chapter_01.html#reference_7105100D869A4B0A9160FA2013D46B7B>

Subscribing to Query Results - can be used to be informated about any changes. This needs a websocket connection.

## get a list of fw rules

## get nat rules


## Example ansible task to add an EPG

```ansible
  - name: Add static EPG
    aci_rest:
      host: "{{host}}"
      username: "{{username}}"
      password: "{{password}}"
      method: post
      path: /api/node/mo/uni/tn-{{item.tenant}}/ap-{{item.approfile}}/epg-{{item.epgname}}.json
      content: "{{ lookup('template', 'templates/epg1.j2') }}"
      validate_certs: no
    with_items: '{{ ports }}'
    ignore_errors: yes
    loop_control:
      pause: 1
```