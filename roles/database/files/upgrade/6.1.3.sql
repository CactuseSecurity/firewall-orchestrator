ALTER TABLE recertification ADD COLUMN IF NOT EXISTS next_recert_date Timestamp;

-- query getRecerts($ownerId: Int, $mgmId: Int) {
--   recert_get_one_owner_one_mgm(args: {i_mgm_id: $mgmId, i_owner_id: $ownerId}, limit: 10) {
--     owner_id
--     ip_match
--     rule_id
--     recert_date
--     recertified
--   }
--}
