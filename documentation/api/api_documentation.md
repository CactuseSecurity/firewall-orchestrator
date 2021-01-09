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
