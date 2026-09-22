# Provisioning Settings Manager Plan

We have the database foundation and an agreed responsibility boundary between the frontend and the persistence manager.

## What exists

| Layer | Available now | Missing |
|---|---|---|
| Database | Hierarchical nodes, JSONB values, cascading relationships, global root, named node upsert constraint | Database-level hierarchy enforcement beyond the manager validation |
| Hasura | Parent/child/value relationships; CRUD for `fw-admin` and `middleware-server`; provisioning GraphQL operations | Live-schema integration verification |
| C# data | Scope-specific DTO hierarchy and enums; typed keys; read/change models; persistence records; manager and stable JSON mapping | Nothing currently identified |
| Verification | DTO, serializer, manager, and in-memory API tests; direct SQL/metadata inspection; opt-in live Hasura smoke test | Executing the live smoke test in an installed integration environment |

The intended hierarchy is:

```text
Global -> DeviceType -> Management -> Gateway
```

Each node stores only its own overrides; effective settings are assembled from root to leaf.

## Resolved groundwork

- [`ProvisioningSettingsTest.cs`](tests-unit/files/FWO.Test/ProvisioningSettingsTest.cs) now reflects the authoritative DTO hierarchy and defaults; its two focused tests pass.
- The provisioning migration is deduplicated and temporarily versioned as [`9.5.99.sql`](database/files/upgrade/9.5.99.sql), matching the branch's placeholder product version and revision-history entry.
- The temporary database test guide is intentionally being removed and is not part of this plan.

## Agreed responsibility boundary

- The frontend determines whether a displayed value is inherited, unchanged, a new override, or an override that should be removed.
- The frontend calls the manager only when the database actually needs to change and explicitly identifies the affected setting keys.
- The manager does not compare complete DTOs, track dirty fields, or infer inheritance intent.
- Saving is patch-based: requested upserts and removals are applied while every unrelated value row remains untouched.
- The existing settings DTOs remain value models and do not acquire persistence or change-tracking state.

## Agreed persistence contracts

- Database keys use lower camel case, for example `implementationMode`, `installOn`, and `securityProfiles`.
- Enum values are stored as JSON strings, never numeric enum ordinals.
- Other values retain their natural JSON types: strings as strings and lists as arrays.
- `(node_type, object_key)` becomes a named `UNIQUE` constraint. This gives Hasura a deliberate constraint name for atomic node upserts through `on_conflict`. [Hasura upsert example](https://hasura.io/learn/graphql/react-rxdb-offline-first/syncing-rxdb-with-hasura/2-push-query/)

## Recommended design

Place `ProvisioningSettingsManager` in `FWO.Config.Api`. That project already owns database-backed configuration and references both `FWO.Data` and `FWO.Api.Client`. The manager should use `ApiConnection`/Hasura, not introduce direct PostgreSQL access.

The API should distinguish two read concepts explicitly:

- A **direct override** is a row stored on the selected node itself.
- An **effective value** is the closest override found while walking from the selected node towards the global node, falling back to the compiled DTO default when no row exists.

Avoid a plain string as the public setting identifier. A generic setting definition keeps the database key and expected C# type together:

```csharp
public sealed record ProvisioningSettingKey<TValue>(
    string DatabaseKey,
    IReadOnlySet<ProvisioningScopeType> AllowedScopes);

public static class ProvisioningSettingKeys
{
    public static readonly ProvisioningSettingKey<ProvisioningLoggingMode> Logging = ...;
    public static readonly ProvisioningSettingKey<List<string>> SecurityProfiles = ...;
}
```

This lets the compiler reject a call that tries to store a string or list under the `Logging` key. The definitions also provide the stable lower-camel-case database names and scope validation in one place.

A suitable manager contract is:

```csharp
Task<ProvisioningSettingsLevel<TSettings>> LoadLevelAsync<TSettings>(
    ProvisioningSettingsScope scope);

Task<ResolvedProvisioningValue<TValue>> LoadEffectiveValueAsync<TValue>(
    ProvisioningSettingsScope scope,
    ProvisioningSettingKey<TValue> key);

Task<ProvisioningSettingsScope> SetOverrideAsync<TValue>(
    ProvisioningSettingsScope scope,
    ProvisioningSettingKey<TValue> key,
    TValue value);

Task ClearOverrideAsync(
    ProvisioningSettingsScope scope,
    ProvisioningSettingKey key);

Task<ProvisioningSettingsScope> ApplyChangesAsync(
    ProvisioningSettingsChangeSet changes);

Task<IReadOnlyList<ProvisioningSettingsScope>> GetChildrenAsync(
    long parentNodeId);
```

`SetOverrideAsync` is preferable to `UpdateOverrideAsync` because it deliberately means insert-or-update. It should ensure the target node exists, upsert exactly one value, and return the resolved scope so a newly allocated `NodeId` reaches the caller.

`ProvisioningSettingsLevel<TSettings>` should contain:

- The node scope and identifiers.
- Fully resolved/effective settings.
- The keys overridden directly at the selected node.
- Optionally, a source-scope map when the UI needs to show which ancestor supplied each inherited value.

`LoadEffectiveValueAsync` returns the applicable value plus its source node, or identifies the compiled default as its source. Comparing that source with the selected node tells the frontend whether the value is local or inherited without requiring a separate direct-row lookup.

`ProvisioningSettingsChangeSet` should contain the target scope, explicit override upserts, and explicit override removals. A typed builder can expose `Set<TValue>(key, value)` and `Remove(key)` so a frontend save involving several fields remains type-safe and executes transactionally. It represents frontend intent directly; it is not derived by comparing complete DTOs in the manager.

Reads must not create database nodes. On the first write to a level that has no node yet, the manager should upsert the node using its scope type and object key. A non-global new node therefore requires a valid `ParentNodeId`. Node creation can remain an internal part of `SetOverrideAsync` and `ApplyChangesAsync`; a public `EnsureLevelAsync` is only necessary if the frontend needs empty nodes for navigation before any overrides exist.

## Save/load behavior

Loading:

1. Fetch the selected node, its values, and up to three ancestors in one fixed-depth GraphQL query.
2. Validate the chain and scope types.
3. Start with the compiled DTO defaults.
4. Apply values in order: global -> device type -> management -> gateway.
5. Return both the effective DTO and the selected node's direct overrides for the frontend.

Saving:

1. Accept an explicit change set from the frontend; an empty change set is a no-op.
2. Validate scope type, object key, parent, setting keys, and the value type allowed for each key.
3. Ensure/upsert the node and update its display metadata.
4. Upsert only the values named in the change set.
5. Delete only the overrides explicitly named for removal.
6. Keep a multi-setting patch in one GraphQL mutation so it is transactional for this Postgres source. [Hasura transaction behavior](https://hasura.io/blog/announcing-hasura-2-0-0-alpha-8)

Subtree deletion should not be part of the first version because the database cascade makes it materially more destructive.

## Implementation sequence

1. [x] Replace the standalone node uniqueness index with the agreed named `UNIQUE` constraint in fresh-install and upgrade SQL.
2. [x] Define stable setting keys, the key-to-type mapping, the read model, and the explicit change-set model.
3. [x] Add `ProvisioningQueries` plus GraphQL files for:
   - node with ancestry and values;
   - parent node by ID for validating an unpersisted level;
   - child nodes;
   - node upsert;
   - transactional patch upserts;
   - explicit override deletion.
4. [x] Implement the manager, serializer, scope factory, and hierarchy validation.
5. [x] Add unit tests for:
   - all value types and enums round-tripping;
   - inheritance precedence;
   - changing one setting without writing unrelated effective values;
   - preserving unrelated existing overrides;
   - clearing only explicitly removed overrides;
   - treating an empty change set as a no-op;
   - invalid parent/type combinations;
   - missing and duplicate nodes;
   - API failures propagating cleanly.
6. [x] Run an integration smoke test against Hasura to verify generated constraint names, permissions, and JSONB behavior.

   The opt-in test is implemented in [`ProvisioningSettingsManagerIntegrationTest.cs`](tests-unit/files/FWO.Test/ProvisioningSettingsManagerIntegrationTest.cs). It runs the same create/upsert/read/update/clear/delete lifecycle once as `fw-admin` and once as `middleware-server`, verifies enum/string/list JSONB round-trips, exercises the named node upsert constraint, and removes only its uniquely named test nodes in cleanup. It is gated by `FWO_RUN_INTEGRATION_TESTS=true`.

   It was executed successfully on 2026-09-22 against an installed Hasura schema. Both the `fw-admin` and `middleware-server` cases passed.

## Verification on 2026-09-21

- Provisioning-focused tests: **54 passed**, **0 failed**; the two live role cases were skipped as designed without integration opt-in.
- Complete `FWO.Test` suite: **5,780 passed**, **0 failed**, **25 skipped**. The skips include existing opt-in integration tests and the two new Hasura role cases.
- `git diff --check`: passed.
- Static schema inspection confirms `provisioning_config_node_node_type_object_key_key` is identical in fresh-install SQL, the `9.5.99` migration, and the Hasura upsert mutation.
- Static metadata inspection confirms full select/insert/update/delete permissions on both provisioning tables for `fw-admin` and `middleware-server`.

## Live integration verification on 2026-09-22

- Installed Hasura health check: **OK**; server version **v2.50.0**.
- Focused provisioning integration test: **2 passed**, **0 failed**, **0 skipped**.
- Both `fw-admin` and `middleware-server` completed the create/upsert/read/update/clear/delete lifecycle successfully.
- The uniquely named smoke-test nodes were deleted by the test cleanup without error.

## Recommended initial assumptions

- Use stable numeric database IDs encoded as `ObjectKey` for managements and gateways.
- Use lower-camel-case setting keys.
- Store enum names as JSON strings.
- Treat frontend-supplied change sets as the authority on which overrides to upsert or remove.

The `9.5.99` version is a development placeholder and must be replaced with the repository's real target version before merge.
