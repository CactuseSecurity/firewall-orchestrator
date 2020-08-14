# format of the JWT

    {
      "sub": "1234567890",
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
