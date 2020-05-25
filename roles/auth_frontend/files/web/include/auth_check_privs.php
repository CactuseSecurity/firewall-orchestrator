<?php
	if (!isset($_SESSION)) session_start();
	require_once ("auth_db-input.php");
	require_once ("auth_db-base.php");
	require_once ("auth_db-gui-config.php");
	require_once ("auth_operating-system.php");  // for syslogging
	$config = new Config();
	$_SESSION["loglevel"] = $config->getLogLevel();
	$_SESSION["logtarget"] = $config->getLogTarget();
	$_SESSION["logfacility"] = $config->getLogFacility();
	$log = new LogConnection();
	$cleaner = new DbInput();  // for clean-function
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	$request = $cleaner->clean_structure($_REQUEST);
	$session = $cleaner->clean_structure($_SESSION);

	if (isset($request['Username'])) $user = $request['Username'];
	if (isset($request['Passwort'])) $pass = $request['Passwort'];
	if (isset ($user) && isset ($pass)) { // index2 only
		$DbConf = new DbConfig($user,$pass); // enters database connection info into session
		$conn = new DbConnection($DbConf);
		$fehler = $conn->check_login($user, $pass);
		if ($fehler == '') {
			if (!isset($_SESSION)) session_start();
			$userconfig = new UserConfig($user);
	
			$_SESSION["dbuser"] = $user;
			$_SESSION["dbpw"] = $pass;
			$_SESSION["auth"] = "true";
			$_SESSION["dbuserid"] = $userconfig->getUserId($user,$pass);
	/*
			// debugging only:
			$felder = $_SESSION;  ksort($felder);  reset($felder);
			while (list($feldname, $val) = each($felder)) { // Schleife fuer Input-Werte
				if ($feldname=='dbpw') $val = 'zensiert';  // password wird nicht mitgeloggt
				$log->log_debug("found session $feldname: .$val.");			
				echo("found session $feldname: .$val.<br>");			
			}
			// debugging end 
	*/
		}
	}
	if (isset($_SESSION["auth"])) {  // successful login took place
		if (isset($request['oldpassword'])) $oldpass  = $cleaner->clean($request['oldpassword'],255);
		else $oldpass  = '';
		$user_id = $cleaner->clean($_SESSION["dbuserid"],100);
		$loglevel = $cleaner->clean($_SESSION["loglevel"],100);
		$logtarget = $cleaner->clean($_SESSION["logtarget"],200);
		if (isset($user)) { // only in index2, login just took place, otherwise valid session is signalled by $_SESSION["auth"] set
			$log->log_login("ITSecOrg User $user successfully logged in.");
			// create Json Web Token
			$sql_code = "select sign('{\"sub\":\"$user_id\",\"name\":\"$user_id\",\"admin\":false, \"hasura\": {\"claims\": {\"x-hasura-default-role\": \"user\", \"x-hasura-user-id\": \"$user_id\"}}}', 'ab957df1a33ea38a821278fb04d92abce830175ce9bcdef0e597622434480ccd');";
			$JWT = $conn->iso_db_query($sql_code);
			$log->log("create JWT $JWT");
			// write last login date to isoadmin
			$sql_code = "UPDATE isoadmin SET isoadmin_last_login = now() WHERE isoadmin_username='$user'";
			$conn->iso_db_query($sql_code);
		}
	} elseif (isset($fehler))  {	// only failed login, when $fehler is set (this accepts cli logins via itsecorg-user)
		$log->log_login("ITSecOrg User $user: failed login ($fehler).");			
		if ($conn->is_session_started()) session_destroy();
		header("Location: ".$stamm."index.php?failure=$fehler");
	}
?>
