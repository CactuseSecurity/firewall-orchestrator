# Owner Import Interface

The owner interface supplies metadata about business applications, their
responsible people, and the server addresses attached to every application.
The data is exchanged as a JSON document that follows the structure shown in
`documentation/interface-descriptions-json/sample-owner-data-normalized.json`.
This document explains the structure so integrators can generate valid input.

## Top-Level Envelope

```json
{
  "owners": [
    { /* owner definition */ }
  ]
}
```

The payload is a single object with one property:

| Property | Type   | Description |
|----------|--------|-------------|
| `owners` | array  | All application owner records. The order is not relevant, but every object inside the array must follow the schema described below. |

## Owner Object

| Field              | Type     | Required | Description |
|--------------------|----------|----------|-------------|
| `name`             | string   | yes      | Unique owner identifier inside the import. Usually matches the CMDB application name. |
| `app_id_external`  | string   | yes      | External system identifier (example `APP-4711`). Used to correlate repeated imports. |
| `main_user`        | string   | yes      | Distinguished Name (DN) of the responsible owner. |
| `modellers`        | string[] | yes      | DNs of groups or users who can modify the application definition. |
| `recert_interval`  | integer  | yes      | Number of days between mandatory recertifications. |
| `import_source`    | string   | yes      | Origin of the record, e.g., `cmdb-export`. Helps auditors trace the data lineage. |
| `app_servers`      | object[] | yes      | Server definitions that represent the application infrastructure. See below. |

Example:

```json
{
  "name": "owner-1",
  "app_id_external": "APP-4711",
  "main_user": "CN=tiso4711,OU=Benutzer,DC=company,DC=DE",
  "modellers": ["CN=app-4711-eigner,OU=Gruppen,DC=company,DC=DE"],
  "recert_interval": 180,
  "import_source": "cmdb-export",
  "app_servers": [ /* … */ ]
}
```

## `app_servers` Entries

`app_servers` is an array that enumerates every IP element (host, network, or
range) assigned to an application owner. Each entry uses the following fields:

| Field    | Type   | Required | Description |
|----------|--------|----------|-------------|
| `ip`     | string | yes      | Starting IP in IPv4 or IPv6 notation. |
| `ip_end` | string | yes      | Last IP in the block. Equal to `ip` for single hosts. |
| `type`   | string | yes      | Defines how the IP range is interpreted. Supported values are listed below. |
| `name`   | string | no       | Friendly identifier for the block. Mandatory only when the type is `host`. |

### Supported `type` values

| Value    | Meaning |
|----------|---------|
| `host`   | Single IP. `ip` and `ip_end` must be identical. `name` carries the host label (`host_10.12.16.88` in the sample). |
| `network`| Subnet definition. `ip` is the network start and `ip_end` is the broadcast/high address. |
| `range`  | Arbitrary inclusive range from `ip` to `ip_end`. |

Example block:

```json
{
  "ip": "10.112.5.88",
  "ip_end": "10.112.5.99",
  "type": "range"
}
```

## Validation Checklist

- Every owner must have at least one `app_servers` entry.
- `ip` and `ip_end` must be valid IPv4 or IPv6 addresses.
- Use consistent casing for DNs to avoid duplicate detection issues.
- Keep the input idempotent: repeatable imports must keep the same
  `app_id_external` values.
- Imports must contain the full dataset. Incremental updates are not supported;
  each run replaces the previous definition.
- Missing IP objects are not deleted but marked as removed and can be reactivated when contained in a consecutive import.
