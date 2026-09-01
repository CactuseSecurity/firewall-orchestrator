-- Groups carry no protocol of their own - their members do. Fmgr imports were
-- previously normalizing service groups (svc_typ_id 2) with a concrete
-- ip_proto_id (0 or the group's own protocol selector) instead of NULL.
-- Align already-imported data with the corrected importers so the next
-- import does not report this as a change.
UPDATE firewall.nw_service
SET ip_proto_id = NULL
WHERE svc_typ_id = 2
    AND ip_proto_id IS NOT NULL;


-- Insert default configuration for compliance diff filter
INSERT INTO config (config_key, config_value, config_user)
VALUES ('complianceDiffFilterExistingViolations', 'false', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
