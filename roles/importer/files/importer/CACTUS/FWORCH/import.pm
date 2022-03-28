package CACTUS::FWORCH::import;
use strict;
use warnings;
use DBD::Pg;
use DBI;
use IO::File;
use Getopt::Long;
use File::Basename;
use FileHandle;
use CGI qw(:standard);
use Time::HiRes qw(time); # fuer hundertstelsekundengenaue Messung der Ausfuehrdauer
use CACTUS::FWORCH;
use CACTUS::read_config;

require Exporter;
our @ISA = qw(Exporter);

our %EXPORT_TAGS = (
	'basic' => [ qw(
		&get_mgm_id_of_import &is_import_running &get_matching_import_id &remove_control_entry
		&insert_control_entry &is_initial_import
		&put_ssh_keys_in_place
		&get_import_infos_for_device &get_import_infos_for_mgm
		&import_cleanup_and_summary &ruleset_does_not_fit &clean_up_fworch_db
		&print_debug &print_verbose &d2u &calc_subnetmask &convert_mask_to_dot_notation
		&print_results_monitor
		&print_results_files_objects
		&print_results_files_rules
		&print_results_files_users
		&print_results_files_zones
		&print_results_files_audit_log
		&get_proto_number
		&set_last_change_time
		&fill_import_tables_from_csv_with_sql
		&fill_import_tables_from_csv
		%network_objects @network_objects %services @services %rulebases @ruleorder @rulebases @zones
		%user %usergroup @user_ar $usergroupdelimiter
		$audit_log_count %auditlog
		$mode $last_change_time_of_config
		$verbose $debug $mgm_name
		@obj_outlist @srv_outlist @rule_outlist
	) ] );

our @EXPORT = ( @{ $EXPORT_TAGS{'basic'} } );
our $VERSION = '0.3';


our $verbose 	= 0;		# globaler Schalter bzgl. der Ausgabe:		1=verbose,		0=silent;
our $debug 		= 0;		# globaler Schalter bzgl. Druck der Debug info:	1=debug, 		0=silent;
our $mode;

# Laufvariablen Parser
our $mgm_name    = '';			# globale Variable fuer den Namen des Managements
# nur innerhalb der objekte gueltig
# hashes zur Speicherung der Parserergebnisse (Eigenschaften)
our @zones;
our %network_objects;
our %services;
our %rulebases;			# hash der rulebases
our @ruleorder;			# Array mit Regel-IDs
#	wird derzeit eigentlich nur fuer netscreen benoetigt
#	CheckPoint:	1-n
#	Netscreen:	policy id), Feld startet mit 1!
#	phion:		wie Check Point

our @network_objects = ();	# liste aller namen der netzobjekte
our @services = ();			# liste aller namen der services
our @rulebases = ();			# liste der definierten rulebases

our $audit_log_count = 0;
our %auditlog;

our %user;
our %usergroup;
our @user_ar = ();
our $usergroupdelimiter = &CACTUS::read_config::read_config('usergroup_delimiter');

our $last_change_time_of_config;

# Listen mit Schnittstelleninformationen
# xxx_outlist: 			Felder im Perl-Hash, die in CSV-Datei geschrieben werden
# xxx_import_fields:	Felder der xxx_import-Tabellen, die aus der CSV-Datei gefuellt werden
# @xxx:					(@services, @objects, ...): Listen mit den Namen? aller Basiselemente

# TODO: zu jedem Gruppenmitglied die Moeglichkeit eines Negationsflags
# TODO: zu jeder Referenz (Gruppenmitglieder, Regelbestandteile) eindeutige UIDs einfuehren und nur diese fuer Import-Prozess verwenden

# Ausgabe der Objektparameter				
our @obj_outlist		=(qw (	name type members member_refs cpver ipaddr ipaddr_last color comments location zone
	UID last_change_admin last_change_time));
# Planung:
# - member enthaelt nur noch eine Liste mit Referenzen und nicht mit Namen
# - Separator: |
# - Anzeige der Negation mit "$$__not__$$"
# - Neues DB-Feld fuer Negation fuer alle Gruppen- und Regelanteilbeziehungen

our @obj_import_fields = qw (
	control_id
	obj_name
	obj_typ
	obj_member_names
	obj_member_refs
	obj_sw
	obj_ip
	obj_ip_end
	obj_color
	obj_comment
	obj_location
	obj_zone
	obj_uid
	last_change_admin
	last_change_time
);

# Ausgabe der Serviceparameter
our @srv_outlist		=(qw (	name typ type members member_refs color ip_proto port port_last src_port src_port_last
	comments rpc_port timeout_std timeout UID last_change_admin last_change_time));

our @svc_import_fields = qw (
	control_id
	svc_name
	svc_typ
	svc_prod_specific
	svc_member_names
	svc_member_refs
	svc_color
	ip_proto
	svc_port
	svc_port_end
	svc_source_port
	svc_source_port_end
	svc_comment
	rpc_nr
	svc_timeout_std
	svc_timeout
	svc_uid
	last_change_admin
	last_change_time
);
# Ausgabe der Userparameter
our @user_outlist	=(qw ( type members member_refs color comments uid expdate last_change_admin ));

our @user_import_fields = qw (
	control_id
	user_name
	user_typ
	user_member_names
	user_member_refs
	user_color
	user_comment
	user_uid
	user_valid_until
	last_change_admin
);

our @zone_outlist	=(qw ( zone ));

our @zone_import_fields = qw (
	control_id
	zone_name
);

# Ausgabe der Regelparameter
our @rule_outlist	=(qw (	rule_id disabled src.op src src.refs dst.op dst dst.refs services.op services services.refs
	action track install time comments name UID header_text src.zone dst.zone last_change_admin parent_rule_uid));

our @rule_import_fields = qw (
	control_id
	rule_num
	rulebase_name
	rule_ruleid
	rule_disabled
	rule_src_neg
	rule_src
	rule_src_refs
	rule_dst_neg
	rule_dst
	rule_dst_refs
	rule_svc_neg
	rule_svc
	rule_svc_refs
	rule_action
	rule_track
	rule_installon
	rule_time
	rule_comment
	rule_name
	rule_uid
	rule_head_text
	rule_from_zone
	rule_to_zone
	last_change_admin
	parent_rule_uid
);

our @auditlog_outlist = qw ( change_time management_name changed_object_name changed_object_uid changed_object_type change_action change_admin );

our @auditlog_import_fields = qw ( control_id import_changelog_nr change_time management_name changed_object_name changed_object_uid changed_object_type change_action change_admin );

############################################################################################################
# allgemeine Funktionen
############################################################################################################

sub put_ssh_keys_in_place {
	my $workdir = shift;
	my $ssh_public_key = shift;
	my $ssh_private_key = shift;
	my $fehler_count = 0;

	# debugging
	# print ("put_ssh_keys_in_place::workdir=$workdir, ssh_public_key=$ssh_public_key, ssh_private_key=$ssh_private_key\n");
	$fehler_count += (system("$echo_bin \"$ssh_private_key\" > $workdir/$CACTUS::FWORCH::ssh_id_basename") != 0);
	if (defined($ssh_public_key) && $ssh_public_key ne "") {
		# only create public key file if the key is defined and not empty
		$fehler_count += (system("$echo_bin \"$ssh_public_key\" > $workdir/$CACTUS::FWORCH::ssh_id_basename.pub") != 0); # only necessary for netscreen
	}
	$fehler_count += (system("$chmod_bin 400 $workdir/$CACTUS::FWORCH::ssh_id_basename") != 0);
	return $fehler_count;
}


sub ruleset_does_not_fit {
	my $rulebasename_to_find = shift;
	my $href_rulesetname = shift;
	my $result = 1;

	while ( (my $key, my $value) = each %{$href_rulesetname}) {
		if ($rulebasename_to_find eq $value->{'local_rulebase_name'}) {
			$result=0;
		}
	}
	return $result;
}

############################################################
# get_mgm_id_of_import(mgm_id,mgm_name,dev_id,dev_name)
# liefert wenn herleitbar die mgm_id zurueck, ansonsten undef
############################################################
sub get_mgm_id_of_import {
	my $mgm_id   = shift;
	my $mgm_name = shift;
	my $dev_id   = shift;
	my $dev_name = shift;

	if (is_empty($mgm_id)) {
		if (!is_empty($dev_id)) {
			$mgm_id = exec_pgsql_cmd_return_value("SELECT mgm_id FROM device WHERE dev_id=$dev_id");
		} elsif (!is_empty($mgm_name)) {
			$mgm_id = exec_pgsql_cmd_return_value("SELECT mgm_id FROM management WHERE mgm_name='$mgm_name'");
		} elsif (!is_empty($dev_name)) {
			$mgm_id = exec_pgsql_cmd_return_value("SELECT mgm_id FROM device WHERE dev_name='$dev_name'");
		} else { # no info to derive mgm_id from has been passed to sub
			undef($mgm_id);
		}
	}
	return $mgm_id;
}

############################################################
# is_import_running()
# liefert FALSE, wenn kein import aktiv ist
############################################################
sub is_import_running {
	my $mgm_id = shift;
	return eval_boolean_sql("SELECT is_import_running($mgm_id)");
}

##############################
# is_initial import(MGM-ID)
# liefert TRUE, wenn noch kein import fuer MGM-ID gelaufen ist
############################################################
sub is_initial_import {
	my $mgm_id = shift;
	return (!defined(&exec_pgsql_cmd_return_value("SELECT control_id FROM import_control WHERE mgm_id=\'$mgm_id\' AND successful_import ")));
}
############################################################
# get_matching_import_id()
# relevanten Import raussuchen (datum und id)
# = letzter Import fuer das Device vor $time
# liefert die passende import ID zurueck
# parameter1: device ID
# parameter2: gesuchter Zeitpunkt
############################################################
sub get_matching_import_id {
	my $dev_id = $_[0];
	my $time = $_[1];
	my ($mgm_id, $dbh, $sth, $relevant_import_id, $err_str, $sqlcode);

	$sqlcode = "SELECT mgm_id FROM device WHERE dev_id=$dev_id";
	$dbh = DBI->connect("dbi:Pg:dbname=$CACTUS::FWORCH::fworch_database;host=$CACTUS::FWORCH::fworch_srv_host;port=$CACTUS::FWORCH::fworch_srv_port","$CACTUS::FWORCH::fworch_srv_user","$CACTUS::FWORCH::fworch_srv_pw");
	if ( !defined $dbh ) { die "Cannot connect to database!\n"; }
	$sth = $dbh->prepare( $sqlcode );
	if ( !defined $sth ) { die "Cannot prepare statement: $DBI::errstr\n"; }
	$sth->execute;
	$err_str = $sth->errstr;
	if (defined($err_str) && length($err_str)>0) { error_handler($err_str); }
	($mgm_id) = $sth->fetchrow();
	$sth->finish;
	if (!defined($mgm_id)) {
		print("Management zum Device $dev_id nicht gefunden.");
		return -1;
	}
	#------
	$sqlcode = "SELECT control_id FROM import_control WHERE mgm_id=$mgm_id AND start_time<='" .
		$time . "' AND successful_import ORDER BY control_id desc LIMIT 1";
	$sth = $dbh->prepare( $sqlcode );
	if ( !defined $sth ) { die "Cannot prepare statement: $DBI::errstr\n"; }
	$sth->execute;
	$err_str = $sth->errstr;
	if (defined($err_str) && length($err_str)>0) { error_handler($err_str); }
	($relevant_import_id) = $sth->fetchrow();
	$sth->finish;
	$dbh->disconnect;
	if (!defined($relevant_import_id)) {
		print("kein Import gefunden.");
		return -1;
	}
	return $relevant_import_id;
}

############################################################
# insert_control_entry(is_initial_import, mgm_id)
# erzeugt neuen eintrag in import_control und 
# liefert die control_id zurueck
############################################################
sub insert_control_entry {
	my $is_initial_import  = shift;
	my $mgm_id  = shift;
	my ($rc, $dbh, $sth);
	my $current_control_id;
	my $sql_str;

	$is_initial_import = (($is_initial_import)?'TRUE':'FALSE');
	$dbh = DBI->connect("dbi:Pg:dbname=$CACTUS::FWORCH::fworch_database;host=$CACTUS::FWORCH::fworch_srv_host;port=$CACTUS::FWORCH::fworch_srv_port",
		"$CACTUS::FWORCH::fworch_srv_user","$CACTUS::FWORCH::fworch_srv_pw");
	if ( !defined $dbh ) { die "Cannot connect to database!\n"; }
	$rc  = $dbh->begin_work;
	$sql_str = "INSERT INTO import_control (is_initial_import,mgm_id) VALUES ($is_initial_import,$mgm_id)";
	#        print ("\n\nSQL: $sql_str\n\n");
	$sth = $dbh->prepare( $sql_str );
	if ( !defined $sth ) { die "Cannot prepare statement: $DBI::errstr\n"; }
	$sth->execute;
	$sth = $dbh->prepare( "SELECT MAX(control_id) FROM import_control;");
	if ( !defined $sth ) { die "Cannot prepare statement: $DBI::errstr\n"; }
	$sth->execute;
	($current_control_id) = $sth->fetchrow();
	$rc  = $dbh->commit;
	$sth->finish;
	$dbh->disconnect;
	return $current_control_id;
}

############################################################
# set_last_change_time($last_change_time_of_config,$current_import_id)
# liefert die control_id zurueck
############################################################
sub set_last_change_time {
	my $last_change_time_of_config = shift;
	my $current_import_id = shift;

	if (defined($last_change_time_of_config)) {
		#		print("\nlast change_time found: $last_change_time_of_config");
		my $sql_cmd1 =
			"UPDATE import_control SET last_change_in_config='$last_change_time_of_config' " .
				"WHERE control_id=$current_import_id";
		CACTUS::FWORCH::exec_pgsql_cmd_no_result($sql_cmd1);
	}
}
############################################################
# remove_control_entry($current_import_id)
# setzt Import-Eintrag auf NOT successful_import, wenn Fehler aufgetreten ist
############################################################
sub remove_control_entry {
	my $import_id = shift;
	my $sql_str = "UPDATE import_control SET successful_import=FALSE WHERE control_id=$import_id";
	return exec_pgsql_cmd_no_result($sql_str);
}

############################################################
# get_import_infos_for_mgm($mgm_id, $fworch_workdir)
# liefert vielfaeltige Infos zu einem zu importierenden Management zurueck
############################################################
sub get_import_infos_for_mgm {
	my $mgm_id = shift;
	my $fworch_workdir = shift;
	my $cfg_dir = shift;
	my ($dbh, $sth, $rc, $sth2);
	my ($err_str, %devices_with_rulebases, $mgm_name, $fields, $tables, $filter, $order, $sqlcode,
		$dev_typ_id,$ssh_hostname,$ssh_user,$ssh_private_key,$ssh_public_key,$hersteller);
	my ($template, $obj_file, $obj_file_base, $user_file, $user_file_base, $rule_file, $rule_file_base,
		$fworch_ctrl, $cmd_str, $is_netscreen, $ssh_port, $config_path_on_mgmt);
	my @result = ();
	my $fehler_cnt = 0;
	my $res = '';
	my $fehler = 0;
	my $res_array_ref;
	my $str_of_config_files = '';
	my $version;

	$sqlcode = "SELECT mgm_name,management.dev_typ_id,dev_typ_manufacturer,dev_typ_version,ssh_hostname,ssh_user,secret,ssh_public_key,ssh_port,config_path FROM management,stm_dev_typ WHERE stm_dev_typ.dev_typ_id=management.dev_typ_id AND mgm_id='$mgm_id'";
	$res_array_ref = exec_pgsql_cmd_return_array_ref($sqlcode, $fehler);
	if (!defined($fehler) || $fehler || !defined($res_array_ref)) {
		if (!defined($fehler)) {
			$fehler = "undefined error";
		}
		$fehler_cnt += 1;
		@result = (1, $fehler);
		return @result;
	}
	($mgm_name,$dev_typ_id,$hersteller,$version,$ssh_hostname,$ssh_user,$ssh_private_key,$ssh_public_key,$ssh_port,$config_path_on_mgmt) = @$res_array_ref;

	$hersteller = lc(remove_space_at_end($hersteller));
	if ($hersteller =~ /netscreen/) { $is_netscreen = 1; $hersteller = 'netscreen'; }
	else { $is_netscreen = 0; }

	#if ($hersteller =~ /check\spoint\sr8x/) { $hersteller = 'checkpointR8x'; }
	print ("version: $version, manufacturer: $hersteller, ");
	if ($hersteller =~ /check\spoint/ && $version eq 'R8x') { $hersteller = 'checkpointR8x'; }
	elsif ($hersteller =~ /check/) { $hersteller = 'checkpoint'; }
	if ($hersteller =~ /phion/) { $hersteller = 'phion'; }

	my $csv_zone_file  = "$fworch_workdir/" . $mgm_name . "_zones" . ".csv";
	my $csv_obj_file   = "$fworch_workdir/" . $mgm_name . "_netzobjekte" . ".csv";
	my $csv_svc_file   = "$fworch_workdir/" . $mgm_name . "_services" . ".csv";
	my $csv_usr_file   = "$fworch_workdir/" . $mgm_name . "_users" . ".csv";
	my $csv_auditlog_file   = "$fworch_workdir/" . $mgm_name . "_auditlog" . ".csv";

	if ($hersteller eq 'checkpoint')  {  # checkpoint
		$obj_file_base  = "objects_5_0.C";
		$obj_file       = $cfg_dir . '/' . $obj_file_base;
		$rule_file_base = "rulebases_5_0.fws";
		$rule_file		= $cfg_dir . '/' . $rule_file_base;
		$user_file_base = "fwauth.NDB";
		$user_file = $cfg_dir . '/' . $user_file_base;
		$str_of_config_files = "${obj_file},${rule_file},${user_file}";
	} elsif ($hersteller eq 'checkpointr8x')  {  # checkpoint R80 ff.
		print ("DEBUG: found hersteller checkpointr8x");
		$obj_file_base  = "nw_objects.json";
		$obj_file       = $cfg_dir . '/' . $obj_file_base;
		$rule_file_base = "rules.json";
		$rule_file		= $cfg_dir . '/' . $rule_file_base;
		#		$user_file_base = "fwauth.NDB";
		#		$user_file = $cfg_dir . '/' . $user_file_base;
		$str_of_config_files = "${obj_file},${rule_file}";
	} elsif ($hersteller eq 'phion')  {  # phion
		$obj_file_base = '';
		$obj_file      = $cfg_dir . "/" . $obj_file_base;
		$rule_file_base = '';
		$rule_file = $obj_file;
		$user_file_base = $obj_file_base;
		$user_file = $obj_file;
		$str_of_config_files = $cfg_dir . '/' . "iso-phion-config.tgz";
	}
	else { # e.g. hersteller = juniper (JUNOS), cisco (ASA), netscreen (ScreenOS)
		$obj_file_base = $mgm_name . ".cfg";
		$obj_file      = $cfg_dir . "/" . $obj_file_base;
		$rule_file_base = $obj_file_base;
		$rule_file = $obj_file;
		$user_file_base = $obj_file_base;
		$user_file = $obj_file;
		$str_of_config_files = $obj_file;
	}
	$rule_file = $cfg_dir . '/' . $rule_file_base;
	@result = (0, "", $mgm_name, $dev_typ_id,
		$obj_file_base,$obj_file,$user_file_base,$user_file,$rule_file_base,$rule_file,
		$csv_zone_file, $csv_obj_file, $csv_svc_file, $csv_usr_file, $csv_auditlog_file,
		$ssh_hostname,$ssh_user,$ssh_private_key,,$ssh_public_key,$hersteller,$is_netscreen,$str_of_config_files,$ssh_port,$config_path_on_mgmt);
	return @result;
}

############################################################
# clean_up_fworch_db ($current_import_id)
# fuehrt vacuum analyze fuer diverse Tabellen durch
# loescht die Eintraege des aktuellen Imports aus den import_*-Tabellen
############################################################
sub clean_up_fworch_db {
	my $current_import_id = shift;

	# loeschen der Import-Tabellen und neuordnen
	exec_pgsql_cmd_no_result ("DELETE FROM import_object	WHERE control_id=$current_import_id");
	exec_pgsql_cmd_no_result ("DELETE FROM import_service	WHERE control_id=$current_import_id");
	exec_pgsql_cmd_no_result ("DELETE FROM import_user		WHERE control_id=$current_import_id");
	exec_pgsql_cmd_no_result ("DELETE FROM import_rule		WHERE control_id=$current_import_id");
	exec_pgsql_cmd_no_result ("DELETE FROM import_zone		WHERE control_id=$current_import_id");
}

# produktunabhaengige Parse Subroutinen
#-------------------------------------------------------------------------------------------

#****************************
# print_debug
# param1	auszugebender String
# return	keiner
# Funktion zum Druck von debugging Info, wenn der Schalter entsprechend gesetzt ist.
#****************************

sub print_debug {
	my $txt = shift;
	my $debug_level = shift;
	my $print_level = shift;

	if (!defined ($debug_level)) { $debug_level = 0; }
	if (!defined ($print_level)) { $print_level = 0; }
	if (&is_numeric($print_level) && &is_numeric($debug_level)) {
		if ($print_level < $debug_level) {	print "Debug ($debug_level/$print_level): $txt.\n"; }
	} else {
		print "Debug ($debug_level/$print_level): $txt.\n";
	}
}

#****************************
# print_verbose
# param1	auszugebender String
# return	keiner
# Funktion zum Druck geschwaetziger (verbose) Info, wenn der Schalter entsprechend gesetzt ist.
#****************************
sub print_verbose {
	print "Verbose: " if ($debug);
	print "$_[0]" if (($verbose)||($debug));
}

#****************************
# d2u - dos 2 unix
# param1	zu aendernder String
# return	\n fuer jedes \r\n in einem String
# Funktion zum Wandeln eines DOS in einen UNIX String
#****************************
sub d2u {
	return ($_[0]=~s/\r\n/\n/g);
}

#****************************
# calc_subnetmask_sub
# param1	netzmaske in der Form xxx
# return	netzmaske als Bitzahl
# Unterfunktion zur Berechnung von Teil-Subnetzmasken
#****************************
sub calc_subnetmask_sub {
	my $netmask_in = shift;
	my $count = 0;
	my $temp = 0;

	if (!defined($netmask_in)) {
		return undef;
	}
	for ( ;($temp < int ($netmask_in));$temp += (2 ** (7 - $count++))) { };
	return $count;
}

#****************************
# calc_subnetmask
# param1	netzmaske in der Form xxx.xxx.xxx.xxx
# return	netzmaske als Bitzahl
# Funktion zur Berechnung von Subnetzmasken
#****************************
sub calc_subnetmask {
	my $netmask_in = shift;
	if (!defined($netmask_in)) {
		return undef;
	}
	$netmask_in =~ /([0-9]+).([0-9]+).([0-9]+).([0-9]+)/;
	my $temp1 = calc_subnetmask_sub($1);
	my $temp2 = calc_subnetmask_sub($2);
	my $temp3 = calc_subnetmask_sub($3);
	my $temp4 = calc_subnetmask_sub($4);
	my $temp0= $temp1 +$temp2 + $temp3 + $temp4;
	print_debug ("\t\t$temp0 = $1 - $temp1\t\t$2 - $temp2\t\t$3 - $temp3\t\t$4 - $temp4\n");
	return $temp0;
}

#****************************
# convert_mask_to_dot_notation
# param1	netzmaske in der Form /xx
# return	netzmaske in dot-Notation (e.g. 255.255.255.248)
#****************************
sub convert_mask_to_dot_notation {
	my $bits = shift;

	if ($bits =~ /(\d\d?)/) {
		$bits = $1/1;
	} else {
		$bits = 32;
	}
	$bits ==  0 && do return "0.0.0.0";
	$bits ==  1 && do return "128.0.0.0";
	$bits ==  2 && do return "192.0.0.0";
	$bits ==  3 && do return "224.0.0.0";
	$bits ==  4 && do return "240.0.0.0";
	$bits ==  5 && do return "248.0.0.0";
	$bits ==  6 && do return "252.0.0.0";
	$bits ==  7 && do return "254.0.0.0";
	$bits ==  8 && do return "255.0.0.0";
	$bits ==  9 && do return "255.128.0.0";
	$bits == 10 && do return "255.192.0.0";
	$bits == 11 && do return "255.224.0.0";
	$bits == 12 && do return "255.240.0.0";
	$bits == 13 && do return "255.248.0.0";
	$bits == 14 && do return "255.252.0.0";
	$bits == 15 && do return "255.254.0.0";
	$bits == 16 && do return "255.255.0.0";
	$bits == 17 && do return "255.255.128.0";
	$bits == 18 && do return "255.255.192.0";
	$bits == 19 && do return "255.255.224.0";
	$bits == 20 && do return "255.255.240.0";
	$bits == 21 && do return "255.255.248.0";
	$bits == 22 && do return "255.255.252.0";
	$bits == 23 && do return "255.255.254.0";
	$bits == 24 && do return "255.255.255.0";
	$bits == 25 && do return "255.255.255.128";
	$bits == 26 && do return "255.255.255.192";
	$bits == 27 && do return "255.255.255.224";
	$bits == 28 && do return "255.255.255.240";
	$bits == 29 && do return "255.255.255.248";
	$bits == 30 && do return "255.255.255.252";
	$bits == 31 && do return "255.255.255.254";
	$bits == 32 && do return "255.255.255.255";
}

#****************************
# get_protocol_number
# param1	string des ip-proto
# return	ip_proto_number
# Funktion zur Umwandlung von Protonamen in Protonummern
#****************************
sub get_proto_number {
	my $proto_in = shift;
	my $proto_out = 'Fehler: proto nicht gefunden';

	if (is_numeric($proto_in)) {
		return $proto_in;
	} elsif ($proto_in eq 'tcp') {
		$proto_out = 6;
	} elsif ($proto_in eq 'udp') {
		$proto_out = 17;
	} elsif ($proto_in eq 'icmp') {
		$proto_out = 1;
	}
	if ($proto_out eq 'Fehler: proto nicht gefunden') {
		#		print ("error: proto_in not found in import.pm::get_proto_number: $proto_in\n");
		undef ($proto_out);
	}
	return $proto_out;
}

#****************************
# print_cell_no_delimiter Output in Dateien
# return	keiner
# schreibt ein Feld der gefundenen Parserergebnisse in Datei, ohne abschliessenden Delimiter
#****************************
sub print_cell_no_delimiter {
	my $file = shift;
	my $cell = shift;
	$cell =~ s/$CACTUS::FWORCH::csv_delimiter/\\$CACTUS::FWORCH::csv_delimiter/g;
	if (defined($cell) && $cell ne '') {
		if ($cell =~ /^\".*?\"$/) {  # Zelle schon von doppelten Anfuehrungszeichen eingerahmt
			print $file "$cell";
		} else {
			print $file "\"$cell\"";
		}
	}
	return;
}

#****************************
# print_cell Output in Dateien
# return	keiner
# schreibt ein Feld der gefundenen Parserergebnisse in Datei
#****************************
sub print_cell {
	my $file = shift;
	my $cell = shift;
	&print_cell_no_delimiter ($file, $cell);
	print_cell_delimiter($file);
	return;
}

sub print_cell_delimiter {
	my $file = shift;
	print $file $CACTUS::FWORCH::csv_delimiter;
	return;
}


############################
# print_results_files_objects
############################
sub print_results_files_objects {
	my $out_dir = shift;
	my $mgm_name = shift;
	my $import_id = shift;
	my $out_file;
	my $header_text = "";
	my ($count,$local_schluessel,$schluessel);

	# Oeffnen der Ausgabedatei fuer Objekte
	#----------------------------------------
	#	print ("beginn csv schreiben\n");
	$out_file = "$out_dir/${mgm_name}_netzobjekte.csv";
	my $fh = new FileHandle;
	if ($fh->open("> $out_file")) {
		foreach $schluessel (sort (@network_objects)) {
			# Ausnahmen fuer die Ausgabe abfragen
			if (!defined($network_objects{"$schluessel.type"})) {
				print ("\nerror key $schluessel, type not defined");
			} else {
				unless ($network_objects{"$schluessel.type"} eq 'sofaware_profiles_security_level'){
					# sollten auch die dynamischen Netzobjekte ausgeblendet werden, ist die folgende Zeile statt der vorhergehenden zu zu aktivieren
					# unless (($network_objects{"$schluessel.type"} eq 'sofaware_profiles_security_level') || ($network_objects{"$schluessel.type"} eq 'dynamic_net_obj')) {
					# Version fuer Nachbearbeitung im Import Modul
					# foreach $local_schluessel (qw ( name type members cp_ver comments color ipaddr ipaddr_last netmask sys location )) {
					#	if (defined ( $network_objects{"$schluessel.$local_schluessel"} )) {
					#		print FILEOUT_NETOBJ $network_objects{"$schluessel.$local_schluessel"};
					#	}
					# Version mit integrierter Nachbearbeitung
					print_cell_no_delimiter($fh, $import_id);
					foreach (@obj_outlist)  {
						$local_schluessel = $_;
						print_cell_delimiter($fh);
						# Ist der Wert definiert? > dann ausgeben
						if (defined ( $network_objects{"$schluessel.$local_schluessel"} )) {
							# Objekttypen fuer Ausgabe umsetzen ?
							if (($local_schluessel eq 'type') && (($network_objects{"$schluessel.$local_schluessel"} eq 'gateway_cluster') || ($network_objects{"$schluessel.$local_schluessel"} eq 'cluster_member'))) {
								print_cell_no_delimiter($fh, 'gateway');
							}
							elsif (($local_schluessel eq 'type') && (($network_objects{"$schluessel.$local_schluessel"} eq 'machines_range'
								|| $network_objects{"$schluessel.$local_schluessel"} eq 'multicast_address_range'))) {
								print_cell_no_delimiter($fh, 'ip_range');
							}
							elsif (($local_schluessel eq 'type') &&
								(($network_objects{"$schluessel.$local_schluessel"} eq 'dynamic_net_obj' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'security_zone_obj' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'ips_sensor' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'voip_gk' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'voip_gw' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'voip_sip' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'voip_mgcp' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'voip_skinny'))) {
								print_cell_no_delimiter($fh, 'host');
							}
							elsif (($local_schluessel eq 'type') &&
								(($network_objects{"$schluessel.$local_schluessel"} eq 'group_with_exclusion' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'gsn_handover_group' ||
									$network_objects{"$schluessel.$local_schluessel"} eq 'domain'))) {
								print_cell_no_delimiter($fh, 'group');
								# da es jetzt eine Aufloesung der Ausnahme-Gruppen in normale Gruppen gibt, ist das jetzt OK
							}
							# IP-adresse?
							elsif (($local_schluessel eq 'ipaddr')||($local_schluessel eq 'ipaddr_last')) {
								# machine ranges immer mit 32 Bit Maske
								if ($network_objects{"$schluessel.type"} eq 'machines_range'){
									if ($network_objects{"$schluessel.$local_schluessel"} ne '') {
										#									print_cell_no_delimiter($fh, $network_objects{"$schluessel.$local_schluessel"}."/32");
										print_cell_no_delimiter($fh, $network_objects{"$schluessel.$local_schluessel"});
									}
								} else { # sonst nur die IP-Adresse
									my $maske;
									if (defined($network_objects{"$schluessel.netmask"}) &&
										length($network_objects{"$schluessel.netmask"})>0 &&
										$local_schluessel eq 'ipaddr' &&
										$network_objects{"$schluessel.ipaddr"} !~ /\//) {
										# berechnen
										my $ip2 = CACTUS::FWORCH::remove_space_at_end(remove_quotes($network_objects{"$schluessel.$local_schluessel"}));
										$maske = $network_objects{"$schluessel.netmask"};
										if ($maske =~ /^\d+\.\d+\.\d+\.\d+$/) {
											$maske = calc_subnetmask($maske); # in bit-Notation umwandeln
										} # else: Maske enthaelt keine Punkte --> direkt als Integer uebernehmen
										if (length($ip2)>0) {
											print_cell_no_delimiter($fh, "$ip2/$maske");
										}
									} elsif ($network_objects{"$schluessel.ipaddr"} !~ /\//) {
										# oder fix auf 32 Bit stellen, wenn maske fehlt und Fehlerausgabe
										if (length($network_objects{"$schluessel.$local_schluessel"})>0) {
											if ($network_objects{"$schluessel.$local_schluessel"} ne '') {
												#											print_cell_no_delimiter($fh, $network_objects{"$schluessel.$local_schluessel"}."/32");
												print_cell_no_delimiter($fh, $network_objects{"$schluessel.$local_schluessel"});
											}
										}
									} else {
										if ($network_objects{"$schluessel.$local_schluessel"} ne '') {
											print_cell_no_delimiter($fh, $network_objects{"$schluessel.$local_schluessel"});
										}
									}
								}
							}
							# default handling
							else {
								print_cell_no_delimiter($fh, $network_objects{"$schluessel.$local_schluessel"});
							}
						}
					}
					# Datensatztrennzeichen ausgeben
					print $fh "\n";
				}
			}
		}
		$fh->close;	# Schliessen der Ausgabedatei fuer NW-Objekte
	} else {
		die "NETOBJ: $out_file konnte nicht geoeffnet werden.\n";
	}

	# Oeffnen der Ausgabedatei fuer Services
	#----------------------------------------
	$out_file = "$out_dir/${mgm_name}_services.csv";
	$fh = new FileHandle;
	if ($fh->open("> $out_file")) {
		# Ausgabe der Datensaetze
		foreach $schluessel (sort (@services)) {
			print_cell_no_delimiter ($fh, "$import_id");
			foreach (@srv_outlist) {
				$local_schluessel = $_;
				print_cell_delimiter($fh);
				$local_schluessel = $_;
				# Ist der Wert definiert? > dann ausgeben
				if (defined ( $services{"$schluessel.$local_schluessel"} )) {
					# Serviceport fuer rpc umsetzen ?
					if (defined ($services{"$schluessel.typ"})) {
						unless (($local_schluessel eq 'port') && ($services{"$schluessel.typ"} eq 'rpc')) {
							print_cell_no_delimiter($fh, $services{"$schluessel.$local_schluessel"});
						}
					} else {
						print_cell_no_delimiter($fh, $services{"$schluessel.$local_schluessel"});
					}
				} elsif ($local_schluessel eq 'rpc_port') {
					# Serviceport fuer rpc umsetzten ?
					if (defined ($services{"$schluessel.typ"})) {
						if ($services{"$schluessel.typ"} eq 'rpc') {
							print_cell_no_delimiter($fh, $services{"$schluessel.port"});
						}
					}
				}
			}
			# Datensatztrennzeichen ausgeben
			print $fh "\n";
		}
		$fh->close;	# Schliessen der Ausgabedatei fuer Services
	} else {
		die "SVC_OUT: $out_file konnte nicht geoeffnet werden.\n";
	}
}

#****************************
# print_results_files_rules Output in Dateien
# return	keiner
# gibt die gefundenen Parserergebnisse in Dateien aus
#****************************
sub print_results_files_rules {
	my $out_dir = shift;
	my $mgm_name = shift;
	my $import_id = shift;
	my $is_first_in_loop;
	my $out_file;
	my $header_text = "";
	my ($count,$local_schluessel,$schluessel,$flat_file);

	foreach $schluessel (sort (@rulebases)) {
		# Oeffnen der Ausgabedatei fuer Rulebases
		#----------------------------------------
		# Regeln definiert? > Datei Oeffnen und schreiben
		if ( !defined($rulebases{"$schluessel.rulecount"}) ) {
			print ("\nwarning in import.pm: empty ruleset rulebases($schluessel.rulecount) not defined");
		} elsif ( $rulebases{"$schluessel.rulecount"} > 0 ) {
			if (defined($rulebases{"$schluessel.ruleorder"})) {
				@ruleorder = split (/,/, $rulebases{"$schluessel.ruleorder"});
			} else  { # ruleorder ist aufsteigend von 0-n
				@ruleorder = ();
				for (my $i=0; $i<$rulebases{"$schluessel.rulecount"}; ++$i) {
					push @ruleorder, ($i);
				}
			}

			# Dateinamen festlegen
			if (!defined($out_dir)) { $out_dir = "."; }
			$flat_file = $schluessel; $flat_file =~ s/\//\_/g;
			$out_file = "$out_dir/${flat_file}_rulebase.csv";
			my $fh = new FileHandle;
			if ($fh->open("> $out_file")) {
				# Ausgabe der Datensaetze
				for ($count = 0; $count < $rulebases{"$schluessel.rulecount"}; $count++) {
					# Regelnummer ausgeben
					print_cell($fh, $import_id); # import_id
					print_cell ($fh, $count);		# regel_nummer
					print_cell_no_delimiter ($fh, $schluessel);								# rulebase_name
					foreach (@rule_outlist) {
						$local_schluessel = $_;
						print_cell_delimiter($fh);
						# Ist der Wert definiert? > dann ausgeben
						if ( defined ($rulebases{"$schluessel.$ruleorder[$count].$local_schluessel"})) {
							print_cell_no_delimiter($fh, $rulebases{"$schluessel.$ruleorder[$count].$local_schluessel"});
						} else {
							if ($local_schluessel eq 'track') {
								print_cell_no_delimiter($fh, 'none');
							}
						}
					}
					print $fh "\n";		# Datensatztrennzeichen ausgeben
				}
				$fh->close;	# Schliessen der Ausgabedatei fuer Rules
			} else {
				die "RULE_OUT: $out_file konnte nicht geoeffnet werden.\n";
			}
			print_verbose ("Output Datei: $out_file wurde geschlossen.\n\n");
			# keine Regeln, keine Regeldatei!
		} else {
			print_verbose ("$schluessel hat keine Regeln.\n");
			print_verbose ("Datei $out_file wurde nicht ausgegeben.\n\n");
		}
	}
}

#****************************
# print_results_files_zones Output in Dateien
# return	keiner
# gibt die gefundenen Parserergebnisse in Dateien aus
#****************************
sub print_results_files_zones {
	my $out_dir = shift;
	my $mgm_name = shift;
	my $import_id = shift;
	my ($out_file,$schluessel);

	if (!defined($out_dir)) { $out_dir = "."; }
	$out_file = "$out_dir/${mgm_name}_zones.csv";
	my $fh = new FileHandle;
	if ($fh->open("> $out_file")) {
		print_verbose ("Output Datei: $out_file wurde geoeffnet.\n");
		foreach $schluessel (sort (@zones)) {
			print_cell_no_delimiter($fh, $import_id);	# import id
			print_cell_delimiter($fh);
			print_cell_no_delimiter($fh, $schluessel);	# zone name
			print $fh "\n";
		}
		$fh->close;	# Schliessen der Ausgabedatei fuer Zonen
	} else {
		die "ZONE-OUT: $out_file konnte nicht geoeffnet werden.\n";
	}
}

#****************************
# print_results_files_audit_log Output in Dateien
# return	keiner
# gibt die gefundenen Parserergebnisse in Dateien aus
#****************************
sub print_results_files_audit_log {
	my $out_dir = shift;
	my $mgm_name = shift;
	my $import_id = shift;
	my ($out_file,$schluessel);

	if (!defined($out_dir)) { $out_dir = "."; }
	$out_file = "$out_dir/${mgm_name}_auditlog.csv";
	my $fh = new FileHandle;
	if ($fh->open("> $out_file")) {
		for (my $idx = 0; $idx<$audit_log_count; $idx ++) {
			print_cell ($fh, $import_id);
			print_cell_no_delimiter ($fh, $idx);
			foreach my $schluessel (@auditlog_outlist) {
				print_cell_delimiter($fh);
				if ( defined ($auditlog{"$idx.$schluessel"})) {			# Ist der Wert definiert? > dann ausgeben
					print_cell_no_delimiter($fh, $auditlog{"$idx.$schluessel"});
				}
			}
			print $fh "\n"; # EOL
			$fh->close;	# Schliessen der Ausgabedatei fuer Auditlog
		}
	} else {
		die "AUDITLOG-OUT: $out_file konnte nicht geoeffnet werden.\n";
	}
}
#****************************
# print_results_files_users Output in Dateien
# return	keiner
# gibt die gefundenen Parserergebnisse in Dateien aus
#****************************
sub print_results_files_users {
	my $out_dir = shift;
	my $mgm_name = shift;
	my $import_id = shift;
	my $s = '';
	my $is_first_in_loop;
	my $schluessel;
	my $out_file;

	if (!defined($out_dir)) { $out_dir = "."; }
	$out_file = "$out_dir/${mgm_name}_users.csv";
	my $fh = new FileHandle;
	if ($fh->open("> $out_file")) {
		foreach my $name (sort @user_ar) {
			print_cell ($fh, $import_id);
			print_cell_no_delimiter($fh, $name);	# user name
			foreach my $schluessel (@user_outlist) {
				print_cell_delimiter($fh);
				if ( defined ($user{"$name.$schluessel"})) {			# Ist der Wert definiert? > dann ausgeben
					print_cell_no_delimiter($fh, $user{"$name.$schluessel"});
				}
			}
			print $fh "\n"; # EOL
		}
	} else {
		die "USER_OUT: $out_file konnte nicht geoeffnet werden.\n";
	}
}

#****************************
# print_results_monitor Testoutput auf den Monitor
# return	keiner
# druckt die gefundenen Parserergebnisse ohne weitere Aufbereitung oder Filterung der Informationen
#****************************
sub print_results_monitor {
	my $mode = $_[0];
	my ($schluessel, $local_schluessel, $count);

	# if steuert, ob jeweiliges printmodul aktiv 	=> 0 unterdruecken
	#						=> 1 ausgeben
	SWITCH_parsemode: {
		# nur im Modus "Objekte"
		if ($mode eq 'objects') {
			if ( 1 ) {
				print "\n\nnetwork_objects:\n";
				foreach $schluessel (sort (keys(  %network_objects))) {
					if (!defined($network_objects{$schluessel})) {
						print ("\nerror print_results_monitor nwobj $schluessel not defined");
					} else {
						print "$schluessel\t\t\t $network_objects{$schluessel} \n";
					}
				}

			}
			if ( 1 ) {
				print "\n\nservices:\n";
				foreach $schluessel (sort (keys( %services ))) {
					print "$schluessel\t\t\t $services{$schluessel} \n";
				}
			}
			if ( 0 ) {
				print "\n\nnetwork_objects:\n";
				foreach $schluessel (sort (@network_objects)) {
					print "\t$schluessel\n";
					foreach $local_schluessel ( @obj_outlist ) {
						print "\t\t$local_schluessel\t";
						if (defined ( $network_objects{"$schluessel.$local_schluessel"} )) {
							print $network_objects{"$schluessel.$local_schluessel"}."\n";
						} else {
							print "\n";
						}
					}
				}
			}
			if ( 0 ) {
				print "\n\nservices:\n";
				foreach $schluessel (sort (@services)) {
					print "\t$schluessel\n";
					foreach $local_schluessel ( @srv_outlist ) {
						print "\t\t$local_schluessel\t";
						if (defined ( $services{"$schluessel.$local_schluessel"} )) {
							print $services{"$schluessel.$local_schluessel"}."\n";
						} else {
							print "\n";
						}
					}
				}
			}
			last SWITCH_parsemode;
		}
		#nur im Modus "Regeln"

		if ($mode eq 'rules') {
			if ( 1 ) {
				my $rule_no;
				print "\n\nrulebases:\n";
				foreach $schluessel (sort (@rulebases)) {
					if (defined($rulebases{"$schluessel.rulecount"} )) {
						$rule_no = $rulebases{"$schluessel.rulecount"};
					} else {
						$rule_no = 0;
					}
					print "\t(#regeln: " . $rule_no . ")\t$schluessel\n";
				}
			}
			if ( 0 ) {
				foreach $schluessel (sort (@rulebases)) {
					print "\n\nruleset:\t$schluessel\n";
					foreach $local_schluessel (sort (keys(%rulebases))) {
						print $local_schluessel."\t".$rulebases{"$local_schluessel"}."\n";
					}
				}
			}
			if ( 1 ) {
				print "\n\nrules:\n";
				foreach $schluessel (sort (@rulebases)) {
					print "Regeln des Regelwerks $schluessel:\n";
					if (defined($rulebases{"$schluessel.rulecount"})) {
						@ruleorder = split (/,/, $rulebases{"$schluessel.ruleorder"});
						for ($count = 0; $count < $rulebases{"$schluessel.rulecount"}; $count++) {
							foreach $local_schluessel ( @rule_outlist ) {
								print "$schluessel\t$count\t$local_schluessel\t";
								if (defined ($rulebases{"$schluessel.$ruleorder[$count].$local_schluessel"})){
									print $rulebases{"$schluessel.$ruleorder[$count].$local_schluessel"};
								}
								print "\n";
							}
						}
						print ("rulecount: " . $rulebases{"$schluessel.rulecount"} . "\n");
						if (defined($rulebases{"$schluessel.ruleorder"})) {
							print ("ruleorder: " . $rulebases{"$schluessel.ruleorder"} . "\n");
						}
					}
					print "\n";
				}
			}
			last SWITCH_parsemode;
		}
	}
}

############################################################
# fill_import_tables_from_csv ($sqldatafile, $fehler_count, $dev_typ_id,
#       $csv_obj_file, $csv_svc_file, $csv_usr_file, $csv_rule_file)
# fuellt die import_tabellen
############################################################
sub fill_import_tables_from_csv {
	my $fehler_count = 0;
	my $dev_typ_id = shift;
	my $csv_zone_file = shift;
	my $csv_obj_file = shift;
	my $csv_svc_file = shift;
	my $csv_usr_file = shift;
	my $rulebases = shift;      # ref to hash of rulebase-infos
	my $fworch_workdir = shift;
	my $csv_audit_log_file = shift;
	my ($csv_rule_file,$fehler, $fields, $sqlcode, $psql_cmd, $start_time);

	$start_time = time();
	if (file_exists($csv_zone_file)) { # optional (netscreen, fortigate)
		$fields = "(" . join(',',@zone_import_fields) . ")";
		$sqlcode = "COPY import_zone $fields FROM STDIN DELIMITER '$CACTUS::FWORCH::csv_delimiter' CSV";
		$fehler = CACTUS::FWORCH::copy_file_to_db($sqlcode,$csv_zone_file);
	}
	if (defined($csv_audit_log_file) && file_exists($csv_audit_log_file)) { # optional if audit log exists
		$fields = "(" . join(',',@auditlog_import_fields) . ")";
		$sqlcode = "COPY import_changelog $fields FROM STDIN DELIMITER '$CACTUS::FWORCH::csv_delimiter' CSV";
		$fehler = CACTUS::FWORCH::copy_file_to_db($sqlcode,$csv_audit_log_file);
	}
	$fields = "(" . join(',',@obj_import_fields) . ")";
	$sqlcode = "COPY import_object $fields FROM STDIN DELIMITER '$CACTUS::FWORCH::csv_delimiter' CSV";
	if ($fehler = CACTUS::FWORCH::copy_file_to_db($sqlcode,$csv_obj_file)) {
		print_error("dbimport: $fehler"); print_linebreak(); $fehler_count += 1;
	}
	$fields = "(" . join(',',@svc_import_fields) . ")";
	$sqlcode = "copy import_service $fields FROM STDIN DELIMITER '$CACTUS::FWORCH::csv_delimiter' CSV";
	if ($fehler = CACTUS::FWORCH::copy_file_to_db($sqlcode,$csv_svc_file)) {
		print_error("dbimport: $fehler"); print_linebreak(); $fehler_count += 1;
	}
	if (file_exists($csv_usr_file)) {
		$fields = "(" . join(',',@user_import_fields) . ")";
		$sqlcode = "COPY import_user $fields FROM STDIN DELIMITER '$CACTUS::FWORCH::csv_delimiter' CSV";
		if ($fehler = CACTUS::FWORCH::copy_file_to_db($sqlcode,$csv_usr_file)) { }
	}
	$fields = "(" . join(',',@rule_import_fields) . ")";
	my @rulebase_ar = ();
	foreach my $d (keys %{$rulebases}) {
		my $rb = $rulebases->{$d}->{'local_rulebase_name'};
		my $rulebase_name_sanitized = join('__', split /\//, $rb);
		if ( !grep( /^$rulebase_name_sanitized$/, @rulebase_ar ) ) {
			@rulebase_ar = (@rulebase_ar, $rulebase_name_sanitized);
			# print ("rulebase_name_sanitized: $rulebase_name_sanitized\n");
			$csv_rule_file = $fworch_workdir . '/' . $rulebase_name_sanitized . '_rulebase.csv';
			print ("rulebase found: $rulebase_name_sanitized, rule_file: $csv_rule_file, device: $d\n");
			$sqlcode = "COPY import_rule $fields FROM STDIN DELIMITER '$CACTUS::FWORCH::csv_delimiter' CSV";
			if ($fehler = CACTUS::FWORCH::copy_file_to_db($sqlcode,$csv_rule_file)) {
				print_error("dbimport: $fehler"); print_linebreak(); $fehler_count += 1;
			}
		} else {
			print ("ignoring another device ($d) with rulebase $rb\n");
		}
	}	print_bold("Database Import ... done in " . sprintf("%.2f",(time() - $start_time)) . " seconds"); print_linebreak();
	return $fehler_count;
}

############################################################
# fill_import_tables_from_csv_with_sql ($dev_typ_id,
#       $sql_obj_file, $sql_svc_file, $sql_usr_file, $sql_rule_file)
# fuellt die import_tabellen mit einzelen sql-befehlen
# nur zum Debuggen verwendet, wenn der CSV-Import schiefgeht
############################################################
sub fill_import_tables_from_csv_with_sql {
	my $fehler_count = 0;
	my $dev_typ_id = shift;
	my $csv_zone_file = shift;
	my $csv_obj_file = shift;
	my $csv_svc_file = shift;
	my $csv_usr_file = shift;
	my $rulebases = shift;      # ref to hash of rulebase-infos
	my $fworch_workdir = shift;
	my $csv_audit_log_file = shift;
	my ($csv_rule_file,$fehler, $fields, $sqlcode, $psql_cmd, $start_time);

	sub build_and_exec_sql_statements { 	# baut aus csv-Files sql statements und fuehrt diese einzeln aus
		my $target_table = shift;
		my $str_of_fields = shift;
		my $csv_file = shift;
		my $input_line;
		my $sqlcode;
		my @values = ();
		my $fehler;
		my $fehler_count=0;
		my $delimiter;

		sub str_to_sql_value {
			my $input_str = remove_quotes(shift);

			if ($input_str eq '') {
				return 'NULL';
			} else  {
				$input_str =~ s/\'/\\\'/g;
				$input_str =~ s/^"//;
				$input_str =~ s/"$//;
				return "'$input_str'";
			}
		}

		my $CSVhdl = new IO::File ("< $csv_file") or $fehler = "Cannot open file $csv_file for reading: $!";
		if ($fehler) {
			print_error("FEHLER: $fehler");
			print_linebreak();
			$fehler_count += 1;
			return $fehler_count;
		}
		$delimiter = qr/$CACTUS::FWORCH::csv_delimiter/;

		while ($input_line = <$CSVhdl>) {
			$sqlcode = "INSERT INTO $target_table ($str_of_fields) VALUES (";
			$input_line =~ s/\n$//;

			$input_line =~ s/\\$delimiter/AbDdia679roe4711/g;
			@values = split (/$delimiter/, $input_line);
			my @values2 = @values;
			@values = ();
			foreach my $val (@values2) {
				$val =~ s/AbDdia679roe4711/$delimiter/g;
				@values = (@values, $val);
			}
			while ($input_line =~ /$delimiter$/) {  # line ends with ; --> add NULL-value for last column
				push @values, "";
				$input_line =~ s/$delimiter$//;
			}
			$sqlcode .= str_to_sql_value($values[0]);
			my $i = 1;
			while ($i<=$#values) {
				$sqlcode .= "," . str_to_sql_value($values[$i++]);
			}
			$sqlcode .= ");";
			#		print ("sql_code: $sqlcode\n");
			if ($fehler = CACTUS::FWORCH::exec_pgsql_cmd_no_result($sqlcode)) {
				output_txt($fehler . "; $csv_file",3);
				$fehler_count += 1;
			}
		}
		$CSVhdl->close;
		return $fehler_count;
	}

	print_bold(" - Database Import (non-bulk)");
	print_linebreak(); print_txt("- writing data into import tables ... ");
	$start_time = time();

	if (file_exists($csv_zone_file)) { # optional nur fuer Netscreen
		$fehler = build_and_exec_sql_statements('import_zone', join(',',@zone_import_fields), $csv_zone_file);
		if ($fehler) { $fehler_count ++; }
	}
	if (defined($csv_audit_log_file) && file_exists($csv_audit_log_file)) { # optional nur wenn auditlog existiert
		$fehler = build_and_exec_sql_statements('import_changelog', join(',',@auditlog_import_fields), $csv_audit_log_file);
		if ($fehler) { $fehler_count ++; }
	}
	$fields = join(',', @obj_import_fields);
	$fehler = build_and_exec_sql_statements('import_object',$fields, $csv_obj_file);
	if ($fehler) { $fehler_count ++; }

	$fields = join(',',@svc_import_fields);
	$fehler = build_and_exec_sql_statements('import_service',$fields, $csv_svc_file);
	if ($fehler) { $fehler_count ++; }

	if (file_exists($csv_usr_file)) {
		$fields = join(',',@user_import_fields);
		$fehler = build_and_exec_sql_statements('import_user',$fields, $csv_usr_file);
	}
	$fields = join(',',@rule_import_fields);
	foreach my $d (keys %{$rulebases}) {
		my $rb = $rulebases->{$d}->{'local_rulebase_name'};
		$csv_rule_file = $fworch_workdir . '/' . $rb . '_rulebase.csv';
		$fehler = build_and_exec_sql_statements('import_rule',$fields, $csv_rule_file);
		if ($fehler) { $fehler_count ++; }
	}
	print_bold ("done in " . sprintf("%.2f",(time() - $start_time)) . " seconds"); print_linebreak();
	return $fehler_count;
}

#####################################################################################

1;
__END__

=head1 NAME

FWORCH::import - Perl extension for fworch Import

=head1 SYNOPSIS

  use CACTUS::FWORCH::import;

=head1 DESCRIPTION

fworch Perl Module support for importing configs into fworch Database

=head2 EXPORT

  global variables

  DB functions

=head1 SEE ALSO

  behind the door


=head1 AUTHOR

  Cactus eSecurity, tmp@cactus.de

=cut
