--1) DB in iso-fill-stm.sql:
-- isoadmin "Check Point Security Management Server Update Process" eingefügt in table isoadmin
   insert into isoadmin 
	(isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username) 
	VALUES (1,'Check Point Security Management Server Update Process','Check Point','auto');
-- stm_dev_typ: R7x eingefügt --> in iso-fill-stm.sql:
   insert into stm_dev_typ
	(dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc)
	VALUES (7,'Check Point','R7x','Check Point','');
	
-- 2) Perl Importer
-- import.pm:
--    Zeile 634ff: 	
/*
						# Objekttypen fuer Ausgabe umsetzen ?
						if (($local_schluessel eq 'type') && (($network_objects{"$schluessel.$local_schluessel"} eq 'gateway_cluster') || ($network_objects{"$schluessel.$local_schluessel"} eq 'cluster_member'))) {
							print_cell_no_delimiter($fh, 'gateway');
						}
						elsif (($local_schluessel eq 'type') && (($network_objects{"$schluessel.$local_schluessel"} eq 'machines_range'
								|| $network_objects{"$schluessel.$local_schluessel"} eq 'multicast_address_range'))) {
							print_cell_no_delimiter($fh, 'ip_range');
						}
						elsif (($local_schluessel eq 'type') && 
							(($network_objects{"$schluessel.$local_schluessel"} eq 'dynamic_net_obj' ||
							  $network_objects{"$schluessel.$local_schluessel"} eq 'security_zone_obj' ||
							  $network_objects{"$schluessel.$local_schluessel"} eq 'ips_sensor' ||
							  $network_objects{"$schluessel.$local_schluessel"} eq 'voip_gk' ||
							  $network_objects{"$schluessel.$local_schluessel"} eq 'voip_gw' ||
							  $network_objects{"$schluessel.$local_schluessel"} eq 'voip_sip' ||
							  $network_objects{"$schluessel.$local_schluessel"} eq 'voip_mgcp' ||
							  $network_objects{"$schluessel.$local_schluessel"} eq 'voip_skinny'))) {
							print_cell_no_delimiter($fh, 'host');
						}
						elsif (($local_schluessel eq 'type') && 
							(($network_objects{"$schluessel.$local_schluessel"} eq 'group_with_exclusion' ||
							 $network_objects{"$schluessel.$local_schluessel"} eq 'gsn_handover_group' ||
							 $network_objects{"$schluessel.$local_schluessel"} eq 'domain'))) {
							print_cell_no_delimiter($fh, 'group');
								# da es jetzt eine Aufloesung der Ausnahme-Gruppen in normale Gruppen gibt, ist das jetzt OK
						}
*/
					
-- checkpoint.pm
--    Zeile 477: 	if ( $parse_obj_attr eq 'ipaddr_first' || $parse_obj_attr eq 'bogus_ip') {    # R75 new feature zone objects without ip_addr
 