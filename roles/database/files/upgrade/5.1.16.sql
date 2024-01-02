-- drop table rule_nwobj_resolved;
-- drop table rule_svc_resolved;
-- drop table rule_user_resolved;

Create table if not exists "rule_svc_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"svc_id" BIGINT NOT NULL,
 primary key ("mgm_id","rule_id","svc_id")
);

Create table if not exists "rule_nwobj_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"obj_id" BIGINT NOT NULL,
 primary key ("mgm_id","rule_id","obj_id")
);

Create table if not exists "rule_user_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"user_id" BIGINT NOT NULL,
 primary key ("mgm_id","rule_id","user_id")
);

DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_nwobj_resolved_obj_id_fkey')
  THEN
		Alter table "rule_nwobj_resolved" add foreign key ("obj_id") references "object" ("obj_id") on update restrict on delete cascade;
  END IF;
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_nwobj_resolved_rule_id_fkey')
  THEN
		Alter table "rule_nwobj_resolved" add foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
  END IF;
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_nwobj_resolved_mgm_id_fkey')
  THEN
		Alter table "rule_nwobj_resolved" add foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
  END IF;

  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_svc_resolved_svc_id_fkey')
  THEN
		Alter table "rule_svc_resolved" add foreign key ("svc_id") references "service" ("svc_id") on update restrict on delete cascade;
  END IF;
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_svc_resolved_rule_id_fkey')
  THEN
		Alter table "rule_svc_resolved" add foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
  END IF;
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_svc_resolved_mgm_id_fkey')
  THEN
		Alter table "rule_svc_resolved" add foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
  END IF;

  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_user_resolved_user_id_fkey')
  THEN
		Alter table "rule_user_resolved" add foreign key ("user_id") references "usr" ("user_id") on update restrict on delete cascade;
  END IF;
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_user_resolved_rule_id_fkey')
  THEN
		Alter table "rule_user_resolved" add foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
  END IF;
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'rule_user_resolved_mgm_id_fkey')
  THEN
		Alter table "rule_user_resolved" add foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;
  END IF;
END $$;

Grant insert on "rule_nwobj_resolved" to group "configimporters";
Grant insert on "rule_svc_resolved" to group "configimporters";
Grant insert on "rule_user_resolved" to group "configimporters";

-----------------------------------------------------

CREATE OR REPLACE FUNCTION import_rule_resolved_nwobj (INT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_nw_obj_id ALIAS FOR $3;
    i_member BIGINT;
    i_obj_id_searched BIGINT;
BEGIN 
    SELECT INTO i_obj_id_searched obj_id FROM rule_nwobj_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND obj_id=i_nw_obj_id;
    IF NOT FOUND THEN
        INSERT INTO rule_nwobj_resolved (mgm_id, rule_id, obj_id) VALUES (i_mgm_id, i_rule_id, i_nw_obj_id);
    END IF;
	IF is_obj_group(i_nw_obj_id) THEN -- add all members seperately
        FOR i_member IN
            SELECT objgrp_flat_member_id FROM objgrp_flat WHERE objgrp_flat_id=i_nw_obj_id
        LOOP
            IF i_nw_obj_id <> i_member THEN 
                PERFORM import_rule_resolved_nwobj(i_mgm_id, i_rule_id, i_member);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION import_rule_resolved_svc (INT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
	i_svc_id ALIAS FOR $3;
    i_member BIGINT;
    i_svc_id_searched BIGINT;
BEGIN 
    SELECT INTO i_svc_id_searched svc_id FROM rule_svc_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND svc_id=i_svc_id;
    IF NOT FOUND THEN
        INSERT INTO rule_svc_resolved (mgm_id, rule_id, svc_id) VALUES (i_mgm_id, i_rule_id, i_svc_id);
    END IF;
	IF is_svc_group(i_svc_id) THEN -- add all flat group members seperately
        FOR i_member IN
            SELECT svcgrp_flat_member_id FROM svcgrp_flat WHERE svcgrp_flat_id=i_svc_id
        LOOP
            IF i_svc_id <> i_member THEN 
                PERFORM import_rule_resolved_svc(i_mgm_id, i_rule_id, i_member);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION import_rule_resolved_usr (INT,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_mgm_id ALIAS FOR $1;
	i_rule_id ALIAS FOR $2;
    i_usr_id ALIAS FOR $3;
    i_member BIGINT;
    i_usr_id_searched BIGINT;
BEGIN 
    SELECT INTO i_usr_id_searched user_id FROM rule_user_resolved WHERE mgm_id=i_mgm_id AND rule_id=i_rule_id AND user_id=i_usr_id;
    IF NOT FOUND THEN
        INSERT INTO rule_user_resolved (mgm_id, rule_id, user_id) VALUES (i_mgm_id, i_rule_id, i_usr_id);
    END IF;
	IF is_user_group(i_usr_id) THEN -- add all flat group members seperately
        FOR i_member IN
            SELECT usergrp_flat_member_id FROM usergrp_flat WHERE usergrp_flat_id=i_usr_id
        LOOP
            IF i_usr_id <> i_member THEN 
                PERFORM import_rule_resolved_usr(i_mgm_id, i_rule_id,i_member);
            END IF;
        END LOOP;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

-----------------------------------------------------

CREATE OR REPLACE FUNCTION import_rule_resolving_initial_filling() RETURNS VOID AS $$
DECLARE
	r_rule_obj_pair RECORD;
	i_mgm_id INT;
BEGIN
	FOR i_mgm_id IN SELECT mgm_id FROM management
	LOOP
		FOR r_rule_obj_pair IN SELECT rule_id, obj_id FROM device LEFT JOIN rule USING (dev_id) LEFT JOIN rule_from USING (rule_id) WHERE device.mgm_id=i_mgm_id
		LOOP
			IF NOT r_rule_obj_pair.obj_id IS NULL THEN 
				PERFORM import_rule_resolved_nwobj(i_mgm_id, r_rule_obj_pair.rule_id, r_rule_obj_pair.obj_id);
			END IF;
		END LOOP;

		FOR r_rule_obj_pair IN SELECT rule_id, obj_id FROM device LEFT JOIN rule USING (dev_id) LEFT JOIN rule_to USING (rule_id) WHERE device.mgm_id=i_mgm_id
		LOOP
			IF NOT r_rule_obj_pair.obj_id IS NULL THEN 
				PERFORM import_rule_resolved_nwobj(i_mgm_id, r_rule_obj_pair.rule_id, r_rule_obj_pair.obj_id);
			END IF;
		END LOOP;

		FOR r_rule_obj_pair IN SELECT rule_id, svc_id FROM device LEFT JOIN rule USING (dev_id) LEFT JOIN rule_service USING (rule_id) WHERE device.mgm_id=i_mgm_id
		LOOP
			IF NOT r_rule_obj_pair.svc_id IS NULL THEN 
				PERFORM import_rule_resolved_svc(i_mgm_id, r_rule_obj_pair.rule_id, r_rule_obj_pair.svc_id);
			END IF;
		END LOOP;

		FOR r_rule_obj_pair IN SELECT rule_id, user_id FROM device LEFT JOIN rule USING (dev_id) LEFT JOIN rule_from USING (rule_id) WHERE device.mgm_id=i_mgm_id
		LOOP
			IF NOT r_rule_obj_pair.user_id IS NULL THEN
				PERFORM import_rule_resolved_usr(i_mgm_id, r_rule_obj_pair.rule_id, r_rule_obj_pair.user_id);
			END IF;
		END LOOP;
	END LOOP;
	RETURN;
END;
$$ LANGUAGE plpgsql;

SELECT * FROM import_rule_resolving_initial_filling();

DROP FUNCTION import_rule_resolving_initial_filling();
