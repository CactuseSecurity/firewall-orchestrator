ALTER TABLE IF EXISTS public.owner
    ADD COLUMN IF NOT EXISTS additional_info JSONB;

-- The following changes are related to the change from rule_from/to_zone (int, currently unused) to rule_src/dst_zone (text, containing joined zone names) in the rule table.

ALTER TABLE IF EXISTS public.rule
    ADD COLUMN IF NOT EXISTS rule_src_zone TEXT;

ALTER TABLE IF EXISTS public.rule
    ADD COLUMN IF NOT EXISTS rule_dst_zone TEXT;

DROP FUNCTION IF EXISTS public.get_rulebase_for_owner;
DROP VIEW IF EXISTS public.rule_api CASCADE;

CREATE OR REPLACE FUNCTION purge_view_rule_with_owner () RETURNS VOID AS $$
DECLARE
    r_temp_record RECORD;
BEGIN
    SELECT INTO r_temp_record schemaname, viewname FROM pg_catalog.pg_views
    WHERE schemaname NOT IN ('pg_catalog', 'information_schema') AND viewname = 'view_rule_with_owner'
    ORDER BY schemaname, viewname;
    IF FOUND THEN
        DROP VIEW IF EXISTS view_rule_with_owner CASCADE;
    END IF;
    DROP MATERIALIZED VIEW IF EXISTS view_rule_with_owner CASCADE;
    RETURN;
END;
$$ LANGUAGE plpgsql;

SELECT * FROM purge_view_rule_with_owner ();
DROP FUNCTION purge_view_rule_with_owner();

DROP VIEW IF EXISTS v_rule_with_src_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_dst_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_ip_owner CASCADE;
DROP VIEW IF EXISTS v_excluded_src_ips CASCADE;
DROP VIEW IF EXISTS v_excluded_dst_ips CASCADE;
DROP VIEW IF EXISTS v_rule_with_rule_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_rule_owner_1 CASCADE;
DROP VIEW IF EXISTS v_rule_ownership_mode CASCADE;
DROP VIEW IF EXISTS v_active_access_allow_rules CASCADE;

DO $$
BEGIN
    IF to_regclass('public.rule') IS NULL
       OR to_regclass('public.rule_from_zone') IS NULL
       OR to_regclass('public.rule_to_zone') IS NULL
       OR to_regclass('public.zone') IS NULL THEN
        RETURN;
    END IF;

    WITH source_zones AS (
        SELECT
            rfz.rule_id,
            string_agg(DISTINCT z.zone_name, '|' ORDER BY z.zone_name) AS joined_zone_names
        FROM public.rule_from_zone rfz
        JOIN public.zone z
            ON z.zone_id = rfz.zone_id
        WHERE z.zone_name IS NOT NULL
        GROUP BY rfz.rule_id
    )
    UPDATE public.rule r
    SET rule_src_zone = source_zones.joined_zone_names
    FROM source_zones
    WHERE r.rule_id = source_zones.rule_id
      AND COALESCE(r.rule_src_zone, '') <> source_zones.joined_zone_names;

    WITH destination_zones AS (
        SELECT
            rtz.rule_id,
            string_agg(DISTINCT z.zone_name, '|' ORDER BY z.zone_name) AS joined_zone_names
        FROM public.rule_to_zone rtz
        JOIN public.zone z
            ON z.zone_id = rtz.zone_id
        WHERE z.zone_name IS NOT NULL
        GROUP BY rtz.rule_id
    )
    UPDATE public.rule r
    SET rule_dst_zone = destination_zones.joined_zone_names
    FROM destination_zones
    WHERE r.rule_id = destination_zones.rule_id
      AND COALESCE(r.rule_dst_zone, '') <> destination_zones.joined_zone_names;
END
$$;

ALTER TABLE IF EXISTS public.rule
    DROP CONSTRAINT IF EXISTS rule_rule_from_zone_fkey;

ALTER TABLE IF EXISTS public.rule
    DROP CONSTRAINT IF EXISTS rule_rule_to_zone_fkey;

DROP INDEX IF EXISTS "IX_Relationship90";
DROP INDEX IF EXISTS "IX_Relationship91";

ALTER TABLE IF EXISTS public.rule
    DROP COLUMN IF EXISTS rule_from_zone;

ALTER TABLE IF EXISTS public.rule
    DROP COLUMN IF EXISTS rule_to_zone;

DROP VIEW IF EXISTS v_rule_with_src_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_dst_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_ip_owner CASCADE;
DROP VIEW IF EXISTS v_excluded_src_ips CASCADE;
DROP VIEW IF EXISTS v_excluded_dst_ips CASCADE;
DROP VIEW IF EXISTS v_rule_with_rule_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_rule_owner_1 CASCADE;
DROP VIEW IF EXISTS v_rule_ownership_mode CASCADE;
DROP VIEW IF EXISTS v_active_access_allow_rules CASCADE;

CREATE OR REPLACE VIEW v_active_access_allow_rules AS
    SELECT rule_id,
    rule_src, rule_dst, rule_svc,
    rule_svc_neg, rule_src_neg, rule_dst_neg,
    mgm_id, rule_uid,
    rule_num_numeric, rule_disabled,
    rule_src_refs, rule_dst_refs, rule_svc_refs,
    rule_src_zone, rule_dst_zone,
    rule_action, rule_track, track_id, action_id,
    rule_installon, rule_comment, rule_name, rule_implied, rule_custom_fields,
    rule_create, removed,
    is_global,
    rulebase_id
    FROM rule r
    WHERE r.active AND
        r.access_rule AND
        r.rule_head_text IS NULL AND
        NOT r.rule_disabled AND
        NOT r.action_id IN (2,3,7);

CREATE OR REPLACE VIEW v_rule_ownership_mode AS
    SELECT c.config_value AS mode FROM config c
    WHERE c.config_key = 'ruleOwnershipMode';

CREATE OR REPLACE VIEW v_rule_with_rule_owner AS
    SELECT r.rule_id, ow.id AS owner_id, ow.name AS owner_name, 'rule' AS matches,
        ow.recert_interval, max(rec.recert_date) AS rule_last_certified
    FROM v_active_access_allow_rules r
    LEFT JOIN rule_metadata met ON (r.rule_uid = met.rule_uid)
    LEFT JOIN rule_owner ro ON (ro.rule_metadata_id = met.rule_metadata_id)
    LEFT JOIN owner ow ON (ro.owner_id = ow.id)
    LEFT JOIN recertification rec ON (rec.rule_metadata_id = met.rule_metadata_id AND rec.owner_id = ow.id AND rec.recertified IS TRUE)
    WHERE NOT ow.id IS NULL
    GROUP BY r.rule_id, ow.id, ow.name, ow.recert_interval;

CREATE OR REPLACE VIEW v_rule_with_rule_owner_1 AS
    SELECT DISTINCT r.rule_id, r.rule_uid, r.rule_name, r.mgm_id, r.rulebase_id, ow.id AS owner_id, met.rule_metadata_id
    FROM v_active_access_allow_rules r
    JOIN rule_metadata met ON r.rule_uid = met.rule_uid
    JOIN rule_owner ro ON ro.rule_metadata_id = met.rule_metadata_id
    JOIN owner ow ON ro.owner_id = ow.id;

CREATE OR REPLACE VIEW v_excluded_src_ips AS
    SELECT DISTINCT o.obj_ip
    FROM v_rule_with_rule_owner r
    LEFT JOIN rule_from rf ON (r.rule_id = rf.rule_id)
    LEFT JOIN objgrp_flat of ON (rf.obj_id = of.objgrp_flat_id)
    LEFT JOIN object o ON (of.objgrp_flat_member_id = o.obj_id)
    WHERE NOT o.obj_ip = '0.0.0.0/0';

CREATE OR REPLACE VIEW v_excluded_dst_ips AS
    SELECT DISTINCT o.obj_ip
    FROM v_rule_with_rule_owner r
    LEFT JOIN rule_to rt ON (r.rule_id = rt.rule_id)
    LEFT JOIN objgrp_flat of ON (rt.obj_id = of.objgrp_flat_id)
    LEFT JOIN object o ON (of.objgrp_flat_member_id = o.obj_id)
    WHERE NOT o.obj_ip = '0.0.0.0/0';

CREATE OR REPLACE VIEW v_rule_with_src_owner AS
    SELECT
        r.rule_id, ow.id AS owner_id, ow.name AS owner_name,
        CASE
            WHEN onw.ip = onw.ip_end
            THEN SPLIT_PART(CAST(onw.ip AS VARCHAR), '/', 1)
            ELSE
                CASE
                    WHEN host(broadcast(inet_merge(onw.ip, onw.ip_end))) = host(onw.ip_end) AND
                         host(inet_merge(onw.ip, onw.ip_end)) = host(onw.ip)
                    THEN text(inet_merge(onw.ip, onw.ip_end))
                    ELSE CONCAT(SPLIT_PART(onw.ip::VARCHAR, '/', 1), '-', SPLIT_PART(onw.ip_end::VARCHAR, '/', 1))
                END
        END AS matching_ip,
        'source' AS match_in,
        ow.recert_interval, max(rec.recert_date) AS rule_last_certified
    FROM v_active_access_allow_rules r
    LEFT JOIN rule_from ON (r.rule_id = rule_from.rule_id)
    LEFT JOIN objgrp_flat of ON (rule_from.obj_id = of.objgrp_flat_id)
    LEFT JOIN object o ON (of.objgrp_flat_member_id = o.obj_id)
    LEFT JOIN owner_network onw ON (onw.ip_end >= o.obj_ip AND onw.ip <= o.obj_ip_end)
    LEFT JOIN owner ow ON (onw.owner_id = ow.id)
    LEFT JOIN rule_metadata met ON (r.rule_uid = met.rule_uid)
    LEFT JOIN recertification rec ON (rec.rule_metadata_id = met.rule_metadata_id AND rec.owner_id = ow.id AND rec.recertified IS TRUE)
    WHERE r.rule_id NOT IN (SELECT DISTINCT rwo.rule_id FROM v_rule_with_rule_owner rwo) AND
    CASE
        WHEN (SELECT mode FROM v_rule_ownership_mode) = 'exclusive' THEN (NOT o.obj_ip IS NULL) AND o.obj_ip NOT IN (SELECT * FROM v_excluded_src_ips)
        ELSE NOT o.obj_ip IS NULL
    END
    GROUP BY r.rule_id, o.obj_ip, o.obj_ip_end, onw.ip, onw.ip_end, ow.id, ow.name, ow.recert_interval;

CREATE OR REPLACE VIEW v_rule_with_dst_owner AS
    SELECT
        r.rule_id, ow.id AS owner_id, ow.name AS owner_name,
        CASE
            WHEN onw.ip = onw.ip_end
            THEN SPLIT_PART(CAST(onw.ip AS VARCHAR), '/', 1)
            ELSE
                CASE
                    WHEN host(broadcast(inet_merge(onw.ip, onw.ip_end))) = host(onw.ip_end) AND
                         host(inet_merge(onw.ip, onw.ip_end)) = host(onw.ip)
                    THEN text(inet_merge(onw.ip, onw.ip_end))
                    ELSE CONCAT(SPLIT_PART(onw.ip::VARCHAR, '/', 1), '-', SPLIT_PART(onw.ip_end::VARCHAR, '/', 1))
                END
        END AS matching_ip,
        'destination' AS match_in,
        ow.recert_interval, max(rec.recert_date) AS rule_last_certified
    FROM v_active_access_allow_rules r
    LEFT JOIN rule_to rt ON (r.rule_id = rt.rule_id)
    LEFT JOIN objgrp_flat of ON (rt.obj_id = of.objgrp_flat_id)
    LEFT JOIN object o ON (of.objgrp_flat_member_id = o.obj_id)
    LEFT JOIN owner_network onw ON (onw.ip_end >= o.obj_ip AND onw.ip <= o.obj_ip_end)
    LEFT JOIN owner ow ON (onw.owner_id = ow.id)
    LEFT JOIN rule_metadata met ON (r.rule_uid = met.rule_uid)
    LEFT JOIN recertification rec ON (rec.rule_metadata_id = met.rule_metadata_id AND rec.owner_id = ow.id AND rec.recertified IS TRUE)
    WHERE r.rule_id NOT IN (SELECT DISTINCT rwo.rule_id FROM v_rule_with_rule_owner rwo) AND
    CASE
        WHEN (SELECT mode FROM v_rule_ownership_mode) = 'exclusive' THEN (NOT o.obj_ip IS NULL) AND o.obj_ip NOT IN (SELECT * FROM v_excluded_dst_ips)
        ELSE NOT o.obj_ip IS NULL
    END
    GROUP BY r.rule_id, o.obj_ip, o.obj_ip_end, onw.ip, onw.ip_end, ow.id, ow.name, ow.recert_interval;

CREATE OR REPLACE VIEW v_rule_with_ip_owner AS
    SELECT DISTINCT uno.rule_id, uno.owner_id, uno.owner_name,
        string_agg(DISTINCT match_in || ':' || matching_ip::VARCHAR, '; ' ORDER BY match_in || ':' || matching_ip::VARCHAR DESC) AS matches,
        uno.recert_interval, uno.rule_last_certified
    FROM (SELECT DISTINCT * FROM v_rule_with_src_owner AS src UNION SELECT DISTINCT * FROM v_rule_with_dst_owner AS dst) AS uno
    GROUP BY uno.rule_id, uno.owner_id, uno.owner_name, uno.recert_interval, uno.rule_last_certified;

CREATE MATERIALIZED VIEW view_rule_with_owner AS
    SELECT DISTINCT ar.rule_id, ar.owner_id, ar.owner_name, ar.matches, ar.recert_interval, ar.rule_last_certified,
    r.rule_num_numeric, r.track_id, r.action_id, r.rule_src_zone, r.rule_dst_zone, r.mgm_id, r.rule_uid,
    r.rule_action, r.rule_name, r.rule_comment, r.rule_track, r.rule_src_neg, r.rule_dst_neg, r.rule_svc_neg,
    r.rule_head_text, r.rule_disabled, r.access_rule, r.xlate_rule, r.nat_rule
    FROM (SELECT * FROM v_rule_with_rule_owner AS rul UNION SELECT * FROM v_rule_with_ip_owner AS ips) AS ar
    LEFT JOIN rule AS r USING (rule_id);

GRANT SELECT ON TABLE view_rule_with_owner TO GROUP secuadmins, reporters, configimporters;

CREATE INDEX IF NOT EXISTS idx_view_rule_with_owner_rule_id ON view_rule_with_owner (rule_id);
CREATE INDEX IF NOT EXISTS idx_view_rule_with_owner_owner_id ON view_rule_with_owner (owner_id);
