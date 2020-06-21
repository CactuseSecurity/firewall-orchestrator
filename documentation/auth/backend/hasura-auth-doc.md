isodb=# select sign('{ "sub": "1234567890", "name": "Tim Purschke", "checkpointreporter": true, "iat": 1516239022, "hasura": { "claims": { "x-hasura-allowed-roles": ["cpreporter","user"], "x-hasura-default-role": "cpreporter", "x-hasura-user-id": "7", "x-hasura-org-id": "123", "x-hasura-custom": "custom-value" } } }', 'ab957df1a33ea38a821278fb04d92abce830175ce9bcdef0e597622434480ccd', 'HS384');

gives:  eyJhbGciOiJIUzM4NCIsInR5cCI6IkpXVCJ9.eyAic3ViIjogIjEyMzQ1Njc4OTAiLCAibmFtZSI6ICJUaW0gUHVyc2Noa2UiLCAiY2hlY2twb2ludHJlcG9ydGVyIjogdHJ1ZSwgImlhdCI6IDE1MTYyMzkwMjIsICJoYXN1cmEiOiB7ICJjbGFpbXMiOiB7ICJ4LWhhc3VyYS1hbGxvd2VkLXJvbGVzIjogWyJjcHJlcG9ydGVyIiwidXNlciJdLCAieC1oYXN1cmEtZGVmYXVsdC1yb2xlIjogImNwcmVwb3J0ZXIiLCAieC1oYXN1cmEtdXNlci1pZCI6ICI3IiwgIngtaGFzdXJhLW9yZy1pZCI6ICIxMjMiLCAieC1oYXN1cmEtY3VzdG9tIjogImN1c3RvbS12YWx1ZSIgfSB9IH0.w8Hqgx2WI0W6sHcIj7Tb5E5-fgsbLIcP8y07GuPIxXeR5E3h9wiXkef4-1SXNqZQ


running a query with Header
Authorization: Bearer Bearer eyJhbGciOiJIUzM4NCIsInR5cCI6IkpXVCJ9.eyAic3ViIjogIjEyMzQ1Njc4OTAiLCAibmFtZSI6ICJUaW0gUHVyc2Noa2UiLCAiY2hlY2twb2ludHJlcG9ydGVyIjogdHJ1ZSwgImlhdCI6IDE1MTYyMzkwMjIsICJoYXN1cmEiOiB7ICJjbGFpbXMiOiB7ICJ4LWhhc3VyYS1hbGxvd2VkLXJvbGVzIjogWyJjcHJlcG9ydGVyIiwidXNlciJdLCAieC1oYXN1cmEtZGVmYXVsdC1yb2xlIjogImNwcmVwb3J0ZXIiLCAieC1oYXN1cmEtdXNlci1pZCI6ICI3IiwgIngtaGFzdXJhLW9yZy1pZCI6ICIxMjMiLCAieC1oYXN1cmEtY3VzdG9tIjogImN1c3RvbS12YWx1ZSIgfSB9IH0.w8Hqgx2WI0W6sHcIj7Tb5E5-fgsbLIcP8y07GuPIxXeR5E3h9wiXkef4-1SXNqZQ

query {
  rule {
    rule_src
    rule_dst
  }
}

{
  "errors": [
    {
      "extensions": {
        "path": "$.selectionSet.rule",
        "code": "validation-failed"
      },
      "message": "field \"rule\" not found in type: 'query_root'"
    }
  ]
}

but verify looks good: 

{
  "alg": "HS384",
  "typ": "JWT"
}

{
  "sub": "1234567890",
  "name": "Tim Purschke",
  "checkpointreporter": true,
  "iat": 1516239022,
  "hasura": {
    "claims": {
      "x-hasura-allowed-roles": [
        "cpreporter",
        "user"
      ],
      "x-hasura-default-role": "cpreporter",
      "x-hasura-user-id": "7",
      "x-hasura-org-id": "123",
      "x-hasura-custom": "custom-value"
    }
  }
}


docker logs c37388157052

see https://hasura.io/docs/1.0/graphql/manual/auth/authorization/roles-variables.html
TODO: add get_user_visible_devices in auth function to create JWT containing dev_ids 
then use this dev_id list in sign command

isodb=# select sign('{ "sub": "1234567890", "name": "Tim Purschke", "checkpointreporter": true, "iat": 1516239022, "hasura": { "claims": { "x-hasura-allowed-roles": ["cpreporter","user"], "x-hasura-default-role": "cpreporter", "X-Hasura-User-Id": "7", "x-hasura-org-id": "123", "X-Hasura-Visible-Devices": "{2,4}" } } }', 'ab957df1a33ea38a821278fb04d92abce830175ce9bcdef0e597622434480ccd', 'HS384');

custom check: {"dev_id":{"_in":"X-Hasura-Visible-Devices"}}

TODO: currently cpreport is entered as fixed role name in hasura - this has to be variably defined in roles table

# Add Basic roles and permissions in postgresql permissions (grants):
- Anonymous
- Authenticated User
- Documenter
- Admin

# Second Layer of roles then allows granular access based on device permissions
