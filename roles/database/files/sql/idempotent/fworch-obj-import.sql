--------------------------------------------
-- FUNCTION:  import_nwobject_main
-- Zweck:     fuegt alle Objekte des aktuellen Imports in die object-Tabelle
-- Zweck:     verwendet die Funktion insert_single_nwobj zum Einfuegen der Einzelobjekte
-- Zweck:     bzw. resolve_nwobj_groups zum Aufloesen der Gruppenlisten
-- Parameter1: control-id
-- Parameter2: flag fuer initial_import
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_nwobj_main(BIGINT, boolean)
  RETURNS void AS
$BODY$
DECLARE
	i_current_import_id ALIAS FOR $1; -- ID des aktiven Imports
	b_is_initial_import	ALIAS FOR $2;
	i_mgm_id  INTEGER; -- zum Holen der mgm_ID fuer Loeschen von Objekten
	r_obj  RECORD;  -- Datensatz mit einzelner obj_id aus import_object-Tabelle des zu importierenden Objekts
	v_group_del   VARCHAR; -- Trennzeichen fuer Gruppenmitglieder
	t_last_change_time TIMESTAMP;
	r_last_times RECORD;
BEGIN 
	SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
	RAISE DEBUG 'import_nwobj_main processing mgm %', i_mgm_id;

	-- Schleife fuer alle (verbliebenen) Eintraege in import_object fuer MGM
	FOR r_obj IN -- jedes Objekt wird mittels insert_single_nwobj eingefuegt
		SELECT obj_id, obj_name FROM import_object WHERE control_id = i_current_import_id
	LOOP
		RAISE DEBUG 'processing obj %', r_obj.obj_name;
		PERFORM import_nwobj_single(i_current_import_id,i_mgm_id,r_obj.obj_id,b_is_initial_import);
	END LOOP;
	
	-- geloeschte Objekte markieren als NOT active
	IF NOT b_is_initial_import THEN
		PERFORM import_nwobj_mark_deleted (i_current_import_id, i_mgm_id);
	END IF;
	RETURN;
END; 
$BODY$
  LANGUAGE 'plpgsql' VOLATILE;

----------------------------------------------------
-- FUNCTION:  import_nwobj_mark_deleted
-- Zweck:     marks all non-existant network objects as non-active
-- Parameter: current_control_id, mgm_id
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_nwobj_mark_deleted(BIGINT,INTEGER) RETURNS VOID AS $$
DECLARE
    i_current_import_id	ALIAS FOR $1;
    i_mgm_id			ALIAS FOR $2;
    i_import_admin_id	INTEGER;
	i_previous_import_id  BIGINT; -- zum Holen der import_ID des vorherigen Imports fuer das Mgmt
	r_obj  RECORD;  -- Datensatz mit einzelner obj_id aus import_object-Tabelle des zu importierenden Objekts
BEGIN
--	SELECT INTO i_import_admin_id import_admin FROM import_control WHERE control_id=i_current_import_id;
	i_previous_import_id := get_previous_import_id_for_mgmt(i_mgm_id,i_current_import_id);
	i_import_admin_id := get_last_change_admin_of_obj_delete (i_current_import_id);
	IF NOT i_previous_import_id IS NULL THEN -- wenn das Management nicht zum ersten Mal importiert wurde
	   	-- alle nicht mehr vorhandenen Objekte in changelog_object als geloescht eintragen
		FOR r_obj IN -- jedes geloeschte Objekt wird in changelog_object eingetragen
			SELECT obj_id,obj_name FROM object WHERE mgm_id=i_mgm_id AND obj_last_seen=i_previous_import_id AND active
		LOOP
			INSERT INTO changelog_object
				(control_id,new_obj_id,old_obj_id,change_action,import_admin,documented,mgm_id)
				VALUES (i_current_import_id,NULL,r_obj.obj_id,'D',i_import_admin_id,FALSE,i_mgm_id);
			PERFORM error_handling('INFO_OBJ_DELETED', r_obj.obj_name);
		END LOOP;
		-- active-flag von allen in diesem Import geloeschten Objekten loeschen
		UPDATE object SET active='FALSE' WHERE mgm_id=i_mgm_id AND obj_last_seen=i_previous_import_id AND active;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_single
-- Zweck:     adds a network object of the current import to the table "object"
-- Parameter: current_control_id
-- Parameter: mgm_id
-- Parameter: import_object.obj_id (id of the network object to import)
-- Parameter: is_initial_import (boolean)
-- RETURNS:   VOID
--
-- Function: import_nwobj_single(BIGINT, integer, BIGINT, boolean)

-- DROP FUNCTION import_nwobj_single(BIGINT, integer, BIGINT, boolean);

CREATE OR REPLACE FUNCTION import_nwobj_single(BIGINT, integer, BIGINT, boolean)
  RETURNS void AS
$BODY$
DECLARE
    i_control_id	ALIAS FOR $1;
    i_mgm_id	ALIAS FOR $2;
    id			ALIAS FOR $3;
    b_is_initial_import ALIAS FOR $4;
    to_import   RECORD; -- der zu importierende Datensatz aus import_object
    i_farbe     INTEGER; -- enthaelt color_id
    ip          CIDR;   -- IP-Adresse
    ip_end      CIDR;   -- letzte IP-Adresse eines Ranges
    z           RECORD; -- Zonensatz (fuer ZonenID)
    i_typ       INTEGER; -- object_typ id (fuer obj_typ_id)
    zoneID      INTEGER;    -- lokale Variable mit ZonenID
	i_admin_id	INTEGER;	-- id des admins der die letzte Aenderung gemacht hat
    existing_obj   RECORD; -- der ev. bestehende  Datensatz aus object
    b_insert	BOOLEAN; -- soll eingefuegt werden oder nicht?
    b_change	BOOLEAN; -- hat sich etwas geändert?
    b_change_security_relevant	BOOLEAN; -- hat sich etwas sicherheitsrelevantes geändert?
    v_change_id VARCHAR;	-- type of change
	b_is_documented BOOLEAN; 
	t_outtext TEXT; 
	i_change_type INTEGER;
	i_new_obj_id  BIGINT;	-- id des neu eingefügten object
	v_comment	VARCHAR;
BEGIN
    b_insert := FALSE;
    b_change := FALSE;
    b_change_security_relevant := FALSE;
	RAISE DEBUG 'processing import_nwobj_single start';
    SELECT INTO to_import * FROM import_object WHERE obj_id = id; -- zu importierenden Datensatz aus import_object einlesen
    IF NOT (to_import.obj_zone IS NULL) THEN  -- wenn Zone-Info vorhanden (i.e. Netscreen-Object)
	    SELECT INTO z zone_id FROM zone WHERE zone_name = to_import.obj_zone AND mgm_id = i_mgm_id; -- ZoneID holen
	    IF NOT FOUND THEN -- TODO: das muss noch automatisiert werden: Neuanlegen einer Zone
			PERFORM add_data_issue(i_control_id, to_import.obj_zone, to_import.obj_zone, NULL, NULL, 'zone object', 'undefined zone obj in network object', NULL);
       		PERFORM error_handling('ERR_ZONE_MISS', to_import.obj_zone);
	    END IF;
	    zoneID := z.zone_id; -- zoneID fuer spaeteres INSERT zwischenspeichern
    ELSE zoneID := NULL; -- zoneID fuer spaeteres INSERT auf NULL setzen
    END IF;
	RAISE DEBUG 'processing import_nwobj_single 2';
    SELECT INTO i_typ obj_typ_id FROM stm_obj_typ WHERE obj_typ_name = to_import.obj_typ; -- obj_typ_id holen (network,host,...)
    IF NOT FOUND THEN -- TODO: das muss noch automatisiert werden: Neuanlegen eines obj_typ
       PERFORM add_data_issue(i_control_id, to_import.obj_typ, to_import.obj_typ, NULL, NULL, NULL, 'nw obj typ', 'undefined nw obj type in network object: ' || to_import.obj_typ, NULL);
       PERFORM error_handling('ERR_OBJTYP_MISS', to_import.obj_typ);
    END IF;
    -- color_id holen (normalisiert ohne SPACES und in Kleinbuchstaben)
	RAISE DEBUG 'processing import_nwobj_single 3';
    SELECT INTO i_farbe color_id FROM stm_color WHERE color_name = LOWER(remove_spaces(to_import.obj_color));
	RAISE DEBUG 'processing import_nwobj_single 4';
    IF NOT FOUND THEN -- TODO: Fehlerbehandlung bzw. automat. Neuanlegen einer Farbe?
		i_farbe := NULL;
		IF to_import.obj_color IS NOT NULL THEN
        	PERFORM add_data_issue(i_control_id, to_import.obj_color, NULL, NULL, NULL, NULL, 'nw obj color', 'undefined color in network object: ' || to_import.obj_color, NULL);
		END IF;
    END IF;
    -- finde Objekt mit gleichem namen, zone_id und Management
	IF (to_import.obj_uid IS NULL OR char_length(to_import.obj_uid) = 0) THEN -- nur der Weg ueber den Namen als ID geht
	    SELECT INTO existing_obj * FROM object
		WHERE (obj_name=to_import.obj_name AND mgm_id=i_mgm_id AND (zone_id=zoneID OR (zone_id IS NULL AND zoneID IS NULL)) AND active);
		-- in diesem Fall muessten alle devices des betroffenen Mgmts neu eingelesen werden, falls eine Umbenennung stattfand
	ELSE  -- obj_uid ist nicht leer: nehme dieses Feld als ID-Anteil anstatt Namen: erschlaegt Umbenennungen
	    SELECT INTO existing_obj * FROM object
		WHERE (obj_uid=to_import.obj_uid AND mgm_id=i_mgm_id AND (zone_id=zoneID OR (zone_id IS NULL AND zoneID IS NULL)) AND active);
	END IF;
	RAISE DEBUG 'processing import_nwobj_single 5';
	IF FOUND THEN  -- object schon vorhanden
		IF (NOT ( 
			are_equal(existing_obj.obj_uid, to_import.obj_uid) AND
			are_equal(existing_obj.obj_typ_id, i_typ) AND
			are_equal(existing_obj.obj_ip,to_import.obj_ip) AND
			are_equal(existing_obj.obj_ip_end, to_import.obj_ip_end) AND
			are_equal(existing_obj.obj_member_names, to_import.obj_member_names) AND
			are_equal(existing_obj.obj_member_refs, to_import.obj_member_refs) AND
			are_equal(existing_obj.obj_name, to_import.obj_name)
		))
		THEN
			b_change := TRUE;
			b_change_security_relevant := TRUE;
		END IF;
		IF (NOT( -- from here on non-security relevant changes
			are_equal(existing_obj.obj_comment, to_import.obj_comment) AND
			are_equal(existing_obj.obj_color_id, i_farbe) AND
			are_equal(existing_obj.obj_location, to_import.obj_location)
		))
		THEN -- object unchanged
			b_change := TRUE;
		END IF;
		IF (b_change) THEN
			v_change_id := 'INFO_OBJ_CHANGED';
		ELSE
			UPDATE object SET obj_last_seen = i_control_id WHERE obj_id = existing_obj.obj_id AND mgm_id=i_mgm_id;
		END IF;
	ELSE
		b_insert := TRUE;
		v_change_id := 'INFO_OBJ_INSERTED'; 
	END IF;
	RAISE DEBUG '1 - nw_obj_single: %', to_import.obj_name;
	IF (b_change OR b_insert) THEN
		PERFORM error_handling(v_change_id, to_import.obj_name);
		i_admin_id := get_admin_id_from_name(to_import.last_change_admin);
	    INSERT INTO object
    	   (mgm_id,obj_name,obj_ip,obj_ip_end,zone_id,obj_typ_id,obj_comment,obj_member_names,obj_member_refs,obj_location,
    	   	obj_color_id,obj_uid,last_change_admin,obj_last_seen,obj_create)
    	   VALUES (i_mgm_id,to_import.obj_name,to_import.obj_ip,to_import.obj_ip_end,zoneID,i_typ,
               to_import.obj_comment,to_import.obj_member_names,to_import.obj_member_refs,to_import.obj_location,i_farbe,to_import.obj_uid,
               	i_admin_id, i_control_id, i_control_id);
		RAISE DEBUG '2 - nw_obj_single: %', to_import.obj_name;
        -- changelog-Eintrag
        SELECT INTO i_new_obj_id MAX(obj_id) FROM object WHERE mgm_id=i_mgm_id; -- ein bisschen fragwuerdig
		IF (b_insert) THEN  -- das nw-objekt wurde neu angelegt
			IF b_is_initial_import THEN
				b_is_documented := TRUE;  t_outtext := 'INITIAL_IMPORT'; i_change_type := 2;
			ELSE
				b_is_documented := FALSE; t_outtext := NULL; i_change_type := 3;
			END IF;
			-- fest verdrahtete Werte: weniger gut
			INSERT INTO changelog_object
				(control_id,new_obj_id,old_obj_id,change_action,import_admin,documented,changelog_obj_comment,mgm_id,change_type_id)
				VALUES (i_control_id,i_new_obj_id,NULL,'I',i_admin_id,b_is_documented,t_outtext,i_mgm_id,i_change_type);
			RAISE DEBUG '3 - nw_obj_single: %', to_import.obj_name;
		ELSE -- change
			IF (b_change_security_relevant) THEN
				v_comment := NULL;
				b_is_documented := FALSE;
			ELSE
				v_comment := 'NON_SECURITY_RELEVANT_CHANGE';
				b_is_documented := TRUE;
			END IF;
			INSERT INTO changelog_object
				(control_id,new_obj_id,old_obj_id,change_action,import_admin,documented,mgm_id,security_relevant,changelog_obj_comment)
				VALUES (i_control_id,i_new_obj_id,existing_obj.obj_id,'C',i_admin_id,b_is_documented,i_mgm_id,b_change_security_relevant,v_comment);
			-- erst jetzt kann active beim alten object auf FALSE gesetzt werden
			UPDATE object SET active = FALSE WHERE obj_id = existing_obj.obj_id; -- altes Objekt auf not active setzen
			RAISE DEBUG '4 - nw_obj_single: %', to_import.obj_name;
		END IF;
	END IF;
    RETURN;
END;
$BODY$
  LANGUAGE 'plpgsql' VOLATILE
  COST 100;
