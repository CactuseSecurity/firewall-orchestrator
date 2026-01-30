# Normalized Firewall Config Interface

Firewall configurations are imported through the normalized JSON format shown
in the two samples inside this directory. Both the single-manager and
multi-manager examples use the same schema, which is described below.

## Top-Level Envelope

```json
{
  "ConfigFormat": "NORMALIZED",
  "ManagerSet": [ { /* manager */ } ],
  "native_config": { }
}
```

| Field          | Type     | Required | Description |
|----------------|----------|----------|-------------|
| `ConfigFormat` | string   | yes      | Always `NORMALIZED`. |
| `ManagerSet`   | object[] | yes      | Collection of manager definitions. Each manager contains a batch of configuration objects. |
| `native_config`| object   | no       | Raw vendor payload (kept empty in the samples). |

## Manager Definition

There may be multiple managers in a single import file which belong to the same super-manager (including the super-manager itself).

| Field           | Type     | Required | Description |
|-----------------|----------|----------|-------------|
| `ManagerUid`    | string   | yes      | Unique identifier for the manager/adom. |
| `ManagerName`   | string   | yes      | Display name (e.g., `forti-mgr cactus`). |
| `IsSuperManager`| boolean  | yes      | `true` if the entry aggregates sub-managers. |
| `DomainUid`     | string   | no       | Vendor-specific domain identifier; empty if n/a. |
| `DomainName`    | string   | no       | Human-readable domain name. |
| `SubManagerIds` | string[] | yes      | IDs of delegated managers; empty array if none. |
| `Configs`       | object[] | yes      | Configuration batches imported for the manager. |

## Config Batch

There may be multiple configs per manager in the future but at the moment the only supported case is a single config awith action 'INSERT'. 

| Field           | Type    | Required | Description |
|-----------------|---------|----------|-------------|
| `ConfigFormat`  | string  | yes      | `NORMALIZED_LEGACY` in the sample payloads. |
| `action`        | string  | yes      | CRUD indicator (`INSERT`, `UPDATE`, ...). |
| `network_objects` | object | yes    | Map of network objects keyed by UID. |
| `service_objects` | object | yes    | Map of service objects keyed by UID. |
| `users`         | object  | yes      | User directory entries keyed by UID (empty in samples). |
| `zone_objects`  | object  | yes      | Map of zone definitions. |
| `rulebases`     | array   | yes      | Rulebase segments assigned to the manager. |
| `gateways`      | array   | yes      | Gateways/firewalls with rulebase links. |

### `network_objects` entry

| Field             | Type   | Description |
|-------------------|--------|-------------|
| `obj_uid`         | string | Unique ID (matches the map key). |
| `obj_name`        | string | Readable name. |
| `obj_ip`          | string | Start IP or CIDR. |
| `obj_ip_end`      | string | End IP for ranges. |
| `obj_color`       | string | UI color metadata. |
| `obj_typ`         | string | Object type (`network`, `host`, etc.). |
| `obj_member_refs` | array/string|null | References to nested objects. |
| `obj_member_names`| array/string|null | Human readable member names. |
| `obj_comment`     | string|null | Optional comment. |

### `service_objects` entry

| Field             | Type   | Description |
|-------------------|--------|-------------|
| `svc_uid`         | string | Unique ID (map key). |
| `svc_name`        | string | Display name. |
| `svc_port`        | integer|null | Start port. |
| `svc_port_end`    | integer|null | End port (equals start for single ports). |
| `svc_color`       | string | UI color metadata. |
| `svc_typ`         | string | Service type (`simple`, `group`, ...). |
| `ip_proto`        | integer | L4 protocol number. |
| `svc_member_refs` | array/string|null | Child object references. |
| `svc_member_names`| array/string|null | Child names. |
| `svc_comment`     | string|null | Optional comment. |
| `svc_timeout`     | integer|null | Optional timeout. |
| `rpc_nr`          | integer|null | RPC metadata. |

### Rulebase Segment

| Field           | Type    | Description |
|-----------------|---------|-------------|
| `uid`           | string  | Rulebase identifier (map target for gateway links). |
| `name`          | string  | Display name. |
| `mgm_uid`       | string  | Owning manager UID. |
| `is_global`     | boolean | Mark global sections. |
| `Rules`         | object  | Map of rule objects keyed by `rule_uid`. |

Each rule entry contains metadata such as `rule_src`, `rule_dst`, `rule_svc`,
action, hit counters, zones, and audit fields (`last_change_admin`, etc.),
mirroring the properties shown in the samples.

### Gateways

| Field                | Type     | Description |
|----------------------|----------|-------------|
| `Uid`                | string   | Firewall identifier. |
| `Name`               | string   | Friendly name. |
| `Routing`            | array    | Route table data (empty in samples). |
| `Interfaces`         | array    | Interface metadata (empty in samples). |
| `RulebaseLinks`      | object[] | Links that bind the gateway to rulebases. |
| `GlobalPolicyUid`    | string   | Optional reference. |
| `EnforcedPolicyUids` | string[] | Enforced access policies. |
| `EnforcedNatPolicyUids` | string[] | Enforced NAT policies. |
| `ImportDisabled`     | boolean  | Skip flag for UI. |
| `ShowInUI`           | boolean  | Whether to display the gateway. |

`RulebaseLinks` specify the relationship between gateways and rulebases:
`to_rulebase_uid` contains the rulebase UID, `link_type` indicates ordering,
and the boolean flags describe whether the link is initial/global/section.

## Operational Notes

- Always deliver the full manager dataset; incremental imports are currently not supported.
- UID keys should remain stable so repeated imports update the same records.
- While the samples focus on IPv4 data, any object can contain IPv6 addresses
  where the firmware supports them.
