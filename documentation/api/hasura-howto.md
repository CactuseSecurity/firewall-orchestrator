# Hasura howto

## concepts and tools

- hasura metadata: Hasura metadata stores information about your tables, relationships, permissions, etc. that is used to generate the GraphQL schema and API: see <https://hasura.io/docs/1.0/graphql/manual/how-it-works/metadata-schema.html>
- graphiql - <https://demo.itsecorg.de/api/console/api-explorer>
- track tables, views, relations --> create queries and expose these
- permissions can be set here: <https://demo.itsecorg.de/api/console/data/schema/public/permissions>

## hasura database

- hasura creates the schemas hdb_catalog and hdb_views in parallel to public (see <https://hasura.io/docs/1.0/graphql/manual/how-it-works/metadata-schema.html>)

  - hdb_catalog.hdb_relationship contains the forein key constraints of the original database
  - hdb_catalog.hdb_permission contains the roles' permissions
  - hdb_catalog.view.hdb_role contains roles

## authentication
how to devine roles & permissions in hasura:
### prerequisites
Hasura permissions are based on roles. Table permissions are defined on a per-role basis, user-specific permissions can be realized using functions with X-Hasura-User-Id as input parameter.

1) create roles (role based access control, RBAC)
     - admin - full admin - able to change tables management, device, stm_...
     
      full access for insert, select, update, delete
     - reporters - able to request reports
     
      no  insert, update, delete for "data tables" like management, rule, object, ...
      
      select access restricted via functions returning visible mgmts or devices
      
    - fw-admin - able to document changes
    
      to be defined later

2) define custom functions (see <https://hasura.io/docs/1.0/graphql/manual/schema/custom-functions.html> for requirements regarding these functions)
~~~console
visible_devices_for_user(user_id) returns setof device-ids
visible_managements_for_user(user_id) returns setof mgmt-ids
~~~
### define permissions using user defined functions:
- add the following code for all exposed tables containing mgm_id:
~~~graphql
   {"mgm_id":{"_in":["visible_managements_for_user(X-Hasura-User-Id)"]}}
~~~
- add the following code for all exposed tables containing dev_id:
~~~graphql
   {"dev_id":{"_in":["visible_devices_for_user(X-Hasura-User-Id)"]}}
~~~

### JWT usage
see <https://hasura.io/blog/hasura-authentication-explained/#jwt-auth>

## How to convert hasura metadata file from json to yaml

### option 1
source: <https://www.commandlinefu.com/commands/view/12221/convert-json-to-yaml>

~~~console
       python -c 'import sys, yaml, json; yaml.safe_dump(json.load(sys.stdin), sys.stdout, default_flow_style=False)' < file.json > file.yaml
~~~

### option 2
source: <https://blog.jasoncallaway.com/2015/10/11/python-one-liner-converting-json-to-yaml/>

```python
#!/usr/bin/env python

import simplejson
import sys
import yaml

print yaml.dump(simplejson.loads(str(sys.stdin.read())), default_flow_style=False)
```
