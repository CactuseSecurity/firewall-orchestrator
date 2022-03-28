----------------------------------------------------
-- Maintenance functions for db cleanup
----------------------------------------------------

CREATE OR REPLACE FUNCTION delete_import(BIGINT) RETURNS VOID AS $$
DECLARE
	i_import_id ALIAS FOR $1; -- ID des zurueckzuruderndes Managements
BEGIN
	DELETE FROM import_service	WHERE control_id = i_import_id;
	DELETE FROM import_object	WHERE control_id = i_import_id;
	DELETE FROM import_rule		WHERE control_id = i_import_id;
	DELETE FROM import_zone		WHERE control_id = i_import_id;
	DELETE FROM import_user		WHERE control_id = i_import_id;
	DELETE FROM changelog_service	WHERE control_id = i_import_id;
	DELETE FROM changelog_object	WHERE control_id = i_import_id;
	DELETE FROM changelog_user	WHERE control_id = i_import_id;
	DELETE FROM changelog_rule	WHERE control_id = i_import_id;
	DELETE FROM service		WHERE svc_create=i_import_id;
	DELETE FROM object		WHERE obj_create=i_import_id;
	DELETE FROM usr			WHERE user_create=i_import_id;
	DELETE FROM rule		WHERE rule_create=i_import_id;
	DELETE FROM rule_order		WHERE control_id=i_import_id; 		-- Loeschen der Regelreihenfolge
	DELETE FROM import_control	WHERE control_id = i_import_id; 		-- abschliessend Loeschen des Control-Eintrags
	RETURN;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_stale_imports(BIGINT) RETURNS VOID AS $$
-- returns all import ids that are unneccessary
DECLARE
	i_import_id ALIAS FOR $1; -- ID des zurueckzuruderndes Managements
BEGIN
	-- to be filled
	RETURN;
END;
$$ LANGUAGE plpgsql;

--  loesche alle rule_order Eintraege, die nicht referenziert werden
-- DELETE FROM rule_order WHERE control_id IN (
--   SELECT control_id FROM import_control WHERE NOT control_id IN (
--      SELECT control_id AS id FROM import_control WHERE control_id IN
-- 	(
-- 		SELECT control_id AS id FROM changelog_service UNION
-- 		SELECT control_id AS id FROM changelog_rule UNION
-- 		SELECT control_id AS id FROM changelog_object UNION
-- 		SELECT control_id AS id FROM changelog_user UNION
-- 		SELECT rule_create AS id FROM rule UNION
-- 		SELECT rule_last_seen AS id FROM rule UNION
-- 		SELECT rt_create AS id FROM rule_to UNION
-- 		SELECT rt_last_seen AS id FROM rule_to UNION
-- 		SELECT rf_create AS id FROM rule_from UNION
-- 		SELECT rf_last_seen AS id FROM rule_from UNION
-- 		SELECT rs_create AS id FROM rule_service UNION
-- 		SELECT rs_last_seen AS id FROM rule_service UNION
-- 		SELECT import_created AS id FROM usergrp_flat UNION
-- 		SELECT import_last_seen AS id FROM usergrp_flat UNION
-- 		SELECT import_created AS id FROM usergrp UNION
-- 		SELECT import_last_seen AS id FROM usergrp UNION
-- 		SELECT import_created AS id FROM objgrp_flat UNION
-- 		SELECT import_last_seen AS id FROM objgrp_flat UNION
-- 		SELECT import_created AS id FROM objgrp UNION
-- 		SELECT import_last_seen AS id FROM objgrp UNION
-- 		SELECT import_created AS id FROM svcgrp_flat UNION
-- 		SELECT import_last_seen AS id FROM svcgrp_flat UNION
-- 		SELECT import_created AS id FROM svcgrp UNION
-- 		SELECT import_last_seen AS id FROM svcgrp UNION
-- 		SELECT obj_create AS id FROM object UNION
-- 		SELECT obj_last_seen AS id FROM object UNION
-- 		SELECT svc_create AS id FROM service UNION
-- 		SELECT svc_last_seen AS id FROM service

-- 	)
--     )
-- );


--  loesche alle import_control Eintraege, die nicht referenziert werden
DELETE FROM import_control WHERE control_id IN (
  SELECT control_id FROM import_control WHERE NOT control_id IN (
     SELECT control_id AS id FROM import_control WHERE control_id IN
	(
		SELECT control_id AS id FROM changelog_service UNION
		SELECT control_id AS id FROM changelog_rule UNION
		SELECT control_id AS id FROM changelog_object UNION
		SELECT control_id AS id FROM changelog_user UNION
		SELECT rule_create AS id FROM rule UNION
		SELECT rule_last_seen AS id FROM rule UNION
		SELECT rt_create AS id FROM rule_to UNION
		SELECT rt_last_seen AS id FROM rule_to UNION
		SELECT rf_create AS id FROM rule_from UNION
		SELECT rf_last_seen AS id FROM rule_from UNION
		SELECT rs_create AS id FROM rule_service UNION
		SELECT rs_last_seen AS id FROM rule_service UNION
		SELECT import_created AS id FROM usergrp_flat UNION
		SELECT import_last_seen AS id FROM usergrp_flat UNION
		SELECT import_created AS id FROM usergrp UNION
		SELECT import_last_seen AS id FROM usergrp UNION
		SELECT import_created AS id FROM objgrp_flat UNION
		SELECT import_last_seen AS id FROM objgrp_flat UNION
		SELECT import_created AS id FROM objgrp UNION
		SELECT import_last_seen AS id FROM objgrp UNION
		SELECT import_created AS id FROM svcgrp_flat UNION
		SELECT import_last_seen AS id FROM svcgrp_flat UNION
		SELECT import_created AS id FROM svcgrp UNION
		SELECT import_last_seen AS id FROM svcgrp UNION
		SELECT obj_create AS id FROM object UNION
		SELECT obj_last_seen AS id FROM object UNION
		SELECT svc_create AS id FROM service UNION
		SELECT svc_last_seen AS id FROM service

	)
    )
);


DROP index "firewall_akey";
DROP index "kunden_akey";
DROP index "kundennetze_akey";
DROP index "management_akey";
DROP index "rule_index";
DROP index "rule_from_unique_index";
DROP index "stm_color_akey";
DROP index "stm_fw_typ_a2key";
DROP index "stm_fw_typ_akey";
DROP index "stm_obj_typ_akey";
DROP index "import_control_start_time_idx";
DROP INDEX import_control_only_one_null_stop_time_per_mgm_when_null;
DROP index "IX_relationship11";
DROP index "IX_Relationship128";
DROP index "IX_Relationship186";
DROP index "IX_relationship7";
DROP index "IX_relationship4";
DROP index "IX_Relationship165";
DROP index "IX_Relationship188";
DROP index "IX_relationship5";
DROP index "IX_relationship8";
DROP index "IX_relationship21";
DROP index "IX_relationship17";
DROP index "IX_Relationship38";
DROP index "IX_Relationship43";
DROP index "IX_Relationship127";
DROP index "IX_Relationship129";
DROP index "IX_Relationship130";
DROP index "IX_Relationship131";
DROP index "IX_relationship13";
DROP index "IX_relationship14";
DROP index "IX_relationship26";
DROP index "IX_relationship28";
DROP index "IX_Relationship65";
DROP index "IX_Relationship66";
DROP index "IX_Relationship105";
DROP index "IX_Relationship106";
DROP index "IX_relationship25";
DROP index "IX_relationship29";
DROP index "IX_relationship27";
DROP index "IX_Relationship72";
DROP index "IX_Relationship73";
DROP index "IX_relationship30";
DROP index "IX_relationship19";
DROP index "IX_relationship20";
DROP index "IX_Relationship74";
DROP index "IX_Relationship75";
DROP index "IX_Relationship118";
DROP index "IX_Relationship119";
DROP index "IX_relationship23";
DROP index "IX_relationship12";
DROP index "IX_relationship18";
DROP index "IX_Relationship52";
DROP index "IX_relationship6";
DROP index "IX_Relationship83";
DROP index "IX_relationship10";
DROP index "IX_relationship9";
DROP index "IX_relationship24";
DROP index "IX_Relationship33";
DROP index "IX_Relationship36";
DROP index "IX_Relationship37";
DROP index "IX_Relationship90";
DROP index "IX_Relationship91";
DROP index "IX_Relationship50";
DROP index "IX_Relationship51";
DROP index "IX_Relationship79";
DROP index "IX_Relationship80";
DROP index "IX_Relationship95";
DROP index "IX_Relationship149";
DROP index "IX_Relationship150";
DROP index "IX_Relationship59";
DROP index "IX_Relationship60";
DROP index "IX_Relationship61";
DROP index "IX_Relationship62";
DROP index "IX_Relationship68";
DROP index "IX_Relationship76";
DROP index "IX_Relationship77";
DROP index "IX_Relationship78";
DROP index "IX_Relationship107";
DROP index "IX_Relationship108";
DROP index "IX_Relationship120";
DROP index "IX_Relationship121";
DROP index "IX_Relationship122";
DROP index "IX_Relationship123";
DROP index "IX_Relationship124";
DROP index "IX_Relationship125";
DROP index "IX_Relationship132";
DROP index "IX_Relationship151";
DROP index "IX_Relationship152";
DROP index "IX_Relationship153";
DROP index "IX_Relationship154";
DROP index "IX_Relationship166";
DROP index "IX_Relationship167";
DROP index "IX_Relationship168";
DROP index "IX_Relationship169";
DROP index "IX_Relationship170";
DROP index "IX_Relationship171";
DROP index "IX_Relationship172";
DROP index "IX_Relationship173";
DROP index "IX_Relationship174";
DROP index "IX_Relationship175";
DROP index "IX_Relationship176";
DROP index "IX_Relationship177";
DROP index "IX_Relationship178";
DROP index "IX_Relationship179";
DROP index "IX_Relationship185";
DROP index "IX_Relationship63";
DROP index "IX_Relationship69";
DROP index "IX_Relationship70";
DROP index "IX_Relationship71";
DROP index "IX_Relationship109";
DROP index "IX_Relationship110";
DROP index "IX_Relationship111";
DROP index "IX_Relationship112";
DROP index "IX_Relationship159";
DROP index "IX_Relationship161";
DROP index "IX_Relationship162";
DROP index "IX_Relationship163";
DROP index "IX_Relationship93";
DROP index "IX_Relationship155";
DROP index "IX_Relationship156";
DROP index "IX_Relationship157";
DROP index "IX_Relationship158";

