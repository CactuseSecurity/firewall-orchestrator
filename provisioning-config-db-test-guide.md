# Provisioning Config Database Test Guide

Use this to quickly verify that the hierarchical provisioning config database model works on an installed FWO system.

## 1. Confirm The Version

From the repository root:

```powershell
Select-String -Path inventory\group_vars\all.yml -Pattern 'product_version'
```

Expected:

```text
product_version: "9.5.2"
```

## 2. Confirm Tables Exist

Connect to the FWO PostgreSQL database and run:

```sql
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN ('provisioning_config_node', 'provisioning_config_value')
ORDER BY table_name;
```

Expected:

```text
provisioning_config_node
provisioning_config_value
```

## 3. Confirm Root Node Exists

```sql
SELECT id, node_type, object_key, parent_id, display_name, sort_order
FROM provisioning_config_node
WHERE node_type = 'global'
  AND object_key = 'global';
```

Expected: exactly one row with `parent_id` as `NULL`.

## 4. Insert Sample Hierarchy

This creates a disposable test tree:

```sql
WITH global_node AS (
    SELECT id
    FROM provisioning_config_node
    WHERE node_type = 'global'
      AND object_key = 'global'
),
device_type_node AS (
    INSERT INTO provisioning_config_node (node_type, object_key, parent_id, display_name, sort_order)
    SELECT 'device_type', 'test-checkpoint', id, 'Test Check Point', 10
    FROM global_node
    ON CONFLICT (node_type, object_key) DO UPDATE
    SET parent_id = EXCLUDED.parent_id,
        display_name = EXCLUDED.display_name,
        sort_order = EXCLUDED.sort_order
    RETURNING id
),
management_node AS (
    INSERT INTO provisioning_config_node (node_type, object_key, parent_id, display_name, sort_order)
    SELECT 'management', 'test-manager', id, 'Test Manager', 20
    FROM device_type_node
    ON CONFLICT (node_type, object_key) DO UPDATE
    SET parent_id = EXCLUDED.parent_id,
        display_name = EXCLUDED.display_name,
        sort_order = EXCLUDED.sort_order
    RETURNING id
)
INSERT INTO provisioning_config_node (node_type, object_key, parent_id, display_name, sort_order)
SELECT 'gateway', 'test-gateway', id, 'Test Gateway', 30
FROM management_node
ON CONFLICT (node_type, object_key) DO UPDATE
SET parent_id = EXCLUDED.parent_id,
    display_name = EXCLUDED.display_name,
    sort_order = EXCLUDED.sort_order;
```

## 5. Insert Sample Values

```sql
INSERT INTO provisioning_config_value (node_id, config_key, config_value)
SELECT id, 'logging', 'true'::jsonb
FROM provisioning_config_node
WHERE node_type = 'global'
  AND object_key = 'global'
ON CONFLICT (node_id, config_key) DO UPDATE
SET config_value = EXCLUDED.config_value;

INSERT INTO provisioning_config_value (node_id, config_key, config_value)
SELECT id, 'installOn', '"manager_policy_targets"'::jsonb
FROM provisioning_config_node
WHERE node_type = 'management'
  AND object_key = 'test-manager'
ON CONFLICT (node_id, config_key) DO UPDATE
SET config_value = EXCLUDED.config_value;

INSERT INTO provisioning_config_value (node_id, config_key, config_value)
SELECT id, 'installOn', '"test-gateway"'::jsonb
FROM provisioning_config_node
WHERE node_type = 'gateway'
  AND object_key = 'test-gateway'
ON CONFLICT (node_id, config_key) DO UPDATE
SET config_value = EXCLUDED.config_value;

INSERT INTO provisioning_config_value (node_id, config_key, config_value)
SELECT id, 'securityProfiles', '["strict", "ips"]'::jsonb
FROM provisioning_config_node
WHERE node_type = 'gateway'
  AND object_key = 'test-gateway'
ON CONFLICT (node_id, config_key) DO UPDATE
SET config_value = EXCLUDED.config_value;
```

## 6. Test Effective Resolution

```sql
WITH RECURSIVE ancestors AS (
    SELECT id, parent_id, 0 AS distance
    FROM provisioning_config_node
    WHERE node_type = 'gateway'
      AND object_key = 'test-gateway'

    UNION ALL

    SELECT parent.id, parent.parent_id, ancestors.distance + 1
    FROM provisioning_config_node parent
    JOIN ancestors ON ancestors.parent_id = parent.id
)
SELECT DISTINCT ON (value.config_key)
    value.config_key,
    value.config_value,
    value.node_id,
    ancestors.distance
FROM provisioning_config_value value
JOIN ancestors ON ancestors.id = value.node_id
ORDER BY value.config_key, ancestors.distance;
```

Expected:

```text
installOn        "test-gateway"       distance 0
logging          true                 inherited from global
securityProfiles ["strict", "ips"]    distance 0
```

The important check is that `installOn` resolves from the gateway, not from the manager, while `logging` still resolves from the global root.

## 7. Test Frontend Child Navigation

```sql
SELECT child.node_type, child.object_key, child.display_name
FROM provisioning_config_node parent
JOIN provisioning_config_node child ON child.parent_id = parent.id
WHERE parent.node_type = 'global'
  AND parent.object_key = 'global'
ORDER BY child.sort_order, child.display_name;
```

Expected: `test-checkpoint` appears as a child of the global root.

## 8. Test Duplicate Protection

This should fail or update through `ON CONFLICT`, depending on how it is written:

```sql
INSERT INTO provisioning_config_node (node_type, object_key, display_name)
VALUES ('gateway', 'test-gateway', 'Duplicate Test Gateway');
```

Expected: unique constraint violation on `idx_provisioning_config_node_type_key`.

This should also fail:

```sql
INSERT INTO provisioning_config_value (node_id, config_key, config_value)
SELECT id, 'installOn', '"duplicate"'::jsonb
FROM provisioning_config_node
WHERE node_type = 'gateway'
  AND object_key = 'test-gateway';
```

Expected: duplicate key violation on the value table primary key.

## 9. Clean Up Test Data

The foreign keys cascade down the test subtree:

```sql
DELETE FROM provisioning_config_node
WHERE node_type = 'device_type'
  AND object_key = 'test-checkpoint';
```

Then verify only the global root remains from this test:

```sql
SELECT node_type, object_key
FROM provisioning_config_node
WHERE object_key LIKE 'test-%'
ORDER BY node_type, object_key;
```

Expected: no rows.

## 10. Hasura Metadata Check

After applying metadata, the Hasura tables should include:

```text
public.provisioning_config_node
public.provisioning_config_value
```

Expected relationships:

- `provisioning_config_node.parent_node`
- `provisioning_config_node.child_nodes`
- `provisioning_config_node.values`
- `provisioning_config_value.node`

Expected roles with access:

- `fw-admin`
- `middleware-server`
