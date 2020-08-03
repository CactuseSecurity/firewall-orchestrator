# Hasura howto

## concepts and tools

- hasura metadata: Hasura metadata stores information about your tables, relationships, permissions, etc. that is used to generate the GraphQL schema and API: see <https://hasura.io/docs/1.0/graphql/manual/how-it-works/metadata-schema.html>
- graphiql - <https://demo.itsecorg.de/api/console/api-explorer>
- track tables, views, relations --> create queries and expose these
- permissions can be set here: <https://demo.itsecorg.de/api/console/data/schema/public/permissions>
- more elaborate example: <https://hasura.io/docs/1.0/graphql/manual/auth/authorization/permission-rules.html#using-column-operators-to-build-rules>


## hasura database

- hasura creates the schemas hdb_catalog and hdb_views in parallel to public (see <https://hasura.io/docs/1.0/graphql/manual/how-it-works/metadata-schema.html>)

  - hdb_catalog.hdb_relationship contains the forein key constraints of the original database
  - hdb_catalog.hdb_permission contains the roles' permissions
  - hdb_catalog.view.hdb_role contains roles

## debugging hasura using docker ps
    docker logs c37388157052

## How to convert hasura metadata file from json to yaml

    python -c 'import sys, yaml, json; yaml.safe_dump(json.load(sys.stdin), sys.stdout, default_flow_style=False)' < file.json > file.yaml

## Sending graphql queries

    Method: POST
    URL:    https://demo.itsecorg.de/api/v1/graphql

    Header:
        x-hasura-admin-secret --> st8chelt1er
        content-type --> application/json
        x-hasura-role-id --> ?

## Using the other APIs
- Use Insomnia
- see <https://hasura.io/docs/1.0/graphql/manual/api-reference/index.html>

### version API

    curl --request GET \
      --url https://127.0.0.1:18443/api//v1/version \
      --header 'content-type: application/json' \
      --data '{"query":""}'

### config API

    curl --request GET \
        --url https://127.0.0.1:18443/api//v1alpha1/config \
        --header 'content-type: application/json' \
        --header 'x-hasura-admin-secret: st8chelt1er' \
        --header 'x-hasura-role: admin' \
        --data '{"query":""}'


### Health API

    curl --request GET \
        --url https://127.0.0.1:18443/api//healthz \
        --header 'content-type: application/json' \
        --data '{"query":""}'

### Create query collection
use json instead of graphql!!!

    curl --request POST \
        --url https://127.0.0.1:18443/api/v1/query \
        --header 'content-type: application/json' \
        --header 'x-hasura-admin-secret: st8chelt1er' \
        --header 'x-hasura-role: admin' \
        --data '{
            "type" : "create_query_collection",
            "args": {
                "name": "my_collection",
                "comment": "an optional comment",
                "definition": {
                    "queries": [
                        {"name": "listNwObjects", "query": "query { object { obj_name } }"}
                    ]
                }
            }
        }'

#### Add query to existing collection

    curl --insecure --request POST \
        --url https://127.0.0.1:18443/api/v1/query \
        --header 'content-type: application/json' \
        --header 'x-hasura-admin-secret: st8chelt1er' \
        --header 'x-hasura-role: admin' \
        --data '{
            "type" : "add_query_to_collection",
            "args": {
                "collection_name": "NwObjQueries",
                "query_name": "listSvcs",
                "query": "query listSvcs { service { svc_name } }"
            }
        }'


Not clear how to access these collections though. Only purpose seems to be for handling allow-list.
See docu hint: In production instances: Enabling the allow-list is highly recommended when running the GraphQL engine in production.
