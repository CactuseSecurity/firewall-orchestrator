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

- <https://hasura.io/blog/hasura-authentication-explained/#jwt-auth>
- how to devine roles & permissions in hasura:

## How to convert hasura metadata file from json to yaml

### option 1
source: <https://www.commandlinefu.com/commands/view/12221/convert-json-to-yaml>

       python -c 'import sys, yaml, json; yaml.safe_dump(json.load(sys.stdin), sys.stdout, default_flow_style=False)' < file.json > file.yaml


### option 2
source: <https://blog.jasoncallaway.com/2015/10/11/python-one-liner-converting-json-to-yaml/>

```
#!/usr/bin/env python

import simplejson
import sys
import yaml

print yaml.dump(simplejson.loads(str(sys.stdin.read())), default_flow_style=False)
```
