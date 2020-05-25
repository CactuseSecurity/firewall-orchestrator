select sign('{ "sub": "1234567890", "name": "John Doe", "admin": true, "iat": 1516239022, "hasura": { "claims": { "x-hasura-allowed-roles": ["editor","user", "mod"], "x-hasura-default-role": "user", "x-hasura-user-id": "1234567890", "x-hasura-org-id": "123", "x-hasura-custom": "custom-value" } } }', 'ab957df1a33ea38a821278fb04d92abce830175ce9bcdef0e597622434480ccd', 'HS384');

results in

eyJhbGciOiJIUzM4NCIsInR5cCI6IkpXVCJ9.eyAic3ViIjogIjEyMzQ1Njc4OTAiLCAibmFtZSI6ICJKb2huIERvZSIsICJhZG1pbiI6IHRydWUsICJpYXQiOiAxNTE2MjM5MDIyLCAiaGFzdXJhIjogeyAiY2xhaW1zIjogeyAieC1oYXN1cmEtYWxsb3dlZC1yb2xlcyI6IFsiZWRpdG9yIiwidXNlciIsICJtb2QiXSwgIngtaGFzdXJhLWRlZmF1bHQtcm9sZSI6ICJ1c2VyIiwgIngtaGFzdXJhLXVzZXItaWQiOiAiMTIzNDU2Nzg5MCIsICJ4LWhhc3VyYS1vcmctaWQiOiAiMTIzIiwgIngtaGFzdXJhLWN1c3RvbSI6ICJjdXN0b20tdmFsdWUiIH0gfSB9.kO2RhCfeD7L59K-_aKVVy0b9E6KXbmAXgkUOfAXmagboZTcNZL4L4nNopUx9rfY5

which used as header Authorization / Bearer leads to 
{
  "errors": [
    {
      "extensions": {
        "path": "$.selectionSet.device",
        "code": "validation-failed"
      },
      "message": "field \"device\" not found in type: 'query_root'"
    }
  ]
} 

--> missing rights for table device?