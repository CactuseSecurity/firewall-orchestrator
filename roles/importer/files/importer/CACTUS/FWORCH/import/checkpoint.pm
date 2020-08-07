package CACTUS::FWORCH::import::parser;

use strict;
use warnings;
use IO::File;
use Getopt::Long;
use File::Basename;
use Time::HiRes qw(time);    # fuer hundertstelsekundengenaue Messung der Ausfuehrdauer
use Net::CIDR;
use CACTUS::FWORCH;
use CACTUS::FWORCH::import;
use Date::Calc qw(Add_Delta_DHMS);

require Exporter;
our @ISA = qw(Exporter);

our %EXPORT_TAGS = ( 'basic' => [ qw( &copy_config_from_mgm_to_iso &parse_config ) ] );

our @EXPORT  = ( @{ $EXPORT_TAGS{'basic'} } );
our $VERSION = '0.3';

# variblendefinition check point parser - global
# -------------------------------------------------------------------------------------------
my $GROUPSEP = $CACTUS::FWORCH::group_delimiter; 

my $UID      = "UID";    # globale konstante UID

# Stati und anderes des Objektparsers
my $ln   = 0;
my $line = '';
our $parse_obj_state = 0;  # moegliche Werte	0	kein status
                           #			1	objectclass gestartet
                           #			2	object gestartet
                           #			3...7	diverse attribute & werte (kontextsensitiv)
                           # 			>10	innerhalb einer Gruppe
our $parse_obj_type;       # State 1 - aktuelle Objektklasse
our $parse_obj_name;       # State 2 - name des aktuellen objektes
our $old_parse_obj_name;   # State 2 - name des letzten objektes
our $parse_obj_attr;       # State 3 - Attribut
our $parse_obj_attr_value; # State 3 - Wert des Attributes
our $parse_obj_attr_ext;   # State 4 - Attributerweiterung (kontextsensitiv)
our $parse_obj_attr_ext_value
  ;    # State 4 - Wert des erweiterten Attributes (kontextsensitiv)
our $parse_obj_attr_ext2;    # State 5 - Attributerweiterung (kontextsensitiv)
our $parse_obj_attr_ext2_value
  ;    # State 5 - Wert des erweiterten Attributes (kontextsensitiv)
our $parse_obj_attr_ext3;    # State 6 - Attributerweiterung (kontextsensitiv)
our $parse_obj_attr_ext3_value
  ;    # State 6 - Wert des erweiterten Attributes (kontextsensitiv)
our $parse_obj_attr_ext4;    # State 7 - Attributerweiterung (kontextsensitiv)
our $parse_obj_attr_ext4_value
  ;    # State 7 - Wert des erweiterten Attributes (kontextsensitiv)
our $parse_obj_groupmember;         # string-array fuer gruppenmitglieder
our $parse_obj_groupmember_refs;    # string-array fuer gruppenmitglieder
our $group_with_exclusion_marker;	# gibt an, ob die aktuelle gruppe eine normale (undefined),
									# positiv- (base), oder negativ-gruppe (exclusion) ist

# Stati und anderes des Rulesparser
our $parserule_state        = 0;     # status des Regelparsers
                                     # 0 kein Regelwerk
                                     # 1 Regelwerk
                                     # 2 Regel
                                     # 14 Gruppe
                                     # 15 Mitgliederliste der Gruppe
                                     # 16 Mitglied einer Compound Gruppe
our $parserule_in_rule_base = 0;     # wenn innerhalb eines Regelwerkes
our $parserule_in_rule      = 0;     # wenn innerhalb einer Regeldefinition
our $parserule_rulenum      = -1;    # aktuelle Regelnummer
our $parserule_rulebasename;         # aktuellen Regelwerkes
our $parserule_ruleparameter;        # Bezeichnung des aktuellen Parameter
our $parserule_groupmember;          # Regelgruppenmitglieder
our $parserule_groupmember_refs;     # String mit Uids
our $parserule_ruleparameter_ext;    # Attributerweiterung (kontextsensitiv)
our $parserule_ruleparameter_ext_value
  ;    # Wert des erweiterten Attributes (kontextsensitiv)
our $parserule_ruleuser
  ;    # user, fuer die die Regel gilt (alle user aus der Quellspalte)
our %usergroup;

#####################################################################################
# Start Check Point Parser
#####################################################################################

sub parse_config {
	my $object_file   = shift;
	my $rulebase_file = shift;
	my $user_db_file  = shift;
	my $rulebase_name = shift;
	my $output_dir    = shift;
	my $verbose       = shift;
	my $mgm_name      = shift;
	my $config_dir    = shift;
	my $import_id     = shift;
	my $audit_log_file= shift;
	my $prev_import_time= shift;
	my $parse_full_audit_log = shift;
	my $result;

	if ($result = cp_parse_main( $object_file, $output_dir, '', $verbose )) { return $result; }

	undef($parse_obj_state);
	undef($parse_obj_type);
	undef($old_parse_obj_name);
	undef($parse_obj_attr);
	undef($parse_obj_attr_value);
	undef($parse_obj_attr_ext);
	undef($parse_obj_attr_ext_value);
	undef($parse_obj_attr_ext2);
	undef($parse_obj_attr_ext2_value);
	undef($parse_obj_groupmember);
	undef($parse_obj_groupmember_refs);

	if ($result = cp_parse_main( $rulebase_file, $output_dir, $rulebase_name, $verbose )) { return $result; }

	if ($result = &cp_parse_users_main($user_db_file, $output_dir, $verbose)) { return $result; }
	if ($result = &cp_parse_users_from_rulebase($rulebase_file)) { return $result; }

	if ( -e $audit_log_file ) {  # if audit.log exists, process it
		&parse_audit_log($audit_log_file, $prev_import_time, $parse_full_audit_log);
	}

	&print_results_files_objects	( $output_dir, $mgm_name, $import_id );
	&print_results_files_rules		( $output_dir, $mgm_name, $import_id );
	&print_results_files_users		( $output_dir, $mgm_name, $import_id );
	&print_results_files_audit_log	( $output_dir, $mgm_name, $import_id );
	return 0;    # done without errors
}

############################################################
# gen_uid (geparste uid)
# wandelt geparste uid in fworch-kompatible UID um
# derzeit ist lediglich ein upercase notwendig
############################################################
sub gen_uid {
	return ( uc(shift) );
}

sub add_delta_db_time {
	my $db_time = shift;
	my $diff_in_seconds = shift;

	my ($year, $month, $day, $hour, $min, $sec) = $db_time =~ m/(\d+)\-(\d+)\-(\d+)\s+(\d+)\:(\d+)\:(\d+)/;		
	($year, $month, $day, $hour, $min, $sec) =  Add_Delta_DHMS( $year, $month, $day, $hour, $min, $sec, 0, 0, 0, $diff_in_seconds );
	return sprintf("%04d-%02d-%02d %02d:%02d:%02d", $year, $month, $day, $hour, $min, $sec);
}

############################################################
# convert_checkpoint_to_db_date (string)
# konvertiert von Format 'Sun Jul 21 12:24:13 2002'
#			  oder       '21Jul2007 12:24:13'
#			  oder       '21-jul-2007'
#             zu  Format '2002-07-21 12:24:13'
############################################################
sub convert_checkpoint_to_db_date {
	my $month = 0;
	my $sec = 0;
	my ($result, $day, $year, $hour, $min);

#	'[Sun ]Jul 21 12:24:13 2007'
	if ( $_[0] =~ /^\w+\s+(\w+)\s+(\d+)\s+(\d+)\:(\d+)\:?(\d+)?\s+(\d+)$/ ) {
		$month = $1;
		$day   = $2;
		$hour  = $3;
		$min   = $4;
		if (defined($5)) { $sec = $5; }
		$year  = $6;
	}
#	'21Jul2007 12:24:13'
	if ( $_[0] =~ /^(\d+)\s?([a-zA-Z]{3})(\d{4})\s+(\d+)\:(\d+)\:?(\d+)?$/ ) {
		$month = $2;
		$day   = $1;
		$hour  = $4;
		$min   = $5;
		if (defined($6)) { $sec = $6; }
		$year  = $3;
	}
	
#	'21-jul-2007'	
	if ( $_[0] =~ /^(\d+)\-([a-zA-Z]{3})\-(\d{4})$/ ) {
		$month = $2;
		$day   = $1;
		$year  = $3;
	}
	SWITCH: {
		if ($month =~ /jan/i) { $month = "01"; last SWITCH; }
		if ($month =~ /feb/i) { $month = "02"; last SWITCH; }
		if ($month =~ /mar/i) { $month = "03"; last SWITCH; }
		if ($month =~ /apr/i) { $month = "04"; last SWITCH; }
		if ($month =~ /may/i) { $month = "05"; last SWITCH; }
		if ($month =~ /jun/i) { $month = "06"; last SWITCH; }
		if ($month =~ /jul/i) { $month = "07"; last SWITCH; }
		if ($month =~ /aug/i) { $month = "08"; last SWITCH; }
		if ($month =~ /sep/i) { $month = "09"; last SWITCH; }
		if ($month =~ /oct/i) { $month = "10"; last SWITCH; }
		if ($month =~ /nov/i) { $month = "11"; last SWITCH; }
		if ($month =~ /dec/i) { $month = "12"; last SWITCH; }
		$month = 0;
	}
	if ( $month ne 0 ) {
		if (defined ($year) && defined($month) && defined($day) && defined($hour) && defined($min) ) {
			$result = sprintf("%04d-%02d-%02d %02d:%02d:%02d", $year, $month, $day, $hour, $min, $sec);
		} elsif (defined ($year) && defined($month) && defined($day) ) {
			$result = sprintf("%04d-%02d-%02d", $year, $month, $day);
		} else {
			print ("WARNING: convert_cp_to_db_date: " . $_[0] . " undefined value: $year-$month-$day $hour:$min:$sec\n"); 
		}
	}
	return $result;
}

############################################################
# convert_db_to_checkpoint_date (string)
# konvertiert von Format '2002-07-21 12:24:13'
#             zu  Format 'Jul 21 12:24:13 2002'
############################################################
sub convert_db_to_checkpoint_date {
	my $result;
	my $month;
	my $day;
	my $year;
	my $rest;
	my $time = ' ';
	
	if ( $_[0] =~ /^(\d+)\-(\d+)\-(\d+)(.*)$/ ) {
		$month = $2;
		$day   = $3;
		$year  = $1;
		$rest  = $4;
	} else {
		return '';
	}
	my @months = qw(Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec);
	$month = $months[$month-1];
	
	if ( $rest =~ /(\d+)\:(\d+)\:?(\d+)?/ ) {
		$time = " $1:$2";
		if (defined($3)) {
			$time .= ":$3";
		}
		$time .= ' ';
	}
	return "$month $day$time$year";
}

############################################################
# copy_config_from_mgm_to_iso($ssh_private_key, $ssh_user, $ssh_hostname, $management_name, $obj_file_base, $cfg_dir, $rule_file_base)
# Kopieren der Config-Daten vom Management-System zum ITSecorg-Server
############################################################
sub copy_config_from_mgm_to_iso {
	my $ssh_user        = shift;
	my $ssh_hostname    = shift;
	my $management_name = shift; # not used
	my $obj_file_base   = shift;
	my $cfg_dir         = shift;
	my $rule_file_base  = shift;
	my $workdir         = shift;
	my $auditlog		= shift;	
	my $prev_import_time= shift;
	my $ssh_port		= shift;
	my $config_path_on_mgmt		= shift;
	my $user_db_file	= 'fwauth.NDB';
	my $cmd;
	my $return_code;
	my $fehler_count = 0;

	if (!defined($config_path_on_mgmt) || $config_path_on_mgmt eq '') {
		$config_path_on_mgmt		= "";
	}
	if (!defined($ssh_port) || $ssh_port eq '') {
		$ssh_port = "22";
	}
	my $tar_archive = 'cp_config.tar.gz';		# compress files for bandwidth optimization
	my $tar_cmd = "cd $config_path_on_mgmt; tar chf $tar_archive $obj_file_base $rule_file_base $user_db_file";
	$cmd = "$ssh_bin -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname \"$tar_cmd\"";
#	print("DEBUG - tar_cmd = $cmd\n");
	$return_code = system($cmd); if ( $return_code != 0 ) { $fehler_count++;	}

	$cmd = "$scp_bin $scp_batch_mode_switch -P $ssh_port -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname:$config_path_on_mgmt$tar_archive $cfg_dir";
#	print("DEBUG - copy_cmd = $cmd\n");
	$return_code = system($cmd); if ( $return_code != 0 ) { $fehler_count++;	}

	$cmd = "$ssh_bin -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname \"rm $tar_archive\"";   # cleanup
#	print("DEBUG - tar_cmd = $cmd\n");

	# gunzip -c xx.tar.gz | tar tvf -
# 	$tar_cmd = "cd $cfg_dir; gunzip -c $tar_archive | tar xf -";
	$tar_cmd = "cd $cfg_dir; tar xf $tar_archive";
	$return_code = system($tar_cmd); if ( $return_code != 0 ) { $fehler_count++;	}

	if ( ! $fehler_count ) {
		&remove_literal_carriage_return("$cfg_dir/$obj_file_base");
		&remove_literal_carriage_return("$cfg_dir/$rule_file_base"); 
	}
	# do not return name of auditlog file to avoid testing for md5sum
	return ( $fehler_count, "$cfg_dir/$obj_file_base,$cfg_dir/fwauth.NDB,$cfg_dir/$rule_file_base" );
}

#****************************
# parser fuer audit log von check point
# insbesondere fuer die Erkennung von deletes (user et al.) zu verwenden
#****************************
# syntaxbeispiele auditlog:
# 1Oct2007 12:18:22 accept sting      <    ObjectName: iso-test-user-1; ObjectType: user; ObjectTable: users; Operation: Create Object; Uid: {69907560-5FF5-4120-A62D-D373654D2C15}; Administrator: tim; Machine: stingray; Subject: Object Manipulation; Operation Number: 0; product: SmartDashboard;
# 2Oct2007  9:52:55 accept sting      <    ObjectName: iso-test-user-1; ObjectType: user; ObjectTable: users; Operation: Delete Object; Uid: {69907560-5FF5-4120-A62D-D373654D2C15}; Administrator: tim; Machine: stingray; Subject: Object Manipulation; Operation Number: 3; product: SmartDashboard;
# 2Oct2007 11:07:26 accept sting      <    ObjectName: isotstusr2; ObjectType: user; ObjectTable: users; Operation: Create Object; Uid: {9ACAF401-9450-4B27-9B4C-D158FC7D2F10}; Administrator: tim; Machine: stingray; Subject: Object Manipulation; Operation Number: 0; product: SmartDashboard;
# 4Oct2007 10:47:44 accept sting      <    ObjectName: cactus; ObjectType: firewall_policy; ObjectTable: fw_policies; Operation: Modify Object; Uid: {4DFA246F-BCB1-4283-B3D6-685FBF8DB89B}; Administrator: tim; Machine: stingray; FieldsChanges: Rule 3 Service: removed 'a-250' ;; Subject: Object Manipulation; Operation Number: 1; product: SmartDashboard;
# 4Oct2007 15:35:07 accept fw1        <    Operation: Log In; Administrator: tufin1; Machine: localhost.localdomain; Subject: Administrator Login; Additional Info: Authentication method: Certificate; Operation Number: 10; product: CPMI Client;
# 4Oct2007 15:35:35 accept fw1        <    Operation: Log Out; Administrator: tufin1; Machine: localhost.localdomain; Subject: Administrator Login; Operation Number: 12; product: CPMI Client;
sub parse_audit_log {
	my $in_file                = shift;
	my $prev_import_time       = shift;
	my $process_whole_auditlog = shift;
	my $line;
	my ($db_date, $found_line, $found_basic_line, $date, $time, $action, $admin, $management, $obj_name, $obj_type, $obj_table, $operation, $uid);
	my $prev_import_date_minus_5_minutes = &add_delta_db_time($prev_import_time,-600);

	open( IN, $in_file ) || die "$in_file konnte nicht geoeffnet werden.\n";

	while (<IN>) {
		$line      = $_;       # Zeileninhalt merken
		$line =~ s/\x0D/\\r/g; # literal carriage return entfernen
		$line =~ s/\r\n/\n/g;  # handle cr,nl (d2u ohne Unterprogrammaufruf)
		chomp($line);
		undef $date; undef $time; undef $action; undef $admin, undef $management;
		undef $obj_name; undef $obj_type; undef $obj_table; undef $operation; undef $uid;
		$found_line = 0;
		$found_basic_line = 0;
				
	# Basisdaten sammeln (Datum, Zeit, ...)
		if ( $line =~ /\s*(\d+\w+\d+)\s+(\d+\:\d+\:\d+)\s+(\w+)\s+(.*?)\s+\<\s+/ ) {
			$date = $1; $time = $2; $action = $3; $management = $4; $found_basic_line = 1;
			$db_date = &convert_checkpoint_to_db_date("$date $time");
			print (" date from auditlog: $db_date; date from prev_import-5minues: $prev_import_date_minus_5_minutes");
		}
		if ($found_basic_line && ((defined($process_whole_auditlog) && $process_whole_auditlog) || $db_date gt $prev_import_date_minus_5_minutes)) {
		#	changelog eintrag mit uid
			print ("processing ...\n");
			if ( $found_basic_line && $line =~
				/\s+\<\s+ObjectName\:\s([\w\_\-\s]+)\;\s+ObjectType\:\s+([\w\_\-\s]+)\;\s+ObjectTable\:\s+([\w\_\-\s]+)\;\s+Operation\:\s+([\w\_\-\s]+)\;\s+Uid\:\s+\{([\w\_\-\s]+)\}\;\s+Administrator\:\s+([\w\_\-\s]+)\;/ ) {
				$obj_name = $1; $obj_type = $2; $obj_table = $3; $operation = $4; $uid = $5; $admin = $6; $found_line = 1;
			}
	
		#	changelog eintrag ohne uid
			if ( $found_basic_line && $line =~
				/\s+\<\s+ObjectName\:\s([\w\_\-\s]+)\;\s+ObjectType\:\s+([\w\_\-\s]+)\;\s+ObjectTable\:\s+([\w\_\-\s]+)\;\s+Operation\:\s+([\w\_\-\s]+)\;\s+Administrator\:\s+([\w\_\-\s]+)\;/ ) {
				$obj_name = $1; $obj_type = $2; $obj_table = $3; $operation = $4; $admin = $5; $found_line = 1;
			}
		#	user login/logout
			if ( $found_basic_line &&  $line =~ /\s+\<\s+Operation\:\s+(Log\s(In|Out))\;\s+Administrator\:\s+(.*?)\;/ ) {
				$operation = $1; $admin = $3; $found_line = 1;
			}
			if ($found_line) {
				$auditlog{"$audit_log_count.change_time"} = $db_date;
	
				if (defined($operation))	{
					if ( $operation =~ /Create Object/ ) { $operation = 'I'; }
					if ( $operation =~ /Modify Object/ ) { $operation = 'C'; }
					if ( $operation =~ /Delete Object/ ) { $operation = 'D'; }
					$auditlog{"$audit_log_count.change_action"} = $operation;
				}
				$auditlog{"$audit_log_count.management_name"} = $management;
				if (defined($admin))	{ $auditlog{"$audit_log_count.change_admin"} = $admin; }
				if (defined($uid))		{ $auditlog{"$audit_log_count.changed_object_uid"} = $uid; }
				if (defined($obj_name))	{ $auditlog{"$audit_log_count.changed_object_name"} = $obj_name; }
				if (defined($obj_type))	{
					if ( $obj_type =~ /\_protocol/ || $obj_type =~ /\_service/ ) {
						$obj_type = 'service';
					}
					if ( $obj_type =~ /network/ || $obj_type =~ /host\_plain/ || $obj_type =~ /address\_range/ ) {
						$obj_type = 'network_object';
					}
					if ( $obj_type =~ /user/ ) {
						$obj_type = 'user';
					}
					if ( $obj_type =~ /policies\_collection/ || $obj_type =~ /firewall_policy/ ) {
						$obj_type = 'rule';
					}				
					$auditlog{"$audit_log_count.changed_object_type"} = $obj_type;
				}
				$audit_log_count ++;
	
			# debugging:
#				print ("parse_audit_log found \@ $date $time for management $management: operation=$operation, admin=$admin");
				if ($operation !~ /^Log/ ) {
					print (", obj_type=$obj_type, obj_name=$obj_name");
					if (defined($uid)) { print (", uid=$uid"); }
				}
				print ("\n");				
			}
		}
	}
	close IN;	
}

#****************************
# Verarbeitung / Aufbereitung der identifizierten Parameter und Values (kontextsesitiv)
#****************************
# result_handler_obj
# obj_state_3
sub result_handler_obj {
	my $local_name;    # Variable als Zwischenspeicher in lokalen Schleifen
	my $found;         # flag, true falls Eintrag bekannt

	if ( $parse_obj_state >= 3 ) {

		# die Attribute von Anfuehrungszeichen befreien
		if ( defined($parse_obj_attr_value) ) {

			# Test auf Primary Management
			if (   ( $parse_obj_attr eq "primary_management" )
				&& ( $parse_obj_attr_value eq "true" ) )
			{
				$mgm_name = $parse_obj_name;    # setting mgm_name defined in CACTUS::FWORCH::import.pm
			}

			# Attribute in lowercase wandeln
			if ( $parse_obj_attr eq "type" ) {
				$parse_obj_attr_value = lc($parse_obj_attr_value);
			}
		}

		# objekte nach typen sortiert in arrays sammeln
	  SWITCH_OBJ_TYPE: {
			if (defined($parse_obj_name)) {
				# globals any obj
				if (   $parse_obj_type eq "globals"
					&& $parse_obj_name eq 'Any'
					&& $parse_obj_attr eq 'AdminInfo' )
				{
					@network_objects = ( @network_objects, $parse_obj_name );
					$network_objects{"$parse_obj_name.name"} = $parse_obj_name;
					$network_objects{"$parse_obj_name.UID"}  =
					  &gen_uid($parse_obj_attr_ext_value);
					$network_objects{"$parse_obj_name.ipaddr"}   = '0.0.0.0';
					$network_objects{"$parse_obj_name.comments"} =
					  '"implied any network object from objects.C global section"';
					$network_objects{"$parse_obj_name.type"}     = 'network';
					$network_objects{"$parse_obj_name.netmask"}  = '0.0.0.0';
					$network_objects{"$parse_obj_name.location"} = 'internal';
					@services = ( @services, $parse_obj_name );
					$services{"$parse_obj_name.name"} = $parse_obj_name;
					$services{"$parse_obj_name.UID"}  =
					  &gen_uid($parse_obj_attr_ext_value);
					$services{"$parse_obj_name.comments"} =
					  '"any service from objects.C global section"';
					$services{"$parse_obj_name.typ"}  = 'simple';
					$services{"$parse_obj_name.type"} = 'simple';
					last SWITCH_OBJ_TYPE;
				}
				# dynamic zone objects
				if ( $parse_obj_name eq 'Internet_Zone' || $parse_obj_name eq 'Trusted_Zone')
				{
					@network_objects = ( @network_objects, $parse_obj_name );
					$network_objects{"$parse_obj_name.name"} = $parse_obj_name;
					$network_objects{"$parse_obj_name.UID"}  =
					  &gen_uid($parse_obj_attr_ext_value);
					$network_objects{"$parse_obj_name.ipaddr"}   = '0.0.0.0';
					$network_objects{"$parse_obj_name.comments"} =
					  '"checkpoint implied zone object, filled dynamically"';
					$network_objects{"$parse_obj_name.type"}     = 'network';
					$network_objects{"$parse_obj_name.netmask"}  = '0.0.0.0';
					$network_objects{"$parse_obj_name.location"} = 'internal';
					last SWITCH_OBJ_TYPE;
				}
			}

			# netzobjekte
			if ( $parse_obj_type eq "network_objects" ) {

				# schon ein bekanntes network_object?
				if ( !defined( $network_objects{"$parse_obj_name.name"} ) ) {
					@network_objects = ( @network_objects, $parse_obj_name );
					$network_objects{"$parse_obj_name.name"} = $parse_obj_name;
				}

				# Daten im Hash ergaenzen
				if ( $parse_obj_state > 10 )
				{    # group member alle eingesammelt, abspeichern und zurueck
					if (   defined($parse_obj_groupmember)
						&& defined($parse_obj_groupmember_refs) )
					{

					   # group member alle eingesammelt, abspeichern und zurueck
						$network_objects{"$parse_obj_name.members"} =
						  $parse_obj_groupmember;
						$network_objects{"$parse_obj_name.member_refs"} =
						  $parse_obj_groupmember_refs;
						$network_objects{"$parse_obj_name.typ"} = 'group';
					}
					$parse_obj_state -= 10;
				}
				if ( defined($parse_obj_attr_value) ) {

					# Attribute aufarbeiten
				  SWITCH_OBJ_ATTR: {
						# start ipv6
						if ( $parse_obj_attr eq 'ipv6_prefix') {    # IPV6 netmask
							$parse_obj_attr_value = CACTUS::FWORCH::remove_space_at_end($parse_obj_attr_value);
							$network_objects{"$parse_obj_name.netmask"} = $parse_obj_attr_value;
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'ipv6_address') {    # IPV6 ip addr
							$parse_obj_attr_value = CACTUS::FWORCH::remove_space_at_end($parse_obj_attr_value);
							$network_objects{"$parse_obj_name.ipaddr"} = $parse_obj_attr_value;
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'ipv6_type') {    # IPV6 ip addr
							$parse_obj_attr_value = CACTUS::FWORCH::remove_space_at_end($parse_obj_attr_value);
							$network_objects{"$parse_obj_name.type"} = $parse_obj_attr_value;
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'type' && $parse_obj_attr_value eq 'ipv6_object') {    # ignore v6 type info
							last SWITCH_OBJ_ATTR;
						}
						# end ipv6
						if ( $parse_obj_attr eq 'ipaddr_first' || $parse_obj_attr eq 'bogus_ip') {    # R75 new feature zone objects without ip_addr
							$parse_obj_attr_value =
							  CACTUS::FWORCH::remove_space_at_end($parse_obj_attr_value);
							$network_objects{"$parse_obj_name.ipaddr"} = $parse_obj_attr_value;
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'AdminInfo' ) {
							$network_objects{"$parse_obj_name.UID"} =
							  &gen_uid($parse_obj_attr_ext_value);
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'By' ) {
							$network_objects{
								"$parse_obj_name.last_change_admin"} =
							  $parse_obj_attr_value;
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'Time' ) {
							$network_objects{"$parse_obj_name.last_change_time"}
							  = $parse_obj_attr_value;

#							print("debug: setting last_change_time for nwobject $parse_obj_name to $parse_obj_attr_value\n");
							last SWITCH_OBJ_ATTR;
						}
						$network_objects{"$parse_obj_name.$parse_obj_attr"} =
						  $parse_obj_attr_value;
					}
				}
				last SWITCH_OBJ_TYPE;
			}

			# services & resources
			if ( $parse_obj_type eq "services" || $parse_obj_type eq "resources") {

				# schon ein bekanntes network_object?
				if ( !defined( $services{"$parse_obj_name.name"} ) ) {
					@services = ( @services, $parse_obj_name );
					$services{"$parse_obj_name.name"} = $parse_obj_name;
				}

				# Daten im Hash ergaenzen
				if ( $parse_obj_state > 10 ) {
					if (   defined($parse_obj_groupmember)
						&& defined($parse_obj_groupmember_refs) )
					{

					   # group member alle eingesammelt, abspeichern und zurueck
						@services{"$parse_obj_name.members"} =
						  $parse_obj_groupmember;
						@services{"$parse_obj_name.member_refs"} =
						  $parse_obj_groupmember_refs;
						@services{"$parse_obj_name.typ"} = 'group';
					}
					$parse_obj_state -= 10;
				}
				if ( defined($parse_obj_attr_value) ) {

					# Attribute aufarbeiten
				  SWITCH_OBJ_ATTR: {
						if ( $parse_obj_attr eq 'AdminInfo' ) {
							$services{"$parse_obj_name.UID"} =
							  &gen_uid($parse_obj_attr_ext_value);
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'By' ) {
							$services{"$parse_obj_name.last_change_admin"} =
							  $parse_obj_attr_value;
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'Time' ) {
							$services{"$parse_obj_name.last_change_time"} =
							  $parse_obj_attr_value;

#							print("debug: setting last_change_time for service $parse_obj_name to $parse_obj_attr_value\n");
							last SWITCH_OBJ_ATTR;
						}
						if ( $parse_obj_attr eq 'port' ) {

							# >portnummer identifizieren
							if ( $parse_obj_attr_value =~ (/"\>(\d+)/) ) {
								$services{"$parse_obj_name.port"}      = $1 + 1;
								$services{"$parse_obj_name.port_last"} = 65535;
							}

							# <portnummer identifizieren
							elsif ( $parse_obj_attr_value =~ (/"\<(\d+)/) ) {
								$services{"$parse_obj_name.port"}      = 1;
								$services{"$parse_obj_name.port_last"} = $1 - 1;
							}

							# portnummer-portnummer identifizieren
							elsif ( $parse_obj_attr_value =~ (/(\d+)-(\d+)/) ) {
								$services{"$parse_obj_name.port"}      = $1;
								$services{"$parse_obj_name.port_last"} = $2;
							}
							else {
								$services{"$parse_obj_name.$parse_obj_attr"} =
								  $parse_obj_attr_value;
							}
							last SWITCH_OBJ_ATTR;
						}
						if ( ( $parse_obj_attr eq 'src_port' ) ) {

							# >portnummer identifizieren SRC
							if ( $parse_obj_attr_value =~ (/"\>(\d+)/) ) {
								$services{"$parse_obj_name.src_port"} = $1 + 1;
								$services{"$parse_obj_name.src_port_last"} =
								  65535;
							}

							# <portnummer identifizieren SRC
							elsif ( $parse_obj_attr_value =~ (/"\<(\d+)/) ) {
								$services{"$parse_obj_name.src_port"}      = 1;
								$services{"$parse_obj_name.src_port_last"} =
								  $1 - 1;
							}

							# portnummer-portnummer identifizieren SRC
							elsif ( $parse_obj_attr_value =~ (/(\d+)-(\d+)/) ) {
								$services{"$parse_obj_name.src_port"}      = $1;
								$services{"$parse_obj_name.src_port_last"} = $2;
							}
							elsif ( $parse_obj_attr_value =~ (/(\d+)/) ) {
								$services{"$parse_obj_name.$parse_obj_attr"} =
								  $parse_obj_attr_value;
							}
							last SWITCH_OBJ_ATTR;
						}
						if ( ( $parse_obj_attr eq 'timeout' ) ) {
							if ( $parse_obj_attr_value == 0 ) {
								$services{"$parse_obj_name.timeout_std"} = 'true';
							}
							else
							{
								$services{"$parse_obj_name.timeout_std"} =
								  'false';
								$services{"$parse_obj_name.$parse_obj_attr"} =
								  $parse_obj_attr_value;
							}
							last SWITCH_OBJ_ATTR;
						}

						# Sonderbehandlung fuer DCERPC
						if ( $parse_obj_attr eq 'uuid' && defined( $services{"$parse_obj_name.type"} ) &&  $services{"$parse_obj_name.type"} eq 'dcerpc' ) {
							$services{"$parse_obj_name.port"} = $1;
							last SWITCH_OBJ_ATTR;
						}

						if ( ( $parse_obj_attr eq 'type' ) ) {	# typ und ip_protokoll setzen
							# Sonderbehandlung fuer resources
							if ( $parse_obj_type eq "resources" && defined($parse_obj_attr_value) ) {
								$services{"$parse_obj_name.typ"}  = 'simple';
								$services{"$parse_obj_name.type"} = "resource::$parse_obj_attr_value";
								$services{"$parse_obj_name.ip_proto"} = 6;
								if ( $parse_obj_attr_value eq 'uri' ) {
									$services{"$parse_obj_name.port"}      = 80;
									$services{"$parse_obj_name.port_last"} = 80;
								}
								if ( $parse_obj_attr_value eq 'ftp' ) {
									$services{"$parse_obj_name.port"}      = 20;
									$services{"$parse_obj_name.port_last"} = 21;
								}
								last SWITCH_OBJ_ATTR;
							}
							$services{"$parse_obj_name.$parse_obj_attr"} = $parse_obj_attr_value;
									
							if ( ( $parse_obj_attr_value eq 'dcerpc' ) || ( $parse_obj_attr_value eq 'rpc' ) )
							{
								$services{"$parse_obj_name.typ"} = 'rpc';
								if ( defined($services{"$parse_obj_name.protocol"}) )
								{  # ip_proto kann ev. nicht gesetzt werden, da info bei rpc meist fehlt
									$services{"$parse_obj_name.ip_proto"} = $services{"$parse_obj_name.protocol"};
								}
							}
							if ( ( $parse_obj_attr_value eq 'icmpv6' ) ) {
								$services{"$parse_obj_name.typ"} = 'simple';
								$services{"$parse_obj_name.ip_proto"} = 1;
								$parse_obj_attr_value = 'icmp';
							}
							if ( ( $parse_obj_attr_value eq 'icmp' ) ) {
								$services{"$parse_obj_name.typ"} = 'simple';
								$services{"$parse_obj_name.ip_proto"} = 1;
							}
							if ( ( $parse_obj_attr_value eq 'igmp' ) ) {
								$services{"$parse_obj_name.typ"} = 'simple';
								$services{"$parse_obj_name.ip_proto"} = 2;
							}
							if (   ( $parse_obj_attr_value eq 'tcp' )
								|| ( $parse_obj_attr_value eq 'tcp_subservice' )
								|| ( $parse_obj_attr_value eq 'tcp_citrix' ) )
							{
								$services{"$parse_obj_name.typ"} = 'simple';
								$services{"$parse_obj_name.ip_proto"} = 6;
							}
							if ( ( $parse_obj_attr_value =~ /^gtp.*?/ ) ) {
								$services{"$parse_obj_name.typ"} = 'simple';
								$services{"$parse_obj_name.ip_proto"} = 6;
							}
							if ( ( $parse_obj_attr_value eq 'udp' ) ) {
								$services{"$parse_obj_name.typ"} = 'simple';
								$services{"$parse_obj_name.ip_proto"} = 17;
							}
							if ( ( $parse_obj_attr_value eq 'other' ) ) {
								$services{"$parse_obj_name.typ"} = 'simple';
								if (
									defined(
										$services{"$parse_obj_name.protocol"}
									)
								  )
								{
									$services{"$parse_obj_name.ip_proto"} =
									  $services{"$parse_obj_name.protocol"};
								}
							}
							if ( ( $parse_obj_attr_value eq 'group' ) ) {
								$services{"$parse_obj_name.typ"} = 'group';
							}
							last SWITCH_OBJ_ATTR;
						}

						# Als default das Attribut und den Wert sichern
						$services{"$parse_obj_name.$parse_obj_attr"} =
						  $parse_obj_attr_value;
					}
				}
			}
			last SWITCH_OBJ_TYPE;
		}

		# Setzten des alten Objektnamens
		$old_parse_obj_name = $parse_obj_name;
	}
	else {
		print("Warnung: result_handler_obj mit obj_state<3 aufgerufen\n");
	}
}

#----------------------------------------
# Funktion parse_cp2
# Parameter: in_file: config file # Parameter: rulesetname, um nicht alle rulesets zu parsen
# Resultat: keins
#----------------------------------------
sub parse_cp2 {
	my $in_file     = $_[0];
	my $mode        = $_[1];
	my $rulesetname = $_[2];
	my $header_FLAG = 0;       # globales Flag fuer Header
	my $last_line;
	my $prev_line;
	my $current_user;

	open( IN, $in_file ) || die "$in_file konnte nicht geoeffnet werden.\n";

#----------------------------------------
# zeilenweises (Ausnahmen bei $mode == 'rules') Einlesen der Konfigurationsdatei und
# Abstieg in die Struktur der objcts_5_0.c ( $mode == 'objects')
# (der Parserframe)
	while (<IN>) {
		$line      = $_;       # Zeileninhalt merken
		$last_line = $line;    # fuer end of line check
		$line =~ s/\x0D/\\r/g; # literal carriage return entfernen
		$line =~ s/\r\n/\n/g;  # handle cr,nl (d2u ohne Unterprogrammaufruf)
		chomp($line);
		$ln++;                 # Zeilennummer merken

	  SWITCH_parsemode: {

			# objects oder rules parsen?
			if ( $mode eq 'objects' ) {

				# group member collection
				if ( $parse_obj_state > 10 ) {

			   #kein neuer Parameter, d.h. ein Gruppenmitglied (Member) gefunden
			   # Pattern <=> Einzugtiefe, Doppelpunkt, 'Zeichenkette'
					if (/^\t{3}:(\w+)/)
					{ # Ende der Member-Definition, weiter aber Member noch nicht wegschreiben
						$parse_obj_state -= 10;
					}
					else {    # Member anfuegen
						 # Pattern <=> Einruecktiefe, Doppelpunkt, "Name", Leerzeichen, 'Namensstring'
						my $member_prefix = '';
						if (defined($group_with_exclusion_marker) &&
							($group_with_exclusion_marker eq 'base' || $group_with_exclusion_marker eq 'exception'))
						{
							$member_prefix = "{$group_with_exclusion_marker}";
							undef ($group_with_exclusion_marker);
						}							
						if (/^\t{4}:Name\s\(([\w\-\.\_]+)\)/) {
							if ( defined($parse_obj_groupmember) ) {
								$parse_obj_groupmember =
								  $parse_obj_groupmember . $GROUPSEP . $member_prefix . $1;
							}
							else {
								$parse_obj_groupmember = $member_prefix . $1;
							}
						}
						elsif (/^\t{4}:Uid\s\(\"\{([\w\-]+)\}\"\)/) {
							if ( defined($parse_obj_groupmember_refs) ) {
								$parse_obj_groupmember_refs =
								    $parse_obj_groupmember_refs
								  . $GROUPSEP
								  . &gen_uid($1);
							}
							else {
								$parse_obj_groupmember_refs = &gen_uid($1);
							}
						}
					}
				}

	  # globaler Aufbau der folgenden Abschnitte:
	  # ->	Suche nach einem Muster in einen Status => Status heraufsetzen
	  #	Pattern <=> Einzugtiefe, ':', ...weitere
	  # ->	optionale Bloecke
	  # ->	Suche nach einem Muster, das den Status beendet => Status herabsetzen
	  #	Pattern <=> Einzugtiefe, Klammerzu

		  # state 1 type ohne Config bzw. einzeilig, z.B. :serverobj (serverobj)
				if (/^\t:([\w\-\.\_]+)\s\(.*?\)$/) {
				}
				elsif (/^\t:([\w\-\.\_]+) */) {

					# anfang parse obj state 1
					# Pattern <=> Einzugtiefe, ':', obj_typ
					print_debug("up $parse_obj_state  => 1 at $ln\n");
					$parse_obj_state = 1;
					$parse_obj_type  = $1;
				}
				elsif (/^\t\)$/) {

					# Ende des state 1
					# Pattern <=> Einzugtiefe, Klammerzu
					$parse_obj_state = 0;
					undef($parse_obj_type);
				}

		  # parse obj state 2   - objektnamen setzen
		  # Pattern <=> Einzugtiefe, ':', 'Leerzeichen', obj_name, 'Leerzeichen'
				if ( (/^\t{2}:\s\(([\"\w\-\.\_]+)\s*/)
					&& (   ( $parse_obj_state == 2 )
						|| ( $parse_obj_state == 1 ) ) )
				{
					print_debug("up $parse_obj_state  => 2 at $ln\n");
					$parse_obj_state = 2;
					$parse_obj_name  = $1;
					undef($parse_obj_groupmember);       # Gruppe initialisieren
					undef($parse_obj_groupmember_refs);  # Gruppe initialisieren
				}

				# Ende des state 2
				# Pattern <=> Einzugtiefe, Klammerzu
				if ( (/^\t{2}\)/) ) {    # Ende der Objekt-Definition
					 # (alter status - neuer status) darf nicht groesser als eine Ebene sein, sonst ist die Struktur falsch interpretiert worden
					$parse_obj_state = 1;
					if ( defined($parse_obj_groupmember) )
					{    # wenn group-member gefunden, diese jetzt wegschreiben
						$parse_obj_state += 10;
						result_handler_obj();
					}
					undef($parse_obj_name);
				}

# parse obj state 3
# in diesem und den hoeheren Stati werden die Objekteigenschaften festgelegt.
# Pattern <=> Einzugtiefe, ':', parameter, 'Leerzeichen', 'Klammerauf', value-wenn definiert, 'Klammerzu',
				if ( (/^\t{3}:(\w+)\s\((.*)/)
					&& (   ( $parse_obj_state == 3 )
						|| ( $parse_obj_state == 2 ) ) )
				{
					print_debug("up $parse_obj_state  => 3 at $ln\n");
					$parse_obj_state = 3;
					$parse_obj_attr  =
					  &remove_space_at_end($1);    # TMP, remove space at end of attribute (eg. ip)
					if ( defined($2) ) {    # Klammer nicht leer
						    # value zwischenspeichern und untersuchen
						my $local_line = $2;
						print_debug("local_line: $local_line at $ln\n");    #TMP
						 # Ist der Wert in einer Zeile mit dem Parameter angegeben?
						 # Pattern <=> value, 'Klammerzu'
						if ( $local_line =~ (/^(.*)\)$/) ) {
							undef($parse_obj_attr_value);
							if ( defined($1) ) {
								$parse_obj_attr_value =
								  &remove_space_at_end($1)
								  ; # TMP, remove space at end of attribute (eg. ip)
							}

						 # derzeit nicht interpretierte Parameter ueberspringen
							unless ( ( $parse_obj_attr eq 'members_query' )
								|| ( $parse_obj_attr eq 'show_in_menus' )
								|| ( $parse_obj_attr eq 'reload_proof' )
								|| ( $parse_obj_attr eq 'etm_enabled' )
#								|| ( $parse_obj_attr eq 'track' )
#								|| ( $parse_obj_attr eq 'Wiznum' )
								|| ( $parse_obj_attr eq 'include_in_any' ) )
							{

								# den Rest im Result Handler aufbereiten
								print_debug("call result handler at $ln\n");
								result_handler_obj();
							}
							print_debug(
								" auto down $parse_obj_state  => 2 at $ln\n");

# Keine Gruppe/Liste geoeffnt, daher direkt den parse_obj_state auf den Level 2 zuruecksetzen
							$parse_obj_state = 2;
							$parse_obj_attr  = "";
						}
					}
				}

# Gruppenmitglieder zu service oder nwobject (wichtig, nur Eintraege ohne 'members_query'!!!)
# Setzt nur den Status um >10 --> Gruppe
# Pattern <=> Einzugtiefe, ':', 'Leerzeichen', 'Klammerauf', 'ReferenceObject', 'Klammerzu',
				if ( (/^\t{3}:\s\(ReferenceObject/) ) {
					print_debug("up $parse_obj_state  => 3 at $ln\n");
					$parse_obj_state = 3;

					# Gruppenstatus setzten
					$parse_obj_state += 10;
				}
# Behandlung von "group_with_exclusion" Objekten
				if ( (/^\t{3}:base\s\(ReferenceObject/) ) {
					$group_with_exclusion_marker = 'base';
					$parse_obj_state = 13;
				}
				if ( (/^\t{3}:exception\s\(ReferenceObject/) ) {
					$group_with_exclusion_marker = 'exception';
					$parse_obj_state = 13;
				}

				# Ende des state 3
				# Pattern <=> Einzugtiefe, Klammerzu
				if ( (/^\t{3}\)/) ) {

# (alter status - neuer status) darf nicht groesser als eine Ebene sein, sonst ist die Struktur falsch interpretiert worden
					$parse_obj_state = 2;
					$parse_obj_attr  = "";
				}

# parse obj state 4
# Pattern <=> Einzugtiefe, ':', parameter, 'Leerzeichen', 'Klammerauf', value-wenn definiert, 'Klammerzu',
				if (
					(/^\t{4}:(\w+)\s\((.*?)\)/)
					&& (   $parse_obj_state == 4
						|| $parse_obj_state == 3
						|| $parse_obj_state == 5 )
				  )
				{
					print_debug("up $parse_obj_state  => 4 at $ln\n");
					$parse_obj_state    = 4;
					$parse_obj_attr_ext = $1;
					if ( defined($2) ) {
						$parse_obj_attr_ext_value = $2;
						print_debug(
							" auto down $parse_obj_state  => 3 at $ln\n");
						if ( defined($parse_obj_attr_ext_value) ) {
							$parse_obj_attr_ext_value =
							  &remove_space_at_end(
								$parse_obj_attr_ext_value)
							  ; # TMP, remove space at end of attribute (eg. ip)
							 # Suche nach \t\t\t\t:chkpf_uid ("{9BD86F11-908B-4723-B7E9-31252A17B034}")
							if ( $parse_obj_attr_ext eq 'chkpf_uid' ) {

								# Aufbereiten der UID, Pattern "{ entfernen
								$parse_obj_attr_ext_value =~ s/\"\{//g;

								# Pattern }" entfernen
								$parse_obj_attr_ext_value =~ s/\}\"//g;
								$parse_obj_attr_ext_value =
								  $parse_obj_attr_ext_value;

								# Abspeichern der UID
								result_handler_obj();
								undef($parse_obj_attr_ext_value);
							}
						}
						$parse_obj_attr_ext = "";
					}
				}

				# Ende des state 4
				# Pattern <=> Einzugtiefe, Klammerzu
				if ( (/^\t{4}\)/) && ( $parse_obj_state < 10 ) ) {

# (alter status - neuer status) darf nicht groesser als eine Ebene sein, sonst ist die Struktur falsch interpretiert worden
					$parse_obj_state = 3;
					undef($parse_obj_attr_ext);
					undef($parse_obj_attr_ext_value);
				}

# parse obj state 5
# Pattern <=> Einzugtiefe, ':', parameter, 'Leerzeichen', 'Klammerauf', value-wenn definiert, 'Klammerzu',
				if (
					(/^\t{5}:(\w+)\s\((.*)/) && (
						   $parse_obj_state == 5
						|| $parse_obj_state == 4
					)
				  )
				{
					print_debug("up $parse_obj_state  => 5 at $ln\n");
					$parse_obj_state = 5;
					if ( defined($2) ) {
						my $local_line = $2;
						$parse_obj_attr = $1;
						if ( $parse_obj_attr eq 'By' ) {

					  # der attr_ext_value steht in einer Zeile mit dem attr_ext
					  # Pattern <=> 'beliebige Zeichen des values', Klammerzu
							if ( $local_line =~ (/^(.*)\)$/) ) {
								$parse_obj_attr_value = $1;

								# Abspeichern des last_change_admin
								result_handler_obj();
							}
						}
						if ( $parse_obj_attr eq 'Time' ) {
							my $timestamp;
							if ( $local_line =~ (/^\"(.*)\"\)$/) ) {
								$timestamp = convert_checkpoint_to_db_date($1);
								if ( defined($timestamp) ) {
									$parse_obj_attr_value = $timestamp;
									result_handler_obj();
									if ( !defined($last_change_time_of_config)
										|| $last_change_time_of_config
										lt $timestamp )
									{

					# das gefundene Aenderungsdatum ist neuer als das bisherige
										$last_change_time_of_config =
										  $timestamp;
									}
								}
							}
						}
					}

					#				$parse_obj_state = 4;
				}

				# Ende des state 5
				# Pattern <=> Einzugtiefe, Klammerzu
				if ( (/^\t{5}\)/) && ( $parse_obj_state < 10 ) ) {

# (alter status - neuer status) darf nicht groesser als eine Ebene sein, sonst ist die Struktur falsch interpretiert worden
					$parse_obj_state = 4;
					undef($parse_obj_attr_ext2);
					undef($parse_obj_attr_ext2_value);
				}

# parse obj state 6
# Pattern <=> Einzugtiefe, ':', parameter, 'Leerzeichen', 'Klammerauf', value-wenn definiert, 'Klammerzu',
				if ( (/^\t{6}:(\w+)\s\((.*)/)
					&& (   ( $parse_obj_state == 6 )
						|| ( $parse_obj_state == 5 ) ) )
				{
					print_debug("up $parse_obj_state  => 6 at $ln\n");
					$parse_obj_state     = 6;
					$parse_obj_attr_ext3 =
					  &remove_space_at_end($1)
					  ;    # TMP, remove space at end of attribute (eg. ip)
					if ( defined($2) ) {
						my $local_line = $2;

					  # der attr_ext_value steht in einer Zeile mit dem attr_ext
					  # Pattern <=> 'beliebe Zeichen des values', Klammerzu
						if ( $local_line =~ (/^(.*)\)$/) ) {
							print_debug(
								" auto down $parse_obj_state  => 6 at $ln\n");
							if ( defined($1) ) {
								$parse_obj_attr_ext3_value =
								  &remove_space_at_end($1);    # TMP, remove space at end of attribute
							}

							#// insert parsresult handler call    //#
							#// wenn weitere Auswertung notwendig //#
							$parse_obj_state = 5;
							undef($parse_obj_attr_ext3);
							undef($parse_obj_attr_ext3_value);

						 # komplexes Attribut, ggf. im Resulthandler auszuwerten
						}
					}
				}

				# Ende des state 6
				# Pattern <=> Einzugtiefe, Klammerzu
				if ( (/^\t{6}\)/) && ( $parse_obj_state < 10 ) ) {

# (alter status - neuer status) darf nicht groesser als eine Ebene sein, sonst ist die Struktur falsch interpretiert worden
#				print_debug("down $parse_obj_state  => 5 at $ln\n") || (($parse_obj_state - 4) >1);
					$parse_obj_state = 5;
					undef($parse_obj_attr_ext3);
					undef($parse_obj_attr_ext3_value);
				}

# parse obj state 7
# Pattern <=> Einzugtiefe, ':', parameter, 'Leerzeichen', 'Klammerauf', value-wenn definiert, 'Klammerzu',
				if ( (/^\t{7}:(\w+)\s\((.*)/)
					&& (   ( $parse_obj_state == 7 )
						|| ( $parse_obj_state == 6 ) ) )
				{
					print_debug("up $parse_obj_state  => 7 at $ln\n");
					$parse_obj_state     = 7;
					$parse_obj_attr_ext4 =
					  &remove_space_at_end($1)
					  ;    # TMP, remove space at end of attribute
					if ( defined($2) ) {
						my $local_line = $2;
						if ( $local_line =~ (/^(.*)\)$/) ) {
							print_debug(
								" auto down $parse_obj_state  => 7 at $ln\n");
							if ( defined($1) ) {
								$parse_obj_attr_ext4_value =
								  &remove_space_at_end($1)
								  ;    # TMP, remove space at end of attribute
							}

							#// insert parsresult handler call    //#
							#// wenn weitere Auswertung notwendig //#
							$parse_obj_state = 6;
							undef($parse_obj_attr_ext4);
							undef($parse_obj_attr_ext4_value);

						 # komplexes Attribut, ggf. im Resulthandler auszuwerten
						 # $parse_obj_attr = "";
						}
					}
				}

				# Pattern <=> Einzugtiefe, Klammerzu
				if ( (/^\t{7}\)/) && ( $parse_obj_state < 10 ) ) {

# (alter status - neuer status) darf nicht groesser als eine Ebene sein, sonst ist die Struktur falsch interpretiert worden
#				print_debug("down $parse_obj_state  => 6 at $ln\n") || (($parse_obj_state - 6) >1);
					$parse_obj_state = 6;
					undef($parse_obj_attr_ext4);
					undef($parse_obj_attr_ext4_value);
				}

				#// insert parsresult handler call    //#
				#// wenn weitere Auswertung notwendig //#
				last SWITCH_parsemode;
			}

			# Rules parsen
			if ( $mode eq 'rules' ) {

				# rule member collection
				# sammelt mitglieder einer Gruppe (entspricht hier einer Spalte)
				if ( $parserule_state > 10 ) {

					#kein neuer Parameter - keine schliessende Klammer
					# Pattern <=> Einzugtiefe, Klammerzu
					unless (/^\t\t\t\)/) {

# innerhalb der Gruppe Parameter suchen
# Pattern <=> Einzugtiefe, ':',Eigenschaft der Gruppe, Leerzeichen Klammerauf, value-wenn definiert, Klammerzu
						if (/^\t{4}:(op)\s\((.*)\)/) {
							$parserule_ruleparameter_ext = $1;
							if ( defined($2) ) {
								if ( $2 eq '"not in"' ) {
									$parserule_ruleparameter_ext_value = "true";
								}
								else {
									$parserule_ruleparameter_ext_value = "false";
								}
								$rulebases{"$parserule_rulebasename.$parserule_rulenum.$parserule_ruleparameter.$parserule_ruleparameter_ext"} = $parserule_ruleparameter_ext_value;
							}
						}

# compound gruppenmitglieder, die Gruppe faengt hier an
# Pattern <=> Einzugtiefe, Doppelpunkt, "compound", Leerzeichen, Klammerauf, 'zeichenkette'
						if (/^\t{4}:(compound)\s\((.*)/) {

							# kein Wert angegeben
							# Pattern <=> Klammerzu
							unless ( $2 =~ m/\)$/ ) {
								$parserule_state = 16;
							}

						# Werte werden zwischengespeichert, aber nicht verwendet
							$parserule_ruleparameter_ext       = $1;
							$parserule_ruleparameter_ext_value = $2;
						}

# einfaches gruppenmitglied faengt hier an
# Pattern <=> Einzugtiefe, ':', Leerzeichen, Klammerauf, "ReferenceObject", Klammerzu
						if (/^\t{4}:\s\(ReferenceObject/) {
							$parserule_state = 15;
						}

						# die eigentliche Gruppenliste hoert auf
						# Pattern <=> Einzugtiefe, Klammerzu
						elsif (/^\t{4}\)/) {
							$parserule_state = 14;
						}

# einfaches Member anfuegen
# Pattern <=> Einzugtiefe, Doppelpunkt, "Name", Leezeichen, 'objektname', Klammerzu
# bei Name-Pattern auch Leerzeichen und optionale Hochkommata-Klammerung hinzugefuegt (fuer UserDefined 3)
						if (   (/^\t{5}:Table\s\(([\w\-\.\_\s]+)\)/)
							&& ( $parserule_state == 15 ) )
						{
							if ($1 =~ /^identity\_roles$/) {
								# now not adding source network_objects but identity awareness user groups
								$parse_obj_type = 'identity_roles';
								if ($prev_line =~ /^\t{5}:Name\s\(\"?([\w\-\.\_\s]+)\"?\)/) {
									$parserule_ruleuser = $1;
								} else {
									print ("id:ERROR: found identity_roles Usergroup but no name in previous line=$prev_line\n");
								}
							}
						}
						elsif (   (/^\t{5}:Name\s\(\"?([\w\-\.\_\s]+)\"?\)/)
							&& ( $parserule_state == 15 ) )
						{
							if ( defined($parserule_groupmember) ) {
								$parserule_groupmember =
								  $parserule_groupmember . $GROUPSEP . $1;
							}
							else {
								$parserule_groupmember = $1;
							}
						}
						elsif ((/^\t{5}:Uid\s\(\"\{([\w\-\.\_\s]+)\}\"\)/)
							&& ( $parserule_state == 15 ) )
						{
####### start identity_roles
							if (defined($parse_obj_type) && $parse_obj_type eq 'identity_roles') {
								# set identity role as user group @ any
								# assuming always any source nw obj for identity rule objects
								# might additionally parse identity_roles.C for specific_identity_entities (both locations and machines)
=cut
        :locations (
                :restrict_to (Specific)
					:specific_identity_entities (
                        : (ReferenceObject
                                :Uid ("{BF1C6029-8D5D-4A05-A3AD-4B67E6E7A7AA}")
                                :Name (Cactus-100)
                                :Table (network_objects)
                        )
                        : (ReferenceObject
                                :Uid ("{ABBA18CB-625D-4231-BF5A-6DBF646A155C}")
                                :Name (CACTUS-WLAN-Clients_client_auth)
                                :Table (network_objects)
                        )
                	)
		)
         :machines (
                :type (machines_restriction)
                :restrict_to (Specific)
                :specific_identity_entities (
                        : (ReferenceObject
                                :Uid ("{7D67B970-ABDE-4A97-8CEB-0EE8F6703750}")
                                :Name (ad_machine_Calamus)
                                :Table (ad_machines)
                        )
                        : (ReferenceObject
                                :Uid ("{DDF26071-6C14-4DC1-8040-605C9042321E}")
                                :Name (ad_machine_TICK)
                                :Table (ad_machines)
                        )
                )
        )
=cut
								if ( defined($parserule_groupmember_refs) ) {
									$parserule_groupmember_refs = $parserule_groupmember_refs . $GROUPSEP . $parserule_ruleuser . "@" .
										&gen_uid($network_objects{"Any.UID"});
									$parserule_groupmember = $parserule_groupmember . $GROUPSEP . $parserule_ruleuser . '@Any';  
								}
								else {
									$parserule_groupmember = $parserule_ruleuser . '@Any';  # assuming always any source nw obj for identity rule objects
									$parserule_groupmember_refs = "$parserule_ruleuser" . "@" . &gen_uid($network_objects{"Any.UID"});
								}
#								print ("id:found id_roles Usergroup User::$parserule_ruleuser, parserule_groupmember_refs: $parserule_groupmember_refs\n");
####### end identity_roles
							} else {
								if ( defined($parserule_groupmember_refs) ) {
									$parserule_groupmember_refs =
									    $parserule_groupmember_refs
									  . $GROUPSEP
									  . &gen_uid($1);
								}
								else {
									$parserule_groupmember_refs = &gen_uid($1);
								}
							}
						}

		   # compound Member anfuegen
		   # Pattern <=> Einzugtiefe, Doppelpunkt, Klammerauf, 'Gruppenmitglied'
						if (   (/^\t{5}:\s\((.*)/)
							&& ( $parserule_state == 16 ) )
						{
							my $compound_name = $1;
							if ( $compound_name =~
								/\w+\-\>\"?([\w\-\.\_\s]+)\"?/ )
							{	# strip of resource-suffix (eg. http->)
								# INFO: check point resources koennen in CP NGX und davor immer nur einzeln in Regeln auftauchen
								$compound_name = $1;
								$parserule_state = 18;   # 18 = resource
							}
							if ( defined($parserule_groupmember) ) {
								$parserule_groupmember =
								    $parserule_groupmember
								  . $GROUPSEP
								  . $compound_name;
							}
							else {
								$parserule_groupmember = $compound_name;
							}

			# Userinfo pro Regel ausfiltern
			# user(-gruppen) stehen vor dem @ zeichen, gefolgt vom netz(-objekt)
							if ( $compound_name =~ m/(.*)\@(.*)/ ) {
								$current_user = $1;
								$current_user =~ s/"//g;
								if ( defined($parserule_ruleuser) ) {
									$parserule_ruleuser =
									    $parserule_ruleuser
									  . $GROUPSEP
									  . $current_user;
								}
								else {
									$parserule_ruleuser = $current_user;
								}
							}
						}

						# resource 
						if (   (/^\t{6}:resource\s\(ReferenceObject$/) && ( $parserule_state == 18 ) )
						{
							$parserule_state = 19;			# 19 = uid einer Resource
						}

						if ( (/^\t{7}:Uid\s\(\"\{(.*?)\}\"\)$/) && ( $parserule_state == 19 ) )
						{
 							$parserule_groupmember_refs = $1;
#							$rulebases{"$parserule_rulebasename.$parserule_rulenum.services.refs"} = $1;
#							$parserule_state = 14;	
						}
						# end resource

						if (   (/^\t{6}:at\s\(ReferenceObject$/) && ( $parserule_state == 16 ) )
						{
							$parserule_state = 17;
						}

						# die Referenz auf das nwobjekt hinzufuegen
						if (   (/^\t{7}:Uid\s\(\"\{(.*)\}\"\)$/) && ( $parserule_state == 17 ) )
						{
							my $user_string = '';
							my $nwobj_uid   = $1;

							if ( defined($current_user) ) {
								$user_string = "$current_user@";
								undef($current_user);
							}
							if ( defined($parserule_groupmember_refs) ) {
								$parserule_groupmember_refs = "$user_string" . &gen_uid($nwobj_uid) . "$GROUPSEP$parserule_groupmember_refs";
							}
							else {
								$parserule_groupmember_refs = "$user_string" . &gen_uid($nwobj_uid);
							}
							$parserule_state = 16;
						}

						# alle Member gefunden, daher wegschreiben
					}
					else {
						if ( defined($parserule_groupmember) ) {
							$rulebases{"$parserule_rulebasename.$parserule_rulenum.$parserule_ruleparameter"} = $parserule_groupmember;
							$rulebases{"$parserule_rulebasename.$parserule_rulenum.$parserule_ruleparameter.refs"} = $parserule_groupmember_refs;
						}

						$parserule_state -= 10;
						undef($parse_obj_type);
						undef($parserule_groupmember);
						undef($parserule_groupmember_refs);
						undef($parserule_ruleuser);
					}
				}

				# parserule state 1 - Regelwerk faengt an
				# Pattern <=> Einzugtiefe, ':rule-base'
				if (/^\t:rule-base/) {
					$parserule_in_rule_base = 1;
					$parserule_rulenum      = -1;
					$parserule_state        = 1;

# den Namen identifizieren
# Pattern <=> beliebiger Anfang, Anfuehrungszeichen, Doppelkreuz, Doppelkreuz, 'name des Regelwerks', Anfuehrungszeichen
					if (/.*\"##(.*)\"/) {
						$parserule_rulebasename = $1;
						if ( defined($rulesetname) && ruleset_does_not_fit($parserule_rulebasename, $rulesetname) )
						{ # Nicht gesuchten Regelsatz uebergehen, $rulesetname = hashref
						  IGNORE_RULESET: while (<IN>) {
								$last_line = $_;
								if ( $last_line =~ /^\t\)/ ) {
									last IGNORE_RULESET;
								}
							}
						}
						else {
							print_debug("Regelwerk $1 wird geparst\n");
							unless (defined($rulebases{"$parserule_rulebasename.name"}))
							{
								@rulebases =  ( @rulebases, $parserule_rulebasename );
								$rulebases{"$parserule_rulebasename.name"} = $parserule_rulebasename;
								$rulebases{"$parserule_rulebasename.rulecount"}  = 0;
							}

		   # LastChangeAdmin und LastSaveTime fuer das Regelwerk identifizieren
						  IGNORE_ADM_INFO: while ( $line = <IN> )
							{    # move to end of AdminInfo
								$last_line = $_;
								if ( $line =~ /^\t\t\)/ ) {
									last IGNORE_ADM_INFO;
								}
								if ( $line =~ /^\t\t\t\t:By\s\((.+?)\)/ ) {
									$rulebases{"$parserule_rulebasename.last_change_admin"} = $1;
								}
								if ( $line =~ /^\t\t\t\t:Time\s\(\"(.+?)\"\)/ )
								{
									my $timestamp = convert_checkpoint_to_db_date($1);
									if ( defined($timestamp) ) {
										$rulebases{"$parserule_rulebasename.last_save_time"} = $timestamp;
										if (!defined($last_change_time_of_config) || $last_change_time_of_config lt $timestamp)
										{	# das gefundene Aenderungsdatum ist neuer als das bisherige
											$last_change_time_of_config = $timestamp;
										}
									}
								}
							}
						}
					}
				}

				# parserule state 2 - Access Regel faengt an
				# Pattern <=> Einzugtiefe, ':rule '
				if (/^\t{2}:rule\s/) {
					$parserule_in_rule = 1;
					$parserule_rulenum++;
					print_debug(
						"switching form state $parserule_state to 2.\n");
					$parserule_state = 2;
					$rulebases{"$parserule_rulebasename.rulecount"} =
					  $parserule_rulenum + 1;

# setzen des last_change_admin (bei CP leider global fuer das gesamte Regelwerk)
					$rulebases{"$parserule_rulebasename.$parserule_rulenum.last_change_admin"} =
					  	$rulebases{"$parserule_rulebasename.last_change_admin"};
					$ruleorder[$parserule_rulenum] =  $parserule_rulenum;    # Regeln sind einfach von 1-n durchnummeriert
					       # neu: Wegschreiben der Regelreihenfolge
					$rulebases{"$parserule_rulebasename.ruleorder"} =
					  join( ',', @ruleorder );
					print_debug(
						"rule: $parserule_in_rule\t$parserule_rulenum\n");
				}
				if (/^\t{2}:rule_adtr\s/)
				{ # eingefuegt, um keine comments von AdrTrans-Regeln hineinzubekommen
					print_debug("ignoring address translation rule\n");
				  IGNORE_ADDR_TRANS: while (<IN>) {
						$last_line = $_;
						if ( $last_line =~ /^\t\t\)/ ) {
							last IGNORE_ADDR_TRANS;
						}
					}    # lese Zeilen bis zur Ende-Klammerung der rule_adtr
				}

				# parserule state 3 - Regeldetails fangen an
				# Pattern <=> Einzugtiefe, ':', beliebiges dahinter
				if ( ( /^\t{3}:(.*)/ && $parserule_state > 1 ) ) {
					$parserule_state = 3;
				}

				# Behandlung der Regeldetails
				if ( $parserule_state >= 3 && $parserule_state < 10 ) {

					#Einzelfaelle behandeln
				  switch_rulepart: {

# value in der gleichen Zeile
# Pattern <=> Einzugtiefe, ':', einer der parameter, Leerzeichen,  Klammerauf, Wert-wenn definiert, Klammerzu
						if (/^\t\t\t:(name|disabled|global_location|comments|header_text)\s\((.*)\)/)
						{
							$parserule_state         = 4;
							$parserule_ruleparameter = $1;
							if ( defined($2) ) {
								my $parserule_ruleparameter_value = $2;
								$rulebases{"$parserule_rulebasename.$parserule_rulenum.$parserule_ruleparameter"} = $parserule_ruleparameter_value;
								if (( $parserule_ruleparameter eq 'comments' ) && ( $parserule_ruleparameter_value =~ m/\@A(\d+)\@/ ) )
								{	# customer specific customizing
									$rulebases{"$parserule_rulebasename.$parserule_rulenum.rule_id"} = $1;
								}
							}
							last switch_rulepart;
						}

				# Value eine Zeile tiefer
				# Pattern <=> Einzugtiefe, ':', action, Leerzeichen,  Klammerauf
						if (/^\t{3}:(action)\s\(/) {
							$parserule_state         = 4;
							$parserule_ruleparameter = $1;

							#neue Zeile einlesen
							$_         = <IN>;
							$line      = $_;
							$last_line = $line;
							$ln++;
							s/\r\n/\n/g
							  ;    # handle cr,nl (d2u ohne Unterprogrammaufruf)
							   # Pattern <=> bel. Leerzeichen, Klammerauf, value
							/\s*:\s\((.*)/;

							if ( defined($1) ) {
								my $parserule_ruleparameter_value = $1;
								$rulebases{"$parserule_rulebasename.$parserule_rulenum.$parserule_ruleparameter"} = $parserule_ruleparameter_value;
							}
							last switch_rulepart;
						}

	 # Value ist eine Auflistung
	 # Pattern <=> Einzugtiefe, ':', eine der Aktionen, Leerzeichen,  Klammerauf
						if (/^\t{3}:(src|dst|services|install|track|time|through)\s\(/)
						{
							$parserule_state         = 14;
							$parserule_ruleparameter = $1;
							last switch_rulepart;
						}

		   # Value ist Rule AdminInfo
		   # Pattern <=> Einzugtiefe, ':', "AdminInfo", Leerzeichen,  Klammerauf
						if (/^\t{3}:(AdminInfo)\s\(/) {
							# Zeilen lesen, bis UID gefunden
							while (<IN>) {
								$line = $_;
								$ln++;
								$last_line = $line;
								s/\r\n/\n/g
								  ; # handle cr,nl (d2u ohne Unterprogrammaufruf)
								 # Pattern <=> Einzugtiefe, ':', "chkpf_uid", Leerzeichen, Klammer auf, Doppeltes Hochkomma, geschw. Klammer auf, 'UID', geschw. Klammer zu, Doppeltes Hochkomma
								if (/^\t\t\t\t:chkpf_uid\s\(\"\{(.*)\}\"\)/) {
									my $rule_uid = &gen_uid($1);

# folgenden Praefix eingefuegt wg. Bug bei CP (zwei Regeln eines Mgmts mit derselben UID)
									$rulebases{"$parserule_rulebasename.$parserule_rulenum.UID"} = $parserule_rulebasename . '__uid__' . $rule_uid;
									last;
								}
								if (/^\t{4}:ClassName\s\(security_header_rule\)/)
								{
									# ignore this rule except for :header_text
									$header_FLAG = 1;
								}
							}
						}
						last switch_rulepart;
					}
				}
				last SWITCH_parsemode;
			}
		}
		$prev_line = $line;		
	}

	# Interpretation des Inputs ist abgeschlossen,
	# Aufraeumen dateihandle etc.
	#----------------------------------------
	#print "\t\t ... parsing abgeschlossen\n\n";
	close(IN);
	print_debug("$in_file closed.\n");

	# check auf Vollstaendigkeit des Config-Files:
	if ( $last_line =~ m/^\)(\n)?$/ ) { return 0; }
	else {
		return "last line of config-file $in_file not correct: <$last_line>";
	}
}

####################################
# cp_parse_main
# Main-Routine zum Parsen von CP-Configs
# Ausgabe: CSV-Dateien im output-directory
# param1: input-filename
# param2: output-directory
# param3: verbose_flag (0/1)
####################################
sub cp_parse_main {
	my $in_file     = $_[0];
	my $out_dir     = $_[1];
	my $rulesetname = $_[2];
	my $verbose     = $_[3];
	my $mode;

# Setzen des Parsermodes
# umschalten des Betriebsmodus anhand der Dateiextension des uebergebenen Konfigurationsfiles
	if ( $in_file =~ /\.fws/ ) {    # eine *.fws datei => Regelwerke
		$mode = 'rules';	
	}
	elsif ( $in_file =~ /\_5_0.C/ ) {    # eine _5_0.C Datei => Objekte
		$mode = 'objects';
	}
	else {                               # etwas unbekanntes
		die "unknown File extension in $in_file - exiting!\n";
	}

	#	print_verbose ("using mode $mode\n");
	print "parsing config file: $in_file\n";

	#	print ("in_file: $in_file, mode: $mode, rulsetname: $rulesetname\n");
	my $result = parse_cp2( $in_file, $mode, $rulesetname );

	if ($mode eq 'objects') {
		&cp_resolve_groups_with_exception();
	}
	
#	&print_results_monitor($mode);
	return $result;
}

sub cp_parse_users_main {
	my $user_db_file = shift;
	my $output_dir = shift;
	my $verbose = shift;
	my $result = 0;
	my $line   = '';
	
	# first check if it is the original DB format or an exported version of the group and user data (using fw dbexport)
	if ( -T $user_db_file ) { # -T: is the file a text only file without binary chars?
		my $INFILE = new IO::File("< $user_db_file") or die "cannot open file $user_db_file\n";
		$line = <$INFILE>; $INFILE->close;
		if (defined($line)) {
			if ( $line =~ /^name;\tgroups;\tcolor;\tcomments;\tis_administrator_group;/ ) {		# start of user group definitions dbexport file
				print "parsing users from csv dbexport: $user_db_file\n";
				&cp_parse_users_csv_db_exported($user_db_file, $output_dir, $verbose);
			}
			if ($line  =~ /\<\?xml/) {
				print "parsing users from xml: $user_db_file\n";
				&cp_parse_users_xml_exported($user_db_file, $output_dir, $verbose);
			}
		}
	} else {	# binary/original user file 
		print "NOTICE: currently not parsing Check Point users from binary config file $user_db_file\n";
		# $result = &cp_parse_users_original_format( $user_db_file, $output_dir, $verbose );
	}
	&cp_parse_users_add_special_all_users_group();
	return $result;
}

sub cp_parse_users_add_special_all_users_group {
	my $name = 'All Users'; 	# adding special predefined user group "All Users" if it does not exist yet
	if (!defined($user{"$name.type"})) {
		push @user_ar, $name;
		$user{"$name.type"} = 'group';
		$user{"$name.uid"} = $name;
		$user{"$name.comments"} = 'special Check Point predefined usergroup containing all users';
	}	
}

sub cp_parse_users_add_member_to_group {
	my $gruppe		= shift;
	my $member		= shift;
	my $debug		= shift;
	my $oldmembers	= "";
	my @group_ar	= ();

	if ( defined( $usergroup{"$gruppe"} ) ) {
		$oldmembers = $usergroup{"$gruppe"} . $usergroupdelimiter;
		@group_ar   = split /[$usergroupdelimiter]/, $oldmembers;
	}
	if ($debug) { print ("          cp_parse_users_add_member_to_group:: found member $member for group $gruppe; members so far: $oldmembers\n"); }
	if ( !grep( /^${member}$/, @group_ar ) && $member ne $gruppe )
	{    # doppelte Member und Rekursion vermeiden
		$usergroup{"$gruppe"} = $oldmembers . $member;
	}
	return;
}

sub cp_parse_users_add_groupmembers_final {
	# bei CP sind Namen und UIDs der User identisch, da uid (derzeit) nicht aus fwauth.NDB ausgelesen werden kann
	foreach my $gruppe ( keys %usergroup ) {
		$user{"$gruppe.member_refs"} = $usergroup{"$gruppe"};
		$user{"$gruppe.members"}     = $usergroup{"$gruppe"};
		$user{"$gruppe.type"}        = "group";
		if ( !grep( /^${gruppe}$/, @user_ar ) ) {
			push @user_ar, $gruppe;
		}
	}
	return;
}

sub cp_parse_users_xml_exported {

=cut	
syntax start: <?xml xxx ?> xxx <users>
userdefinition:
<user>
<Name>xxx</Name>
<Class_Name>user|user_group|external_group</Class_name>
user_group: <members><reference>  <Name>user-in-group</Name>    <Table>users</Table></reference></members>
<groups><groups><Name>xxx</Name></groups></groups>
<expiration_date><![CDATA[31-dec-2005]]></expiration_date>
</user>
ende: </users>
=cut	

	my $in_file_main    = shift;
	my $fworch_workdir     = shift;
	my $debuglevel_main = shift;
	my $line            = '';
	my $last_line       = '';
	my $parse_error     = 0;
	my $line_no       = 0;
	my $INFILE = new IO::File("< $in_file_main") or die "cannot open file $in_file_main\n";
	my $debug_lines = '';
	my $debug = 0;
	my $start = 1;
	my ($name, $type, $color, $comments, $is_admin, $uid, $expdate, $group_string, $rest_of_line);

	LINE: while ( $line = <$INFILE> ) {
		$last_line = $line;			
		$line_no++;
		if ($start && $line !~ /\<users\>/) { next LINE; } else { $start = 0; }
		if (!$start && $line =~ /\<user\>/) {
			if ( $line =~ /\<user\>\<Name\>(.+?)\<\/Name\>\<Class\_Name\>(.+?)\<\/Class\_Name\>(.+)$/ ) {		# start of user definition 
				$name = $1; $type = $2; $uid = $name; $rest_of_line = $3;
			} else {
				print ("debug: no match in line: $line\n");
			}
		}
		if ($line =~ /\<groups\>\s*(.*?)\s*<\/groups\>\s*\<ldap\_filter\>/ ) {		# group membership definition
			my $group_string = $1;
			while ($group_string ne '') {
#				print ("DEBUG: group_string=$group_string\n");
				if ($group_string =~ /^\<groups\>\s*\<Name\>\s*(.+?)\s*\<\/Name\>\s*\<Table\>\s*users\s*\<\/Table\>\s*\<\/groups\>(.*)/ ) {		# group membership definition
					my $group = $1;
					$group_string = $2;
					&cp_parse_users_add_member_to_group ($group, $name, 0);
#					print ("DEBUG: found group for user $name=$group\n");
				} else {
#					print ("DEBUG warning: found no match in group_string $group_string - would result in endless looping\n");
					$group_string = '';
				}
			} 
		}
		if ($line =~ /\<\/user\>/) {  # end of user definition 
			if ($type eq 'external_group' || $type eq 'user_group') { $type = 'group'; }
			if ($type eq 'user') { $type = 'simple'; }
			if ($type eq'simple' || $type eq 'group') {
				push @user_ar, $name;
				$user{"$name.type"} = $type;
				$user{"$name.uid"} = $uid;
				if ( defined($color) ) { $user{"$name.color"} = $color; }
				if ( defined($comments) ) { $user{"$name.comments"} = $comments; }
				if ( defined($expdate) ) { $user{"$name.expdate"} = $expdate; }
			}
			undef($name); undef($type); undef($color); undef($comments); undef($is_admin); undef($uid); undef($expdate); undef($group_string);
		}
	}
	$INFILE->close;
	&cp_parse_users_add_groupmembers_final;
	# check auf Vollstaendigkeit des Config-Files:
	if ( $last_line =~ /\<\/users\>/  && !$parse_error) { return 0; }
	else {
		print ("error while parsing exported userdata, last_line: <$last_line>");
		return "error while parsing exported userdata, last_line: <$last_line>";
	}	
}

sub cp_parse_users_csv_db_exported {

=cut	

limitations:

fw dbexport of user data does not export IDs (UIDs) so the UID field will be filled with the user name

syntax groups:

name;	groups;	color;	comments;	is_administrator_group;
group1;	;	red;	;	;
group2;	;	red;	;	;
group3;	;	red;	Kommentar3;	;
group4;	;	orange;	Kommentar4;	;


syntax users:

name;	color;	groups;	destinations;	sources;	auth_method;	fromhour;	tohour;	expiration_date;	days;	accept_track;	internal_password;	SKEY_seed;	SKEY_number;	SKEY_passwd;	SKEY_gateway;	comments;	radius_server;	vlan_auth_list;	userc;	expire;	isakmp.transform;	isakmp.data.integrity;	isakmp.encryption;	isakmp.methods;	isakmp.encmethods;	isakmp.hashmethods;	isakmp.authmethods;	isakmp.shared.secret;	tacacs_server;	SKEY_method;	administrator;	administrator_profile;
user1;	red;	{group1,group4};	{Any};	{Any};  Internal Password;	00:00;  23:59;  20-Sep-1999;    {MON,TUE,WED,THU,FRI,SAT,SUN};  None;   5bV0ZOoq921Bs;  ;       ;       ;       ;       Kommentar1;        Any;    {};     {DES,DES,MD5};  60;     ;       ;       ;       ;       ;       ;       ;       ;       Any;    ;       false;  ;
user2;	orange;	{group2,group3,group1};	{Any};  {Any};  Internal Password;      00:00;  23:59;  15-feb-2001;    {MON,TUE,WED,THU,FRI,SAT,SUN};  AuthAlert;      nb.NZXrB2qyxA;  ;       ;       ;       ;       Testuser;       Any;    {};     {,,None};       ;       ESP;    SHA1;   3DES;   ;       {DES,3DES};     {MD5,SHA1};     {signatures};   ;       Any;    ;       false;  ;
user3;	orange;	{group2};	{Any};  {Any};  Internal Password;      08:00;  18:00;  27-Sep-1999;    {MON,TUE,WED,THU,FRI,SAT,SUN};  None;   7b.SEuf5N.YV2;  ;       ;       ;       ;       Testkennung f. acde;    Any;    {};     {DES,DES,MD5};  60;     ;       ;       ;       ;       ;       ;       ;       ;       Any;    ;       false;  ;

=cut	

	my $in_file_main    = shift;
	my $fworch_workdir     = shift;
	my $debuglevel_main = shift;
	my $line            = '';
	my $last_line       = '';
	my $parse_error     = 0;
	my $line_no       = 0;
	my $INFILE = new IO::File("< $in_file_main") or die "cannot open file $in_file_main\n";
	my $debug_lines = '';
	my $debug = 0;
	my $type;

	while ( $line = <$INFILE> ) {
		$line_no++;
		if ( $line =~ /^name;\tgroups;\tcolor;\tcomments;\tis_administrator_group;/ ) {		# start of user group definitions 
			$type = 'group';
		} elsif ( $line =~ /^name;\tcolor;\tgroups;\tdestinations;\tsources;/ ) {		# start of user definitions 
			$type = 'simple';		
		} else { # no header - plain user or group data from here on
			my $name;
			my $color;
			my $comments;
			my $is_admin;
			my $uid;
			my $expdate;
			my $group_string;

			if ($type eq 'group' && $line =~ /^(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);/ ) {
				$name = $1;
				$group_string = $2;
				$color = lc($3);
				$comments = $4;
			} elsif ($type eq 'simple' && $line =~ /^(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?)\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);\t(.*?);/ ) {
				$name = $1;
				$color = lc($2);
				$group_string = $3;
				$comments = $17;
				$expdate = &convert_checkpoint_to_db_date($9);
			} else { # line not parsable
				$parse_error = 1;
				print ("     $line_no: this line was not parsable: $line\n");
			}
			$uid = $name;
			if ($group_string ne '') {
				my $group_string_without_brackets;
				if ( $group_string =~ /\{(.*?)\}/ ) { $group_string_without_brackets = $1; }
				my @groups_this_user_belongs_to_ar = split /,/, $group_string_without_brackets;
				foreach my $gruppe (@groups_this_user_belongs_to_ar) {
#					print ("                   adding user $name to group $gruppe\n");
					if ( $gruppe ne 'ReferenceObject' ) {
#						TODO: Fehler fuer toten Link ausgeben 
						&cp_parse_users_add_member_to_group ($gruppe, $name, $debug);
					}
				}
			}
			push @user_ar, $name;
#			print ("     $line_no: adding new user $name\n");
			$user{"$name.type"} = $type;
			$user{"$name.uid"} = $uid;
			if ( defined($color) ) { $user{"$name.color"} = $color; }
			if ( defined($comments) ) { $user{"$name.comments"} = $comments; }
			if ( defined($expdate) ) { $user{"$name.expdate"} = $expdate; }
		}
		$last_line = $line;
	}
	$INFILE->close;
	&cp_parse_users_add_groupmembers_final;
	# check auf Vollstaendigkeit des Config-Files:
	if ( $last_line =~ m/^.+?\;(.*?\;){31}/  && !$parse_error) { return 0; }
	else {
		print ("error while parsing exported userdata, last_line: <$last_line>");
		return "error while parsing exported userdata, last_line: <$last_line>";
	}
}
# parse user groups that are externally defined (e.g. LDAP) from rules in rulebase file
# does not work for individual users - all these groups will be empty

sub cp_parse_users_from_rulebase { # ($rulebase_file)
	my $in_file_main    = shift;
	my $line            = '';
	my $last_line;
	my $INFILE = new IO::File("< $in_file_main") or die "cannot open file $in_file_main\n";
	my $type;
	my $name;
	my $comments;
	my $uid;

	print "parsing users from rulebase file: $in_file_main\n";
	LINE: while ( $line = <$INFILE> ) {
		chomp($line);
		if ($line =~ /^\t\t\t\t\t\: \(\"?(.+?)\@.+?$/) { # externally defined (e.g. ldap) user groups
			$name = $1;
			$comments = "";
			$type = 'group';
			$uid = $name;
			if (!defined($user{"$name.type"})) {
				push @user_ar, $name;
				$user{"$name.type"} = 'group';
				$user{"$name.uid"} = $name;
				$user{"$name.comments"} = '';
			}	
		}
		if ($line =~ /\t+\:Table \(identity\_roles\)$/) { # identity awareness user groups
			if ($last_line =~ /\t+\:Name \((.+)\)/) {
				$name = $1;
				$comments = "identity awareness user group";
				$type = 'group';
				$line = <$INFILE>;
				if ($line =~ /\t+\:Uid \(\"\{(.+)\}\"\)$/) {
					$uid = $1;
					if (!defined($user{"$name.type"})) {
						push @user_ar, $name;
						$user{"$name.type"} = 'group';
#						$user{"$name.uid"} = $uid;
						$user{"$name.uid"} = $name;
						$user{"$name.comments"} = $comments;
					}
				}
			}	
		}
		$last_line = $line;
	}
	$INFILE->close;
	return 0;
}

sub cp_parse_users_original_format {
	sub cp_parse_users_remove_nonprintable_chars {
		sub cp_parse_user_remove_bin_data {
			my $in_file_main  = shift;
			my $out_file_main = shift;
			my $line          = '';
		
			my $INFILE = new IO::File("< $in_file_main") or die "cannot open file $in_file_main\n";
			binmode($INFILE);
			my $OUTFILE = new IO::File("> $out_file_main") or die "cannot open file $out_file_main\n";
			binmode($OUTFILE);
#			while ( sysread( $INFILE, $line, 256 ) ) { print $OUTFILE substr( $line, 3 ); }
			while ( sysread( $INFILE, $line, 256 ) ) { print $OUTFILE substr( $line, 0 ); }
			$INFILE->close; $OUTFILE->close;
			return;
		}
		
		my $in_file   = shift;
		my $out_file  = shift;
		my $line      = '';
		my $orig_line = '';
	
		&cp_parse_user_remove_bin_data( $in_file, "${in_file}_non_bin" );
#		my $INFILE = new IO::File("< ${in_file}")
		my $INFILE = new IO::File("< ${in_file}_non_bin")
		  or die "cannot open file $in_file\n";
		my $OUTFILE = new IO::File("> $out_file")
		  or die "cannot open file $in_file\n";
		binmode($INFILE);
	
		while ( $line = <$INFILE> ) {
			$orig_line = $line;
			$line =~ s/\n/%EOL%/g;    #  preserve important non-printable chars
			$line =~ s/\t/%TAB%/g;
			if ( $line =~ /[^[:print:]]/ )
			{                         # replace all special characters with EOL
				$line =~ s/[^[:print:]]+/%EOL%%EOL%/g;     # zwei EOL, um eine leere Zeile zu erhalten
			}
			$line =~ s/%EOL%/\n/g;
			$line =~ s/%TAB%/\t/g;
			print $OUTFILE $line;
		}
		$INFILE->close;
		$OUTFILE->close;
	}

	my $in_file_main    = shift;
	my $fworch_workdir     = shift;
	my $debuglevel_main = shift;
	my $line            = '';
	my $name;
	my $type;
	my $color;
	my $comments;
	my $uid;
	my $last_change_admin;
	my $expdate;
	my $user_def_type = 'simple';
	my $line_no       = 0;
	my $userparse_state = 0;		#	0 = no user, 1 = in user definition, 2 = in group definition
	my $debug_lines = '';
	my $debug = 0;
	my $printable_usr_file = $in_file_main . '_printable';

	&cp_parse_users_remove_nonprintable_chars( $in_file_main, $printable_usr_file );
	my $INFILE = new IO::File("< $printable_usr_file") or die "cannot open file $printable_usr_file\n";

	while ( $line = <$INFILE> ) {
		$line_no++;
		if ( $line =~ /^([\w0-9\-_\s\.]+)\(\"?\1\"?$/ ) {		# only take lines "username(username" as user definitions
			$userparse_state = 1; # found start of user definition (username)
			undef($type); undef($color); undef($uid); undef($comments); undef($last_change_admin); undef($expdate);
			$name = $1;
			if ($debug) { print ("$line_no: processing user $name\n"); }
		}
		if ( $userparse_state && ($name =~ /[0-9a-fA-F]{16}/ ||  $name =~ /[0-9a-fA-F]{24}/ || $name eq 'ALL_EXTUSRGROUPS' || $name eq 'ALL_TEMPLATES' ||
			 $name eq 'ALL_GROUPS' || $name eq 'GROUPS_AND_EXTGROUPS' || $name eq 'ALL_KEYH' || $name eq 'Default' ))
		{	# no real user
			$userparse_state = 0; undef($name);
		}
		if ( $userparse_state && $line =~ /^\)?$/ ) { # end of user def (empty lines as well as closing brackets)
			if (defined($type) && defined($name) && $type ne 'rsacacertkey' && $type ne 'keyh' && $type ne 'template' )
			{	# wenn Benutzer noch nicht existiert: anlegen
				if ( !grep( /^${name}$/, @user_ar ) ) {
					push @user_ar, $name;
					if ($debug) { print ("     $line_no: adding new user $name\n") }
				}
				$user{"$name.type"} = $type;
				if ( defined($color) ) { $user{"$name.color"} = $color; }
				if ( defined($comments) ) { $user{"$name.comments"} = $comments; }
				$user{"$name.uid"} = $name; # nicht schoen, aber in den CP-Regeln sind fuer die User keine Uids!?, daher dient der Username als UID
				if ( defined($last_change_admin) ) { $user{"$name.last_change_admin"} = $last_change_admin; }
				if ( defined($expdate) ) { $user{"$name.expdate"} = $expdate; }
			}
			else {
				if ($debug) { print ("undefined type; final line: $line; name: $name, type: $type.\n"); }
			}
			$userparse_state = 0;
		}
		if ( $userparse_state && ($line =~ /^\t\t:ClassName\s\(([\w0-9\-_\.]+?)\)/ || $line =~ /^\t:type \((\w+)\)$/ ) )
		{
			if ($1 eq 'template' || $1 eq 'user_template') {	# ignore all template users
				$userparse_state = 0;
			}
			if (defined($type)) { 
				if ($debug) { print ("      $line_no :: found type $1, but type already defined as $type, line: $line"); }
			} else {
				$type = $1;
				if ($debug) { print ("      $line_no :: found type $type, line: $line"); }
				if ($type eq 'usrgroup' || $type eq 'extusrgroup' || $type eq 'external_group')
						{ $type = 'group'; }
				else	{ $type = 'simple'; }
			}
		}
		if ( $userparse_state) { $debug_lines .= $line_no . $line; }
		if ( $userparse_state && $line =~ /^\t\t:chkpf_uid \("\{([\w0-9\-_\.]+?)\}"\)$/ ) { $uid = &gen_uid($1); }
		if ( $userparse_state && $line =~ /^\t\t\t:By \(([\w0-9\-_\.]+)\)$/ ) { $last_change_admin = $1; }
		if ( $userparse_state && $line =~ /^\t:Uid \("\{([\w0-9\-_\.]+?)\}"\)$/ ) { $uid = &gen_uid($1); }

#		usergroup processing
		if ( $line =~ /:groups/ ) { # start of a group statement
			if ($debug) { print ("     start of group statement in $line_no, userparse_state = $userparse_state: $line"); }
		}
		if ( $userparse_state && $line =~ /^\t:groups \(\)$/ ) { # no group memberships
			if ($debug) { print ("     $line_no: user $name does not belong to any groups: $line"); }
		}
		if ( $userparse_state && $line =~ /^\t\:groups \($/ ) {
			$userparse_state = 2;
			if ($debug) { print ("     $line_no: user $name belongs to groups: "); }
		}
		if ( $userparse_state == 2 && ($line =~ /^$/ || $line =~ /^\t\t\)$/) ) #  || $line =~ /^\t\t:\s\(/ || $line =~ /^\t?\)/ ))
		{    # (premature) end of group-statement
			$userparse_state = 1;
		}
		if ( $userparse_state == 2 && $line =~ /^\s+: \(([\w0-9\-_\.]+)$/ ) { 
			if ($debug) { print ("     $line_no: entering cp_parse_users_add_member_to_group, line: $line"); }			
			&cp_parse_users_add_member_to_group( $1, $name, $debug );
		}
	}
	$INFILE->close;
	if ($debug) { print $debug_lines; }
	&cp_parse_users_add_groupmembers_final;
}

sub cp_resolve_groups_with_exception {
	sub is_group {
		my $nwobj = shift;
		if (defined ($network_objects{"$nwobj.type"})) {
			return ($network_objects{"$nwobj.type"} eq 'group');			
		} else {
			print ("WARNING: type of $nwobj not defined\n");
			return 0;
		}
	}
	sub is_non_empty_group {
		my $nwobj = shift;
		if (is_group($nwobj) && defined($network_objects{"$nwobj.members"}) && $network_objects{"$nwobj.members"} ne '') {
			return 1;
		} else {
			return 0;
		}
	}
	sub is_group_with_exclusion {
		my $nwobj = shift;
		return ($network_objects{"$nwobj.type"} eq 'group_with_exclusion');
	}
	sub is_simple_ip {
		my $obj_in = shift;
		return ($network_objects{"$obj_in.type"} eq 'host' || $network_objects{"$obj_in.type"} eq 'gateway');
	}
	sub is_network {
		my $obj_in = shift;
		return ($network_objects{"$obj_in.type"} eq 'network');
	}
	sub is_ip_range {
		my $obj_in = shift;
		return ($network_objects{"$obj_in.type"} eq 'machines_range');
	}
	sub to_cidr {
		my $obj_in = shift;
		my $obj;
		if (is_network($obj_in)) {
			$obj = Net::CIDR::addrandmask2cidr($network_objects{"$obj_in.ipaddr"}, $network_objects{"$obj_in.netmask"});
		} elsif (is_simple_ip($obj_in)) {
			$obj = $network_objects{"$obj_in.ipaddr"} . "/32";
		} elsif (is_ip_range($obj_in)) {
			$obj = $network_objects{"$obj_in.ipaddr"} . "-" . $network_objects{"$obj_in.ipaddr_last"} ;
		} else {
			print ("WARNING: $obj_in is neither network nor host nor range, but " . $network_objects{"$obj_in.type"}. " - empty?\n");
			if (defined($network_objects{"$obj_in.ipaddr"})) {
				$obj = $network_objects{"$obj_in.ipaddr"} . "/32";
			} else {
				undef $obj;
			}
		}
		return $obj;
	}
	sub list_to_cidr {
		my $obj_ar_in = shift; # ref to array
		my @obj_ar_out = ();
		
		foreach my $obj (@{$obj_ar_in}) {
			my $obj_out = &to_cidr($obj);
			if (defined($obj_out)) {
				@obj_ar_out = (@obj_ar_out, $obj_out);
			}
		}
		return @obj_ar_out;
	}
	sub ip_overlaps_with_list {
		my $obj1_in = shift;
		my $obj_array_ref_in = shift;

		my $obj1 = &to_cidr($obj1_in);
		my @cidr_array = ();
		foreach my $obj (@{$obj_array_ref_in}) {
			@cidr_array = (@cidr_array, &to_cidr($obj));
		}
		my $result = &Net::CIDR::cidrlookup($obj1, @cidr_array);
		return ($result);
	}
	sub ips_overlap {
		my $obj1_in = shift;
		my $obj2_in = shift;

		my $obj1 = &to_cidr($obj1_in);
		my $obj2 = &to_cidr($obj2_in);
		my $result = &Net::CIDR::cidrlookup($obj1, $obj2);
		return ($result);
	}
	sub flatten {
		my $group_in = shift;
		if (defined($group_in) && defined($network_objects{"$group_in.members"}) && defined($network_objects{"$group_in.member_refs"})) {
			my $member_string = $network_objects{"$group_in.members"}; 
			my $member_ref_string = $network_objects{"$group_in.member_refs"}; 
			my @member_ar = split (/[$GROUPSEP]/, $member_string); 
			my @member_ref_ar = split (/[$GROUPSEP]/, $member_ref_string);
			my $i = 0;
			while ($i < scalar @member_ar) {
				my $m = $member_ar[$i]; 
				if (&is_non_empty_group ($m) && defined($m) && defined($GROUPSEP) && defined($network_objects{"$m.members"})) {
					splice(@member_ar, $i, 1, split(/[$GROUPSEP]/, $network_objects{"$m.members"}));
					$member_string = join ("$GROUPSEP", @member_ar);
					splice(@member_ref_ar,$i,1, split(/[$GROUPSEP]/, $network_objects{"$m.member_refs"}));
					$member_ref_string = join ("$GROUPSEP", @member_ref_ar);
				} else {
					$i++;
				}
				@member_ar = split (/[$GROUPSEP]/, $member_string);
				@member_ref_ar = split (/[$GROUPSEP]/, $member_ref_string);
			}
			$network_objects{"$group_in.members"} = $member_string;
			$network_objects{"$group_in.member_refs"} = $member_ref_string;
	#		print ("flatten_result: $member_string\n");
		}
		return;
	}
	sub reduce_ip_by_one {
		my $ip_in = shift;
		my $result;
		
		if ($ip_in =~ /^0.0.0.0$/) { return undef; }
		if ($ip_in =~ /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/ ) {
			if ($4 ne '0') {		$result = "$1.$2.$3." . ($4-1);
			} elsif ($3 ne '0') {	$result = "$1.$2." . ($3-1) . ".255";
			} elsif ($2 ne '0') {	$result = "$1." . ($2-1) . ".255.255";
			} else {				$result = ($1-1) . ".255.255.255";
			}
			return $result;
		} else {
			print ("WARNING: ip not well-formed: $ip_in\n");
		}
	}	
	sub increase_ip_by_one {
		my $ip_in = shift;
		my $result;
		
		if ($ip_in =~ /^255.255.255.255$/) { return undef; }
		if ($ip_in =~ /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/ ) {
			if ($4 ne '255') {		$result = "$1.$2.$3." . ($4+1);
			} elsif ($3 ne '255') {	$result = "$1.$2." . ($3+1) . ".0";
			} elsif ($2 ne '255') {	$result = "$1." . ($2+1) . ".0.0";
			} else {				$result = ($1+1) . ".0.0.0";
			}
			return $result;
		} else {
			print ("WARNING: ip not well-formed: $ip_in\n");
		}
	}
	sub pad_ip {		# used only for correct sorting
		my $ip = shift;
		my $result;
		if ($ip =~ /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/ ) {
			$result = sprintf("%03d.%03d.%03d.%03d", $1, $2, $3, $4);
		} else {
			print ("WARNING: ip not well-formed: $ip\n");
			return undef;
		}
		return $result;
	}
	sub calculate_non_overlapping_group {
		my $pos_ip = shift;
		my $neg_ip = shift;
		my $group_name = shift;
		
		my ($pos_ip_range) = Net::CIDR::cidr2range($pos_ip);
		my ($neg_ip_range) = Net::CIDR::cidr2range($neg_ip);
		my ($pos_start, $pos_end) = split(/\-/, $pos_ip_range);
		my ($neg_start, $neg_end) = split(/\-/, $neg_ip_range);
		my @new_objs;

		if (&pad_ip($pos_start) lt &pad_ip($neg_start) && &pad_ip($pos_end) gt &pad_ip($neg_end)) { 
		#	neg is fully contained in pos
		#	pos: 1.0.0.0-1.1.255.255, neg 1.1.2.3-1.1.2.3
			@new_objs = Net::CIDR::range2cidr(split(/,/, "$pos_start-" .
						&reduce_ip_by_one($neg_start) .
				 "," .	&increase_ip_by_one($neg_end) . "-" . $pos_end));
		} elsif (&pad_ip($neg_start) le &pad_ip($pos_start) && &pad_ip($neg_end) lt &pad_ip($pos_end)) {
		#	neg cuts off the beginning of pos
		#	pos: 1.6.0.0-1.7.255.255, neg 1.5.255.254-1.6.0.2
			@new_objs = Net::CIDR::range2cidr(&increase_ip_by_one($neg_end) . "-$pos_end");
		} elsif (&pad_ip($pos_start) lt &pad_ip($neg_start) && &pad_ip($neg_end) ge &pad_ip($pos_end)) {
		#	neg cuts off the end of pos --> pos = old_pos_start to start_of_neg - 1
		#	pos: 1.6.0.0-1.7.255.255, neg 1.7.255.254-1.8.0.2
			@new_objs = Net::CIDR::range2cidr("$pos_start-" . &reduce_ip_by_one($neg_start));
		} else {
		#	neg fully contains pos --> result is empty, do nothing
			@new_objs = ();
		}
		return join ("$GROUPSEP", @new_objs);
	}
	sub obj_is_created {
		my $cidr_obj = shift;
		
		# currently we do not keep any original objects in pos group (assuming only object "any" in pos group anyway)
		return 1;
	}
	sub create_new_obj {
		my $new_obj = shift;
		my $group_name = shift;
		my $obj_name = "${group_name}_${new_obj}_isogrpexcl";
		my $obj_ref = "$obj_name.ref";

		@network_objects = (@network_objects, $obj_name);
		$network_objects{"$obj_name.name"} = $obj_name;
		$network_objects{"$obj_name.UID"} = $obj_ref;
		
		if ($new_obj =~ /\// && $new_obj !~ /\/32$/ ) { # network
			$network_objects{"$obj_name.type"} = 'network';
			$network_objects{"$obj_name.ipaddr"} = $new_obj;
		} elsif ($new_obj =~ /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/ || $new_obj =~ /\/32$/) { # simple host
			$network_objects{"$obj_name.type"} = 'host';
			$network_objects{"$obj_name.ipaddr"} = $new_obj;
		} elsif ($new_obj =~ /^(\d+)\.(\d+)\.(\d+)\.(\d+)-(\d+)\.(\d+)\.(\d+)\.(\d+)$/) { # range 
			$network_objects{"$obj_name.type"} = 'machines_range';
			print ("WARNING: $new_obj is range, but this should never occur.\n");
		} else {
			print ("WARNING: $new_obj is neither network nor host nor range.\n");
		}		
		return ($obj_name, $obj_ref);
	} 
	sub add_obj_to_group {
		my $obj_name = shift;
		my $obj_ref = shift;
		my $group_name = shift;
		
		if (defined($network_objects{"$group_name.members"}) && $network_objects{"$group_name.members"} ne '') {
			$network_objects{"$group_name.members"} .= "${GROUPSEP}$obj_name";	
			$network_objects{"$group_name.member_refs"} .= "${GROUPSEP}$obj_ref";	
		} else {
			$network_objects{"$group_name.members"} = $obj_name;	
			$network_objects{"$group_name.member_refs"} = $obj_ref;	
		}			
	}	
	sub remove_obj_from_group {
		my $obj_name = shift;
		my $group_name = shift;

		my $idx = 0;
		my @member_ar = split (/[$GROUPSEP]/, $network_objects{"$group_name.members"});
		my @member_ref_ar = split (/[$GROUPSEP]/, $network_objects{"$group_name.member_refs"});		
		while ($idx < scalar (@member_ar)) {
			if ($member_ar[$idx] eq $obj_name) {
				splice (@member_ar, $idx, 1);
				splice (@member_ref_ar, $idx, 1);
			} 
			$idx++;
		}		
		$network_objects{"$group_name.members"} = join ("$GROUPSEP", @member_ar);	
		$network_objects{"$group_name.member_refs"} = join ("$GROUPSEP", @member_ref_ar);	
	}			
	sub is_obj_the_any_obj {
		my $obj_name_in = shift;
		return (lc($network_objects{"$obj_name_in.name"}) eq 'any');		
	}	
	
	foreach my $nwobj ( @network_objects ) {
		if (&is_group_with_exclusion($nwobj)) {
#			print ("processing group_with_exclusion: $nwobj\n");
			my $members = $network_objects{"$nwobj.members"};;
			
			# remove pos_group from $nwobj and add its content instead
			my ($positiv_gruppe, $negativ_gruppe, $pos_idx, $neg_idx, $pos_grp_ref, $neg_grp_ref);
			my $idx = 0;
			foreach my $member (split (/[$GROUPSEP]/, $network_objects{"$nwobj.members"})) {
				if ($member =~ /^\{base\}(.+)$/) { $positiv_gruppe = $1; $pos_idx = $idx; }
				if ($member =~ /^\{exception\}(.+)$/) { $negativ_gruppe = $1; $neg_idx = $idx; }
			}
			my @pos_member_list;
			if (is_obj_the_any_obj($positiv_gruppe)) {
				@pos_member_list = ("$positiv_gruppe");
			} else {
				&flatten($positiv_gruppe);	# flatten = untergruppen aufloesen
				@pos_member_list = split (/[$GROUPSEP]/, $network_objects{"$positiv_gruppe.members"});
			}
			&flatten($negativ_gruppe);
			my @neg_member_list; 			
			if (defined($negativ_gruppe) && defined($network_objects{"$negativ_gruppe.members"})) {
				@neg_member_list = split (/[$GROUPSEP]/, $network_objects{"$negativ_gruppe.members"});
			} else {
				@neg_member_list = ();				
			}	 
			# convert all list members to cidr notation
			@pos_member_list = &list_to_cidr(\@pos_member_list);
			@neg_member_list = &list_to_cidr(\@neg_member_list);
	
			# ab hier ist @pos_member_list massgeblich (keine Veraenderung von $network_objects)
			foreach my $negmember (@neg_member_list) {
				if (&Net::CIDR::cidrlookup($negmember, @pos_member_list)) { # overlapping?
					# am Ende dieses ifs wird die Liste @pos_member_list komplett neu definiert fuer den naechsten Schleifendurchlauf 
					my $i = 0;
					while ($i < scalar @pos_member_list) {
						my $posmember = $pos_member_list[$i]; 
						if (&Net::CIDR::cidrlookup ($negmember, $posmember)) {
							my $new_obj_str = &calculate_non_overlapping_group ($posmember, $negmember);
							my @new_objs = split (/[$GROUPSEP]/, $new_obj_str);
							splice(@pos_member_list, $i, 1, @new_objs);
							# for optimizing only:
							$i += scalar @new_objs;
						} else {
							$i++;
						}
					}
				}
			}
			# pos gruppe normieren/zusammenfassen/vereinfachen
			my @new_non_redundant_objects = ();
			foreach my $new_obj (@pos_member_list) {
				@new_non_redundant_objects = Net::CIDR::cidradd($new_obj, @new_non_redundant_objects);						
			}
			# jetzt werden die $network_objects wieder angefasst
			# redefine members of group to empty group
			$network_objects{"$nwobj.members"} = '';
			$network_objects{"$nwobj.member_refs"} = '';
			foreach my $obj (@new_non_redundant_objects) {
				my ($obj_name, $obj_ref);
				if (obj_is_created($obj)) {				
					($obj_name, $obj_ref) = &create_new_obj ($obj, $nwobj);
				} else { # 
#					($obj_name, $obj_ref) = lookup_nwobj_data ($obj); 	
				}
				&add_obj_to_group ($obj_name, $obj_ref, $nwobj);			
			}
		}
	}
	return;
}    

1;

__END__

=head1 NAME

CACTUS::FWORCH::parser - Perl extension for fworch check point parser

=head1 SYNOPSIS

  use CACTUS::FWORCH::import::checkpoint;

=head1 DESCRIPTION

IT Security Organizer Perl Module
support for importing configs into fworch Database

=head2 EXPORT

  global variables

=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Cactus eSecurity, tmp@cactus.de

=cut
