--remove stm_owner_mapping_source

ALTER TABLE rule_owner
DROP CONSTRAINT IF EXISTS rule_owner_owner_mapping_source_id_stm_owner_mapping_source_for;

DROP TABLE IF EXISTS stm_owner_mapping_source;

-- add column to rule_owner
ALTER TABLE rule_owner
ADD COLUMN IF NOT EXISTS matched_objects jsonb;

ALTER TABLE rule_owner
DROP CONSTRAINT IF EXISTS rule_owner_matched_objects_for_ip_based;

ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_matched_objects_for_ip_based
CHECK ( owner_mapping_source_id  != 1 OR matched_objects IS NOT NULL );