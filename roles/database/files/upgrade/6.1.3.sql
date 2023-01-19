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

CREATE OR REPLACE FUNCTION refresh_recert_entries () RETURNS VOID AS $$
DECLARE
    r_mgm RECORD;
BEGIN
    FOR r_mgm IN SELECT mgm_id FROM management WHERE NOT do_not_import
    LOOP
        PERFORM recert_refresh_per_management(r_mgm.mgm_id);
    END LOOP;
    RETURN;
END;
$$ LANGUAGE plpgsql;


SELECT * FROM refresh_recert_entries ();
DROP FUNCTION refresh_recert_entries();
