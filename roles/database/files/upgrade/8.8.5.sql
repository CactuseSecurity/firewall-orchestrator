-- rename changes_found column to rule_changes_found in import_control table
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'import_control'
          AND column_name = 'changes_found'
    ) THEN
        EXECUTE 'ALTER TABLE import_control RENAME COLUMN changes_found TO rule_changes_found';
    END IF;
END
$$;

-- add any_changes_found column to import_control table
ALTER table "import_control"
    ADD COLUMN IF NOT EXISTS "any_changes_found" Boolean Default FALSE;


-- now set the any_changes_found column to true for all imports that have security relevant changes

DROP VIEW IF EXISTS view_imports_with_security_relevant_changes;

CREATE OR REPLACE VIEW view_imports_with_security_relevant_changes AS
    SELECT clr.control_id AS import_id, clr.mgm_id
    FROM changelog_rule clr
    WHERE clr.security_relevant

    UNION

    SELECT clo.control_id AS import_id, clo.mgm_id
    FROM changelog_object clo
    WHERE clo.security_relevant

    UNION

    SELECT cls.control_id AS import_id, cls.mgm_id
    FROM changelog_service cls
    WHERE cls.security_relevant

    UNION

    SELECT clu.control_id AS import_id, clu.mgm_id
    FROM changelog_user clu
    WHERE clu.security_relevant;


UPDATE import_control
SET any_changes_found = true
WHERE control_id IN (
    SELECT import_id
    FROM view_imports_with_security_relevant_changes
);

DROP VIEW IF EXISTS view_imports_with_security_relevant_changes;
