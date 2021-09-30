# Integrating Cisco ACI

## Acronyms
- Cisco Identity Services Engine (ISE)
- Scalable Group Tag (SGT)
- Scalable Group Access Control Entries (SGACE's)

## intro
Cisco DNA Center uses SSH to establish a trust relationship with ISE. It also uses RestAPI to configure the contracts and policies into ISE. ISE pxGrid information bus is used by Cisco DNA Center to retrieve context information like the SGT's available.


## online sandbox APIC

source: https://devnetsandbox.cisco.com/RM/Diagram/Index/c3c949dc-30af-498b-9d77-4f1c07d835f9?diagramType=Topology

- SDA policy management: https://community.cisco.com/t5/security-documents/policy-provisioning-and-operation-in-sda/ta-p/3712744
- base (UI) URL: https://sandboxdnac.cisco.com/
- doc URL: https://developer.cisco.com/docs/dna-center/api/1-3-3-x/
- API reference: https://developer.cisco.com/docs/dna-center/#!cisco-dna-2-2-2-api-api-sda-get-sda-fabric-info
- doc API: https://developer.cisco.com/docs/dna-center/#!software-defined-access-sda/software-defined-access-api
- API URL: https://sandboxdnac.cisco.com/dna/intent/api/v1/...
- Community: https://community.cisco.com/t5/networking-blogs/welcome-to-the-dna-center-api-support-community/ba-p/3663632
- user: devnetuser
- password: Cisco123!

## user setup

## login

base64 encode the credentials:

    tim@acantha:~$ echo devnetuser:Cisco123! | base64 
    ZGV2bmV0dXNlcjpDaXNjbzEyMyEK
    tim@acantha:~$

For some reason the authentication only works when you replace the final K with padding character "=".

API call:

```console
curl --request POST \
  --url https://sandboxdnac.cisco.com/dna/system/api/v1/auth/token \
  --header 'Authorization: Basic ZGV2bmV0dXNlcjpDaXNjbzEyMyE=' \
  --header 'Content-Type: application/json' \
```
gives

```json
{
  "Token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI2MGVjNGU0ZjRjYTdmOTIyMmM4MmRhNjYiLCJhdXRoU291cmNlIjoiaW50ZXJuYWwiLCJ0ZW5hbnROYW1lIjoiVE5UMCIsInJvbGVzIjpbIjVlOGU4OTZlNGQ0YWRkMDBjYTJiNjQ4ZSJdLCJ0ZW5hbnRJZCI6IjVlOGU4OTZlNGQ0YWRkMDBjYTJiNjQ4NyIsImV4cCI6MTYzMjU2NDU0NSwiaWF0IjoxNjMyNTYwOTQ1LCJqdGkiOiJmMDQyNGIwZi05NTE1LTQxYTctYjVjZC03ODUyNThlZjYzMzYiLCJ1c2VybmFtZSI6ImRldm5ldHVzZXIifQ.ptbCQ4JN6TRyD1ufmI0evKcrNlwthO071YIIISGBOXbxT2VvRTq5x7EPF16gk98N1hYKN80Sx3Q5-XUcwH0TUNoPS7X7kKh7bZmRxWxO59GhKCjtdGgoZmc3sz2jNf1q6CQzsh4gnwQypUUpZq57U6Dwa1RqpY-O7Mtp69akJW7P40_Zz68_vNyS610xkUNiLt4IeVw6ZavblJ2pchyiFJ9tp5rBPMMPpVCl7X63UDSjgWn1echV0Vs3rpq140ZjrWHEsb8DE2LLXG0aWcLcHaXYRkgmueJmbipkAj2KdYE2CsBRPP3naf_-I1JBN6cmFfMkpKH6GWWqT8eYV3BGGA"
}
```

## logout

## get network devices

```console
curl --request GET \
  --url https://sandboxdnac.cisco.com/dna/intent/api/v1/network-device \
  --header 'Content-Type: application/json' \
  --header 'X-Auth-Token: eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI2MGVjNGU0ZjRjYTdmOTIyMmM4MmRhNjYiLCJhdXRoU291cmNlIjoiaW50ZXJuYWwiLCJ0ZW5hbnROYW1lIjoiVE5UMCIsInJvbGVzIjpbIjVlOGU4OTZlNGQ0YWRkMDBjYTJiNjQ4ZSJdLCJ0ZW5hbnRJZCI6IjVlOGU4OTZlNGQ0YWRkMDBjYTJiNjQ4NyIsImV4cCI6MTYzMjU2NDU0NSwiaWF0IjoxNjMyNTYwOTQ1LCJqdGkiOiJmMDQyNGIwZi05NTE1LTQxYTctYjVjZC03ODUyNThlZjYzMzYiLCJ1c2VybmFtZSI6ImRldm5ldHVzZXIifQ.ptbCQ4JN6TRyD1ufmI0evKcrNlwthO071YIIISGBOXbxT2VvRTq5x7EPF16gk98N1hYKN80Sx3Q5-XUcwH0TUNoPS7X7kKh7bZmRxWxO59GhKCjtdGgoZmc3sz2jNf1q6CQzsh4gnwQypUUpZq57U6Dwa1RqpY-O7Mtp69akJW7P40_Zz68_vNyS610xkUNiLt4IeVw6ZavblJ2pchyiFJ9tp5rBPMMPpVCl7X63UDSjgWn1echV0Vs3rpq140ZjrWHEsb8DE2LLXG0aWcLcHaXYRkgmueJmbipkAj2KdYE2CsBRPP3naf_-I1JBN6cmFfMkpKH6GWWqT8eYV3BGGA'
```

## get sda information

unfortunately the sandbox does not seam to conain any sda config:



## get a list of fw rules

## get nat rules


