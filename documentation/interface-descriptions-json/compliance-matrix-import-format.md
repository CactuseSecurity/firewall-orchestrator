# Compliance Matrix Import Interface

The compliance matrix import creates or synchronizes a network-zone matrix from
a JSON file. Upload the file under **Settings > Compliance > Matrix Import**.
See the complete
[sample compliance matrix import](sample-compliance-matrix-import.json).

## Top-Level Object

```json
{
  "name": "Corporate network zones",
  "comment": "Imported from the network source of truth",
  "areas": [
    { /* network zone */ }
  ]
}
```

| Field     | Type     | Required | Description |
|-----------|----------|----------|-------------|
| `name`    | string   | yes      | Name of the compliance matrix. The importer uses this value to find the matrix on later imports. |
| `comment` | string   | no       | Description or source information displayed with the matrix. |
| `areas`   | object[] | yes      | Complete list of network zones in the matrix. |

The matrix name must not be empty. If a matrix with this name already exists,
it can be updated only if it was originally created by an import. The importer
does not overwrite a manually created matrix with the same name.

## Network-Zone Object

Each entry in `areas` defines one network zone and its allowed, directional
communications.

| Field              | Type     | Required | Description |
|--------------------|----------|----------|-------------|
| `name`             | string   | yes      | Human-readable zone name. Must be unique within the matrix. |
| `id_string`        | string   | yes      | Stable technical identifier. Must be unique within the matrix and remain unchanged between imports. |
| `subnets`          | object[] | yes      | Complete list of IP addresses or ranges assigned to the zone. May be empty. |
| `communication_to` | object[] | yes      | Complete list of destination zones to which communication from this zone is allowed. May be empty. |

`communication_to` is directional. Allowing communication from `office` to
`datacenter` does not automatically allow communication from `datacenter` to
`office`. A destination is referenced by its zone `id_string`:

```json
{
  "name": "Office",
  "id_string": "office",
  "subnets": [
    {
      "name": "Office network",
      "ip": "10.10.0.0/16"
    }
  ],
  "communication_to": [
    {
      "id_string": "datacenter"
    }
  ]
}
```

Every `communication_to.id_string` must identify a zone available in the
resulting matrix. Communications not listed for a source zone are restricted.

## Subnet Object

A subnet entry supports a single address, CIDR notation, or an inclusive range.

| Field    | Type   | Required | Description |
|----------|--------|----------|-------------|
| `name`   | string | no       | Optional descriptive name for the address or range. |
| `ip`     | string | yes      | Single IP address, CIDR network, explicit `start-end` range, or the first address when `ip_end` is used. IPv4 and IPv6 are supported. |
| `ip_end` | string | no       | Last address of an inclusive range. Omit it when `ip` contains a single address, CIDR network, or explicit range. |

Valid examples are:

```json
{ "name": "Host", "ip": "192.0.2.10" }
{ "name": "Network", "ip": "192.0.2.0/24" }
{ "name": "Inline range", "ip": "192.0.2.10-192.0.2.20" }
{ "name": "Range", "ip": "192.0.2.10", "ip_end": "192.0.2.20" }
```

The start and end of a range must use the same IP address family, and the start
must not be greater than the end.

## Synchronization Behavior

The JSON document represents the complete desired state of the imported matrix:

- A new matrix is created when no matrix with the supplied `name` exists.
- Existing imported zones are matched by `id_string`; their names and IP ranges
  are updated.
- Existing imported zones omitted from `areas` are deactivated.
- For each source zone, allowed destinations omitted from `communication_to`
  are removed.
- The matrix comment and recorded import-source filename are updated on
  reimport.

Always export the full matrix. Do not use a partial document to update only one
zone or connection.

The import is not transactional. If processing fails after some zones or
connections have been saved, those earlier changes can remain in the matrix.
Validate the complete document before importing it into a production system.

## Validation Checklist

- Save the input as a valid `.json` file.
- Set a non-empty matrix `name`.
- Keep every zone `name` and `id_string` unique within the matrix.
- Keep `id_string` values stable across imports.
- Reference only valid destination `id_string` values in `communication_to`.
- Provide valid IPv4 or IPv6 values for every subnet.
- Do not define zones with the reserved identifiers
  `AUTO_CALCULATED_ZONE_INTERNET` or
  `AUTO_CALCULATED_ZONE_UNDEFINED_INTERNAL`; these zones are managed by FWO
  when automatic zone calculation is enabled.
