-- rule_owner table
ALTER TABLE rule_owner
ADD COLUMN IF NOT EXISTS rule_id bigint,
ADD COLUMN IF NOT EXISTS created bigint,
ADD COLUMN IF NOT EXISTS removed bigint,
ADD COLUMN IF NOT EXISTS owner_mapping_source_id bigint; -- stm_ for source (ip_based, custom_field, name_field, manual) todo

-- backfill new columns for existing rows
DO $$
DECLARE
    latest_import_id bigint;
BEGIN
    SELECT MAX(control_id) INTO latest_import_id FROM import_control;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'rule_owner'
          AND column_name = 'rule_id'
    ) THEN
        UPDATE rule_owner ro
        SET rule_id = r.rule_id
        FROM rule_metadata met
        JOIN rule r ON r.rule_uid = met.rule_uid AND r.mgm_id = met.mgm_id
        WHERE ro.rule_metadata_id = met.rule_metadata_id
          AND ro.rule_id IS NULL;
    END IF;

    IF latest_import_id IS NOT NULL THEN
        UPDATE rule_owner
        SET created = latest_import_id
        WHERE created IS NULL;
    END IF;

    UPDATE rule_owner
    SET owner_mapping_source_id = 4
    WHERE owner_mapping_source_id IS NULL;
END $$;


-- set not null if not done
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'rule_owner'
          AND column_name = 'rule_id'
          AND is_nullable = 'YES'
    ) THEN
        ALTER TABLE rule_owner ALTER COLUMN rule_id SET NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'rule_owner'
          AND column_name = 'created'
          AND is_nullable = 'YES'
    ) THEN
        ALTER TABLE rule_owner ALTER COLUMN created SET NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'rule_owner'
          AND column_name = 'owner_id'
          AND is_nullable = 'YES'
    ) THEN
        ALTER TABLE rule_owner ALTER COLUMN owner_id SET NOT NULL;
    END IF;

    IF EXISTS (
		SELECT 1 FROM information_schema.columns
		WHERE table_name = 'rule_owner'
		  AND column_name = 'owner_mapping_source_id'
		  AND is_nullable = 'YES'
	) THEN
		ALTER TABLE rule_owner ALTER COLUMN owner_mapping_source_id SET NOT NULL;
	END IF;
END $$;

-- set primary key
DO $$
BEGIN
    DELETE FROM rule_owner ro
    USING (
        SELECT rule_id, owner_id, created, MIN(ctid) AS keep_ctid
        FROM rule_owner
        GROUP BY rule_id, owner_id, created
        HAVING COUNT(*) > 1
    ) dups
    WHERE ro.rule_id = dups.rule_id
      AND ro.owner_id = dups.owner_id
      AND ro.created = dups.created
      AND ro.ctid <> dups.keep_ctid;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_name = 'rule_owner'
          AND constraint_type = 'PRIMARY KEY'
    ) AND NOT EXISTS (
        SELECT 1 FROM rule_owner
        WHERE rule_id IS NULL OR owner_id IS NULL OR created IS NULL
    ) THEN
        ALTER TABLE rule_owner
        ADD CONSTRAINT pk_rule_owner
        PRIMARY KEY (rule_id, owner_id, created);
    END IF;
END $$;

-- owner source table
CREATE TABLE if not EXISTS stm_owner_mapping_source
(
    "owner_mapping_source_type_id" BIGINT PRIMARY KEY,
    "owner_mapping_source_type_name" Varchar NOT NULL
);
-- add owner source
INSERT INTO stm_owner_mapping_source (owner_mapping_source_type_id, owner_mapping_source_type_name)
VALUES
    (1, 'ip_based'),
    (2, 'custom_field'),
    (3, 'name_field'),
    (4, 'manual')
ON CONFLICT (owner_mapping_source_type_id) DO NOTHING;

-- just one "active" (rule_id, owner_id) + performance
CREATE UNIQUE INDEX IF NOT EXISTS idx_rule_owner_removed_is_null_unique ON rule_owner (rule_id, owner_id) WHERE removed IS NULL;

-- create fks
ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_rule_foreign_key;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_rule_foreign_key FOREIGN KEY (rule_id) REFERENCES rule(rule_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_created_import_control_foreign_key;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_created_import_control_foreign_key FOREIGN KEY (created) REFERENCES import_control(control_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_removed_import_control_foreign_key;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_removed_import_control_foreign_key FOREIGN KEY (removed) REFERENCES import_control(control_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_owner_mapping_source_id_stm_owner_mapping_source_foreign_key;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_owner_mapping_source_id_stm_owner_mapping_source_foreign_key FOREIGN KEY (owner_mapping_source_id) 
REFERENCES stm_owner_mapping_source(owner_mapping_source_type_id) ON UPDATE RESTRICT ON DELETE CASCADE;


-- import_control 
-- alter import_control delete unused/exported columns 
-- owner source table
CREATE TABLE if not EXISTS stm_import
(
    "import_type_id" Integer PRIMARY KEY,
    "import_type_name" Varchar NOT NULL
);

INSERT INTO stm_import (import_type_id, import_type_name)
VALUES
    (1, 'rule'),
    (2, 'owner'),
    (3, 'admin via reinitialize button')
ON CONFLICT (import_type_id) DO NOTHING;

-- Set all imports to rule import - if import_type_id null
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name='import_control' 
          AND column_name='import_type_id'
    ) THEN
        ALTER TABLE import_control
        ADD COLUMN import_type_id INTEGER;

		UPDATE import_control
		SET import_type_id = 1
		WHERE import_type_id IS NULL;

		ALTER TABLE import_control
		ALTER COLUMN import_type_id SET NOT NULL;
    END IF;
END
$$;

-- change name of fields
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name='import_control'
          AND column_name='rule_changes_found'
    ) THEN
        ALTER TABLE import_control RENAME COLUMN rule_changes_found TO changes_found;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name='import_control'
          AND column_name='any_changes_found'
    ) THEN
        ALTER TABLE import_control RENAME COLUMN any_changes_found TO policy_changes_found;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'import_control'
          AND column_name = 'rule_owner_mapping_done'
    ) THEN
        ALTER TABLE import_control
        ADD COLUMN rule_owner_mapping_done BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
END$$;

-- mgm_id now nullable
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'import_control'
          AND column_name = 'mgm_id'
          AND is_nullable = 'NO'
    ) THEN
        ALTER TABLE import_control
        ALTER COLUMN mgm_id DROP NOT NULL;
    END IF;
END
$$;

-- constraint mgm_id not null, if import_type_id = 1
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'import_control_mgm_id_required_for_import_type_1'
    ) THEN
        ALTER TABLE import_control
        ADD CONSTRAINT import_control_mgm_id_required_for_import_type_1
        CHECK (
            import_type_id <> 1
            OR mgm_id IS NOT NULL
        );
    END IF;
END
$$;

-- runs without problems in pgadmin - drops "old / unused" fields
DO $$
DECLARE
    col RECORD;
BEGIN
    FOR col IN
        SELECT column_name
        FROM information_schema.columns
        WHERE table_name = 'import_control'
          AND column_name IN (
              'delimiter_group',
              'delimiter_zone',
              'delimiter_user',
              'delimiter_list',
              'last_change_in_config',
              'is_full_import'
          )
    LOOP
        EXECUTE format(
            'ALTER TABLE import_control DROP COLUMN IF EXISTS %I',
            col.column_name
        );
    END LOOP;
END
$$;

-- rule_to_owner was intended as a rule–owner link table; replaced by rule_owner
DROP TABLE IF EXISTS public.rule_to_owner CASCADE;
