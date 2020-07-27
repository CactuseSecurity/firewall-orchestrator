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
how to devine roles & permissions in hasura.

Starting point: https://hasura.io/docs/1.0/graphql/manual/auth/authorization/index.html

For an example see <https://dev.to/lineup-ninja/modelling-teams-and-user-security-with-hasura-204i>

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
see <https://hasura.io/blog/hasura-authentication-explained/#jwt-auth> and <https://dev.to/lineup-ninja/modelling-teams-and-user-security-with-hasura-204i>

#### Example JWT content
~~~console
"https://hasura.io/jwt/claims": {
    "x-hasura-allowed-roles": [
      "user"
    ],
    "x-hasura-default-role": "user",
    "x-hasura-user": "bdb04fa3-4de3-4434-8d7f-75b10fe2669a",

  },
~~~

#### Example permissions
~~~graphql
{
    "team": {
        "memberships": {
            "_and": [
                {
                    "roles": {
                        "event_write": {
                            "_eq": true
                        }
                    }
                },
                {
                    "user_id": {
                        "_eq": "x-hasura-user"
                    }
                }
            ]
        }
    }
}
~~~

### simple test of authorization

Define permissions on management table using hasura data console for role reporters as follows:

~~~console

SELECT * FROM hdb_catalog.hdb_permission ORDER BY table_schema ASC, table_name ASC, role_name ASC, perm_type ASC ;
table_schema | table_name | role_name | perm_type | perm_def | comment | is_system_defined 

public       | management | reporters | select    | {"filter": {"mgm_id": {"_in": ["visible_managements_for_user(X-Hasura-User-Id)"]}}, "columns": ["client_id", "config_path", "dev_typ_id", "do_not_import", "hide_in_gui", "last_import_md5_complete_config", "mgm_comment", "mgm_create", "mgm_id", "mgm_name", "mgm_update", "ssh_hostname", "ssh_user"], "computed_fields": [], "allow_aggregations": false} |         | f
(1 row)
~~~

Define a dummy function visible_managements_for_user that returns a set containing only mgm_id 1 [1] as follows:
~~~plpgsql
CREATE OR REPLACE FUNCTION public.get_user_visible_devices(integer)
    RETURNS SETOF integer
AS $BODY$
DECLARE
	i_user_id ALIAS FOR $1;
	i_dev_id integer;
	b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices bool_or(role_can_view_all_devices) FROM role_to_user JOIN role USING (role_id) WHERE role_to_user.user_id=i_user_id;
    IF b_can_view_all_devices THEN
        FOR i_dev_id IN SELECT dev_id FROM device
        LOOP
            RETURN NEXT i_dev_id;
        END LOOP;
    ELSE
        FOR i_dev_id IN SELECT device_id FROM role_to_user JOIN role USING (role_id) JOIN role_to_device USING (role_id) WHERE role_to_user.user_id=i_user_id
        LOOP
            RETURN NEXT i_dev_id;
        END LOOP;
    END IF;
	RETURN;
END;

CREATE OR REPLACE FUNCTION public.get_user_visible_managements(integer)
    RETURNS SETOF integer 
AS $BODY$
DECLARE
	i_user_id ALIAS FOR $1;
	i_mgm_id integer;
	b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices bool_or(role_can_view_all_devices) FROM role_to_user JOIN role USING (role_id) WHERE role_to_user.user_id=i_user_id;
    IF b_can_view_all_devices THEN
        FOR i_mgm_id IN SELECT mgm_id FROM management
        LOOP
            RETURN NEXT i_mgm_id;
        END LOOP;
    ELSE
        FOR i_mgm_id IN SELECT mgm_id FROM role_to_user JOIN role USING (role_id) JOIN role_to_device USING (role_id) JOIN device ON (role_to_device.device_id=device.dev_id) WHERE role_to_user.user_id=i_user_id
        LOOP
            RETURN NEXT i_mgm_id;
        END LOOP;
    END IF;
	RETURN;
END;
$BODY$;
~~~

use graphiql to
- define all parameters directly (no auth, no jwt) like so:
  - unset x-hasura-admin-secret
  - set x-hasura-user-id: "tim"
  - set x-hasura-


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
