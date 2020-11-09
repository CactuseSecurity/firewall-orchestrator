<?php
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
TopDir	/usr/local/fworch		# Installationsverzeichnis
	
###################### database connection ##############################
fworch database hostname              localhost
fworch database type                  DBX_PGSQL
fworch database name                  fworchdb
fworch database port                  5432

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
usergroup uiusers members: ralf tim hugo
usergroup demo members: demo2 demo7 dem8
usergroup testminimum members: demo1

# group privileges
usergroup demo privileges:              view-reports document-changes view-change-admin-names change-documentation
usergroup testminimum privileges:       view-reports
usergroup uiusers privileges:         view-all-objects-filter view-reports document-changes admin-users admin-devices admin-tenants change-documentation view-import-status

# tenant visibility (separator is here ',' not space!)
# usergroup demo visible-tenants:                       'tenant 1', 'tenant 2'
usergroup demo visible-tenants:                         ALL
usergroup testminimum visible-tenants:                  ALL
usergroup uiusers visible-tenants:                    ALL

# device and management systems visible in the GUI
usergroup demo visible-managements:                     ALL
usergroup demo visible-devices:                         ALL
usergroup testminimum visible-managements:              sting
usergroup testminimum visible-devices:                  ALL
usergroup uiusers visible-managements:                ALL
usergroup uiusers visible-devices:                    ALL

# report visibility
usergroup demo visible-reports:                         changes rule
# possible values: changes (Aenderungen), usage (Verwendung), rule (Regelsuche), config (Konfiguration)

usergroup testminimum visible-reports:                  config 
usergroup uiusers visible-reports:                    ALL

# parameters for document changes
docu-changes default-tenant demo: Revi
docu-changes default-tenant demo-minimum: Revi
docu-changes default-tenant uiusers: Gold
docu-changes default-request-type uiusers: ARS
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

ImportDir               /usr/local/fworch/importer		# Import main directory
PerlInc                 /usr/local/fworch/importer		# Perl Include Directory
fworch_workdir		/tmp/isotmp				# Temporaeres Verzeichnis fuer Import-Daten
archive_dir		/var/fworch/import_archive		# Verzeichnis fuer Archivierung von Fehlimporten
simple_bin_dir		/bin					# wo liegen tar, date, mkdir, ...
save_import_results_to_file	1

# delimiter
usergroup_delimiter	|
csv_delimiter           %
csv_user_delimiter      ;
group_delimiter      	|

fworch_srv_user            isoimporter
output_method           text

echo_bin                /bin/echo
scp_bin                 /usr/bin/scp
ssh_bin                 /usr/bin/ssh
chmod_bin               /bin/chmod
scp_batch_mode_switch   -B -q

psql_exe                        /usr/bin/psql			# for netscreen predef-services copy from
psql_params                     -t -q -A -h $fworch_srv_host -d $fworch_database -U $fworch_srv_user

# LDAP stuff
LDAP_enabled            1
LDAP_c                          de
LDAP_o                          cactus
LDAP_server                     localhost
</pre>
</ul> 