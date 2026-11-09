-- rule hit import (#3297): last hits are no longer written by the regular import but by a separate,
-- scheduled rule hit import run, which stores them per gateway. Running this script again changes nothing.

-- last hit of a rule per gateway; rule_metadata.rule_last_hit keeps the latest hit across all gateways
CREATE TABLE IF NOT EXISTS firewall.rule_hit
(
    "rule_metadata_id" BIGINT NOT NULL,
    "dev_id" Integer NOT NULL,
    "rule_last_hit" Timestamp with time zone NOT NULL,
    primary key ("rule_metadata_id", "dev_id")
);

-- pending rule hit import runs: written by the middleware scheduler, removed by the importer handling the run
CREATE TABLE IF NOT EXISTS public.rule_hit_import_request
(
    "mgm_id" Integer NOT NULL,
    "requested_at" Timestamp with time zone NOT NULL Default now(),
    primary key ("mgm_id")
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'rule_hit_rule_metadata_id_rule_metadata_fkey') THEN
        ALTER TABLE firewall.rule_hit ADD CONSTRAINT rule_hit_rule_metadata_id_rule_metadata_fkey
            FOREIGN KEY (rule_metadata_id) REFERENCES firewall.rule_metadata(rule_metadata_id)
            ON UPDATE RESTRICT ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'rule_hit_dev_id_device_fkey') THEN
        ALTER TABLE firewall.rule_hit ADD CONSTRAINT rule_hit_dev_id_device_fkey
            FOREIGN KEY (dev_id) REFERENCES public.device(dev_id)
            ON UPDATE RESTRICT ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'rule_hit_import_request_mgm_id_management_fkey') THEN
        ALTER TABLE public.rule_hit_import_request ADD CONSTRAINT rule_hit_import_request_mgm_id_management_fkey
            FOREIGN KEY (mgm_id) REFERENCES public.management(mgm_id)
            ON UPDATE RESTRICT ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_rule_hit_dev_id ON firewall.rule_hit (dev_id);

-- import_control rows of rule hit import runs, kept apart from configuration imports (type 1)
INSERT INTO stm_import (import_type_id, import_type_name) VALUES (5, 'rule hit') ON CONFLICT DO NOTHING;

-- schedule of the rule hit import: by default once a day at midnight; existing values are kept
INSERT INTO config (config_key, config_value, config_user)
VALUES
    ('importRuleHitsSleepTime', '1', 0),
    ('importRuleHitsSleepTimeUnit', 'Days', 0),
    ('importRuleHitsStartAt', '00:00:00', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
