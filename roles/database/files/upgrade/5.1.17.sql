-- todo:
	-- parse inline layers
	-- UI
		-- add rule number counting for layes (2.1, 2.2, ...)
		-- make layers collapsible

CREATE OR REPLACE FUNCTION are_equal(SMALLINT,SMALLINT) RETURNS BOOLEAN AS $$
BEGIN
IF (($1 IS NULL AND $2 IS NULL) OR $1=$2) THEN
	RETURN TRUE;
ELSE 
	RETURN FALSE;
END IF;
END;
$$ LANGUAGE plpgsql;

Create table if not exists "parent_rule_type"
(
	"id" smallserial,
	"name" Varchar NOT NULL,
 primary key ("id")
);

insert into parent_rule_type (id, name) VALUES (1, 'section');          -- do not restart numbering
insert into parent_rule_type (id, name) VALUES (2, 'guarded-layer');    -- restart numbering, rule restrictions are ANDed to all rules below it, layer is not entered if guard does not apply
insert into parent_rule_type (id, name) VALUES (3, 'unguarded-layer');  -- restart numbering, no further restrictions

Alter table "rule" ADD COLUMN IF NOT EXISTS "parent_rule_id" BIGINT;
Alter table "rule" ADD COLUMN IF NOT EXISTS "parent_rule_type" smallint;

Alter table "import_rule" ADD COLUMN IF NOT EXISTS "parent_rule_uid" Text;

Alter table "rule" drop constraint if exists "rule_rule_parent_rule_id_fkey" CASCADE;
Alter table "rule" drop constraint if exists "rule_parent_rule_type_id_fkey" CASCADE;
Alter table "rule" add constraint rule_rule_parent_rule_id_fkey foreign key ("parent_rule_id") references "rule" ("rule_id") on update restrict on delete cascade;
Alter table "rule" add constraint rule_parent_rule_type_id_fkey foreign key ("parent_rule_type") references "parent_rule_type" ("id") on update restrict on delete cascade;
