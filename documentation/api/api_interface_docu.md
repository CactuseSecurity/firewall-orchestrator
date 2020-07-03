# create api interface documentaion using @2fd/graphdoc

see <https://github.com/2fd/graphdoc#install>

## auto generate full schema

1. needs 2.3 GB additional hdd space and 8 GB RAM !!!

       sudo apt install npm
       sudo npm install -g @2fd/graphdoc
        
       export NODE_OPTIONS="--max-old-space-size=4096"
       graphdoc -x "x-hasura-admin-secret: st8chelt1er" -e http://localhost:8080/v1/graphql -o /usr/share/itsecorg/ui/Blazor/FWO/FWO/api_schema

       ✓ complete: 2962 files generated.

2. view with  https://localhost:8443/api_schema/index.html

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
