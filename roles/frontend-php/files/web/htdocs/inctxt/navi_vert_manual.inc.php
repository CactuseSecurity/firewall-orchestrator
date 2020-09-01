<?php
// $Id: navi_vert_manual.inc.php,v 1.1.2.6 2011-05-23 12:15:38 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/navi_vert_manual.inc.php,v $
	echo '<div id="menu"><b>Inhaltsverzeichnis</b><ul>';
	$stamm = '/';
	$man_path = $stamm . 'man/';
	require_once ("check_privs.php");
 	echo '<li><a href="' . $man_path . 'manual.php">Einleitung</a></li>';
	if ($allowedToDocumentChanges)
	 	echo '<li><a href="' . $man_path . 'man_architecture.php">Architektur</a></li>';
	if ($allowedToDocumentChanges) {
		echo '<li>Konzepte';
		echo '<ul>';
			echo '<li><a href="' . $man_path . 'man_concepts.php#tenants">Mandanten</a></li>';
			echo '<li><a href="' . $man_path . 'man_concepts.php#roles">Rollenkonzept</a></li>';
			echo '<li><a href="' . $man_path . 'man_concepts.php#revision">Revisionssicherheit</a></li>';
		echo '</ul></li>';
	}
	if ($allowedToDocumentChanges) {
		echo '<li>Dokumentation';
		echo '<ul>';
		if ($allowedToChangeDocumentation)
		echo '<li><a href="' . $man_path . 'man_documentation.php">Dokumentation</a></li>';
			echo '<li><a href="' . $man_path . 'man_change_docu.php">&Auml;nderungen</a></li>';
		echo '</li></ul>';
	}
	if ($allowedToViewReports) {
		echo '<li>Reporting';
		echo '<ul>';
			echo '<li><a href="' . $man_path . 'man_reporting.php">allgemein</a></li>';
			if (in_array('1', explode(',',$report_filter))) {
				echo '<li><a href="' . $man_path . 'man_report_config.php">Konfiguration</a></li>';
			}
			if (in_array('2', explode(',',$report_filter))) {
			echo '<li><a href="' . $man_path . 'man_report_change.php">&Auml;nderungen</a></li>';
			}
			if (in_array('3', explode(',',$report_filter))) {
			echo '<li><a href="' . $man_path . 'man_report_usage.php">Verwendung</a></li>';
			}
			if (in_array('4', explode(',',$report_filter))) {
			echo '<li><a href="' . $man_path . 'man_report_rulesearch.php">Regelsuche</a></li>';
			}
			echo '</ul></li>';
	}
	if ($allowedToDocumentChanges) {
		echo '<li>Systempflege';
		echo '<ul>';
			echo '<li><a href="' . $man_path . 'man_maintenance.php">Allgemeines</a></li>';
			echo '<li><a href="' . $man_path . 'man_files.php">Dateistruktur</a></li>';
			echo '<li><a href="' . $man_path . 'man_configuration.php">Web-Config</a></li>';
			echo '<li><a href="' . $man_path . 'man_config_files.php">Config-Dateien</a></li>';
		echo '</ul></li>';
	}
?>
</ul>
</div>
<script type="text/javascript" src="<?php echo $stamm ?>js/tree.js"></script>