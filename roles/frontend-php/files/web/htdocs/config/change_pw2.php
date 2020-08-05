<?php
	$stamm="/";
	$page="config";
	setlocale(LC_CTYPE, "de_DE.UTF-8");
	if (!isset($_SESSION)) session_start();
	require_once("db-base.php");
	require_once ("db-input.php");
	require_once ("operating-system.php");
	$cleaner = new DbInput();  // for clean-function
	$session = $cleaner->clean_structure($_SESSION);
	$request = $cleaner->clean_structure($_REQUEST);

	$user		= $session["dbuser"];
	$oldpass	= $request['oldpassword'];
	$newpass	= $request['newpassword'];
	$newpass2	= $request['newpassword2'];

	$DbConf = new DbConfig($user,$oldpass);
	$conn = new DbConnection($DbConf);
	$log = new LogConnection();
	
	$error_str = "";
	// Password Policy:
	$min_len_pw = 9;
	$alpha_char_regex = "/[a-zA-Z]/";
	$special_char_regex = "/[\!\%\$\"\&\/\(\)\=\?\'\<\>\-\_\#\+\*\,\;\.\:\s\@\{\}\[\]\^\§]/";
	if(!preg_match($special_char_regex, $newpass))				$error_str = "Passwort ohne Sonderzeichen";
	if(!preg_match($alpha_char_regex, $newpass))				$error_str = "Passwort ohne Buchstaben";
	if(!preg_match("/\d/", $newpass))							$error_str = "Passwort ohne Ziffern";
	if(strlen($newpass) < $min_len_pw)							$error_str = "Passwort nicht lang genug";
	if (!$conn->isoadmin_check_pwd_history($user, $newpass))	$error_str = "Passwort wurde bereits verwendet";
	if($newpass==$oldpass)										$error_str = "Altes und neues Passwort sind identisch";
	// End Password Policy
	if ($newpass<>$newpass2)									$error_str = "Das neue Passwort wurde nicht zweimal identisch eingegeben";
	$check_login_result = $conn->check_login($user, $oldpass);
	// the following two states are valid results to proceed with pwd change:
	if ($check_login_result!='password_must_be_changed' and $check_login_result!='') $error_str = "Das alte Passwort ist nicht korrekt";
		
	if (!$error_str) {
		//Save the request in SQL syntax to a string
		$sql_request = 'ALTER ROLE "' . $user . '" WITH PASSWORD ' . "'" . $newpass . "'";
		$results = $conn->fworch_db_query($sql_request);
		if ($results) {
			$_SESSION['dbpwd'] = $newpass;	// pwd has been changed: set new pwd in session for get_text_msg calls
			$log->log_debug("KennwortPolicy::Kennwort for user $user erfolgreich modifiziert (mittels ALTER ROLE).");
			$success_str = 'Passwort erfolgreich ge&auml;ndert';
			// set isoadmin_password_must_be_changed = false
			$sql_request = "UPDATE isoadmin SET isoadmin_password_must_be_changed = FALSE WHERE isoadmin_username = '$user'; ";
			$sql_request .= "UPDATE isoadmin SET isoadmin_last_password_change = now() WHERE isoadmin_username = '$user'; ";
			$results = $conn->fworch_db_query($sql_request);			
			if (!$results) {
				$log->log_debug("KennwortPolicy::isoadmin_kennwort_must_be_changed for user $user nicht erfolgreich modifiziert.");
				$log->log_error("KennwortPolicy::isoadmin_kennwort_must_be_changed for user $user nicht erfolgreich modifiziert.");
				$log->log_login("KennwortPolicy::isoadmin_kennwort_must_be_changed for user $user nicht erfolgreich modifiziert.");
			} else {
				$log->log_login("isoadmin_password_must_be_changed for user $user erfolgreich modifiziert.");
			}
			$conn->isoadmin_append_pwd_hash($user, $newpass);				
		} else {
			$error_str = "Fehler: Passwort nicht ge&auml;ndert";
			$log->log_login("KennwortPolicy::Kennwort for user $user nicht erfolgreich modifiziert (nach ALTER ROLE).");
		}
	}
?>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>ITSecOrg Password Change failed</title>
<meta name="robots" content="index,follow">
<meta http-equiv="cache-control" content="no-cache">
<meta name="revisit-after" content="2 days">
<meta http-equiv="content-language" content="de">
<script type="text/javascript" src="<?php echo $stamm ?>js/client.js"></script>
<script type="text/javascript" src="<?php echo $stamm ?>js/script.js"></script>
<link rel="stylesheet" type="text/css" href="<?php echo $stamm ?>css/firewall.css">
<script language="javascript" type="text/javascript">
  if(is_ie) document.write("<link rel='stylesheet' type='text/css' href='<?php echo $stamm ?>css/firewall_ie.css'>");
</script>
</head>
<body onLoad="changeColor1('n4')";>
<?php
	include("header.inc.php");
	include("navi_head.inc.php");
	include("navi_hor.inc.php");
	include("navi_vert_config_main.inc.php");
 ?>
<div id="inhalt">&nbsp;
<b class="headlinemain">
<?php
$log->log_login("KennwortChange::the end result for user $user; error=$error_str");

if ($error_str)
	echo $error_str . ' - bitte <a href="/config/change_pw.php"><b class="headlinemain">wiederholen</b></a)>.';
else {
	echo $success_str . ' - bitte <a href="/index.php?abmelden=true"><b class="headlinemain">neu anmelden</b></a>.';
// 	session_destroy();
}
?>
</b>
</div>
</body></html>