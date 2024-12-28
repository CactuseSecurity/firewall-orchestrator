/*
	logic for checking overlap of ip ranges:
	not (end_ip1 < start_ip2 or start_ip1 > end_ip2)
	=
	end_ip1 >= start_ip2 and start_ip1 <= end_ip2

	ip1 = owner_network.ip
	ip2 = object.ip

	--> 
		owner_network.ip_end >= object.ip and owner_network.ip <= object.ip_end

		here:
	--> 
		owner_network.ip_end >= o.obj_ip and owner_network.ip <= o.obj_ip_end

*/

DROP VIEW IF EXISTS v_rule_with_src_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_dst_owner CASCADE;
DROP VIEW IF EXISTS v_rule_with_ip_owner CASCADE;

CREATE OR REPLACE VIEW v_active_access_allow_rules AS 
	SELECT * FROM rule r
	WHERE r.active AND 					-- only show current (not historical) rules 
		r.access_rule AND 				-- only show access rules (no NAT)
		r.rule_head_text IS NULL AND 	-- do not show header rules
		NOT r.rule_disabled AND 		-- do not show disabled rules
		NOT r.action_id IN (2,3,7);		-- do not deal with deny rules

CREATE OR REPLACE VIEW v_rule_ownership_mode AS
	SELECT c.config_value as mode FROM config c
	WHERE c.config_key = 'ruleOwnershipMode';

CREATE OR REPLACE VIEW v_rule_with_rule_owner AS
	SELECT r.rule_id, ow.id as owner_id, ow.name as owner_name, 'rule' AS matches,
		ow.recert_interval, met.rule_last_certified, met.rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid AND r.dev_id=met.dev_id)
	LEFT JOIN rule_owner ro ON (ro.rule_metadata_id=met.rule_metadata_id)
	LEFT JOIN owner ow ON (ro.owner_id=ow.id)
	WHERE NOT ow.id IS NULL
	GROUP BY r.rule_id, ow.id, ow.name, met.rule_last_certified, met.rule_last_certifier;

CREATE OR REPLACE VIEW v_excluded_src_ips AS
	SELECT distinct o.obj_ip
	FROM v_rule_with_rule_owner r
	LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
	LEFT JOIN objgrp_flat of ON (rf.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	WHERE NOT o.obj_ip='0.0.0.0/0';

CREATE OR REPLACE VIEW v_excluded_dst_ips AS
	SELECT distinct o.obj_ip
	FROM v_rule_with_rule_owner r
	LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
	LEFT JOIN objgrp_flat of ON (rt.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	WHERE NOT o.obj_ip='0.0.0.0/0';

    -- if start_ip1 <= end_ip2 and start_ip2 <= end_ip1:
    --     overlap_start = max(start_ip1, start_ip2)
    --     overlap_end = min(end_ip1, end_ip2)
    --     return (overlap_start, overlap_end)
    -- else:
    --     return None  # No overlap

CREATE OR REPLACE VIEW v_rule_with_src_owner AS 
	SELECT
		r.rule_id, ow.id as owner_id, ow.name as owner_name, 
		CASE
			WHEN onw.ip = onw.ip_end
			THEN SPLIT_PART(CAST(onw.ip AS VARCHAR), '/', 1) -- Single IP overlap, removing netmask
			ELSE
				CASE WHEN	-- range is a single network
					host(broadcast(inet_merge(onw.ip, onw.ip_end))) = host (onw.ip_end) AND
					host(inet_merge(onw.ip, onw.ip_end)) = host (onw.ip)
				THEN
					text(inet_merge(onw.ip, onw.ip_end))
				ELSE
					CONCAT(SPLIT_PART(onw.ip::VARCHAR,'/', 1), '-', SPLIT_PART(onw.ip_end::VARCHAR, '/', 1))
				END
		END AS matching_ip,
		'source' AS match_in,
		ow.recert_interval, met.rule_last_certified, met.rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_from ON (r.rule_id=rule_from.rule_id)
	LEFT JOIN objgrp_flat of ON (rule_from.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network onw ON (onw.ip_end >= o.obj_ip AND onw.ip <= o.obj_ip_end)
	LEFT JOIN owner ow ON (onw.owner_id=ow.id)
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid AND r.dev_id=met.dev_id)
	WHERE r.rule_id NOT IN (SELECT distinct rwo.rule_id FROM v_rule_with_rule_owner rwo) AND
	CASE
		when (select mode from v_rule_ownership_mode) = 'exclusive' then (NOT o.obj_ip IS NULL) AND o.obj_ip NOT IN (select * from v_excluded_src_ips)
		else NOT o.obj_ip IS NULL
	END
	GROUP BY r.rule_id, o.obj_ip, o.obj_ip_end, onw.ip, onw.ip_end, ow.id, ow.name, met.rule_last_certified, met.rule_last_certifier;

CREATE OR REPLACE VIEW v_rule_with_dst_owner AS 
	SELECT 
		r.rule_id, ow.id as owner_id, ow.name as owner_name, 
		CASE
			WHEN onw.ip = onw.ip_end
			THEN SPLIT_PART(CAST(onw.ip AS VARCHAR), '/', 1) -- Single IP overlap, removing netmask
			ELSE
				CASE WHEN	-- range is a single network
					host(broadcast(inet_merge(onw.ip, onw.ip_end))) = host (onw.ip_end) AND
					host(inet_merge(onw.ip, onw.ip_end)) = host (onw.ip)
				THEN
					text(inet_merge(onw.ip, onw.ip_end))
				ELSE
					CONCAT(SPLIT_PART(onw.ip::VARCHAR,'/', 1), '-', SPLIT_PART(onw.ip_end::VARCHAR, '/', 1))
				END
		END AS matching_ip,
		'destination' AS match_in,
		ow.recert_interval, met.rule_last_certified, met.rule_last_certifier
	FROM v_active_access_allow_rules r
	LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
	LEFT JOIN objgrp_flat of ON (rt.obj_id=of.objgrp_flat_id)
	LEFT JOIN object o ON (of.objgrp_flat_member_id=o.obj_id)
	LEFT JOIN owner_network onw ON (onw.ip_end >= o.obj_ip AND onw.ip <= o.obj_ip_end)
	LEFT JOIN owner ow ON (onw.owner_id=ow.id)
	LEFT JOIN rule_metadata met ON (r.rule_uid=met.rule_uid AND r.dev_id=met.dev_id)
	WHERE r.rule_id NOT IN (SELECT distinct rwo.rule_id FROM v_rule_with_rule_owner rwo) AND
	CASE
		when (select mode from v_rule_ownership_mode) = 'exclusive' then (NOT o.obj_ip IS NULL) AND o.obj_ip NOT IN (select * from v_excluded_dst_ips)
		else NOT o.obj_ip IS NULL
	END
	GROUP BY r.rule_id, o.obj_ip, o.obj_ip_end, onw.ip, onw.ip_end, ow.id, ow.name, met.rule_last_certified, met.rule_last_certifier;

CREATE OR REPLACE VIEW v_rule_with_ip_owner AS
	SELECT DISTINCT	uno.rule_id, uno.owner_id, uno.owner_name,
		string_agg(DISTINCT match_in || ':' || matching_ip::VARCHAR, '; ' order by match_in || ':' || matching_ip::VARCHAR desc) as matches,
		uno.recert_interval, uno.rule_last_certified, uno.rule_last_certifier
	FROM ( SELECT DISTINCT * FROM v_rule_with_src_owner AS src UNION SELECT DISTINCT * FROM v_rule_with_dst_owner AS dst) AS uno
	GROUP BY uno.rule_id, uno.owner_id, uno.owner_name, uno.recert_interval, uno.rule_last_certified, uno.rule_last_certifier;

CREATE OR REPLACE FUNCTION purge_view_rule_with_owner () RETURNS VOID AS $$
DECLARE
    r_temp_record RECORD;
BEGIN
    select INTO r_temp_record schemaname, viewname from pg_catalog.pg_views
    where schemaname NOT IN ('pg_catalog', 'information_schema') and viewname='view_rule_with_owner'
    order by schemaname, viewname;
    IF FOUND THEN
        DROP VIEW IF EXISTS view_rule_with_owner CASCADE;
    END IF;
    DROP MATERIALIZED VIEW IF EXISTS view_rule_with_owner CASCADE;
    RETURN;
END;
$$ LANGUAGE plpgsql;

SELECT * FROM purge_view_rule_with_owner ();
DROP FUNCTION purge_view_rule_with_owner();

-- LargeOwnerChange: remove MATERIALIZED for small installations
-- SmallOwnerChange: add MATERIALIZED for large installations
CREATE MATERIALIZED VIEW view_rule_with_owner AS
	SELECT DISTINCT ar.rule_id, ar.owner_id, ar.owner_name, ar.matches, ar.recert_interval, ar.rule_last_certified, ar.rule_last_certifier,
	r.rule_num_numeric, r.track_id, r.action_id, r.rule_from_zone, r.rule_to_zone, r.dev_id, r.mgm_id, r.rule_uid,
	r.rule_action, r.rule_name, r.rule_comment, r.rule_track, r.rule_src_neg, r.rule_dst_neg, r.rule_svc_neg,
	r.rule_head_text, r.rule_disabled, r.access_rule, r.xlate_rule, r.nat_rule
	FROM ( SELECT DISTINCT * FROM v_rule_with_rule_owner AS rul UNION SELECT DISTINCT * FROM v_rule_with_ip_owner AS ips) AS ar
	LEFT JOIN rule AS r USING (rule_id)
	GROUP BY ar.rule_id, ar.owner_id, ar.owner_name, ar.matches, ar.recert_interval, ar.rule_last_certified, ar.rule_last_certifier,
		r.rule_num_numeric, r.track_id, r.action_id, r.rule_from_zone, r.rule_to_zone, r.dev_id, r.mgm_id, r.rule_uid,
		r.rule_action, r.rule_name, r.rule_comment, r.rule_track, r.rule_src_neg, r.rule_dst_neg, r.rule_svc_neg,
		r.rule_head_text, r.rule_disabled, r.access_rule, r.xlate_rule, r.nat_rule;

-- refresh materialized view view_rule_with_owner;

-------------------------
-- recert refresh trigger

-- create or replace function refresh_view_rule_with_owner()
-- returns trigger language plpgsql
-- as $$
-- begin
--     refresh materialized view view_rule_with_owner;
--     return null;
-- end $$;

-- drop trigger IF exists refresh_view_rule_with_owner_delete_trigger ON recertification CASCADE;

-- create trigger refresh_view_rule_with_owner_delete_trigger
-- after delete on recertification for each statement 
-- execute procedure refresh_view_rule_with_owner();

GRANT SELECT ON TABLE view_rule_with_owner TO GROUP secuadmins, reporters, configimporters;


CREATE TABLE IF NOT EXISTS refresh_log (
    id SERIAL PRIMARY KEY,
    view_name TEXT NOT NULL,
    refreshed_at TIMESTAMPTZ DEFAULT now(),
    status TEXT
);

CREATE OR REPLACE FUNCTION refresh_view_rule_with_owner()
RETURNS SETOF refresh_log AS $$
DECLARE
    status_message TEXT;
BEGIN
    -- Attempt to refresh the materialized view
    BEGIN
        REFRESH MATERIALIZED VIEW view_rule_with_owner;
        status_message := 'Materialized view refreshed successfully';
    EXCEPTION
        WHEN OTHERS THEN
            status_message := format('Failed to refresh view: %s', SQLERRM);
    END;

    -- Log the operation
    INSERT INTO refresh_log (view_name, status)
    VALUES ('view_rule_with_owner', status_message);

    -- Return the log entry
    RETURN QUERY SELECT * FROM refresh_log WHERE view_name = 'view_rule_with_owner' ORDER BY refreshed_at DESC LIMIT 1;
END;
$$ LANGUAGE plpgsql VOLATILE;


-- Create indexes on the materialized view
CREATE INDEX IF NOT EXISTS idx_view_rule_with_owner_rule_id ON view_rule_with_owner (rule_id);
CREATE INDEX IF NOT EXISTS idx_view_rule_with_owner_owner_id ON view_rule_with_owner (owner_id);
