<?php
// $Id: submit_documentation_data.php,v 1.1.2.6 2012-06-06 09:35:29 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/Attic/submit_documentation_data.php,v $
	// get all POST parameters from $_REQUEST
	// call function to write data to db
	
	if (!isset($_SESSION)) session_start();
	require_once ("db-input.php");
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$glob_request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);
	require_once("check_privs.php");
	require_once("operating-system.php");
	require_once("db-base.php");
	require_once("db-change.php");
	require_once("db-input.php");
	require_once("db-div.php");
	require_once("db-client.php");
	
	define ('DEBUGGING', 0);		// either 0 (turned off) or 1 (on)
	function output_debug($txt) {
		$dbg_header = "submit_documentation.php::";
		if (DEBUGGING)
			echo "$dbg_header$txt<br>";	
	}
	
	if (isset($glob_request['change_docu'])) $page = "changedoc"; else $page = "doc";
	if (($page=="changedoc" and !$allowedToChangeDocumentation) or ($page=="doc" and !$allowedToDocumentChanges)) {
		header("Location: ".$stamm."index2.php");
	}
	
	if (!isset($_SESSION)) session_start();
	if (!isset($_SESSION["auth"])) { header("Location: /index.php"); }
		
	$filter	= new RuleChangesFilter($glob_request, $session, 'change_documentation');
	$db_connection = new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"]));
	$clist	= new ClientList($filter,$db_connection);
	$rtlist	= new RequestTypeList($filter,$db_connection);
	
	$felder = $glob_request;  ksort($felder);  reset($felder);
	if (DEBUGGING) { while (list($feldname, $val) = each($felder)) { output_debug("found $feldname: $val"); } reset($felder); }
	
	$comment = '';
	$input = new DbInput();  // for clean-function
	while (list($feldname, $val) = each($felder)) { // Schleife fuer Input-Werte
		$feldwertclean = $input->clean($val,1000000);
		output_debug ("found: $feldname: $val");
		if ($feldwertclean<>"") {
			$found_one = true;
			if (!(strpos($feldname,'$')===false))
				list($typ,$idx) = explode("$", $feldname);
			else
				$typ = $feldname;
			switch ($typ) {
				case 'change_selection': 
					$splitted = explode('validfor$', $feldwertclean);
					foreach ($splitted as $single_change) {
						if ($single_change <> '') {  // ignore first (i.e. empty) entry
							list($base_table,$change_id) = explode('$',$single_change);
							$changelog{"$change_id"} = $base_table;
						}
					}
					break;
				case 'client_id':		$client_id[$idx]		= $feldwertclean; break;
				case 'request_type_id':	$request_type_id[$idx]	= $feldwertclean; break;
				case 'request':			$auftrag[$idx]			= $feldwertclean; break;
				case 'comment':			$comment				= $feldwertclean; break;
				case 'doku_admin':		$doku_admin				= $feldwertclean; break;
				case 'doku_admin_id':	$doku_admin_id			= $feldwertclean; break;
				case 'doc_type':		break;
				default: output_debug ("no match found - default NOP for $val"); break;
	 		}
		}
	}
	$anzahl_requests = count($auftrag);	//	print_r ($auftrag);
	$anzahl_changelogs = count($changelog);
	if (DEBUGGING) output_debug ("found a total of $anzahl_changelogs changes, resulting from $anzahl_requests requests"); 
	
	if ($anzahl_changelogs>0 && $anzahl_requests>0) { // wenn keine Aenderung ausgewaehlt oder kein Auftrag eingegeben--> nix machen
		// taking $auftrag as master (as this is the only mandatory field) --> deleting all other values in all arrays
		for ($i=0; $i<$anzahl_requests; ++$i) if (!isset($auftrag[$i])) unset ($request_type_id[$i], $client_id[$i]);
		
		$change_element = new ChangedElement(array(),$filter,NULL); // only used for function "set_changelog_sql_values"
		$client_id			= array_merge($client_id); // ev. Luecken entfernen
		$request_type_id	= array_merge($request_type_id); // ev. Luecken entfernen
		$auftrag 			= array_merge($auftrag);
		reset($changelog); reset($auftrag); reset($client_id); reset($request_type_id);

		$LineBreakStr = "' || E'\\n' || '"; 
		$sql_code = '';
		$comment_header = date("d.m.Y H:i") . $LineBreakStr . "Administrator: $doku_admin" . $LineBreakStr;
		$dblist = new DbList();
		$dblist->initSessionConnection();
		
		if ($glob_request['doc_type'] == 'doc') { // noch nicht dokumentierte Aenderungen dokumentieren
			$anzahl_auftraege = $anzahl_requests;
//			$anzahl_auftraege = count($auftrag);
			output_debug ("anzahl_auftraege: $anzahl_auftraege");
			$change_request_str = '';
			for ($i=0; $i<$anzahl_auftraege; ++$i) {
				if (isset($auftrag[$i])) {
					$comment_header .= "Neuer ";
					if (isset($request_type_id[$i]) and $request_type_id[$i]!='NULL') {
						$rt_name = $rtlist->getRequestTypeName($request_type_id[$i]);
		 				$comment_header .= "$rt_name-"; // Bindestrich zwischen Auftragstyp und dem Wort "Auftrag" (z.B. ARS-Auftrag) 	
					}
					$comment_header .= "Auftrag";
					if (isset($client_id[$i]) and $client_id[$i]!='NULL') {
						$client_name = $clist->getClientName($client_id[$i]);
		 				$comment_header .= " Mandant: $client_name"; 	
					}
					$comment_header .= ", Auftragsnummer: " . $auftrag[$i] . $LineBreakStr;
					$sql_code .= "INSERT INTO request (request_number,client_id,request_type_id) VALUES ('" . $auftrag[$i] . "'," .
						$client_id[$i] . "," . $request_type_id[$i] . "); ";
					while (list($changelog_id, $table_name) = each($changelog)) {
						list($log_id_name, $request_change_table, , ) =	$change_element->set_changelog_sql_values($table_name); 
						$sql_code .= ("INSERT INTO $request_change_table " .
							"($log_id_name,request_id) VALUES ($changelog_id,SELECT MAX(request_id) FROM request; ");
					}
					// build change_request string for changelog_xxx
					if ($change_request_str<>'') $change_request_str .= '<br>';
					if ($client_id[$i]!='NULL') $change_request_str .= "$client_name: ";
					if ($request_type_id[$i]!='NULL') $change_request_str .= "$rt_name-";
					$change_request_str .= $auftrag[$i];
				}
				// pointer auf Anfang des Feldes zuruecksetzen
				reset($changelog); 
			}
			while (list($changelog_id, $table_name) = each($changelog)) {
				list($log_id_name, , $changelog_table, $comment_field) = $change_element->set_changelog_sql_values($table_name); 
				$sql_code .= ("UPDATE $changelog_table SET doku_admin='" . $doku_admin_id . "', $comment_field='" .
					$comment_header . $comment . "', documented=TRUE" .
					", change_request_info='$change_request_str'" .					
					" WHERE $log_id_name=$changelog_id; ");
			}
			output_debug ("sql: $sql_code");
			$redirect_destination = "Location: /documentation.php";
		} else { // bereits dokumentierte Aenderungen korrigieren
	///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////	
	
			// zu allen zu korrigierenden Aenderungen die alten Doku-Daten einlesen (request, request_xxx_change, changelog_xxx)
			$sql_code = "SELECT local_change_id, change_element, change_comment FROM view_reportable_changes WHERE FALSE";
			while (list($changelog_id, $table_name) = each($changelog))
				$sql_code .= " OR (local_change_id=$changelog_id AND change_element='" . $table_name . "')";
			reset($changelog); // pointer auf Anfang des Feldes zuruecksetzen
			output_debug ("sql: $sql_code");
			$changes_to_change_list = $dblist->db_connection->iso_db_query($sql_code);
			
			// die alten zu l�schenden Relationen (request <-> request_xxx_change) l�schen
			$rows = $changes_to_change_list->rows;
			$sql_code = '';
			for ($zi = 0; $zi < $rows; ++ $zi) {
				$table_name = $changes_to_change_list->data[$zi]['change_element'];
				$log_id = $changes_to_change_list->data[$zi]['local_change_id'];
				output_debug ("$zi. table_name: $table_name");
				list($log_id_name, $request_xxx_change_table, $changelog_table, $comment_field) = $change_element->set_changelog_sql_values($table_name);
				$sql_code .= "DELETE FROM $request_xxx_change_table WHERE $log_id_name=$log_id; ";
			}
			output_debug ("<br>sql: $sql_code");
			$dblist->db_connection->iso_db_query($sql_code);
			
			// anschliessend alle Eintraege in request loeschen, auf die nicht mehr verwiesen wird.
			// a) nicht referenzierte Requests identifizieren
			$sql_code = " (select request.request_id from request left join request_rule_change using (request_id)" .
						" where request_rule_change.request_id IS NULL) INTERSECT " . 
						" (select request.request_id from request left join request_object_change using (request_id)" . 
						" where request_object_change.request_id IS NULL) INTERSECT " . 
						" (select request.request_id from request left join request_service_change using (request_id) " .
						" where request_service_change.request_id IS NULL) INTERSECT " .
						" (select request.request_id from request left join request_user_change using (request_id) " .
						" where request_user_change.request_id IS NULL) ";
			output_debug ("sql: $sql_code");
			$unreferenced_requests = $dblist->db_connection->iso_db_query($sql_code);
	
			// b) sql_code zum Loeschen der nicht referenzierten Requests erzeugen
			$sql_code = '';
			$unref_req_rows = $unreferenced_requests->rows;
			output_debug ("found $unref_req_rows unreferenced requests");
			for ($zi = 0; $zi < $unref_req_rows; ++ $zi) {
				$unref_request_id = $unreferenced_requests->data[$zi]['request_id'];
				output_debug ("found unreferenced request: $unref_request_id");
				$sql_code .= "DELETE FROM request WHERE request_id=$unref_request_id; ";			
			}
			output_debug ("sql_code for deleting unref requests: $sql_code");
	
			// die neuen requests einfuegen
			$new_comment = $LineBreakStr . $LineBreakStr . date("d.m.Y H:i") . $LineBreakStr . "Administrator: $doku_admin" . $LineBreakStr;
			$change_request_str = '';
			for ($i=0; $i<count($client_id); ++$i) {
				$new_comment .= "Neuer ";
				if ($request_type_id[$i]!='NULL') {
					$rt_name = $rtlist->getRequestTypeName($request_type_id[$i]);
	 				$new_comment .= "$rt_name-"; // Bindestrich zwischen Auftragstyp und dem Wort "Auftrag" (z.B. ARS-Auftrag) 	
				}
				$new_comment .= "Auftrag";
				if ($client_id[$i]!='NULL') {
					$client_name = $clist->getClientName($client_id[$i]);
	 				$new_comment .= " Mandant: $client_name"; 	
				} 	
				$new_comment .= ", Auftragsnummer: " . $auftrag[$i] . $LineBreakStr;
				if ($change_request_str<>'') $change_request_str .= '<br>';
				if ($client_id[$i]!='NULL') $change_request_str .= "$client_name: ";
				if ($request_type_id[$i]!='NULL') $change_request_str .= "$rt_name-";
				$change_request_str .= $auftrag[$i];

				$sql_code .= "INSERT INTO request (request_number,client_id,request_type_id) VALUES ('" . $auftrag[$i] . "'," . $client_id[$i] . "," . $request_type_id[$i] . "); ";
				reset($changelog); // pointer auf Anfang des Feldes zuruecksetzen
				while (list($changelog_id, $table_name) = each($changelog)) {
					list($log_id_name, $request_change_table, , ) =	$change_element->set_changelog_sql_values($table_name); 
					$sql_code .= ("INSERT INTO $request_change_table " .
						"($log_id_name,request_id) VALUES ($changelog_id,SELECT MAX(request_id) FROM request); ");
				}
			}
			output_debug ("sql_code for deleting unref requests and adding new req-relations: $sql_code");
	
			reset($changelog); // pointer auf Anfang des Feldes zuruecksetzen
			while (list($changelog_id, $table_name) = each($changelog)) {
				output_debug ("table_name: $table_name, id: $changelog_id");
				list($log_id_name, $request_change_table, $changelog_table, $comment_field ) =	$change_element->set_changelog_sql_values($table_name); 
				// $old_comment aus $changes_to_change_list herausholen
				for ($zi = 0; $zi < $rows; ++ $zi) {
					$old_table_name = $changes_to_change_list->data[$zi]['change_element'];
					$old_log_id = $changes_to_change_list->data[$zi]['local_change_id'];
					if ($old_log_id == $changelog_id and $table_name==$old_table_name) $old_comment = $changes_to_change_list->data[$zi]['change_comment'];
				}
				output_debug ("found old_comment: $old_comment");
				$sql_code .= ("UPDATE $changelog_table SET doku_admin='" . $doku_admin_id . "', $comment_field='" .
					$old_comment . $new_comment . $comment . "', documented=TRUE " .
						", change_request_info='$change_request_str'" .		
						" WHERE $log_id_name=$changelog_id; ");
			}
			$redirect_destination = "Location: /documentation.php?change_docu=1";
		}
		output_debug ("sql_code before execution: $sql_code");
		$dblist->db_connection->iso_db_query($sql_code);
		if (!DEBUGGING)
			header($redirect_destination);
	}
	echo "Bitte markieren Sie eine zu dokumentierende bzw. zu korrigierende &Auml;nderung.<br>";
?>