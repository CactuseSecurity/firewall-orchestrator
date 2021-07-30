# Hasura Documentation

## Upgrade tables & permissions

In FWO v5.3.4 we are migrating from hasura v1.x to v2.x. 
Therefore upgrades to database tables and permissions now need to be handled as follows:
- apply your changes (e.g. permissions) in the graphql console (https://<ip>:9443/api/) after logging in with hasura admin secret (to be found in ~/etc/secrets/hasura_admin_pwd)
- go to settings menu (cogwheel in top right corner)
- choose export metadata to json file
- copy the metadata {...} part of this file to source roles/api/files/replace_metadata.json (also into metadata {} part)
- run `ansible-playbook site.yml "installation_mode=upgrade" -K`

## Configuration parameters

### HASURA_GRAPHQL_DATABASE_URL
- value: "postgres://{{ api_user }}:{{ api_user_password }}@{{ fworch_db_host }}:{{ fworch_db_port }}/{{ fworch_db_name }}"
- description: the database connection string (currently using a single database for firewall and metadata)

### HASURA_GRAPHQL_ENABLE_CONSOLE
- value:   "true"
- description: default is true, set this to false if you want to disable access to hasura console (loosing graphiql access as well)

### HASURA_GRAPHQL_ENABLE_TELEMETRY
- value: "false"
- description: do not send telemtry data to hasura

### HASURA_GRAPHQL_ADMIN_SECRET
- value: "{{ api_hasura_admin_secret }}"
- description: randomly generated admin secret for hasura console access

### HASURA_GRAPHQL_LOG_LEVEL
- value: "{{ api_log_level }}"
- description: default = info

### HASURA_GRAPHQL_ENABLED_LOG_TYPES
- value: '{{ api_HASURA_GRAPHQL_ENABLED_LOG_TYPES }}'
- description: default="startup, http-log, websocket-log"

### HASURA_GRAPHQL_CONSOLE_ASSETS_DIR
- value: "/srv/console-assets"
- description: ?

### HASURA_GRAPHQL_V1_BOOLEAN_NULL_COLLAPSE
- value: "true"
- description: true means make the graphql API v2.x backward compatible with v1.0 (null result in where clause means true). Default settings "false" breaks query functionality. This might have to be migrated later to ensure new standard logic. See <https://hasura.io/docs/latest/graphql/core/guides/upgrade-hasura-v2.html#what-has-changed>.

### HASURA_GRAPHQL_CORS_DOMAIN
- value: "*"
- description: See https://hasura.io/docs/latest/graphql/core/deployment/graphql-engine-flags/config-examples.html. Value "*" means no restrictions. For CORS explanation see <https://en.wikipedia.org/wiki/Cross-origin_resource_sharing>. Can be restricted in customer environment if needed.

### HASURA_GRAPHQL_JWT_SECRET
- value:
```
'{
    "type": "{{ api_hasura_jwt_alg|quote }}",
    "key": "{{ api_hasura_jwt_secret | regex_replace(''\n'', ''\\n'') }}",
    "claims_namespace_path": "$"
 }'
```
- description: the JWT secret containing of algorithm, key (public key part) and an optional claims_namespace_path with default value "$", meaning to specific path.

### HTTP_PROXY
- value: "{{ http_proxy }}"
- description: allows outbound connections for the docker container via a proxy.

### HTTPS_PROXY
- value: "{{ https_proxy }}"
- description: allows outbound connections for the docker container via a proxy.
