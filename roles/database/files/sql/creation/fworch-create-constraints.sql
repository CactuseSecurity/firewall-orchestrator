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
ALTER TABLE object ADD CONSTRAINT object_obj_ip_not_null CHECK (obj_ip IS NOT NULL OR obj_typ_id=2);
ALTER TABLE object ADD CONSTRAINT object_obj_ip_end_not_null CHECK (obj_ip_end IS NOT NULL OR obj_typ_id=2);
ALTER TABLE owner ADD CONSTRAINT owner_name_unique_in_tenant UNIQUE ("name","tenant_id");
ALTER TABLE owner_network ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_unique UNIQUE (owner_id, ip);
ALTER TABLE request.reqelement ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
ALTER TABLE request.implelement ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
Alter Table "rule" add Constraint "rule_altkey" UNIQUE ("dev_id","rule_uid","rule_create",xlate_rule);
Alter Table "rule_metadata" add Constraint "rule_metadata_alt_key" UNIQUE ("rule_uid","dev_id");
Alter Table "service" add Constraint "svc_altkey" UNIQUE ("mgm_id","svc_uid","svc_create");
Alter Table "stm_dev_typ" add Constraint "Alter_Key1" UNIQUE ("dev_typ_name","dev_typ_version");
Alter Table "usr" add Constraint "usr_altkey" UNIQUE ("mgm_id","user_name","user_create");
Alter Table "zone" add Constraint "Alter_Key10" UNIQUE ("mgm_id","zone_name");

create unique index if not exists only_one_future_recert_per_owner_per_rule on recertification(owner_id,rule_metadata_id,recert_date)
    where recert_date IS NULL;

--- compliance
CREATE EXTENSION IF NOT EXISTS btree_gist;
ALTER TABLE compliance.ip_range ADD CONSTRAINT "exclude_overlapping_ip_ranges"
EXCLUDE USING gist (
    network_zone_id WITH =,
    numrange(ip_range_start - '0.0.0.0'::inet, ip_range_end - '0.0.0.0'::inet, '[]') WITH &&
);
