# Integrating Cisco ACI

## online sandbox APIC

source: https://devnetsandbox.cisco.com/RM/Diagram/Index/c3c949dc-30af-498b-9d77-4f1c07d835f9?diagramType=Topology

- base (UI) URL: https://sandboxdnac.cisco.com/
- doc URL: https://developer.cisco.com/docs/dna-center/api/1-3-3-x/
- API URL: https://sandboxdnac.cisco.com/dna/intent/api/v1/...
- user: devnetuser
- password: Cisco123!

## user setup

## login

base64 encode the credentials:

    tim@acantha:~$ echo devnetuser:Cisco123! | base64 
    ZGV2bmV0dXNlcjpDaXNjbzEyMyEK
    tim@acantha:~$

API call:

```console
curl --request POST \
  --url https://sandboxdnac.cisco.com/dna/system/api/v1/auth/token \
  --header 'Authorization: ZGV2bmV0dXNlcjpDaXNjbzEyMyEK' \
  --header 'Content-Type: application/json' \
  --data '{ "Token": null }'
```
gives

    internal server error 500

```json

```

## logout

## get config

```console
```

## get change notifications

## get a list of fw rules

## get nat rules


