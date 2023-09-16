-- $Id: iso-svc-import.sql,v 1.1.2.5 2011-09-16 15:01:11 tim Exp $
-- $Source: /home/cvs/iso/package/install/database/Attic/iso-svc-import.sql,v $

----------------------------------------------------
-- FUNCTION:  import_svc_main
-- Zweck:     fuegt alle Services des aktuellen Imports in die service-Tabelle
-- Zweck:     verwendet die Funktion insert_single_svc zum Einfuegen der Einzelelemente
-- Zweck:     bzw. import_svc_mark_deleted und import_svc_refhandler_main zum Aufloesen der Referenzen
-- Parameter: control-id
-- RETURNS:   VOID
--
-- Function: public.import_svc_main(BIGINT, boolean)

-- DROP FUNCTION public.import_svc_main(BIGINT, boolean);

CREATE OR REPLACE FUNCTION public.import_svc_main(
    BIGINT,
    boolean)
  RETURNS void 
  LANGUAGE plpgsql
  AS
$BODY$
DECLARE
	i_current_import_id ALIAS FOR $1; -- ID des aktiven Imports
	b_is_initial_import ALIAS FOR $2; -- flag, ob initialer Import
	r_ctrl RECORD; -- zum Holen der start_time fuer Loeschen von Elementen
	i_mgm_id  INTEGER; -- zum Holen der mgm_ID fuer Loeschen von Elementen
	r_svc  RECORD;  -- Datensatz mit einzelner svc_id aus import_service-Tabelle des zu importierenden Services
	v_group_del   VARCHAR; -- Trennzeichen fuer Gruppenmitglieder
	i_dev_typ_id  INTEGER;  -- Datensatz fuer Device-Typ
	r_devtyp  RECORD;  -- Datensatz fuer Device-Typ
	is_netscreen BOOLEAN;  -- netscreen (wg. timeout in minuten statt sekunden)
	i_timeout_factor	INTEGER;
	t_last_change_time TIMESTAMP;
	r_last_times RECORD;
BEGIN 
	SELECT INTO r_ctrl mgm_id FROM import_control WHERE control_id=i_current_import_id;
	i_mgm_id := r_ctrl.mgm_id;
	SELECT INTO i_dev_typ_id dev_typ_id FROM management WHERE mgm_id = i_mgm_id; -- Hersteller-String holen
	SELECT INTO r_devtyp dev_typ_manufacturer AS hersteller FROM stm_dev_typ WHERE dev_typ_id = i_dev_typ_id; -- Hersteller-String holen
	is_netscreen := (lower(r_devtyp.hersteller) = 'netscreen');
	IF (is_netscreen) THEN i_timeout_factor := 60;
	ELSE i_timeout_factor := 1;	END IF;

/*
	IF NOT b_is_initial_import THEN
		-- Objekte ausklammern, die vor dem vorherigen Import-Zeitpunkt geaendert wurden, Tuning-Masznahme
		SELECT INTO r_last_times MAX(start_time) AS last_import_time, MAX(last_change_in_config) AS last_change_time
			FROM import_control WHERE mgm_id=i_mgm_id AND NOT control_id=i_current_import_id AND successful_import;
		IF (r_last_times.last_change_time IS NULL) THEN t_last_change_time := r_last_times.last_import_time;
		ELSE 
			IF (r_last_times.last_import_time<r_last_times.last_change_time) THEN t_last_change_time := r_last_times.last_import_time;
		 	ELSE t_last_change_time := r_last_times.last_change_time;
		 	END IF;
	 	END IF;
		t_last_change_time := t_last_change_time - CAST('24 hours' AS INTERVAL); -- einen Tag abziehen, falls Zeitsync-Probleme
		UPDATE service SET svc_last_seen=i_current_import_id WHERE mgm_id=i_mgm_id AND active AND NOT svc_uid IS NULL AND svc_uid IN
			(SELECT svc_uid FROM import_service WHERE last_change_time<t_last_change_time AND NOT last_change_time IS NULL);
		DELETE FROM import_service WHERE last_change_time<t_last_change_time AND NOT last_change_time IS NULL AND NOT svc_uid IS NULL;
	END IF;
*/	
	FOR r_svc IN -- jedes Objekt wird mittels insert_single_nwobj eingefuegt
		SELECT svc_id FROM import_service WHERE control_id = i_current_import_id
	LOOP
		-- RAISE DEBUG 'before import_svc_single';
		PERFORM import_svc_single(i_current_import_id,i_mgm_id,r_svc.svc_id,i_timeout_factor,b_is_initial_import);
		-- RAISE DEBUG 'successfully finished import_svc_single';
	END LOOP;

	IF NOT b_is_initial_import THEN 	-- geloeschte Elemente markieren als NOT active
		PERFORM import_svc_mark_deleted (i_current_import_id, i_mgm_id);
	END IF;
	RETURN;
END; 
$BODY$;
ALTER FUNCTION public.import_svc_main(BIGINT, boolean) OWNER TO fworch;

----------------------------------------------------
-- FUNCTION:  import_svc_mark_deleted
-- Zweck:     markiert alle nicht mehr vorhandenen Services als not active
-- Parameter: current_control_id, mgm_id
-- Parameter: import_service.svc_id (die ID des zu importierenden Services)
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_svc_mark_deleted(BIGINT,INTEGER) RETURNS VOID AS $$
DECLARE
    i_current_import_id	ALIAS FOR $1;
    i_mgm_id			ALIAS FOR $2;
    i_import_admin_id	BIGINT;
	i_previous_import_id  BIGINT; -- zum Holen der import_ID des vorherigen Imports fuer das Mgmt
	r_svc  RECORD;  -- Datensatz mit einzelner svc_id aus import_service-Tabelle des zu importierenden Services
BEGIN
	RAISE DEBUG 'import_svc_mark_deleted start';
	i_previous_import_id := get_previous_import_id_for_mgmt(i_mgm_id,i_current_import_id);
	RAISE DEBUG 'import_svc_mark_deleted 1';
	i_import_admin_id := get_last_change_admin_of_obj_delete (i_current_import_id);	
	RAISE DEBUG 'import_svc_mark_deleted 2';
	IF NOT i_previous_import_id IS NULL THEN -- wenn das Management nicht zum ersten Mal importiert wurde
	   	-- alle nicht mehr vorhandenen Services in changelog_object als geloescht eintragen
		FOR r_svc IN -- jedes geloeschte Element wird in changelog_service eingetragen
			SELECT svc_id,svc_name FROM service WHERE mgm_id=i_mgm_id AND svc_last_seen=i_previous_import_id AND active
		LOOP
			RAISE DEBUG 'svc % deleted', r_svc.svc_name;
			INSERT INTO changelog_service
				(control_id,new_svc_id,old_svc_id,change_action,import_admin,documented,mgm_id)
				VALUES (i_current_import_id,NULL,r_svc.svc_id,'D',i_import_admin_id,FALSE,i_mgm_id);
			PERFORM error_handling('INFO_SVC_DELETED', r_svc.svc_name);
		END LOOP;
		-- active-flag von allen in diesem Import geloeschten Objekten loeschen
		UPDATE service SET active='FALSE' WHERE mgm_id=i_mgm_id AND svc_last_seen=i_previous_import_id AND active;
	END IF;
	RAISE DEBUG 'import_svc_mark_deleted finished';
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_single
-- Zweck:     fuegt einen Dienst des aktuellen Imports in die service-Tabelle
-- Parameter1: current_control_id
-- Parameter2: mgm_id
-- Parameter3: ID des zu importierenden Elements)
-- RETURNS:   VOID
--
-- Function: public.import_svc_single(integer, integer, integer, integer, boolean)

-- DROP FUNCTION public.import_svc_single(integer, integer, integer, integer, boolean);

CREATE OR REPLACE FUNCTION public.import_svc_single(
    BIGINT,
    integer,
    BIGINT,
    integer,
    boolean)
  RETURNS void 
  LANGUAGE plpgsql
  AS
$BODY$
DECLARE
    i_control_id	ALIAS FOR $1;
    i_mgm_id		ALIAS FOR $2;
    id			ALIAS FOR $3;
    i_timeout_factor 	ALIAS FOR $4;
    b_is_initial_import ALIAS FOR $5;    
    to_import   RECORD; -- der zu importierende Datensatz aus import_object
    i_farbe		INTEGER; -- enthaelt color_id
    i_typ		INTEGER; -- enthaelt service typ
	r_proto     RECORD;    -- fuer ip_proto_id record
	protoID     INTEGER;   -- fuer ip_proto_id
	srcport     INTEGER;   -- Source-Port start
	srcport_end INTEGER;   -- Source-Port end
	port        INTEGER;   -- Destination-Port start
	port_end    INTEGER;   -- Destination-Port end
	timeout     INTEGER;   -- timeout
	std_timeout BOOLEAN;   -- timeout steht auf standard-wert
	err_txt             VARCHAR;   -- text fuer Fehlerausgabe (variablen-Ausgabe)
	i_admin_id	INTEGER; -- ID des last_change_admins
    existing_svc   RECORD; -- der ev. bestehende  Datensatz aus object
    b_insert	BOOLEAN; -- soll eingefuegt werden oder nicht?
    b_change	BOOLEAN; -- hat sich etwas geaendert?
    b_change_security_relevant	BOOLEAN; -- hat sich etwas sicherheitsrelevantes geaendert?
    v_change_id VARCHAR;	-- type of change
	b_is_documented BOOLEAN; 
	t_outtext TEXT; 
	i_change_type INTEGER;
	i_new_svc_id  BIGINT;	-- id des neu eingefuegten object
	v_comment	VARCHAR;	
BEGIN
    -- RAISE DEBUG 'import_svc_single::start';
    b_insert := FALSE;
    b_change := FALSE;
    b_change_security_relevant := FALSE;
    SELECT INTO to_import * FROM import_service WHERE svc_id = id; -- zu importierenden Datensatz aus import_service einlesen
    --RAISE DEBUG 'import_svc_single::SELECT INTO to_import * FROM import_service WHERE svc_id = id (after)';
    --RAISE DEBUG 'ip_proto found: %', to_import.ip_proto;
	IF NOT (to_import.ip_proto IS NULL OR char_length(cast (to_import.ip_proto as varchar)) = 0 
			OR CAST(to_import.ip_proto as integer)<0 OR to_import.ip_proto = '') THEN  -- wenn ip-proto vorhanden (simple) und nicht negativ
		--RAISE DEBUG 'import_svc_single::first if true';
		IF is_numeric(to_import.ip_proto) THEN
			protoID := CAST(to_import.ip_proto AS INTEGER);
		ELSE
			SELECT INTO r_proto ip_proto_id FROM stm_ip_proto WHERE ip_proto_name = lower(to_import.ip_proto); -- protocolID holen
			IF NOT FOUND THEN -- TODO: das muss noch automatisiert werden: Neuanlegen eines IP-Protokolls
				PERFORM error_handling('ERR_PROTO_MISS', to_import.ip_proto);
			END IF;
			protoID := r_proto.ip_proto_id;
		END IF;
	ELSE protoID := NULL;
		RAISE DEBUG 'import_svc_single::first if false';
	END IF;
	IF (to_import.svc_port IS NULL) THEN  -- wenn kein Port enthalten
		port := NULL;  -- Port auf 0 setzen
	ELSE
		port := CAST(to_import.svc_port AS INTEGER); -- TEXT in INTEGER umwandeln, TODO: Fehlerbehandlung
		IF (port<0 OR port>65535) THEN  -- Port ungueltig?
			err_txt := 'Service: ' || to_import.svc_name || ', port: ' || to_import.svc_source_port_end;
			PERFORM error_handling('ERR_PORT_INVALID', err_txt);
			IF (port<0) THEN port := 0;
			ELSE port := 65535;
			END IF;
		END IF;
	END IF;
	IF (to_import.svc_port_end IS NULL) THEN  -- wenn kein Port enthalten
		port_end := port;  -- Port nur ein Port gemeint
	ELSE
		port_end := CAST(to_import.svc_port_end AS INTEGER); -- TEXT in INTEGER umwandeln, TODO: Fehlerbehandlung
		IF (port_end<0 OR port_end>65535) THEN  -- Port ungueltig?
			err_txt := 'Service: ' || to_import.svc_name || ', port-end: ' || to_import.svc_source_port_end;
			PERFORM error_handling('ERR_PORT_INVALID', err_txt);
			IF (port_end<0) THEN port_end := 0;
			ELSE port_end := 65535;
			END IF;
		END IF;
	END IF;
	IF (to_import.svc_source_port IS NULL) THEN  -- wenn kein Port enthalten
		srcport := NULL;  -- Port auf 0 setzen
	ELSE
		srcport := CAST(to_import.svc_source_port AS INTEGER); -- TEXT in INTEGER umwandeln, TODO: Fehlerbehandlung
		IF (srcport<0 OR srcport>65535) THEN  -- Port ungueltig?
			err_txt := 'Service: ' || to_import.svc_name || ', src-port: ' || to_import.svc_source_port;
			PERFORM error_handling('ERR_PORT_INVALID', err_txt);
			IF (srcport<0) THEN srcport := 0;
			ELSE srcport := 65535;
			END IF;
		END IF;
	END IF;
	IF (to_import.svc_source_port_end IS NULL) THEN  -- wenn kein Port enthalten
		srcport_end := srcport;  -- nur ein Port gemeint
	ELSE
		srcport_end := CAST(to_import.svc_source_port_end AS INTEGER); -- TEXT in INTEGER umwandeln, TODO: Fehlerbehandlung
		IF (srcport_end<0 OR srcport_end>65535) THEN  -- Port ungueltig?
			err_txt := 'Service: ' || to_import.svc_name || ', src-port-end: ' || to_import.svc_source_port_end;
			PERFORM error_handling('ERR_PORT_INVALID', err_txt);
			IF (srcport_end<0) THEN srcport_end := 0;
			ELSE srcport_end := 65535;
			END IF;
		END IF;
	END IF;
	IF (to_import.svc_timeout IS NULL) THEN  -- wenn kein Timeout enthalten
		timeout := NULL;  -- Timeout auf 0 setzen
		std_timeout := TRUE;  -- hier greift der Standard-Timeout
	ELSE
		timeout := CAST(to_import.svc_timeout AS INTEGER); -- TEXT in INTEGER umwandeln, TODO: Fehlerbehandlung
		timeout := timeout * i_timeout_factor;
	END IF;
	SELECT INTO i_typ svc_typ_id FROM stm_svc_typ WHERE svc_typ_name = to_import.svc_typ; -- svc_typ_id holen (simple,group,rpc,...)
	IF NOT FOUND THEN -- TODO: das muss noch automatisiert werden: Neuanlegen eines svc_typ
		PERFORM error_handling('ERR_SVCTYP_MISS', to_import.svc_typ);
	END IF;
	-- color_id holen (normalisiert ohne SPACES und in Kleinbuchstaben)
	SELECT INTO i_farbe color_id FROM stm_color WHERE color_name = LOWER(remove_spaces(to_import.svc_color));
	IF NOT FOUND THEN -- TODO: Fehlerbehandlung bzw. automat. Neuanlegen einer Farbe?
		i_farbe := NULL;
--		PERFORM error_handling('ERR_COLOR_MISS', to_import.svc_color);
	END IF;
	
-- neu (Integration der alten Triggerfunktionen)	
	IF (to_import.svc_uid IS NULL OR char_length(cast (to_import.svc_uid as varchar)) = 0) THEN -- nur der Weg ueber den Namen als ID geht
	    SELECT INTO existing_svc * FROM service	WHERE svc_name=to_import.svc_name AND mgm_id=i_mgm_id AND active;
	ELSE  -- svc_uid ist nicht leer: nehme dieses Feld als ID-Anteil anstatt Namen: erschlaegt Umbenennungen
	    SELECT INTO existing_svc * FROM service WHERE svc_uid=to_import.svc_uid AND mgm_id=i_mgm_id AND active;
	END IF;
	IF FOUND THEN  -- service existiert bereits
		IF (NOT (   
			are_equal(existing_svc.svc_typ_id, i_typ) AND
			are_equal(existing_svc.svc_member_names, to_import.svc_member_names) AND
			are_equal(existing_svc.svc_member_refs, to_import.svc_member_refs) AND
			are_equal(existing_svc.ip_proto_id, protoID) AND
			are_equal(existing_svc.svc_port, port) AND
			are_equal(existing_svc.svc_port_end, port_end) AND
			are_equal(existing_svc.svc_rpcnr, to_import.rpc_nr) AND
			are_equal(existing_svc.svc_source_port, srcport) AND
			are_equal(existing_svc.svc_source_port_end, srcport_end) AND
			are_equal(existing_svc.svc_timeout, timeout) AND
			are_equal(existing_svc.svc_uid, to_import.svc_uid) AND
			are_equal(existing_svc.svc_name, to_import.svc_name) AND
			are_equal(existing_svc.svc_prod_specific, to_import.svc_prod_specific)
		))
		THEN
			b_change := TRUE;
			b_change_security_relevant := TRUE;
		END IF;
		IF (NOT( -- ab hier die nicht sicherheitsrelevanten Aenderungen
			are_equal(existing_svc.svc_comment, to_import.svc_comment) AND
			are_equal(existing_svc.svc_color_id, i_farbe)
		))
		THEN -- object unveraendert
			b_change := TRUE;
		END IF;
		IF (b_change) THEN
			v_change_id := 'INFO_SVC_CHANGED';
		ELSE
			UPDATE service SET svc_last_seen = i_control_id WHERE svc_id = existing_svc.svc_id;
		END IF;
	ELSE
		b_insert := TRUE;
		v_change_id := 'INFO_SVC_INSERTED'; 
	END IF;
	IF (b_change OR b_insert) THEN
		PERFORM error_handling(v_change_id, to_import.svc_name);
		i_admin_id := get_admin_id_from_name(to_import.last_change_admin);
		RAISE DEBUG 'inserting service with uid %', to_import.svc_uid;
		INSERT INTO service (
			mgm_id,svc_name,svc_timeout,ip_proto_id,svc_port,svc_port_end,
			svc_source_port,svc_source_port_end,svc_typ_id,svc_comment,svc_member_names,svc_member_refs,
			svc_color_id,svc_rpcnr,svc_prod_specific,svc_uid,last_change_admin,
			svc_last_seen,svc_create
		)
		VALUES (
			i_mgm_id,to_import.svc_name,timeout,protoID,port,port_end,srcport,srcport_end,i_typ,
			to_import.svc_comment,to_import.svc_member_names,to_import.svc_member_refs,i_farbe,to_import.rpc_nr,to_import.svc_prod_specific,
			to_import.svc_uid,i_admin_id,i_control_id,i_control_id
		);
        
        -- changelog-Eintrag
        SELECT INTO i_new_svc_id MAX(svc_id) FROM service WHERE mgm_id=i_mgm_id; -- ein bisschen fragwuerdig
		IF (b_insert) THEN  -- der service wurde neu angelegt
			IF b_is_initial_import THEN
				b_is_documented := TRUE;  t_outtext := 'INITIAL_IMPORT'; i_change_type := 2;
			ELSE
				b_is_documented := FALSE; t_outtext := NULL; i_change_type := 3;
			END IF;
			-- fest verdrahtete Werte: weniger gut
			INSERT INTO changelog_service
				(control_id,new_svc_id,old_svc_id,change_action,import_admin,documented,changelog_svc_comment,mgm_id,change_type_id)
				VALUES (i_control_id,i_new_svc_id,NULL,'I',i_admin_id,b_is_documented,t_outtext,i_mgm_id,i_change_type);
		ELSE -- change
			IF (b_change_security_relevant) THEN
				v_comment := NULL;
				b_is_documented := FALSE;
			ELSE
				v_comment := 'NON_SECURITY_RELEVANT_CHANGE';
				b_is_documented := TRUE;
			END IF;
			INSERT INTO changelog_service
				(control_id,new_svc_id,old_svc_id,change_action,import_admin,documented,mgm_id,
					security_relevant,changelog_svc_comment)
				VALUES (i_control_id,i_new_svc_id,existing_svc.svc_id,'C',i_admin_id,b_is_documented,
					i_mgm_id,b_change_security_relevant,v_comment);
			-- erst jetzt kann active beim alten Dienst auf FALSE gesetzt werden
			UPDATE service SET active = FALSE WHERE svc_id = existing_svc.svc_id; -- alten Dienst auf not active setzen
		END IF;
	END IF;
    RETURN;
END;
$BODY$;
ALTER FUNCTION public.import_svc_single(BIGINT, integer, BIGINT, integer, boolean) OWNER TO fworch;

