<?php

	$vert_menu_width = "168";
	function create_link($stamm, $current_page_name, $url, $title, $menu_number, $menue_page_name, $open_new_window) {
		$target=' ';
		if (isset($open_new_window) and $open_new_window) $target = ' target="_blank" ';
		$result = "<td><a href=\"" . $url . "\"" . $target .
			"onMouseOver=\"changeColor('n" . $menu_number . "','C00')\" OnMouseOut=\"changeColor('n" . $menu_number . "','" . 
			(($current_page_name==$menue_page_name)?"C00":"D1D1D1") . "')\" id=\"nav_hor_text\">$title</a></td>" .
			"<td><img src=\"" . $stamm . "img/navi_linie.gif\" width=\"9\" height=\"23\"></td>\n";
		return $result;
	}
	require_once ('multi-language.php');
	if (!isset($mulit_lang)) $language = new Multilanguage($_SESSION["dbuser"]);
?>
<div id="navi-hor">
	<table width="100%" cellpadding="0" cellspacing="0">
		<tr>
			<!-- <td style="height:2px;width:100%;background:url(<?php echo $stamm ?>img/linie_d_h.gif); repeat-x"> -->
 			<td style="height:2px;width:100%;background:url(<?php echo $stamm ?>img/linie_d_h.gif) repeat-x;">
				<img src="<?php echo $stamm ?>img/1p_tr.gif" height="2" width="100%"></td>
		</tr>
		<tr>
			<td style="height:30px;">
				<table cellpadding="0" cellspacing="0">
					<tr>
						<td align="center" style="width:<?php echo $vert_menu_width?>px;"><B>fworch v<?php include ("version.inc.php"); ?><B></td>
						<td><img src="<?php echo $stamm ?>img/navi_linie.gif" width="9" height="23"></td>
						<?php
							if ($allowedToDocumentChanges)
								echo create_link($stamm, $page, '/documentation.php', $language->get_text_msg ('documentation', 'html'), '1', 'doc', false);
							if ($allowedToChangeDocumentation)
								echo create_link($stamm, $page, '/documentation.php?change_docu=1', $language->get_text_msg_no_break ('change_documentation', 'html'), '2', 'changedoc', false);
							echo create_link($stamm, $page, '/reporting.php', 'Reporting', '3', 'rep', false);
							echo create_link($stamm, $page, '/config/configuration.php', $language->get_text_msg ('settings', 'html'), '4', 'config', false);
							echo create_link($stamm, $page, 'http://www.itsecorg.de/support/', 'fworchSupport', '5', 'support', true);
							echo create_link($stamm, $page, '/man/manual.php', $language->get_text_msg ('manual', 'html'), '6', 'man', false);
							$url = "javascript:OpenHilfe('" . $page . "')";
							echo create_link($stamm, $page, $url, $language->get_text_msg ('help', 'html'), '7', 'hilfe', false);
							echo create_link($stamm, $page, '/index.php?abmelden=true', $language->get_text_msg_no_break ('logout', 'html'),
								'8', 'logout', false);
						?>						
						<td><a href="http://www.cactus.de" target="_blank">
							<img src="<?php echo $stamm ?>img/only_cactus.gif" width="17" height="23" style="margin-left:10px;"
								alt="www.cactus.de" title="www.cactus.de"></a></td>
						<td>&nbsp; </td>
						<td>&nbsp; </td>
						<td><?php echo $language->get_text_msg_no_break ('logged_in', 'html') ?>:&nbsp;<?php echo $_SESSION['dbuser']?></td>
					</tr><tr>
						<td style="width:160px;"><img src="<?php echo $stamm ?>img/1p_tr.gif" height="5" width="160"></td>
						<?php
							if ($allowedToDocumentChanges) echo "<td colspan=\"2\" class=\"navi_col_def\" id=\"n1\"></td>";
						?>
						<?php
							if ($allowedToChangeDocumentation) echo "<td colspan=\"2\" class=\"navi_col_def\" id=\"n2\"></td>";
						?>
						<td colspan="2" class="navi_col_def" id="n3"></td>
						<td colspan="2" class="navi_col_def" id="n4"></td>
						<td colspan="2" class="navi_col_def" id="n5"></td>
						<td colspan="2" class="navi_col_def" id="n6"></td>
						<td colspan="2" class="navi_col_def" id="n7"></td>
						<td colspan="2" class="navi_col_def" id="n8"></td>
						<td><img src="<?php echo $stamm ?>img/1p_tr.gif" width="9" height="5"></td>
						<td><img src="<?php echo $stamm ?>img/1p_tr.gif" width="1" height="5"></td>
					</tr>
				</table>
			</td>
		</tr>
	</table>
</div>

<div style="position:absolute;top:33px;left:0px;z-index:100;">
	<table width="100%" cellpadding="0" cellspacing="0">
		<tr>
			<td style="height:2px;width:100%;background:url(<?php echo $stamm ?>img/linie_h_d.gif) repeat-x;">
				<img src="<?php echo $stamm ?>img/1p_tr.gif" height="2" width="100%"></td>
		</tr>
	</table>
</div>

<script language="javascript" type="text/javascript">
	function OpenHilfe(seite){
		var hilfe = seite;
		window.open('/hilfe.php?seite='+hilfe,'hilfe','width=500,height=500,toolbar=no,menubar=yes,location=no,status=no,scrollbars=yes,resizable=yes,top=10,left=10');
	}
</script>
