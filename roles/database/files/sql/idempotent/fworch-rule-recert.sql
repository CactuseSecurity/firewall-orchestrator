-- adjust rule/owner entries in recertification table

-- select * from recert_refresh_one_owner_one_mgm(2,1,NULL::TIMESTAMP);
-- select * from recert_refresh_per_management(1);

-- this function returns a table of future recert entries 
-- but does not write them into the recertification table
CREATE OR REPLACE FUNCTION recert_get_one_owner_one_mgm
	(i_owner_id INTEGER, i_mgm_id INTEGER)
	RETURNS SETOF recertification AS
$$
DECLARE
	b_super_owner BOOLEAN;
	i_recert_entry_id INTEGER;
BEGIN
	b_super_owner := FALSE;
	SELECT INTO i_recert_entry_id id FROM owner WHERE id=i_owner_id AND is_default;
	IF FOUND THEN 
		b_super_owner := TRUE;
	END IF;

	IF b_super_owner THEN
		RETURN QUERY
		SELECT
			NULL::bigint AS id,
			M.rule_metadata_id, 
			R.rule_id, 
			V.matches::VARCHAR as ip_match, 
			i_owner_id,
			NULL::VARCHAR AS user_dn,
			NULL::BOOLEAN AS recertified,
			NULL::TIMESTAMP AS recert_date,
			NULL::VARCHAR AS comment,
			(I.start_time::timestamp + make_interval (days => O.recert_interval)) AS next_recert_date
		FROM 
			view_rule_with_owner V 
			LEFT JOIN rule R USING (rule_id)			
			LEFT JOIN rule_metadata M ON (R.rule_uid=M.rule_uid AND R.dev_id=M.dev_id)
			LEFT JOIN owner O ON (V.owner_id=O.id)
			LEFT JOIN import_control I ON (R.rule_create=I.control_id) 
		WHERE V.owner_id IS NULL AND R.mgm_id=i_mgm_id AND R.active;
	ELSE
		RETURN QUERY
		SELECT
			NULL::bigint AS id,
			M.rule_metadata_id, 
			R.rule_id, 
			V.matches::VARCHAR as ip_match, 
			i_owner_id,
			NULL::VARCHAR AS user_dn,
			NULL::BOOLEAN AS recertified,
			NULL::TIMESTAMP AS recert_date,
			NULL::VARCHAR AS comment,
			(I.start_time::timestamp + make_interval (days => O.recert_interval)) AS next_recert_date
		FROM 
			view_rule_with_owner V 
			LEFT JOIN rule R USING (rule_id)			
			LEFT JOIN rule_metadata M ON (R.rule_uid=M.rule_uid AND R.dev_id=M.dev_id)
			LEFT JOIN owner O ON (V.owner_id=O.id)
			LEFT JOIN import_control I ON (R.rule_create=I.control_id) 
		WHERE V.owner_id=i_owner_id AND R.mgm_id=i_mgm_id AND R.active;
	END IF;
END;
$$ LANGUAGE plpgsql STABLE;
