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


-- Flow accesses created by the request workflow before 9.4.5 stored group based sources,
-- destinations and services as group references only, although their access hash was calculated
-- from the hashes of the group members. The member references in flow.access_source,
-- flow.access_destination and flow.access_service were left out.
-- An access without members cannot be hashed again (an access needs at least one source,
-- destination and service), so the flow sync keeps skipping every affected management.
-- Restore the missing member references from the group memberships. Flow groups are immutable
-- (their hash is derived from their members), so this reproduces exactly the member set the
-- stored access hash was calculated from.

INSERT INTO flow.access_source (access_id, nwobj_id)
SELECT DISTINCT access_group.access_id, group_member.nwobj_id
FROM flow.access_source_grp AS access_group
    JOIN flow.nwgroup_member AS group_member ON group_member.nwgrp_id = access_group.nwgrp_id
ON CONFLICT DO NOTHING;

INSERT INTO flow.access_destination (access_id, nwobj_id)
SELECT DISTINCT access_group.access_id, group_member.nwobj_id
FROM flow.access_destination_grp AS access_group
    JOIN flow.nwgroup_member AS group_member ON group_member.nwgrp_id = access_group.nwgrp_id
ON CONFLICT DO NOTHING;

INSERT INTO flow.access_service (access_id, svcobj_id)
SELECT DISTINCT access_group.access_id, group_member.svcobj_id
FROM flow.access_service_grp AS access_group
    JOIN flow.svcgroup_member AS group_member ON group_member.svcgrp_id = access_group.svcgrp_id
ON CONFLICT DO NOTHING;
