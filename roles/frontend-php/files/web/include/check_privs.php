<?php
	if (!isset($_SESSION)) session_start();
	require_once ("db-input.php");
	require_once ("db-base.php");
	require_once ("db-gui-config.php");
	require_once ("operating-system.php");  // for syslogging
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
			$_SESSION["allowedToDocumentChanges"] = $userconfig->allowedToDocumentChanges();
			$_SESSION["allowedToChangeDocumentation"] = $userconfig->allowedToChangeDocumentation();
			$_SESSION["allowedToConfiguretenants"] = $userconfig->allowedToConfiguretenants();
			$_SESSION["allowedToConfigureDevices"] = $userconfig->allowedToConfigureDevices();
			$_SESSION["allowedToConfigureUsers"] = $userconfig->allowedToConfigureUsers();
			$_SESSION["allowedToViewReports"] = $userconfig->allowedToViewReports();
			$_SESSION["allowedToViewAdminNames"] = $userconfig->allowedToViewAdminNames();
			$_SESSION["allowedToViewAllObjectsOfMgm"] = $userconfig->allowedToViewAllObjectsOfMgm();
			$_SESSION["allowedToViewImportStatus"] = $userconfig->allowedToViewImportStatus();
			$_SESSION["defaulttenant"] = $userconfig->getDefaulttenant();
			$_SESSION["defaultRequestType"] = $userconfig->getDefaultRequestType();
			$_SESSION["tenantFilter"] = $userconfig->getVisibletenantFilter();
			$_SESSION["VisibleManagements"] = $userconfig->getVisibleManagements();
			$_SESSION["ManagementFilter"] = $userconfig->getManagementFilter();
			$_SESSION["ReportFilter"] = $userconfig->getReportFilter();
			$_SESSION["RuleHeaderOffset"] = $userconfig->getRuleHeaderOffset();
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
		} elseif ($fehler == "password_must_be_changed") {
			$userconfig = new UserConfig($user);			
			$_SESSION["dbuser"] = $user;
			$_SESSION["dbpw"] = $pass;
			$_SESSION["auth"] = "true";
			$log->log_debug("Redirecting to change pwd site ...");
			session_write_close();
			header("Location: ".$stamm."config/change_pw.php");
		}
	}
	if (isset($_SESSION["auth"])) {  // successful login took place
		if (isset($request['oldpassword'])) $oldpass  = $cleaner->clean($request['oldpassword'],255);
		else $oldpass  = '';
		$allowedToDocumentChanges = $cleaner->clean($_SESSION["allowedToDocumentChanges"],100);
		$allowedToChangeDocumentation = $cleaner->clean($_SESSION["allowedToChangeDocumentation"],100);
		$allowedToConfiguretenants = $cleaner->clean($_SESSION["allowedToConfiguretenants"],100);
		$allowedToConfigureUsers = $cleaner->clean($_SESSION["allowedToConfigureUsers"],100);
		$allowedToConfigureDevices = $cleaner->clean($_SESSION["allowedToConfigureDevices"],100);
		$allowedToViewReports = $cleaner->clean($_SESSION["allowedToViewReports"],100);
		$allowedToViewAdminNames = $cleaner->clean($_SESSION["allowedToViewAdminNames"],100);
		$allowedToViewImportStatus = $cleaner->clean($_SESSION["allowedToViewImportStatus"],100);
		$allowedToViewAllObjectsOfMgm =	$cleaner->clean($_SESSION["allowedToViewAllObjectsOfMgm"],100);
		$default_tenant = $cleaner->clean($_SESSION["defaulttenant"],200);
		$default_request_type = $cleaner->clean($_SESSION["defaultRequestType"],100);
		$user_id = $cleaner->clean($_SESSION["dbuserid"],100);
		$tenant_filter = $cleaner->clean($_SESSION["tenantFilter"],1000);
		$management_filter = $cleaner->clean($_SESSION["ManagementFilter"],1000);
		$report_filter = $cleaner->clean($_SESSION["ReportFilter"],1000);
		$ruleheaderoffset = $cleaner->clean($_SESSION["RuleHeaderOffset"],100);
		$loglevel = $cleaner->clean($_SESSION["loglevel"],100);
		$logtarget = $cleaner->clean($_SESSION["logtarget"],200);
		if (isset($user)) { // only in index2, login just took place, otherwise valid session is signalled by $_SESSION["auth"] set
			$log->log_login("fworch User $user successfully logged in.");
			// write last login date to uiuser
			$sql_code = "UPDATE uiuser SET uiuser_last_login = now() WHERE uiuser_username='$user'";
			$conn->fworch_db_query($sql_code);
		}
	} elseif (isset($fehler))  {	// only failed login, when $fehler is set (this accepts cli logins via fworch-user)
		$log->log_login("fworch User $user: failed login ($fehler).");			
		if ($conn->is_session_started()) session_destroy();
		header("Location: ".$stamm."index.php?failure=$fehler");
	}
?>
