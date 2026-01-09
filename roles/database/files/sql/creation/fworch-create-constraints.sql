CREATE OR REPLACE FUNCTION is_single_ip (ip CIDR)
	RETURNS BOOLEAN
	LANGUAGE 'plpgsql' IMMUTABLE COST 1
	AS
$BODY$
	BEGIN
		RETURN masklen(ip)=32 AND family(ip)=4 OR masklen(ip)=128 AND family(ip)=6;
	END;
$BODY$;

Alter Table "changelog_object" add Constraint "alt_key_changelog_object" UNIQUE ("abs_change_id");
Alter Table "changelog_rule" add Constraint "alt_key_changelog_rule" UNIQUE ("abs_change_id");
Alter Table "changelog_service" add Constraint "alt_key_changelog_service" UNIQUE ("abs_change_id");
Alter Table "changelog_user" add Constraint "alt_key_changelog_user" UNIQUE ("abs_change_id");
Alter Table "import_changelog" add Constraint "Alter_Key14" UNIQUE ("import_changelog_nr","control_id");
Alter Table "import_control" add Constraint "control_id_stop_time_unique" UNIQUE ("stop_time","control_id");
Alter Table "object" add Constraint "obj_altkey" UNIQUE ("mgm_id","zone_id","obj_uid","obj_create");
ALTER TABLE object ADD CONSTRAINT object_obj_ip_is_host CHECK (is_single_ip(obj_ip));
ALTER TABLE object ADD CONSTRAINT object_obj_ip_end_is_host CHECK (is_single_ip(obj_ip_end));
-- magic numbers here: 1 = host object, 3 = network object, 4 = range object
ALTER TABLE "object" ADD CONSTRAINT object_obj_ip_not_null CHECK (NOT (obj_ip IS NULL AND obj_typ_id IN (1, 3, 4)));
ALTER TABLE "object" ADD CONSTRAINT object_obj_ip_end_not_null CHECK (NOT (obj_ip_end IS NULL AND obj_typ_id IN (1, 3, 4)));
ALTER TABLE owner ADD CONSTRAINT owner_name_unique_in_tenant UNIQUE ("name","tenant_id");
ALTER TABLE owner_network ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_is_host CHECK (is_single_ip(ip));
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_end_is_host CHECK (is_single_ip(ip_end));
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_unique UNIQUE (owner_id, ip, ip_end, import_source);
ALTER TABLE request.reqelement ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
ALTER TABLE request.implelement ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
-- Alter Table "rule" add Constraint "rule_altkey" UNIQUE ("dev_id","rule_uid","rule_create",xlate_rule);
Alter Table "rule" ADD Constraint "rule_unique_mgm_id_rule_uid_rule_create_xlate_rule" UNIQUE ("mgm_id", "rule_uid","rule_create","xlate_rule");
-- Alter Table "rule_metadata" add Constraint "rule_metadata_alt_key" UNIQUE ("rule_uid","dev_id");
-- Alter Table "rule_metadata" add Constraint "rule_metadata_alt_key" UNIQUE ("rule_uid","dev_id","rulebase_id");
ALTER TABLE rule_metadata ADD Constraint "rule_metadata_rule_uid_unique" unique ("rule_uid");
ALTER TABLE rule_metadata ADD CONSTRAINT rule_metadata_mgm_id_rule_uid_unique UNIQUE (mgm_id, rule_uid);
Alter table "rulebase" add CONSTRAINT unique_rulebase_mgm_id_uid UNIQUE ("mgm_id", "uid");
Alter table "rulebase_link" add CONSTRAINT unique_rulebase_link
	UNIQUE (
	"gw_id",
	"from_rulebase_id",
	"from_rule_id",
	"to_rulebase_id",
	"created"
	);
Alter Table "service" add Constraint "svc_altkey" UNIQUE ("mgm_id","svc_uid","svc_create");
Alter Table "stm_dev_typ" add Constraint "Alter_Key1" UNIQUE ("dev_typ_name","dev_typ_version");
Alter Table "usr" add Constraint "usr_altkey" UNIQUE ("mgm_id","user_name","user_create");
CREATE UNIQUE INDEX if not exists "zone_mgm_id_zone_name_removed_is_null_unique" ON zone (mgm_id, zone_name) WHERE removed IS NULL;

create unique index if not exists only_one_future_recert_per_owner_per_rule on recertification(owner_id,rule_metadata_id,recert_date)
    where recert_date IS NULL;

--- compliance

CREATE EXTENSION IF NOT EXISTS btree_gist;
ALTER TABLE compliance.ip_range ADD CONSTRAINT "exclude_overlapping_ip_ranges"
EXCLUDE USING gist (
    network_zone_id WITH =,
    numrange(ip_range_start - '0.0.0.0'::inet, ip_range_end - '0.0.0.0'::inet, '[]') WITH &&
)
WHERE (removed IS NULL);

--- report template

ALTER TABLE report_template
ADD CONSTRAINT unique_report_template_name UNIQUE (report_template_name);


