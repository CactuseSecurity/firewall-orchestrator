ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_ip_unique;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_unique UNIQUE (owner_id, ip, ip_end, import_source);
ALTER TABLE ext_request ADD COLUMN IF NOT EXISTS locked boolean default false;

-- Create indexes on the materialized view
CREATE INDEX IF NOT EXISTS idx_view_rule_with_owner_rule_id ON view_rule_with_owner (rule_id);
CREATE INDEX IF NOT EXISTS idx_view_rule_with_owner_owner_id ON view_rule_with_owner (owner_id);

-- -- function used during import of owner data
CREATE OR REPLACE FUNCTION recert_refresh_per_owner(i_owner_id INTEGER) RETURNS VOID AS $$
DECLARE
	r_mgm    RECORD;
BEGIN
	BEGIN
		FOR r_mgm IN
			SELECT mgm_id, mgm_name FROM management
		LOOP
			PERFORM recert_refresh_one_owner_one_mgm (i_owner_id, r_mgm.mgm_id, NULL::TIMESTAMP);
		END LOOP;

	EXCEPTION WHEN OTHERS THEN
		RAISE EXCEPTION 'Exception caught in recert_refresh_per_owner while handling management %', r_mgm.mgm_name;
	END;
	RETURN;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION owner_change_triggered ()
    RETURNS TRIGGER
    AS $BODY$
BEGIN
    IF NOT NEW.id IS NULL THEN
        PERFORM recert_refresh_per_owner(NEW.id);
    END IF;
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE;
ALTER FUNCTION public.owner_change_triggered () OWNER TO fworch;


DROP TRIGGER IF EXISTS owner_change ON owner CASCADE;

CREATE TRIGGER owner_change
    AFTER INSERT OR UPDATE OR DELETE ON owner
    FOR EACH ROW
    EXECUTE PROCEDURE owner_change_triggered ();

CREATE OR REPLACE FUNCTION owner_network_change_triggered ()
    RETURNS TRIGGER
    AS $BODY$
BEGIN
    IF NOT NEW.owner_id IS NULL THEN
        PERFORM recert_refresh_per_owner(NEW.owner_id);
    END IF;
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE;
ALTER FUNCTION public.owner_network_change_triggered () OWNER TO fworch;

DROP TRIGGER IF EXISTS owner_network_change ON owner_network CASCADE;

CREATE TRIGGER owner_network_change
    AFTER INSERT OR UPDATE OR DELETE ON owner_network
    FOR EACH ROW
    EXECUTE PROCEDURE owner_network_change_triggered ();


CREATE TABLE refresh_log (
    id SERIAL PRIMARY KEY,
    view_name TEXT NOT NULL,
    refreshed_at TIMESTAMPTZ DEFAULT now(),
    status TEXT
);

CREATE OR REPLACE FUNCTION refresh_view_rule_with_owner()
RETURNS SETOF refresh_log AS $$
DECLARE
    status_message TEXT;
BEGIN
    -- Attempt to refresh the materialized view
    BEGIN
        REFRESH MATERIALIZED VIEW view_rule_with_owner;
        status_message := 'Materialized view refreshed successfully';
    EXCEPTION
        WHEN OTHERS THEN
            status_message := format('Failed to refresh view: %s', SQLERRM);
    END;

    -- Log the operation
    INSERT INTO refresh_log (view_name, status)
    VALUES ('view_rule_with_owner', status_message);

    -- Return the log entry
    RETURN QUERY SELECT * FROM refresh_log WHERE view_name = 'view_rule_with_owner' ORDER BY refreshed_at DESC LIMIT 1;
END;
$$ LANGUAGE plpgsql VOLATILE;
