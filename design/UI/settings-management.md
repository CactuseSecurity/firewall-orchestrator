```graphql
query getManagement {
  management {
    mgm_id
    mgm_name
    dev_typ_id
    config_path
    importer_hostname
    mgm_comment
    ssh_hostname
    ssh_port
    secret
    ssh_public_key
    ssh_user
    hide_in_gui
    force_initial_import
    do_not_import
  }
}
# $importerHostname should be picked from a list of available importers - no table yet
mutation newManagement(
  $mgmName: String!, 
  $devTypId: Int!, 
  $configPath: String,
  $importerHostname: String!,
  $mgmComment: String,
  $sshHostname: String!,
  $sshPort: Int!,
  $sshPublicKey: String,
  $secret: String!,
  $sshUser: String,
  $hideInGui: Boolean!,
  $forceInitialImport: Boolean!,
  $doNotImport: Boolean!
) {
  insert_management(
    objects: {
  mgm_name: $mgmName,
  dev_typ_id: $devTypId, 
  config_path: $configPath,
  importer_hostname: $importerHostname,
  mgm_comment: $mgmComment,
  ssh_hostname: $sshHostname,
  ssh_port: $sshPort,
  ssh_public_key: $sshPublicKey,
  secret: $secret,
  ssh_user: $sshUser,
  hide_in_gui: $hideInGui,
  force_initial_import: $forceInitialImport,
  do_not_import: $doNotImport
    }) {
    affected_rows
  }
}

# Query variables:
{
  "mgmName": "hugo",
  "devTypId": 7, 
  "importerHostname": "fworch-srv",
  "sshHostname": "fworch-srv",
  "sshPort": 22,
  "secret": "private-key",
  "sshUser": "fworch-import-user",
  "hideInGui": false,
  "doNotImport": false,
  "forceInitialImport": false
}

mutation setManagement($devTypId: Int!, $mgm_name: String!, $secret: String!) {
  insert_management(objects: {config_path: "", dev_typ_id: 10, importer_hostname: "", mgm_name: "$mgm_name", ssh_hostname: "", secret: "", ssh_user: ""}) {
    affected_rows
  }
}
```
