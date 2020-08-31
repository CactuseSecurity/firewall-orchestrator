<?php
// $Id: auftragsfilter.inc.php,v 1.1.2.3 2012-03-05 17:11:49 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/inctxt/Attic/auftragsfilter.inc.php,v $
	// tenant_list anlegen
	// setzt das Vorhandensein von $filter und $db_connection voraus
	// afip steht f�r AuftragsFilter.Inc.Php
	require_once ("db-tenant.php");
	$afip_tenant_list = new tenantList($filter,$db_connection);
?>	

<table width="200" cellspacing="0" cellpadding="0" class="tabfilter">
	<tr><td class="celltitle" colspan="2"><span class="texttitle">Auftragsfilter</span></td></tr>
	<tr><td class="ptfilter line_l">Auftraggeber</td>
		<td class="ptfilter line_r"><?php echo $afip_tenant_list->get_tenant_menue_string_tight(0, 0,$filter_mandatory = false) ?></td>
	</tr>
	<tr><td class="ptfilter line_l">Auftragsnummer
		<td class="ptfilter line_r"><input type="text" name="request_filter" class="filter filter110 dist_unt" value=""></td>
	</tr>
</table>	