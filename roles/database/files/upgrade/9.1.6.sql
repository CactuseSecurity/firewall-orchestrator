DROP FUNCTION IF EXISTS public.get_rulebase_for_owner(rulebase, integer);
DROP VIEW IF EXISTS public.rule_api;

ALTER TABLE "object" DROP CONSTRAINT IF EXISTS object_obj_last_seen_fkey;
ALTER TABLE "service" DROP CONSTRAINT IF EXISTS service_svc_last_seen_fkey;
ALTER TABLE "usr" DROP CONSTRAINT IF EXISTS usr_user_last_seen_fkey;
ALTER TABLE "zone" DROP CONSTRAINT IF EXISTS zone_zone_last_seen_fkey;
ALTER TABLE "objgrp" DROP CONSTRAINT IF EXISTS objgrp_import_last_seen_fkey;
ALTER TABLE "objgrp_flat" DROP CONSTRAINT IF EXISTS objgrp_flat_import_last_seen_fkey;
ALTER TABLE "svcgrp" DROP CONSTRAINT IF EXISTS svcgrp_import_last_seen_fkey;
ALTER TABLE "svcgrp_flat" DROP CONSTRAINT IF EXISTS svcgrp_flat_import_last_seen_fkey;
ALTER TABLE "usergrp" DROP CONSTRAINT IF EXISTS usergrp_import_last_seen_fkey;
ALTER TABLE "usergrp_flat" DROP CONSTRAINT IF EXISTS usergrp_flat_import_last_seen_fkey;
ALTER TABLE "rule" DROP CONSTRAINT IF EXISTS rule_rule_last_seen_fkey;
ALTER TABLE "rule_from" DROP CONSTRAINT IF EXISTS rule_from_rf_last_seen_fkey;
ALTER TABLE "rule_to" DROP CONSTRAINT IF EXISTS rule_to_rt_last_seen_fkey;
ALTER TABLE "rule_service" DROP CONSTRAINT IF EXISTS rule_service_rs_last_seen_fkey;

DROP INDEX IF EXISTS "IX_Relationship108";
DROP INDEX IF EXISTS "IX_Relationship121";
DROP INDEX IF EXISTS "IX_Relationship123";
DROP INDEX IF EXISTS "IX_Relationship125";
DROP INDEX IF EXISTS "IX_Relationship152";
DROP INDEX IF EXISTS "IX_Relationship154";
DROP INDEX IF EXISTS "IX_Relationship167";
DROP INDEX IF EXISTS "IX_Relationship169";
DROP INDEX IF EXISTS "IX_Relationship171";
DROP INDEX IF EXISTS "IX_Relationship173";
DROP INDEX IF EXISTS "IX_Relationship175";
DROP INDEX IF EXISTS "IX_Relationship177";
DROP INDEX IF EXISTS "IX_Relationship179";

ALTER TABLE "object" DROP COLUMN IF EXISTS "obj_last_seen";
ALTER TABLE "service" DROP COLUMN IF EXISTS "svc_last_seen";
ALTER TABLE "usr" DROP COLUMN IF EXISTS "user_last_seen";
ALTER TABLE "zone" DROP COLUMN IF EXISTS "zone_last_seen";
ALTER TABLE "objgrp" DROP COLUMN IF EXISTS "import_last_seen";
ALTER TABLE "objgrp_flat" DROP COLUMN IF EXISTS "import_last_seen";
ALTER TABLE "svcgrp" DROP COLUMN IF EXISTS "import_last_seen";
ALTER TABLE "svcgrp_flat" DROP COLUMN IF EXISTS "import_last_seen";
ALTER TABLE "usergrp" DROP COLUMN IF EXISTS "import_last_seen";
ALTER TABLE "usergrp_flat" DROP COLUMN IF EXISTS "import_last_seen";
ALTER TABLE "rule_from" DROP COLUMN IF EXISTS "rf_last_seen";
ALTER TABLE "rule_to" DROP COLUMN IF EXISTS "rt_last_seen";
ALTER TABLE "rule_service" DROP COLUMN IF EXISTS "rs_last_seen";
ALTER TABLE "rule" DROP COLUMN IF EXISTS "rule_last_seen";

ALTER TABLE "rule" DROP CONSTRAINT IF EXISTS rule_removed_fkey;
ALTER TABLE "rule" ADD CONSTRAINT rule_removed_fkey
    FOREIGN KEY ("removed") REFERENCES "import_control" ("control_id")
    ON UPDATE RESTRICT ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS idx_rule_removed ON "rule" ("removed");

CREATE OR REPLACE VIEW public.rule_api AS
    SELECT
        rule_id, last_change_admin, rule_name, mgm_id, parent_rule_id, parent_rule_type, active, rule_num, rule_num_numeric,
        rule_ruleid, rule_uid, rule_disabled, rule_src_neg, rule_dst_neg, rule_svc_neg, action_id, track_id,
        rule_src, rule_dst, rule_svc, rule_src_refs, rule_dst_refs, rule_svc_refs, rule_from_zone, rule_to_zone,
        rule_action, rule_track, rule_installon, rule_time, rule_comment, rule_head_text, rule_implied, rule_create,
        dev_id, rule_custom_fields, access_rule, nat_rule, xlate_rule, is_global, rulebase_id, removed
    FROM rule;

CREATE OR REPLACE FUNCTION public.get_rulebase_for_owner(
    rulebase_row rulebase,
    ownerid integer
)
RETURNS SETOF rule_api
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
    RETURN QUERY
    SELECT *
    FROM (
        WITH src_rules AS (
            SELECT r.rule_id, r.rule_src_neg, r.rulebase_id, rf_o.obj_ip, rf_o.obj_ip_end, rf.negated
            FROM rule_api r
            LEFT JOIN rule_from rf ON r.rule_id = rf.rule_id
            LEFT JOIN objgrp_flat rf_of ON rf.obj_id = rf_of.objgrp_flat_id
            LEFT JOIN object rf_o ON rf_of.objgrp_flat_member_id = rf_o.obj_id
            WHERE r.rulebase_id = rulebase_row.id
              AND r.active = true
              AND rule_head_text IS NULL
        ),
        dst_rules AS (
            SELECT r.rule_id, r.rule_dst_neg, r.rulebase_id, rt_o.obj_ip, rt_o.obj_ip_end, rt.negated
            FROM rule_api r
            LEFT JOIN rule_to rt ON r.rule_id = rt.rule_id
            LEFT JOIN objgrp_flat rt_of ON rt.obj_id = rt_of.objgrp_flat_id
            LEFT JOIN object rt_o ON rt_of.objgrp_flat_member_id = rt_o.obj_id
            WHERE r.rulebase_id = rulebase_row.id
              AND r.active = true
              AND rule_head_text IS NULL
        )
        SELECT r.*
        FROM src_rules s
        LEFT JOIN owner_network onw ON ip_ranges_overlap(s.obj_ip, s.obj_ip_end, ip, ip_end, s.negated != s.rule_src_neg)
        JOIN rule_api r ON r.rule_id = s.rule_id
        WHERE onw.owner_id = ownerid
        UNION
        SELECT r.*
        FROM dst_rules d
        LEFT JOIN owner_network onw ON ip_ranges_overlap(d.obj_ip, d.obj_ip_end, ip, ip_end, d.negated != d.rule_dst_neg)
        JOIN rule_api r ON r.rule_id = d.rule_id
        WHERE onw.owner_id = ownerid
    ) AS combined
    ORDER BY rule_name ASC;
END;
$function$;
