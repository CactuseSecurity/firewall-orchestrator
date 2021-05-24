-- todo:
-- parse inline layers
-- UI
-- add rule number counting for layes (2.1, 2.2, ...)
-- make layers collapsible

CREATE OR REPLACE FUNCTION are_equal (smallint, smallint)
    RETURNS boolean
    AS $$
BEGIN
    RAISE DEBUG 'are_equal_smallint start';
    -- RAISE DEBUG 'are_equal_smallint 1, p1=%, p2=%', $1, $1;
    IF (($1 IS NULL AND $2 IS NULL) OR ((NOT $1 IS NULL AND NOT $2 IS NULL) AND $1 = $2)) THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$
LANGUAGE plpgsql;

CREATE TABLE IF NOT EXISTS "parent_rule_type" (
    "id" smallserial,
    "name" varchar NOT NULL,
    PRIMARY KEY ("id")
);

INSERT INTO parent_rule_type (id, name)
    VALUES (1, 'section');

-- do not restart numbering
INSERT INTO parent_rule_type (id, name)
    VALUES (2, 'guarded-layer');

-- restart numbering, rule restrictions are ANDed to all rules below it, layer is not entered if guard does not apply
INSERT INTO parent_rule_type (id, name)
    VALUES (3, 'unguarded-layer');

-- restart numbering, no further restrictions
ALTER TABLE "rule"
    ADD COLUMN IF NOT EXISTS "parent_rule_id" BIGINT;

ALTER TABLE "rule"
    ADD COLUMN IF NOT EXISTS "parent_rule_type" smallint;

ALTER TABLE "import_rule"
    ADD COLUMN IF NOT EXISTS "parent_rule_uid" Text;

ALTER TABLE "rule"
    DROP CONSTRAINT IF EXISTS "rule_rule_parent_rule_id_fkey" CASCADE;

ALTER TABLE "rule"
    DROP CONSTRAINT IF EXISTS "rule_parent_rule_type_id_fkey" CASCADE;

ALTER TABLE "rule"
    ADD CONSTRAINT rule_rule_parent_rule_id_fkey FOREIGN KEY ("parent_rule_id") REFERENCES "rule" ("rule_id") ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE "rule"
    ADD CONSTRAINT rule_parent_rule_type_id_fkey FOREIGN KEY ("parent_rule_type") REFERENCES "parent_rule_type" ("id") ON UPDATE RESTRICT ON DELETE CASCADE;

