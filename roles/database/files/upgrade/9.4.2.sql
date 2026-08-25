-- Reserve -1 for services matching any IP protocol. This is distinct from
-- protocol 0 (HOPOPT), so imported protocol-agnostic services retain their
-- intended semantics.
INSERT INTO stm_ip_proto (ip_proto_id, ip_proto_name, ip_proto_comment)
VALUES (-1, 'ANY', 'Any IP protocol')
ON CONFLICT (ip_proto_id) DO UPDATE
SET
    ip_proto_name = EXCLUDED.ip_proto_name,
    ip_proto_comment = EXCLUDED.ip_proto_comment;
