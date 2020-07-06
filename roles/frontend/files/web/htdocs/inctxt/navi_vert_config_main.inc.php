<?php
// $Id: navi_vert_config_main.inc.php,v 1.1.2.6 2012-04-30 17:21:21 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/navi_vert_config_main.inc.php,v $
	echo '<div id="menu">' . $language->get_text_msg('settings', 'html') . '<ul>';
//	echo '<li><a href="/config/change_pw.php">' . $language->get_text_msg('change_password', 'html') . '</a></li>';
	if ($allowedToConfigureDevices) { echo '<li><a href="/config/config_system.php">' . $language->get_text_msg('change_devices', 'html') . '</a></li>'; }
//	if ($allowedToConfigureUsers) { echo '<li><a href="/config/config_user.php">Benutzerdaten</a></li>'; }
	if ($allowedToConfigureClients) echo '<li><a href="/config/config_client.php">' . $language->get_text_msg('change_clients', 'html') . '</a></li>';
	if ($allowedToViewImportStatus) { echo '<li><a href="/config/import_status.php">Import-Status</a></li>'; }
	echo '</ul></div>';
?>
<script type="text/javascript" src="<?php echo $stamm ?>js/tree.js"></script>