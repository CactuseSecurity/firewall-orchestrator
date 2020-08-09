<?php
// $Id: config_single_user.php,v 1.1.2.3 2009-12-29 13:32:19 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/config/Attic/config_single_user.php,v $
	$stamm="/";
	$page="config";
	require_once("check_privs.php");
	setlocale(LC_CTYPE, "en_US.UTF-8");	
	if (!$allowedToConfigureUsers) { header("Location: ".$stamm."config/configuration.php"); }	
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>fworch Change Device Config</title>
<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
	if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
</script>
</head>

<body class="iframe">
<div id="device_config">
<?php
//	$vars = $request; reset($vars); while (list($key, $val) = each($vars)) { echo "$key => $val<br>"; } reset ($vars);
	require_once ("db-base.php");
	require_once ("db-config.php");
	require_once ("db-div.php");	// ManagementList
	require_once ("display-table.php");
	require_once ("display_menus.php");

	$e = new PEAR();
	$size = 70;		// size of text input field
	$db_connection = new DbConnection(new DbConfig($session["dbuser"],$session["dbpw"]));
	$aktion = $request['aktion'];
	$ergebnis = '';
	switch ($aktion) {
		case 'new_user': // user wird neu angelegt
			$user_id = '';
			$username			= '';	$first_name			= '';
			$last_name			= '';	$start_date 		= '';
			$end_date			= '';	$email				= '';
			$is_isoadmin		= false;
			break;
		case 'save':	// user wird abgespeichert: lese die Variablen aus $request
			$user_id	= $request['userId'];
			if (isset($request['userId']) and $request['userId']<>'') $isoadmin = new IsoadminUser($db_connection, $user_id);
			$username			= $request['username'];
			$first_name			= $request['first_name'];
			$last_name 			= $request['last_name'];
			$start_date			= $request['start_date'];
			$end_date			= $request['end_date'];
			$email				= $request['email'];
			$is_isoadmin		= $request['is_isoadmin'];
			$input_fehler = '';

			if (preg_match("/^$/", $username)) $input_fehler = 'Username darf nicht leer sein.'; 			
			if ($input_fehler == '') {
				if (isset($request['userId']) and $request['userId']<>'')	// Aenderung an bestehendem User
				
				// CREATE ROLE "username" WITH PASSWORD '1n1t1al' LOGIN IN GROUP secuadmins, isoadmins;
				// insert into isoadmin (isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username) VALUES (14,'Vorname','Nachname','username');
				
					$sql_code = "UPDATE device SET dev_name='$dev_name', mgm_id=$dev_mgm_id, dev_rulebase='$dev_rulebase', dev_typ_id=$dev_typ, " .
							"do_not_import=" . (($dev_do_import)?"FALSE":"TRUE") . ", dev_update='$dev_updated' WHERE dev_id=$dev_id";
				else { // neues Device anlegen
					$user_id_code = "SELECT MAX(user_id)+1 AS user_id FROM isoadmin";
					$next_free_user_id = $db_connection->fworch_db_query($user_id_code); $next_free_user_id_no = $next_free_user_id->data[0]['user_id'];
					$sql_code = 'CREATE ROLE "username" WITH PASSWORD \'1n1t1al\' LOGIN IN GROUP secuadmins';
					if ($is_isoadmin) $sql_code .= ',isoadmins';
					$sql_code .= '; ';
					$sql_code .= "INSERT INTO isoadmin (isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username) VALUES " .
						"($next_free_user_id_no,'$first_name','$last_name','$username');";
				}
				echo "sql_code: $sql_code<br>";
//				$result = $db_connection->fworch_db_query($sql_code);
				if (!$e->isError($result) and $result) $ergebnis = '<BR><B>Speichern erfolgreich: </B>';
				else $ergebnis = '<BR><B>FEHLER: Speichern nicht erfolgreich!</B>';
			} else {
				$ergebnis = "<BR><B>Speichern nicht erfolgreich: $input_fehler</B>";
			}
			break;	
		default: 		// isoadmin-daten aus der datenbank holen
			$user_id			= $request['userId'];
			$isoadmin			= new IsoadminUser($db_connection, $user_id);
			$username			= $isoadmin->getUserName();
			$first_name			= $isoadmin->getFirstName();
			$last_name 			= $isoadmin->getLastName();
			$start_date			= $isoadmin->getStartDate();
			$end_date			= $isoadmin->getEndDate();
			$email				= $isoadmin->getEmail();
			$is_isoadmin		= $isoadmin->getIsIsoadmin();
	}
	$headers = array('fworch Administrator', $user_id);
	$user_table = new DisplayTable('fworch Administrator', $headers);
	
	if ($aktion=='display' or ($aktion=='save' and (!isset($input_fehler) or $input_fehler=='')))
		{ $nurlesen = 'readonly'; $select_disabled = 'disabled'; }
	else
		{ $nurlesen = ''; $select_disabled = ''; }
	$form = '<FORM id="user_form" name="user_form" method="POST" target="_self">';
	$form .= $user_table->displayTableOpen() . $user_table->displayTableHeaders();
	$form .= '<input type="hidden" name="userId" value="' . $user_id . '">';
	$form .= $user_table->displayRowSimple() . $user_table->displayColumn('Username') . $user_table->displayColumn('<input type="text" name="username" value="' .
			$username. '" ' . $nurlesen . ' size="'  . $size . '">');
	$form .= $user_table->displayRowSimple() . $user_table->displayColumn('Vorname') . $user_table->displayColumn('<input type="text" name="first_name" value="' .
			$first_name. '" ' . $nurlesen . ' size="'  . $size . '">');
	$form .= $user_table->displayRowSimple() . $user_table->displayColumn('Nachname') . $user_table->displayColumn('<input type="text" name="last_name" value="' .
			$last_name. '" ' . $nurlesen . ' size="'  . $size . '">');
	$form .= $user_table->displayRowSimple() . $user_table->displayColumn('Eintrittsdatum') . $user_table->displayColumn('<input type="text" name="start_date" value="' .
			$start_date. '" ' . $nurlesen . ' size="'  . $size . '">');
	$form .= $user_table->displayRowSimple() . $user_table->displayColumn('Datum des Ausscheidens') . $user_table->displayColumn('<input type="text" name="end_date" value="' .
			$end_date. '" ' . $nurlesen . ' size="'  . $size . '">');
	$form .= $user_table->displayRowSimple() . $user_table->displayColumn('Email-Adresse') . $user_table->displayColumn('<input type="text" name="email" value="' .
			$email. '" ' . $nurlesen . ' size="'  . $size . '">');
	$form .= $user_table->displayRowSimple() . $user_table->displayColumn('fworch-Superuser?') . $user_table->displayColumn('<SELECT ' . $select_disabled  . ' name="is_isoadmin">' .
			'<OPTION ' . (($is_isoadmin)?'selected':'') . ' value="1">ja</OPTION>' .
			'<OPTION ' . (($is_isoadmin)?'':'selected') . ' value="0">nein</OPTION>');
	$form .= $user_table->displayTableClose();
	$form .= '</FORM>';
	echo "&nbsp;<br>$ergebnis<br><br>$form"; 
?>
</div>
</body></html>