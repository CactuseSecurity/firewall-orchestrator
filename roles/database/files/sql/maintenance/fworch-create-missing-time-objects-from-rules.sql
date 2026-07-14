/*
Create placeholder time objects for rule.rule_time references that are missing in time_object
and backfill missing rows in rule_time.

For every time object UID referenced in rule.rule_time, this script inserts one time_object
per management when no matching entry exists yet. The created import is set to the earliest
rule_create referencing that UID within the same management.

Afterwards, the script inserts one rule_time row per rule/time-object reference when missing.
*/

WITH referenced_time_objects AS (
    SELECT
        r.mgm_id,
        btrim(ref.time_obj_uid) AS time_obj_uid,
        MIN(r.rule_create) AS created
    FROM rule AS r
    CROSS JOIN LATERAL unnest(string_to_array(COALESCE(r.rule_time, ''), '|')) AS ref(time_obj_uid)
    WHERE btrim(ref.time_obj_uid) <> ''
    GROUP BY
        r.mgm_id,
        btrim(ref.time_obj_uid)
)
INSERT INTO time_object (
    mgm_id,
    time_obj_uid,
    time_obj_name,
    created
)
SELECT
    referenced_time_objects.mgm_id,
    referenced_time_objects.time_obj_uid,
    referenced_time_objects.time_obj_uid,
    referenced_time_objects.created
FROM referenced_time_objects
WHERE NOT EXISTS (
    SELECT 1
    FROM time_object AS t
    WHERE t.mgm_id = referenced_time_objects.mgm_id
      AND t.time_obj_uid = referenced_time_objects.time_obj_uid
);

WITH rule_time_refs AS (
    SELECT DISTINCT
        r.rule_id,
        r.mgm_id,
        r.rule_create AS created,
        r.removed,
        btrim(ref.time_obj_uid) AS time_obj_uid
    FROM rule AS r
    CROSS JOIN LATERAL unnest(string_to_array(COALESCE(r.rule_time, ''), '|')) AS ref(time_obj_uid)
    WHERE btrim(ref.time_obj_uid) <> ''
)
INSERT INTO rule_time (
    rule_id,
    time_obj_id,
    created,
    removed
)
SELECT
    rtr.rule_id,
    matched_time_object.time_obj_id,
    rtr.created,
    rtr.removed
FROM rule_time_refs AS rtr
JOIN LATERAL (
    SELECT t.time_obj_id
    FROM time_object AS t
    WHERE t.mgm_id = rtr.mgm_id
      AND t.time_obj_uid = rtr.time_obj_uid
      AND COALESCE(t.created, rtr.created) <= rtr.created
      AND (t.removed IS NULL OR t.removed >= rtr.created)
    ORDER BY t.created DESC NULLS LAST, t.time_obj_id DESC
    LIMIT 1
) AS matched_time_object ON TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM rule_time AS rt
    WHERE rt.rule_id = rtr.rule_id
      AND rt.time_obj_id = matched_time_object.time_obj_id
      AND rt.created = rtr.created
);
