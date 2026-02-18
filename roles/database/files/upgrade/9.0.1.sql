-- add owner_responsible_type lookup table
CREATE TABLE IF NOT EXISTS owner_responsible_type
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
    active boolean default true,
    sort_order int default 0,
    allow_modelling boolean NOT NULL DEFAULT false,
    allow_recertification boolean NOT NULL DEFAULT false
);

INSERT INTO owner_responsible_type (id, name, active, sort_order)
VALUES
    (1, 'Main responsible', true, 10),
    (2, 'Supporting responsible', true, 20),
    (3, 'Optional escalation responsible', true, 30)
ON CONFLICT DO NOTHING;

CREATE UNIQUE INDEX IF NOT EXISTS owner_responsible_type_name_unique ON owner_responsible_type(name);

UPDATE owner_responsible_type
SET allow_modelling = true,
    allow_recertification = true
WHERE id IN (1, 2);

-- mgm_id now nullable
DO $$
DECLARE
  seq_name text;
  max_id bigint;
BEGIN
  seq_name := pg_get_serial_sequence('owner_responsible_type', 'id');
  IF seq_name IS NOT NULL THEN
    SELECT COALESCE(MAX(id), 0) INTO max_id FROM owner_responsible_type;
    PERFORM setval(seq_name, max_id, true);
  END IF;
END
$$;

-- constraint mgm_id not null, if import_type_id = 1
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint c
    JOIN pg_class t ON t.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = t.relnamespace
    WHERE c.conname = 'owner_responsible_type_foreign_key'
      AND t.relname = 'owner_responsible'
      AND n.nspname = current_schema()
  ) THEN
    ALTER TABLE owner_responsible
      ADD CONSTRAINT owner_responsible_type_foreign_key
      FOREIGN KEY (responsible_type)
      REFERENCES owner_responsible_type(id)
      ON UPDATE RESTRICT
      ON DELETE RESTRICT;
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