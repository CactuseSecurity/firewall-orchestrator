<?php
	require_once("auth_check_privs.php");
	if (isset($_SESSION['auth'])) {
		require_once ('multi-language.php');
		$language = new Multilanguage($_SESSION["dbuser"]);
		echo '<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">';
		echo '<html>';
		echo '<head>';
		echo '<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">';
		echo '<title>Cactus eSecurity GmbH - Firewall Orchestrator</title>';
		echo '<meta http-equiv="cache-control" content="no-cache">';
		echo '<meta http-equiv="content-language" content="de">';
		echo '<link rel="stylesheet" type="text/css" href="' . $stamm . 'css/firewall.css">';
		echo '</head>';
		echo '<body>';
		echo ' <div id="inhalt">';
		
		echo 'User successfully logged in';
		$JWT = '';
		
		// display JWT header
		// display logon status
		// - roles
		
		
		echo ' <li class="abstand"><a href="http://cactus.de/support" target="_blank">ITSecOrgSupport</a>:';
		echo $language->get_text_msg ('support_description', 'html') . '</li>';
		echo '<li class="abstand"><a href="/man/manual.php">' . $language->get_text_msg ('manual', 'html') . '</a>:'; 
		echo $language->get_text_msg ('manual_description', 'html') . '</li>';
		echo "<li class=\"abstand\"><a href=\"javascript:OpenHilfe('ind2')\">" . $language->get_text_msg ('help', 'html') . '</a>:'; 
		echo $language->get_text_msg ('help_description', 'html')  . '</li>';
		echo ' <li class="abstand"><a href="index.php?abmelden=true">' . $language->get_text_msg ('logout', 'html') . '</a></li>';
		echo ' </ul>';
		echo ' </div>';
		echo ' <div id="navi-vert-linie"><img src="' . $stamm . 'img/linie_vert.gif" id="linie-vert"></div>';
		echo '<script language="javascript" type="text/javascript">';
		echo '</script>';
		echo '</body></html>';		
	}
?>
