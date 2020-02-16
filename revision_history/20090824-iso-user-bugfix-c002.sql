-- importer/CACTUS/ISO/import/checkpoint.pm und importer/iso-importer-single.pl austauschen
-- evtl. agents/checkpoint-cp-config-locally.sh austauschen

-- psql -U itsecorg -h localhost -d isov1 -c "\i /usr/local/itsecorg/install/database/iso-import-main.sql"

switch to user itsecorg
$ cd importer
$ ./iso-importer-single.pl mgm_name=<mgm-name> -no-md5-checks -clear-all-rules
$ ./iso-importer-single.pl mgm_name=<mgm-name> -no-md5-checks


/*

SELECT max(control_id) AS import_id FROM import_control INNER JOIN management USING (mgm_id) INNER JOIN device ON (management.mgm_id=device.mgm_id) WHERE start_time<='2008-09-01 10:10' AND NOT stop_time IS NULL AND successful_import AND  ( FALSE  OR management.mgm_id=4  OR management.mgm_id=60  OR management.mgm_id=712  ) AND  ( device.dev_id IS NULL  OR device.dev_id=4  OR device.dev_id=60  OR device.dev_id=701  )  AND device.dev_id=4 GROUP BY device.dev_id

SELECT max(control_id) AS import_id, import_control.mgm_id AS mgm_id FROM import_control INNER JOIN management USING (mgm_id) INNER JOIN device ON (management.mgm_id=device.mgm_id) WHERE start_time<='2008-09-01 10:10' AND NOT stop_time IS NULL AND  ( FALSE  OR management.mgm_id=4  OR management.mgm_id=60  OR management.mgm_id=712  ) AND  ( device.dev_id IS NULL  OR device.dev_id=4  OR device.dev_id=60  OR device.dev_id=701  )  AND successful_import GROUP BY import_control.mgm_id

SELECT max(control_id) AS import_id, device.dev_id AS dev_id FROM import_control INNER JOIN management USING (mgm_id) INNER JOIN device ON (management.mgm_id=device.mgm_id) WHERE start_time<='2008-09-01 10:10' AND NOT stop_time IS NULL AND  ( FALSE  OR management.mgm_id=4  OR management.mgm_id=60  OR management.mgm_id=712  ) AND  ( device.dev_id IS NULL  OR device.dev_id=4  OR device.dev_id=60  OR device.dev_id=701  )  AND successful_import GROUP BY device.dev_id

SELECT obj_id,object.mgm_id FROM object INNER JOIN management ON (object.mgm_id=management.mgm_id) INNER JOIN device ON (management.mgm_id=device.mgm_id) WHERE object.mgm_id=4 AND  ( FALSE  OR management.mgm_id=4  OR management.mgm_id=60  OR management.mgm_id=712  ) AND  ( device.dev_id IS NULL  OR device.dev_id=4  OR device.dev_id=60  OR device.dev_id=701  ) 



SELECT rule.rule_id FROM rule  JOIN temp_filtered_rule_ids ON (temp_filtered_rule_ids.rule_id=rule.rule_id)  WHERE  temp_filtered_rule_ids.report_id=1598843077 GROUP BY rule.rule_id 


SELECT rule_order.dev_id, rule_order.rule_number, rule.*, from_zone.zone_name,to_zone.zone_name FROM rule_order INNER JOIN device ON (rule_order.dev_id=device.dev_id)
  INNER JOIN management ON (device.mgm_id=management.mgm_id)  INNER JOIN rule USING (rule_id) LEFT JOIN zone as from_zone ON rule.rule_from_zone=from_zone.zone_id 
LEFT JOIN zone as to_zone ON rule.rule_to_zone=to_zone.zone_id INNER JOIN import_control ON (rule_order.control_id=import_control.control_id)
 INNER JOIN stm_track ON (stm_track.track_id=rule.track_id) INNER JOIN stm_action ON (stm_action.action_id=rule.action_id) INNER JOIN temp_filtered_rule_ids ON (rule.rule_id=temp_filtered_rule_ids.rule_id)
 WHERE temp_filtered_rule_ids.report_id=1598843077 AND successful_import AND (rule_order.control_id, management.mgm_id) IN ((107,60),(62,712),(185,4))
 ORDER BY rule_order.dev_id,from_zone.zone_name,to_zone.zone_name,rule_order.rule_number




SELECT rule_order.dev_id, rule_order.rule_number, rule.*, from_zone.zone_name,to_zone.zone_name FROM rule_order INNER JOIN device ON (rule_order.dev_id=device.dev_id)
  INNER JOIN management ON (device.mgm_id=management.mgm_id)  INNER JOIN rule USING (rule_id) LEFT JOIN zone as from_zone ON rule.rule_from_zone=from_zone.zone_id 
LEFT JOIN zone as to_zone ON rule.rule_to_zone=to_zone.zone_id INNER JOIN import_control ON (rule_order.control_id=import_control.control_id)
 INNER JOIN stm_track ON (stm_track.track_id=rule.track_id) INNER JOIN stm_action ON (stm_action.action_id=rule.action_id)
 WHERE successful_import AND (rule_order.control_id, management.mgm_id) IN ((107,60),(62,712),(185,4))
 ORDER BY rule_order.dev_id,from_zone.zone_name,to_zone.zone_name,rule_order.rule_number



select dev_id, dev_name, mgm_id, do_not_import from device;
select mgm_id, mgm_name, do_not_import from management;
select control_id, mgm_id, mgm_name, start_time FROM import_control LEFT JOIN management USING (mgm_id) WHERE successful_import ORDER BY control_id DESC LIMIT 10;


-- komplettes Regelwerk des letzten erfolgreichen Imports:

CREATE VIEW v_rulebase_last_import AS 
SELECT rule_order.dev_id, rule_order.rule_number, rule.*, from_zone.zone_name AS from_zone,to_zone.zone_name AS to_zone
 FROM rule_order
 INNER JOIN device ON (rule_order.dev_id=device.dev_id AND control_id IN (select max(control_id) from import_control where successful_import))
 INNER JOIN management ON (device.mgm_id=management.mgm_id) 
 INNER JOIN rule USING (rule_id)
 LEFT JOIN zone as from_zone ON rule.rule_from_zone=from_zone.zone_id 
 LEFT JOIN zone as to_zone ON rule.rule_to_zone=to_zone.zone_id
 INNER JOIN import_control ON (rule_order.control_id=import_control.control_id)
 INNER JOIN stm_track ON (stm_track.track_id=rule.track_id)
 INNER JOIN stm_action ON (stm_action.action_id=rule.action_id)
 WHERE successful_import
-- AND (rule_order.control_id, management.mgm_id) IN ((185,4))
  ORDER BY rule_order.dev_id,from_zone.zone_name,to_zone.zone_name,rule_order.rule_number;

-- nur Regeln mit Usern:

SELECT * FROM rule_from LEFT JOIN v_rulebase_last_import ON (v_rulebase_last_import.rule_uid=rule_from.rule_id) WHERE NOT rule_from.user_id IS NULL AND rule_from.active;

select max(control_id) from import_control where successful_import;
select mgm_id from management limit 1;


ALTER DATABASE name RENAME TO newname

-- SELECT * FROM rule_from LEFT JOIN v_rulebase_last_import ON (v_rulebase_last_import.rule_uid=rule_from.rule_id) WHERE NOT rule_from.user_id IS NULL AND rule_from.active;
-- select  * from rule where rule_id IN (1902, 1903, 1908);
-- select * from v_rulebase_last_import where rule_src like '%@%';

SELECT * FROM get_rule_src(51, NULL, '2008-09-02')


-- GUI-SQL Statement 
SELECT usr.user_name, usr.user_id, object.obj_name, object.obj_id, object.obj_ip, object.zone_id, 
stm_obj_typ.obj_typ_name AS obj_type 
FROM object
LEFT JOIN stm_obj_typ USING (obj_typ_id) 
 LEFT JOIN rule_from ON (object.obj_id=rule_from.obj_id)
  LEFT JOIN usr ON (usr.user_id=rule_from.user_id)  
  WHERE rule_from.rule_id=1900 --> Zeitpunkt  des richtigen Users wird nicht berücksichtigt!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
  AND object.obj_id IN (SELECT * FROM get_rule_src(1900, NULL, '2008-09-02')) 
   ORDER BY obj_name, user_name




---------------------------


-- View erzeugen, die die User-Auth-Regeln des letzten Imports in der DB lesbar anzeigt

SELECT rule_order.dev_id, rule_order.rule_number, rule.*, from_zone.zone_name AS from_zone,to_zone.zone_name AS to_zone
 FROM rule_order
 INNER JOIN device ON (rule_order.dev_id=device.dev_id AND control_id IN (select max(control_id) from import_control where successful_import))
 INNER JOIN management ON (device.mgm_id=management.mgm_id) 
 INNER JOIN rule USING (rule_id)
 LEFT JOIN zone as from_zone ON rule.rule_from_zone=from_zone.zone_id 
 LEFT JOIN zone as to_zone ON rule.rule_to_zone=to_zone.zone_id
 INNER JOIN import_control ON (rule_order.control_id=import_control.control_id)
 INNER JOIN stm_track ON (stm_track.track_id=rule.track_id)
 INNER JOIN stm_action ON (stm_action.action_id=rule.action_id)
 WHERE successful_import AND rule_src LIKE '%@%'
  ORDER BY rule_order.dev_id,from_zone.zone_name,to_zone.zone_name,rule_order.rule_number;

-- Die User-Auth-Regeln des letzten Imports anzeigen
SELECT rule_order.dev_id, rule_order.rule_number, rule.rule_id, rule.rule_src, rule.rule_dst, rule.rule_svc, rule.rule_action
 FROM rule_order
 INNER JOIN device ON (rule_order.dev_id=device.dev_id AND control_id IN (select max(control_id) from import_control where successful_import))
 INNER JOIN management ON (device.mgm_id=management.mgm_id) 
 INNER JOIN rule USING (rule_id)
 INNER JOIN import_control ON (rule_order.control_id=import_control.control_id)
 INNER JOIN stm_track ON (stm_track.track_id=rule.track_id)
 INNER JOIN stm_action ON (stm_action.action_id=rule.action_id)
 WHERE successful_import AND rule_src LIKE '%@%'
 ORDER BY rule_order.dev_id,rule_order.rule_number;

-- Von einer dieser Regeln - hier rule_id 51 (2x unten angeben) - die Quellen anzeigen:
SELECT usr.user_name, usr.user_id, object.obj_name, object.obj_id, object.obj_ip, object.zone_id, 
stm_obj_typ.obj_typ_name AS obj_type 
FROM object
LEFT JOIN stm_obj_typ USING (obj_typ_id) 
 LEFT JOIN rule_from ON (object.obj_id=rule_from.obj_id)
  LEFT JOIN usr ON (usr.user_id=rule_from.user_id)  
  WHERE rule_from.rule_id=51 
  AND object.obj_id IN (SELECT * FROM get_rule_src(51, NULL, '2008-09-02')) 
   ORDER BY obj_name, user_name;



- SELECT device.mgm_id FROM rule_order LEFT JOIN device USING (dev_id) WHERE rule_id=53 LIMIT 1;
-- select * from get_import_id_for_mgmt_at_time(4,'2008-09-03');

SELECT obj_id, rf_last_seen, rf_create FROM rule,rule_from
WHERE rf_last_seen>=19 AND rf_create<=19 AND rule.rule_id=rule_from.rule_id AND rule.rule_id=53;

SELECT obj_id, rf_last_seen, rf_create FROM rule,rule_from
WHERE rule.rule_id=rule_from.rule_id AND rule.rule_id=53;


    obj_id last_seen, create
--> 7,     17,        11

a) Funktion, die zu jedem User wieder die Referenzen korrigiert
   --> es wird nicht erkannt, dass die User wieder existieren, die Einträge in rule_from mit den neuen user_ids fehlen
b) im Import-Prozess überprüfen, ob die User-Datei vollständig übertragen wurde
c) in der Webgui (db-rule.php::function get_rule_src) richtiger Zeitpunkt des Users wird nicht berücksichtigt
d) Konsistenz-Check während jedes Imports?


 select * from usr where user_name='Cactus-extern';
 select * from rule_from where user_id=17 or user_id=35;

für alle basis-objekte fehlt das ref-handling der rule_to und rule_from im Insert-Fall: 
	PERFORM import_nwobj_refhandler_change_rule_from_refs			(i_old_id, i_new_id, i_current_import_id);
	PERFORM import_nwobj_refhandler_change_rule_to_refs				(i_old_id, i_new_id, i_current_import_id);

Das gleiche Problem tritt auch z.B. bei NW-Diensten auf:
a) Dienste in objects.C löschen
b) wieder hinzufügen
--> Regeln enthalten keine Dienste mehr


-- SELECT obj_id, rf_last_seen, rf_create FROM rule,rule_from
-- WHERE rule.rule_id=rule_from.rule_id AND rule.rule_id=53;

select * from object where obj_id=7;
select * from rule_from where obj_id=7;

select object.*, rule_from.* from rule_from left join object using (obj_id)
	where object.obj_uid='26405A41-425C-4053-8002-497BB82D76B1';

-->	rf_last_seen<=17

-- select usr.*, rule_from.* from rule_from left join usr using (user_id)
--	where usr.user_uid='Cactus-extern';

-->	rf_last_seen<=17

Im Import-Lauf 17 wurden alle Quell-Objekte und User aus den Regeln entfernt:

select * from changelog_user where control_id=17; --> 17 User-Deletes und sonst nix in changelog_xxx


Bei FMG wurden
- mit control_id =  97632 alle User gelöscht und
- mit 97633 wieder neu angelegt.
- Letzter erfolgreicher Import vor dem Desaster: 97632 --> prüfen.
- Letzter erfolgreicher Import: ???

Auf Test-System wurden
- mit control_id =  17 alle User gelöscht und
- mit 19 wieder neu angelegt.
- Letzter erfolgreicher Import vor dem Desaster: 15.
- Letzter erfolgreicher Import: 36

DELETE FROM usergrp WHERE usergrp_id IN (SELECT user_id FROM usr WHERE user_create=19)
  OR usergrp_member_id IN (SELECT user_id FROM usr WHERE user_create=19); -- Alle faelschlich neu erzeugten User aus Gruppen loeschen
DELETE FROM usergrp_flat WHERE usergrp_flat_id IN (SELECT user_id FROM usr WHERE user_create=19)
  OR usergrp_flat_member_id IN (SELECT user_id FROM usr WHERE user_create=19); -- Alle faelschlich neu erzeugten User aus Gruppen loeschen

DELETE FROM changelog_user WHERE control_id=19 OR new_user_id IN (SELECT user_id FROM usr WHERE user_create=19)
  OR old_user_id IN (SELECT user_id FROM usr WHERE user_create=19); -- Alle faelschlich erzeugten changelog_user-Eintraege loeschen
DELETE FROM usr WHERE user_create=19; -- Alle faelschlich neu erzeugten User loeschen

UPDATE usr SET user_last_seen=36, active=TRUE WHERE user_last_seen=15; -- Lebenszeiten usr wieder verlaengern
UPDATE rule_from SET rf_last_seen=36 WHERE rf_last_seen=17; -- Lebenszeiten rule_from wieder verlaengern
 --> warum hier der Unterschied??? 15<>17


Test2:
Auf Test-System wurden
- mit control_id =  38 alle User gelöscht und
- mit 39 wieder neu angelegt.
- Letzter erfolgreicher Import vor dem Desaster: 36.
- Letzter erfolgreicher Import: 39

DELETE FROM usergrp WHERE usergrp_id IN (SELECT user_id FROM usr WHERE user_create=39)
  OR usergrp_member_id IN (SELECT user_id FROM usr WHERE user_create=39); -- Alle faelschlich neu erzeugten User aus Gruppen loeschen
DELETE FROM usergrp_flat WHERE usergrp_flat_id IN (SELECT user_id FROM usr WHERE user_create=39)
  OR usergrp_flat_member_id IN (SELECT user_id FROM usr WHERE user_create=39); -- Alle faelschlich neu erzeugten User aus Gruppen loeschen

DELETE FROM changelog_user WHERE control_id=39 OR new_user_id IN (SELECT user_id FROM usr WHERE user_create=39)
  OR old_user_id IN (SELECT user_id FROM usr WHERE user_create=39); -- Alle faelschlich erzeugten changelog_user-Eintraege loeschen
DELETE FROM usr WHERE user_create=39; -- Alle faelschlich neu erzeugten User loeschen

UPDATE usr SET user_last_seen=39, active=TRUE WHERE user_last_seen=36; -- Lebenszeiten usr wieder verlaengern
UPDATE rule_from SET rf_last_seen=39 WHERE rf_last_seen=36; -- Lebenszeiten rule_from wieder verlaengern

*/