<?php
// $Id: reporting_filter.inc.php,v 1.1.2.8 2012-10-21 15:56:21 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/reporting_filter.inc.php,v $
	require_once("db-div.php");
	require_once("db-tenant.php");
	$rflist = new ReportFormatList();
	$clist = new tenantList($filter,$db_connection);
	$iplist = new IpProtoList($filter,$db_connection);
	$dbuser = $_SESSION['dbuser'];
?>

<table width="730" cellspacing="0" cellpadding="0">
	<tr>
		<td width="350" valign="top">
			<div id="zeitpunkt" style="display:block;">
				<table width="350" cellspacing="0" cellpadding="0" class="tabfilter">
					<tr>
						<td class="celltitle" colspan="2"><span class="texttitle"><?php echo $language->get_text_msg('config_time', 'html') ?></span></td>
	     			</tr>
	     			<tr>
						<td class="ptfilter line_l"><?php echo $language->get_text_msg('report_time', 'html'); ?></td>
						<td class="line_r"><input type="text" name="zeitpunkteins" class="filter filter210 dist_single" value="<?php echo date('Y-m-d H:i')?>"></td>
					</tr>
				</table>
			</div>
	
			<div id="zweizeitpunkt" style="display:none;">
				<table width="350" cellspacing="0" cellpadding="0" class="tabfilter">
					<tr>
						<td class="celltitle" colspan="2"><span class="texttitle"><?php echo $language->get_text_msg('report_headline_changes', 'html'); ?></span></td>
					</tr>
					<tr>
						<td class="ptfilter line_l"><?php echo $language->get_text_msg('report_changes_start_time', 'html'); ?></td>
						<td class="line_r"><input type="text" name="zeitpunktalt" class="filter filter210 dist_ob" value="<?php
								$year = date('Y');
								$prev_year = sprintf("%04d", $year-1);
								$prev_month = sprintf("%02d", date('n')-1);
								if ($prev_month == "00") {
									$year = date('Y')-1;
									$prev_month = "12";
								}
//								echo "$year-$prev_month-01 00:00";
								echo "$prev_year-01-01";
							?>">
						</td>
					</tr>
					<tr>
						<td class="ptfilter line_l"><?php echo $language->get_text_msg('report_changes_end_time', 'html'); ?></td>
						<td class="line_r"><input type="text" name="zeitpunktneu" class="filter filter210 dist_unt"	value="<?php
//							echo date('Y-m-01 00:00');
							echo date('Y-01-01');
						?>"></td>
					</tr>
				</table>
			</div>
		</td>
		<td width="30"><img src="<?php echo $stamm ?>img/1p_tr.gif" width="30" height="1" alt=""></td>
		<td width="350" valign="top">
			<?php
				echo '<table width="350" cellspacing="0" cellpadding="0" class="tabfilter"><tr>';
			   	echo '<td class="celltitle"><span class="texttitle">' . $language->get_text_msg('tenant', 'html') . '</span></td></tr>';
				echo '<tr><td class="ptfilter line_l_r">';
				echo $clist->get_simple_tenant_menue_string($clist->filter_is_mandatory($tenant_filter), $dbuser);
				echo '</td></tr></table>';
				//	include($stamm."inctxt/auftragsfilter.inc.php");
			?>
		</td>
	</tr>
</table>
<!--Zweite Filter-->
<div id="filter_id" style="display:inline;"><img src="<?php echo $stamm ?>img/1p_tr.gif" width="1" height="2"><br>
	<table width="730" cellspacing="0" cellpadding="0">
		<tr>
			<td width="350" valign="top">
				<table width="350" cellspacing="0" cellpadding="0" class="tabfilter">
					<tr>
						<td class="celltitle" colspan="2"><span class="texttitle"><?php echo $language->get_text_msg('source', 'html') ?></span></td>
					</tr>
					<tr>
						<td class="ptfilter line_l">Name</td>
						<td class="line_r"><input type="text" name="quellname" class="filter filter210 dist_ob"></td>
					</tr>
					<tr>
						<td class="ptfilter line_l">IP</td>
						<td class="line_r"><input type="text" name="quell_ip" class="filter filter210 dist_unt"></td>
					</tr>
				</table>

				<table width="350" cellspacing="0" cellpadding="0" class="tabfilter2">
					<tr>
						<td class="celltitle" colspan="2"><span class="texttitle"><?php echo $language->get_text_msg('destination', 'html') ?></span></td>
					</tr>
					<tr>
						<td class="ptfilter line_l">Name</td>
						<td class="line_r"><input type="text" name="zielname" class="filter filter210 dist_ob"></td>
					</tr>
					<tr>
						<td class="ptfilter line_l">IP</td>
						<td class="line_r"><input type="text" name="ziel_ip" class="filter filter210 dist_unt"></td>
					</tr>
				</table>

				<table width="350" cellspacing="0" cellpadding="0" class="tabfilter2">
					<tr>
						<td class="celltitle" colspan="2"><span class="texttitle"><?php echo $language->get_text_msg('service', 'html') ?></span></td>
					</tr>
					<tr>
						<td class="ptfilter line_l">Name</td>
						<td class="line_r"><input type="text" name="dienstname" class="filter filter210 Dist_ob"></td>
					</tr>
					<tr>
						<td class="ptfilter line_l"><?php echo $language->get_text_msg('ip_protocol', 'html') ?></td>
						<td class="line_r">
							<select name="dienst_ip" class="filter filter210 dist_mit">
								<option value="-1"><?php echo $language->get_text_msg('please_select', 'html') ?></option>
								<?php echo $iplist->getIpProtoForm(); ?>
							</select>
						</td>
					</tr>
					<tr>
						<td class="ptfilter line_l">Destination-Port</td>
						<td class="line_r"><input type="text" name="dienstport" class="filter filter210 dist_unt"></td>
					</tr>
				</table>

			</td>
			<td width="30">&nbsp;</td>
			<td width="350" valign="top">
				<table width="350" cellspacing="0" cellpadding="0" class="tabfilter">
					<tr>
						<td class="celltitle" colspan="2"><span class="texttitle"><?php echo $language->get_text_msg('user', 'html') ?></span></td>
					</tr>
					<tr>
						<td class="ptfilter line_l"><?php echo $language->get_text_msg('surname', 'html') ?></td>
						<td class="line_r"><input type="text" name="ben_name" class="filter filter210 dist_ob"></td>
					</tr>
					<tr>
						<td class="ptfilter line_l"><?php echo $language->get_text_msg('first_name', 'html') ?></td>
						<td class="line_r"><input type="text" name="ben_vor" class="filter filter210 dist_mit"></td>
					</tr>
					<tr>
						<td class="ptfilter line_l"><?php echo $language->get_text_msg('user_id', 'html') ?></td>
						<td class="line_r"><input type="text" name="ben_id" class="filter filter210 dist_unt"></td>
					</tr>
				</table>

				<table width="350" cellspacing="0" cellpadding="0" class="tabfilter2">
					<tr>
						<td class="celltitle"><span class="texttitle"><?php echo $language->get_text_msg('rule_comment', 'html') ?></span></td>
					</tr>
					<tr>
						<td class="ptfilter line_l_r"><input type="text" name="regelkommentar" class="filter filter210 dist_single"></td>
					</tr>
				</table>

				<?php 
					echo '<table width="350" cellspacing="0" cellpadding="0" class="tabfilter2">' . 
						'<tr>' .
							'<td class="celltitle"><span class="texttitle">' . $language->get_text_msg('special_filters', 'html') . '</span></td>' .
				        '</tr>' .
				        '<tr><td class="ptfilter line_l_r" style="padding:7px 10px;"><input type="checkbox" name="inactive" value="1" checked="checked">' .
							$language->get_text_msg('show_only_active_rules', 'html') . '</td></tr>';
					if ($allowedToViewAllObjectsOfMgm) 
						echo '<tr><td class="ptfilter line_l_r" style="padding:7px 10px;"><input type="checkbox" name="notused" value="1" checked="checked">' .
							$language->get_text_msg('show_only_rule_objects', 'html') . '</td></tr>';
					else echo '<input type="hidden" name="notused" value="1">'; 
					echo '<tr><td class="ptfilter line_l_r" style="padding:7px 10px;"><input type="checkbox" name="anyrules" value="1" checked="checked">' .
						$language->get_text_msg('ip_filter_shows_any_objects', 'html') . '</td></tr>' .
						'<tr><td class="ptfilter line_l_r" style="padding:7px 10px;"><input type="checkbox" name="negrules" value="1" checked="checked">' .
							$language->get_text_msg('ip_filter_shows_negated_rule_parts', 'html') . '</td></tr>' .
						'</table>';
				?>
			</td>
		</tr>
	</table>
	<div id="extraFields" style="display:inline;">
		<?php echo $linie ?>
		<table width="730" cellspacing="0" cellpadding="0">
			<tr>
				<td style="text-align:right;width:365px;">
				<?php echo $rflist->GetReportFormatMenue(); ?></td>
				<td style="width:365px;">
					<table width="365" cellpadding="0" cellspacing="0">
						<tr>
							<td style="width:185px;">
								<input type="submit" value="<?php echo $language->get_text_msg('generate_report', 'html') ?>" class="button" style="margin-left:15px;" onClick="javascript:SubMitForm('Print');"></td>
							<td style="text-align:right;width:180px;">
								<input type="button" value="<?php echo $language->get_text_msg('remove_filter', 'html') ?>" class="button" onClick="javascript:ResetFields();"></td>
						</tr>
					</table>
				</td>
			</tr>
		</table>
	</div>
</div>
<div id="filter_id_min" style="display:inline;">
	<?php echo $linie ?>
	<table width="730" cellspacing="0" cellpadding="0">
		<tr>
			<td width="200">
				<div id="extra_filter_button" style="display:inline;">
					<img src="/img/icon_plus2.gif" width="16" height="16" alt="Tabelle einblenden" title="Tabelle einblenden" align="absmiddle"
					onClick="document.getElementById('filter_id').style.display='inline';document.getElementById('filter_id_min').style.display='none';position_iframe('Report_Frame');">
					&nbsp;&nbsp;<b>Weitere Filteroptionen</b><br>
				</div>	
			</td>
			<td width="530">
				<div>
					<table width="530" cellspacing="0" cellpadding="0">
						<tr>
							<td style="text-align:right;width:365px;">
								<?php echo $rflist->GetReportFormatMenueHTMLOnly(); ?></td>
							<td width="185"><input type="submit" value="Report erstellen" class="button" style="margin-left:15px;"
								onClick="javascript:SubMitForm('Print');"></td>
						</tr>
					</table>
				</div>	
			</td>
		</tr>
	</table>
</div>