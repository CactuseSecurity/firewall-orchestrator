# Firewall Orchstrator - Common API Calls

## import

## show import status

```graphql
query getImportStatus {
  management(order_by: { mgm_name: asc }) {
    mgm_id
    mgm_name
    last_import: import_controls(order_by: { control_id: desc }, limit: 1) {
      control_id
      start_time
      stop_time
      successful_import
      import_errors
      last_change_in_config
    }
    last_successful_import: import_controls(
      where: { successful_import: { _eq: true } }
      order_by: { control_id: desc }
      limit: 1
    ) {
      control_id
      start_time
      stop_time
      successful_import
      import_errors
      last_change_in_config
    }
    first_import: import_controls(order_by: { control_id: asc }, limit: 1) {
      control_id
      start_time
      stop_time
      successful_import
      import_errors
    }
  }
}
```

### rollback incomplete import of management

```graphql
mutation deleteIncompleteImport($mgmId: Int!) {
  delete_import_control(
    where: {
      mgm_id: { _eq: $mgmId }
      successful_import: { _eq: false }
      stop_time: { _is_null: true }
    }
  ) {
    affected_rows
  }
}
```

## common helper doc
### How to convert file from json to yaml

    python -c 'import sys, yaml, json; yaml.safe_dump(json.load(sys.stdin), sys.stdout, default_flow_style=False)' < file.json > file.yaml

### How to convert a yaml file to json

    python -c 'import sys, yaml, json; json.dump(yaml.safe_load(sys.stdin), sys.stdout)' < meta.yaml >meta.json

### How to convert JSON pretty print

from pp to compact:
    python -c 'import sys, json; json.dump(json.load(sys.stdin), sys.stdout)' < file.json > file.json

from compact to pp:
    python -c 'import sys, json; json.dump(json.load(sys.stdin), sys.stdout, indent=3)' < file.json > file.json
