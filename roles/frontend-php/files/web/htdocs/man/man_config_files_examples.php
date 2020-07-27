<?php
// $Id: man_config_files_examples.php,v 1.1.2.3 2011-09-15 18:59:49 tim Exp $
// $Source: /home/cvs/iso/package/web/htdocs/man/Attic/man_config_files_examples.php,v $
	$stamm="/";
	$page="man";
	require_once("check_privs.php");
	if (!$allowedToDocumentChanges) header("Location: ".$stamm."index2.php");
?>
<br>
<br>
<br>
<b class="headline">Beispiel-Konfigurationsdateien</b>

<ul>
<li>iso.conf
<pre>
ITSecOrgDir	/usr/share/itsecorg		# Installationsverzeichnis
	
###################### database connection ##############################
itsecorg database hostname              localhost
itsecorg database type                  DBX_PGSQL
itsecorg database name                  isodb
itsecorg database port                  5432

###################### logging options ##################################
set loglevel		6
set logtarget syslog
syslog_facility		local6

###################### misc #############################################
rule_header_offset	99000					# used for creating netscreen header_rules (containing zone-info)
</pre>
<li>gui.conf
<pre>
###################### display options ##################################

display max_number_of_rules_in_ruleview 3000
display number_of_undocumented_changes 200
display undocumented_rule_changes maxcols 30
display undocumented_rule_changes maxrows 2

###################### User-Berechtigungen ##############################
# group memberships
usergroup isoadmins members: ralf tim hugo
usergroup demo members: demo2 demo7 dem8
usergroup testminimum members: demo1

# group privileges
usergroup demo privileges:              view-reports document-changes view-change-admin-names change-documentation
usergroup testminimum privileges:       view-reports
usergroup isoadmins privileges:         view-all-objects-filter view-reports document-changes admin-users admin-devices admin-clients change-documentation view-import-status

# client visibility (separator is here ',' not space!)
# usergroup demo visible-clients:                       'Client 1', 'Client 2'
usergroup demo visible-clients:                         ALL
usergroup testminimum visible-clients:                  ALL
usergroup isoadmins visible-clients:                    ALL

# device and management systems visible in the GUI
usergroup demo visible-managements:                     ALL
usergroup demo visible-devices:                         ALL
usergroup testminimum visible-managements:              sting
usergroup testminimum visible-devices:                  ALL
usergroup isoadmins visible-managements:                ALL
usergroup isoadmins visible-devices:                    ALL

# report visibility
usergroup demo visible-reports:                         changes rule
# possible values: changes (Aenderungen), usage (Verwendung), rule (Regelsuche), config (Konfiguration)

usergroup testminimum visible-reports:                  config 
usergroup isoadmins visible-reports:                    ALL

# parameters for document changes
docu-changes default-client demo: Revi
docu-changes default-client demo-minimum: Revi
docu-changes default-client isoadmins: Gold
docu-changes default-request-type isoadmins: ARS
docu-changes number-of-requests 1
docu-changes display-approver 0
docu-changes request-type 0
docu-changes comment-is-mandatory 1

# config parameters
config password minimal-length 6
</pre>
<li>import.conf
<pre>
ImportSleepTime         300					# Zeit zwischen den Import-Laeufen in Sekunden

ImportDir               /usr/share/itsecorg/importer		# Import main directory
PerlInc                 /usr/share/itsecorg/importer		# Perl Include Directory
iso_workdir		/tmp/isotmp				# Temporaeres Verzeichnis fuer Import-Daten
archive_dir		/var/itsecorg/import_archive		# Verzeichnis fuer Archivierung von Fehlimporten
simple_bin_dir		/bin					# wo liegen tar, date, mkdir, ...
save_import_results_to_file	1

# delimiter
usergroup_delimiter	|
csv_delimiter           %
csv_user_delimiter      ;
group_delimiter      	|

iso_srv_user            isoimporter
output_method           text

echo_bin                /bin/echo
scp_bin                 /usr/bin/scp
ssh_bin                 /usr/bin/ssh
chmod_bin               /bin/chmod
scp_batch_mode_switch   -B -q

psql_exe                        /usr/bin/psql			# for netscreen predef-services copy from
psql_params                     -t -q -A -h $iso_srv_host -d $iso_database -U $iso_srv_user

# LDAP stuff
LDAP_enabled            1
LDAP_c                          de
LDAP_o                          cactus
LDAP_server                     localhost
</pre>
</ul> 