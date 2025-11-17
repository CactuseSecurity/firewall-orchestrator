-- adjust rule/owner entries in recertification table

-- select * from recert_refresh_one_owner_one_mgm(2,1,NULL::TIMESTAMP);
-- select * from recert_refresh_per_management(1);

-- this refresh trigger will only be called when deleting open recerts from recertification table 
-- (once per statement, not per row)



-- This function returns a table of future recert entries 
-- but does not write them into the recertification table
CREATE OR REPLACE FUNCTION recert_get_one_owner_one_mgm(
    i_owner_id INTEGER,
    i_mgm_id INTEGER
)
RETURNS SETOF recertification AS
$$
DECLARE
    b_super_owner BOOLEAN := FALSE;
BEGIN
    -- Check if this is the super owner
    SELECT TRUE INTO b_super_owner FROM owner WHERE id = i_owner_id AND is_default;

    RETURN QUERY
    SELECT DISTINCT
        NULL::bigint AS id,
        M.rule_metadata_id,
        R.rule_id,
        V.matches::VARCHAR AS ip_match,
        CASE WHEN b_super_owner THEN NULL ELSE i_owner_id END AS owner_id,
        NULL::VARCHAR AS user_dn,
        FALSE::BOOLEAN AS recertified,
        NULL::TIMESTAMP AS recert_date,
        NULL::VARCHAR AS comment,
        MAX((
            SELECT MAX(value)::TIMESTAMP
            FROM (
                SELECT I.start_time::timestamp + make_interval(days => O.recert_interval) AS value
                UNION
                SELECT C.recert_date + make_interval(days => O.recert_interval) AS value
            ) AS tmp
        )) AS next_recert_date,
        NULL::bigint AS owner_recert_id
    FROM 
        view_rule_with_owner V
        LEFT JOIN rule R USING (rule_id)
        LEFT JOIN rule_metadata M ON (R.rule_uid = M.rule_uid AND R.dev_id = M.dev_id)
        LEFT JOIN owner O ON (
            CASE WHEN b_super_owner THEN O.is_default ELSE V.owner_id = O.id END
        )
        LEFT JOIN import_control I ON (R.rule_create = I.control_id)
        LEFT JOIN recertification C ON (M.rule_metadata_id = C.rule_metadata_id)
    WHERE
        (
            (b_super_owner AND V.owner_id IS NULL)
            OR
            (NOT b_super_owner AND V.owner_id = i_owner_id)
        )
        AND R.mgm_id = i_mgm_id
        AND R.active
        AND (recert_date IS NULL OR (recert_date IS NOT NULL AND recertified))
    GROUP BY M.rule_metadata_id, R.rule_id, V.matches;
END;
$$ LANGUAGE plpgsql STABLE;



-- function used during import of a single management config
CREATE OR REPLACE FUNCTION recert_refresh_per_management (i_mgm_id INTEGER) RETURNS VOID AS $$
DECLARE
	r_owner   RECORD;
BEGIN
	BEGIN		
		FOR r_owner IN
			SELECT id, name FROM owner
		LOOP
			PERFORM recert_refresh_one_owner_one_mgm (r_owner.id, i_mgm_id, NULL::TIMESTAMP);
		END LOOP;
	EXCEPTION WHEN OTHERS THEN
		RAISE EXCEPTION 'Exception caught in recert_refresh_per_management while handling owner %', r_owner.name;
	END;
	RETURN;
END;
$$ LANGUAGE plpgsql;

-- select * from recert_get_one_owner_one_mgm(4,1)

-- this function returns a table of future recert entries 
-- but does not write them into the recertification table
CREATE OR REPLACE FUNCTION recert_get_one_owner_one_mgm
	(i_owner_id INTEGER, i_mgm_id INTEGER)
	RETURNS SETOF recertification AS
$$
DECLARE
	b_super_owner BOOLEAN := FALSE;
	i_recert_entry_id INTEGER;
	i_super_owner_interval INTEGER;
BEGIN
	SELECT INTO i_recert_entry_id id FROM owner WHERE id=i_owner_id AND is_default;
	IF FOUND THEN 
		b_super_owner := TRUE;
	END IF;

	-- ignore rule_id/owner_id combinations with existing decertification entries
	-- owner_id=0 and not recertified and NOT recert_date is null
	IF b_super_owner THEN
		SELECT INTO i_super_owner_interval recert_interval FROM OWNER WHERE is_default;

		RETURN QUERY
		SELECT DISTINCT
			NULL::bigint AS id,
			M.rule_metadata_id, 
			R.rule_id, 
			V.matches::VARCHAR as ip_match, 
			0::int as owner_id,
			NULL::VARCHAR AS user_dn,
			FALSE::BOOLEAN AS recertified,
			NULL::TIMESTAMP AS recert_date,
			NULL::VARCHAR AS comment,
			MAX((SELECT MAX(value)::TIMESTAMP AS next_recert_date
				FROM (
					SELECT I.start_time::timestamp + make_interval (days => o.recert_interval) AS value
					UNION
					SELECT C.recert_date + make_interval (days => o.recert_interval) AS value
				) AS temp_table)),
            NULL::bigint AS owner_recert_id            
		FROM 
			view_rule_with_owner V 
			LEFT JOIN rule R USING (rule_id)			
			LEFT JOIN rule_metadata M ON (R.rule_uid=M.rule_uid AND R.dev_id=M.dev_id)
			LEFT JOIN owner O ON (O.id=0)
			LEFT JOIN import_control I ON (R.rule_create=I.control_id)
			LEFT JOIN recertification C ON (M.rule_metadata_id=C.rule_metadata_id)
		WHERE V.owner_id IS NULL AND R.mgm_id=i_mgm_id AND R.active AND (recert_date IS NULL OR (NOT recert_date IS NULL AND recertified))
		GROUP BY M.rule_metadata_id, R.rule_id, V.matches;
	ELSE
		RETURN QUERY
		SELECT
			NULL::bigint AS id,
			M.rule_metadata_id, 
			R.rule_id, 
			V.matches::VARCHAR as ip_match, 
			i_owner_id,
			NULL::VARCHAR AS user_dn,
			FALSE::BOOLEAN AS recertified,
			NULL::TIMESTAMP AS recert_date,
			NULL::VARCHAR AS comment,
			MAX((SELECT MAX(value)::TIMESTAMP AS next_recert_date
				FROM (
					SELECT I.start_time::timestamp + make_interval (days => o.recert_interval) AS value
					UNION
					SELECT C.recert_date + make_interval (days => o.recert_interval) AS value
				) AS temp_table)),
            NULL::bigint AS owner_recert_id            
		FROM 
			view_rule_with_owner V 
			LEFT JOIN rule R USING (rule_id)			
			LEFT JOIN rule_metadata M ON (R.rule_uid=M.rule_uid AND R.dev_id=M.dev_id)
			LEFT JOIN owner O ON (V.owner_id=O.id)
			LEFT JOIN import_control I ON (R.rule_create=I.control_id)
			LEFT JOIN recertification C ON (M.rule_metadata_id=C.rule_metadata_id)
		WHERE V.owner_id=i_owner_id AND R.mgm_id=i_mgm_id AND R.active AND (recert_date IS NULL OR (NOT recert_date IS NULL AND recertified))
		GROUP BY M.rule_metadata_id, R.rule_id, V.matches;
	END IF;
END;
$$ LANGUAGE plpgsql STABLE;
