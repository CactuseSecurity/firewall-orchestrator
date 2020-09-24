<?php
	$stamm="";
	$page="ind2";
	require_once("check_privs.php");
	if (isset($_SESSION['auth'])) {
		require_once ('multi-language.php');
		$language = new Multilanguage($_SESSION["dbuser"]);
		echo '<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">';
		echo '<html>';
		echo '<head>';
		echo '<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">';
		echo '<title>Cactus eSecurity GmbH - fworch</title>';
		echo '<meta http-equiv="cache-control" content="no-cache">';
		echo '<meta http-equiv="content-language" content="de">';
		echo '<link rel="stylesheet" type="text/css" href="' . $stamm . 'css/firewall.css">';
		echo '<script type="text/javascript" src="' . $stamm . 'js/browser.js"></script>';
		echo '<script type="text/javascript" src="' . $stamm . 'js/script.js"></script>';
		echo '</head>';
		echo '<body>';
		echo '<div id="farbe_oben"><img src="' . $stamm . 'img/1p_tr.gif" width="100%" height="10"></div>';
		include($stamm."inctxt/navi_head.inc.php");
		include($stamm."inctxt/navi_hor.inc.php");
	
		echo ' <div id="inhalt">';
	 	echo '<ul>';
		if ($allowedToDocumentChanges) {
			echo '<li class="abstand"><a href="documentation.php">' . $language->get_text_msg ('documentation', 'html') . '</a>: ';
			echo $language->get_text_msg ('documentation_description', 'html') . ' </li>';
		}
		if ($allowedToChangeDocumentation) {
			echo '<li class="abstand"><a href="documentation.php?change_docu=1">' . $language->get_text_msg ('change_documentation', 'html') . '</a>: ';
			echo $language->get_text_msg ('change_documentation_description', 'html') . '</li>';
		}
		if ($allowedToViewReports) {
			echo '<li class="abstand"><a href="reporting.php">Reporting</a>: ' . $language->get_text_msg ('reporting_description', 'html') . '</li>';
		}
		echo '<li class="abstand"><a href="/config/configuration.php">' . $language->get_text_msg ('settings', 'html');
		echo "</a>: " . $language->get_text_msg ('settings_description', 'html') . '</li>';
	
		echo ' <li class="abstand"><a href="http://cactus.de/support" target="_blank">fworchSupport</a>:';
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
