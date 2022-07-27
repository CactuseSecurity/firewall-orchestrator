# How to add REST endpoints to hasura

## Create query in graphiql

- Click on REST button in the middle of the window to get to "Create Endpoint" menu
- enter name, location and method
- endpoint will be created in path /api/api/rest/(location)
- click "create endpoint" at the bottom

## test / edit endpoint

- In hasura API browser choose second tab "REST"
- click on endpoint (left hand side) or edit (right hand side)

## Exporting

Export metadata as usual via settings.

## sample calls for testing


### Getting data with admin rights:

  curl --insecure --request GET --url https://localhost:24449/api/api/rest/getManagements --header 'content-type: application/json' --header 'x-hasura-admin-secret: not4production'

  {"management":[{"mgm_id":15,"mgm_name":"sting"}, {"mgm_id":2,"mgm_name":"checkpoint_demo"}, {"mgm_id":1,"mgm_name":"fortigate_demo"}]}


### Getting data with user rights:

  curl --insecure --request GET --url https://localhost:24449/api/api/rest/getDevices --header 'content-type: application/json' --header 'Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6InRpbSIsIngtaGFzdXJhLXVzZXItaWQiOiI5IiwieC1oYXN1cmEtdXVpZCI6InVpZD10aW0sb3U9dGVuYW50MCxvdT1vcGVyYXRvcixvdT11c2VyLGRjPWZ3b3JjaCxkYz1pbnRlcm5hbCIsIngtaGFzdXJhLXRlbmFudC1pZCI6IjEiLCJ4LWhhc3VyYS12aXNpYmxlLW1hbmFnZW1lbnRzIjoieyAyLDEsMTUgfSIsIngtaGFzdXJhLXZpc2libGUtZGV2aWNlcyI6InsgMSwyLDE1IH0iLCJyb2xlIjoicmVwb3J0ZXIiLCJ4LWhhc3VyYS1hbGxvd2VkLXJvbGVzIjpbInJlcG9ydGVyIl0sIngtaGFzdXJhLWRlZmF1bHQtcm9sZSI6InJlcG9ydGVyIiwibmJmIjoxNjU4OTE5NjE2LCJleHAiOjE2NTg5MzQwNzYsImlhdCI6MTY1ODkxOTYxNiwiaXNzIjoiRldPIE1pZGRsZXdhcmUgTW9kdWxlIiwiYXVkIjoiRldPIn0.xaNyuOMzgIEhZPhPfMcJrjBHOtMZtXNJZVG04OWbrR4DdRozdh5OC9oMxRDOb7oC8NXJUF4gqog-4YQ1BXSyLNW4-oolcuKLBoJGmXil-xlYRFG5YBB4sIsrwXKRGrtyN-6l-VdreuKHb1grmsfdnjpUjsVg0JqqRtHJIi29S0Pl1wEgkvlF9rarGmr5jVuv7rYJmqFQ5cUUO88NtYKT1bjQlc3XcXzT3l3ywxi1iC9z7hHkAE_AmtDU1YgcKEDxCPz0fJc4Gkuy_DYvau6PtAfBnnKDiwpqyu3UbSt4xIHy8Z5csxMD9RCAgAVY7akyxjhZv2CFjafCdsvEGJASlA'

  {"device":[{"dev_id":1,"dev_name":"fortigate_demo"}, {"dev_id":2,"dev_name":"checkpoint_demo"}, {"dev_id":15,"dev_name":"sting-test"}]}

### Adding data:

```graphql
mutation addTenant($tenantName:String!) {
  insert_tenant(objects: {tenant_name: $tenantName}) {affected_rows}
}
```

  curl --insecure --request POST --url https://localhost:24449/api/api/rest/tenant --header 'content-type: application/json' --header 'x-hasura-admin-secret: not4production' --data '{"tenantName": "abc" }'

  {"insert_tenant":{"affected_rows" : 1}}
