-- Reserve -1 for services matching any IP protocol. This is distinct from
-- protocol 0 (HOPOPT), so imported protocol-agnostic services retain their
-- intended semantics.
INSERT INTO stm_ip_proto (ip_proto_id, ip_proto_name, ip_proto_comment)
VALUES (-1, 'ANY', 'Any IP protocol')
ON CONFLICT (ip_proto_id) DO UPDATE
SET
    ip_proto_name = EXCLUDED.ip_proto_name,
    ip_proto_comment = EXCLUDED.ip_proto_comment;

-- Canonical ANY flow service objects are globally unique by their deterministic hash.
-- Restore any legacy record to the only valid lifecycle state before enforcing it.
UPDATE flow.svcobject
SET
    state = 'implemented',
    removed_date = NULL
WHERE ip_proto_id = -1
    AND port_start IS NULL
    AND port_end IS NULL
    AND (state IS DISTINCT FROM 'implemented' OR removed_date IS NOT NULL);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'flow_svcobject_canonical_any_lifecycle_check'
            AND conrelid = 'flow.svcobject'::regclass
    ) THEN
        ALTER TABLE flow.svcobject
            ADD CONSTRAINT flow_svcobject_canonical_any_lifecycle_check
            CHECK (
                ip_proto_id <> -1
                OR port_start IS NOT NULL
                OR port_end IS NOT NULL
                OR (state = 'implemented' AND removed_date IS NULL)
            );
    END IF;
END;
$$;
