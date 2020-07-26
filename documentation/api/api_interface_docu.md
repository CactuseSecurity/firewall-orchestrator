# create api interface documentation

## auto generate full schema using @2fd/graphdoc

see <https://github.com/2fd/graphdoc#install>

1. needs 2.3 GB additional hdd space, 8 GB RAM and 3.5 minutes to generate !!!

       sudo apt install npm
       sudo npm install -g @2fd/graphdoc
        
       export NODE_OPTIONS="--max-old-space-size=4096"
       sudo -E -u {{ fwo-main-user }} graphdoc -x "x-hasura-admin-secret: st8chelt1er" --force \
         -e http://localhost:8080/v1/graphql -o {{ fwo-base-dir }}/ui/Blazor/FWO/FWO/wwwroot/api_schema

       ✓ complete: 2962 files generated.

2. view with <https://localhost/api_schema/index.html>

   not really sure if result is worth it :-(

## alternatively show all types using query

This generates a very lengthy list.

        query IntrospectionQuery {
            __schema {
              types {
                name
                description
              }
            }
        }


      {
        "data": {
          "__schema": {
            "types": [
              {
                "name": "Boolean",
                "description": null
              },
     [snip]
              {
                "name": "device",
                "description": "columns and relationships of \"device\""
              },
              {
                "name": "device_aggregate",
                "description": "aggregated selection of \"device\""
              },
              {
                "name": "device_aggregate_fields",
                "description": "aggregate fields of \"device\""
              },
              {
                "name": "device_aggregate_order_by",
                "description": "order by aggregate values of table \"device\""
              },
              {
     [snip]
              {
                "name": "zone_variance_order_by",
                "description": "order by variance() on columns of table \"zone\""
              }
            ]
          }
        }
      }

### generate more information including field names and descriptions

        query IntrospectionQuery {
              __schema {
                     types {
                            name
                            fields {
                                   __typename
                                   name
                                   description
                            }
                            description
                     }
              }
        }
