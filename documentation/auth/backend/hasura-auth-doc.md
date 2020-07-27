# Hasura Auth
- how to define roles & permissions in hasura.
- Starting point: https://hasura.io/docs/1.0/graphql/manual/auth/authorization/index.html
- For an example see <https://dev.to/lineup-ninja/modelling-teams-and-user-security-with-hasura-204i>
- see [hasura doc on auth](https://hasura.io/docs/1.0/graphql/manual/auth/authorization/roles-variables.html)

## Todos
- define role based access model:
  - full admin (able to change tables management, device, stm_...)
  - fw admin (able to document changes)
  - reporter (able to request reports)
- add get_user_visible_devices in auth function to create JWT containing dev_ids then use this dev_id list in sign command
- define permissions for all roles
- find an elegant way to define permissions (only via web UI?)
- defining permissions only works for tables containing dev or mgm
- create auth site (using .NET)

## JWT
### sign JWT

```console
isodb=# select sign('{ "sub": "1234567890", "name": "Tim Purschke", "checkpointreporter": true, "iat": 1516239022, "hasura": { "claims": { "x-hasura-allowed-roles": ["cpreporter","user"], "x-hasura-default-role": "cpreporter", "x-hasura-user-id": "7", "x-hasura-org-id": "123", "x-hasura-custom": "custom-value" } } }', 'ab957df1a33ea38a821278fb04d92abce830175ce9bcdef0e597622434480ccd', 'HS384');
```

gives: eyJhbGciOiJIUzM4NCIsInR5cCI6IkpXVCJ9.eyAic3ViIjogIjEyMzQ1Njc4OTAiLCAibmFtZSI6ICJUaW0gUHVyc2Noa2UiLCAiY2hlY2twb2ludHJlcG9ydGVyIjogdHJ1ZSwgImlhdCI6IDE1MTYyMzkwMjIsICJoYXN1cmEiOiB7ICJjbGFpbXMiOiB7ICJ4LWhhc3VyYS1hbGxvd2VkLXJvbGVzIjogWyJjcHJlcG9ydGVyIiwidXNlciJdLCAieC1oYXN1cmEtZGVmYXVsdC1yb2xlIjogImNwcmVwb3J0ZXIiLCAieC1oYXN1cmEtdXNlci1pZCI6ICI3IiwgIngtaGFzdXJhLW9yZy1pZCI6ICIxMjMiLCAieC1oYXN1cmEtY3VzdG9tIjogImN1c3RvbS12YWx1ZSIgfSB9IH0.w8Hqgx2WI0W6sHcIj7Tb5E5-fgsbLIcP8y07GuPIxXeR5E3h9wiXkef4-1SXNqZQ

### running a query with Header

```console
Authorization: Bearer <JWT>
```

## Add Basic roles and permissions in postgresql permissions (grants)
- Anonymous
- Authenticated User
- Documenter
- Admin

Second Layer of roles then allows granular access based on device permissions

## prerequisites
Hasura permissions are based on roles. Table permissions are defined on a per-role basis, user-specific permissions can be realized using functions with X-Hasura-User-Id as input parameter.

1) create roles (role based access control, RBAC)
     - admin - full admin - able to change tables management, device, stm_...
     
      full access for insert, select, update, delete
     - reporters - able to request reports
     
      no  insert, update, delete for "data tables" like management, rule, object, ...
      
      select access restricted via functions returning visible mgmts or devices
      
    - fw-admin - able to document changes
    
      to be defined later

2) use stored procedures to create set of ids
~~~console
  - FUNCTION public.get_user_visible_devices(integer) RETURNS SETOF integer
  - FUNCTION public.get_user_visible_managements(integer) RETURNS SETOF integer 
~~~

3) add the result of the functions to the jwt!
see <https://hasura.io/blog/hasura-authentication-explained/#jwt-auth> and <https://dev.to/lineup-ninja/modelling-teams-and-user-security-with-hasura-204i>

Use existing functions to create jwt:
  - FUNCTION public.get_user_visible_devices(integer) RETURNS SETOF integer
  - FUNCTION public.get_user_visible_managements(integer) RETURNS SETOF integer 

~~~console
  select sign('{
  "sub": "1234567890",
  "name": "Tim Purschke",
  "iat": 1516239022,
  "hasura": {
    "claims": {
      "x-hasura-allowed-roles": ["anonymous","cpreporter","reporter","admin"],
      "x-hasura-default-role": "reporter",
      "x-hasura-role": "reporter",
      "x-hasura-user-id": "tim",
      "x-hasura-org-id": "123",
      "x-hasura-visible-managements": "{1,7,17}",
	    "x-hasura-visible-devices": "{1,4}"
    }
  }
}
', '<secret>');

-- result: eyJhbGciOiJIUzM4NCIsInR5cCI6IkpXVCJ9.ewogICJzdWIiOiAiMTIzNDU2Nzg5MCIsCiAgIm5hbWUiOiAiVGltIFB1cnNjaGtlIiwKICAiaWF0IjogMTUxNjIzOTAyMiwKICAiaGFzdXJhIjogewogICAgImNsYWltcyI6IHsKICAgICAgIngtaGFzdXJhLWFsbG93ZWQtcm9sZXMiOiBbImFub255bW91cyIsImNwcmVwb3J0ZXIiLCJyZXBvcnRlciIsImFkbWluIl0sCiAgICAgICJ4LWhhc3VyYS1kZWZhdWx0LXJvbGUiOiAicmVwb3J0ZXIiLAogICAgICAieC1oYXN1cmEtcm9sZSI6ICJyZXBvcnRlciIsCiAgICAgICJ4LWhhc3VyYS11c2VyLWlkIjogInRpbSIsCiAgICAgICJ4LWhhc3VyYS1vcmctaWQiOiAiMTIzIgogICAgfQogIH0KfQo.KYpM7Tcg4DJT1G8ps1rTfCdfL3nJWp5tBzz5lMayAO2x1Sl5X2PkdmzhdPr7sxzM

~~~

4) set permissions for tables in data
custom row check for tables with management: 
~~~json
{"mgm_id":{"_in":"X-Hasura-Visible-Managements"}}
~~~
custom row  check for tables with management: 
~~~json
{"mgm_id":{"_in":"X-Hasura-Visible-Managements"}}
~~~

## simple test of authorization

Define permissions on management table using hasura data console for role reporters as follows:

~~~console
SELECT * FROM hdb_catalog.hdb_permission ORDER BY table_schema ASC, table_name ASC, role_name ASC, perm_type ASC ;
table_schema | table_name | role_name | perm_type | perm_def | comment | is_system_defined 

public       | management | reporters | select    | {
    "filter": {
        "mgm_id": {
            "_in": "X-Hasura-visible-managements"
        }
    },
    "columns": [
        "mgm_id",
        ...
        "importer_hostname"
    ],
    "computed_fields": [],
    "allow_aggregations": false
} |         | f
(1 row)
~~~

- use pgjwt to create jwt as follows
  - get secret from /usr/share/itsecorg/api/jwt.secret
  - create JWT as above

use graphiql to
- define all parameters directly (no auth, no jwt) like so:
  - unset x-hasura-admin-secret
  - set Authorization to "Bearer <JWT>"
