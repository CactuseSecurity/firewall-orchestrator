# IP Area Import Interface

This interface loads network areas and their associated subnets. The payload
is documented by `documentation/interface-descriptions-json/sample-ip-data-normalized.json`.

## Envelope

```json
{
  "areas": [
    { /* area */ }
  ]
}
```

## Area Object

| Field       | Type     | Required | Description |
|-------------|----------|----------|-------------|
| `name`      | string   | yes      | Human readable area label (`AREA51`). |
| `id_string` | string   | yes      | Unique technical identifier used for correlation. |
| `subnets`   | object[] | yes      | List of subnet records attached to the area. |

### Subnet Entry

| Field | Type   | Required | Description |
|-------|--------|----------|-------------|
| `ip`  | string | yes      | Network in CIDR notation (IPv4 or IPv6). |
| `name`| string | yes      | Friendly subnet name. |

Example:

```json
{
  "name": "AREA51",
  "id_string": "NA51",
  "subnets": [
    { "ip": "10.10.10.0/24", "name": "Netz01" },
    { "ip": "10.10.34.16/30", "name": "Netz02" }
  ]
}
```

## Validation Notes

- Every area requires at least one subnet.
- Subnet CIDR strings must be canonical; overlapping subnets are technically
  allowed but should be avoided to keep reporting deterministic.
- Imports replace the full dataset. Incremental subnet updates are not supported.
