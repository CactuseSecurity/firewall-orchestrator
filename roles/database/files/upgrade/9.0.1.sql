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

-- migrate modelling mail recipients to multi-select JSON representation
INSERT INTO config (config_key, config_value, config_user) VALUES ('modReqEmailOtherAddresses', '', 0) ON CONFLICT DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('modDecommEmailOtherAddresses', '', 0) ON CONFLICT DO NOTHING;

UPDATE config
SET config_value = '{"none":false,"other_addresses":false,"owner_responsible_type_ids":[2]}'
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'OwnerGroupOnly';

UPDATE config
SET config_value = '{"none":false,"other_addresses":false,"owner_responsible_type_ids":[1]}'
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'OwnerMainResponsible';

UPDATE config
SET config_value = '{"none":false,"other_addresses":false,"ensure_at_least_one_notification":true,"owner_responsible_type_ids":[2,1]}'
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'FallbackToMainResponsibleIfOwnerGroupEmpty';

UPDATE config
SET config_value = '{"none":false,"other_addresses":true,"owner_responsible_type_ids":[]}'
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'OtherAddresses';

UPDATE config
SET config_value = (
    SELECT json_build_object(
        'none', false,
        'other_addresses', false,
        'owner_responsible_type_ids', COALESCE(json_agg(id ORDER BY sort_order DESC), '[]'::json)
    )::text
    FROM owner_responsible_type
    WHERE active = true
)
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'AllOwnerResponsibles';

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
