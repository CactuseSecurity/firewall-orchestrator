# format of the JWT
see <https://tools.ietf.org/html/rfc7519>

## example
    {
      "sub": "https://localhost/api/v1/graphql",
      "name": "Tim Purschke",
      "iat": 1516239022,
      "exp": 1516289022,
      "hasura": {
        "claims": {
          "x-hasura-allowed-roles": ["anonymous","forti","check","reporter","admin"],
          "x-hasura-default-role": "forti",
          "x-hasura-role": "forti",
          "x-hasura-user-id": "fgreporter",
          "x-hasura-org-id": "123",
          "x-hasura-visible-managements": "{1,7,17}",
          "x-hasura-visible-devices": "{1,4}"
        }
      }
    }


## desription

- name: name of the logged in user
- iat: issued at (not valid before) - in seconds since 1.1.1970
- exp: expires at - in seconds since 1.1.1970
- sub: subject or URI of the target (prinipal)
- hasura.claims.x-hasura-user-id: username
- hasura.claims.x-hasura-role: one role of the user
- hasura.claims.x-hasura-default-role: the default role of the user
