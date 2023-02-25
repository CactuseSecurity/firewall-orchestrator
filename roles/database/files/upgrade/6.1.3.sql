ALTER TABLE recertification ADD COLUMN IF NOT EXISTS next_recert_date Timestamp;

-- creating triggers for owner changes:

CREATE OR REPLACE FUNCTION owner_change_triggered ()
    RETURNS TRIGGER
    AS $BODY$
BEGIN
    PERFORM recert_refresh_per_owner(NEW.id);
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;
ALTER FUNCTION public.owner_change_triggered () OWNER TO fworch;


DROP TRIGGER IF EXISTS owner_change ON owner CASCADE;

CREATE TRIGGER owner_change
    BEFORE INSERT OR UPDATE ON owner
    FOR EACH ROW
    EXECUTE PROCEDURE owner_change_triggered ();

CREATE OR REPLACE FUNCTION owner_network_change_triggered ()
    RETURNS TRIGGER
    AS $BODY$
BEGIN
    PERFORM recert_refresh_per_owner(NEW.id);
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;
ALTER FUNCTION public.owner_network_change_triggered () OWNER TO fworch;

DROP TRIGGER IF EXISTS owner_network_change ON owner_network CASCADE;

CREATE TRIGGER owner_network_change
    BEFORE INSERT OR UPDATE ON owner_network
    FOR EACH ROW
    EXECUTE PROCEDURE owner_network_change_triggered ();


--- refreshing future recert entries:

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


-- LargeOwnerChange: comment out the next line to not refresh recert entries during upgrade
SELECT * FROM refresh_recert_entries ();
DROP FUNCTION refresh_recert_entries();

