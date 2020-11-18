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
	my $cmd;
	my $return_code = 0;
	my $parser_py = "/usr/bin/python3 ./fworch_parse_config_cp_r8x_api.py";
	my $users_csv = "$output_dir/${mgm_name}_users.csv";
	my $users_delimiter = "%"; # value is defined in parser_py = ./fworch_parse_config_cp_r8x_api.py !!!


# parsing rulebases
	my $rulebase_names = get_ruleset_name_list($rulebase_name);
	my @rulebase_name_ar = split /,/, $rulebase_names;
	foreach my $rulebase (@rulebase_name_ar) {
		$cmd = "$parser_py -m $mgm_name -i $import_id -r \"$rulebase\" -f \"$object_file\" > \"$output_dir/${rulebase}_rulebase.csv\"";
#		print("DEBUG - cmd = $cmd\n");
		$return_code = system($cmd); 
		if ( $return_code != 0 ) { print("ERROR in parse_config found: $return_code\n") }
	}
# parsing users
	$cmd = "$parser_py -m $mgm_name -i $import_id -u -f \"$object_file\" > \"$output_dir/${mgm_name}_users.csv\"";
#	print("DEBUG - cmd = $cmd\n");
	$return_code = system($cmd); 
	# system("ls -l $output_dir");
	if ( $return_code != 0 ) { print("ERROR in parse_config::users found: $return_code\n") }
	
	# in case of no users being returned, remove users_csv file
	if (-r $users_csv) {
		my $empty_flag = 0;
		open FH, $users_csv;
		my $firstline = <FH>;
		# print ("firstline=$firstline###\n");
		if(index($firstline,$users_delimiter)==-1) {
				#print ("test: empty_flag=$empty_flag\n");
				$empty_flag = 1;
		}
		close FH;
		if ($empty_flag == 1){
				print ("unlink users_csv file $users_csv\n");
				unlink $users_csv;
		}
	}
	
# parsing svc objects
	$cmd = "$parser_py -m $mgm_name -i $import_id -s -f \"$object_file\" > \"$output_dir/${mgm_name}_services.csv\"";
#	print("DEBUG - cmd = $cmd\n");
	$return_code = system($cmd); 
	if ( $return_code != 0 ) { print("ERROR in parse_config::services found: $return_code\n") }
# parsing nw objects
	$cmd = "$parser_py -m $mgm_name -i $import_id -n -f \"$object_file\" > \"$output_dir/${mgm_name}_netzobjekte.csv\"";
#	print("DEBUG - cmd = $cmd\n");
	$return_code = system($cmd); 
	if ( $return_code != 0 ) { print("ERROR in parse_config::network_objects found: $return_code\n") }
# parsing zones (if needed)
#	$cmd = "$parser_py -m $mgm_name -i $import_id -z -f \"$object_file\" > \"$output_dir/${mgm_name}_zones.csv\"";
#	$return_code = system($cmd); 
#	if ( $return_code != 0 ) { print("ERROR in parse_config::zones found: $return_code\n") }
	return $return_code;
}

# replace space with _
sub filename_escape_chars {
	my $input_filename = shift;
	my $escaped_filename;
	
	$escaped_filename = 
	
	return 
}

############################################################
# copy_config_from_mgm_to_iso($ssh_private_key, $ssh_user, $ssh_hostname, $management_name, $obj_file_base, $cfg_dir, $rule_file_base)
# Kopieren der Config-Daten vom Management-System zum ITSecorg-Server
############################################################
sub copy_config_from_mgm_to_iso {
	my $api_user        = shift;
	my $api_hostname    = shift;
	my $management_name = shift; # not used
	my $obj_file_base   = shift;
	my $cfg_dir         = shift;
	my $layer_name		= shift;
	my $workdir         = shift;
	my $auditlog		= shift;	
	my $prev_import_time= shift;
	my $api_port		= shift;
	my $config_path_on_mgmt		= shift;
	my $rulebase_names_hash_ref	= shift;
	my $return_code;
	my $fehler_count = 0;
	my $domain_setting = "";
	my $api_port_setting = "";
	my $ssl_verify = "";
	my $python_bin = "/usr/bin/python3";
	my $base_path = "/usr/local/fworch/importer";
	my $lib_path;
	my $get_config_bin;
	my $enrich_config_bin;
	my $get_cmd;
	my $enrich_cmd;

	my $rulebase_names = get_ruleset_name_list($rulebase_names_hash_ref);
	# first extract password from $ssh_id_basename (normally containing ssh priv key)
	my $pwd = `cat $workdir/$CACTUS::FWORCH::ssh_id_basename`;
	if ( ${^CHILD_ERROR_NATIVE} ) { $fehler_count++; }

	chomp($pwd);
	if ( -r "$workdir/${CACTUS::FWORCH::ssh_id_basename}.pub" ) {
		$ssl_verify = "-s $workdir/${CACTUS::FWORCH::ssh_id_basename}.pub";
	}
	if ($config_path_on_mgmt ne '') {
		$domain_setting = "-D " . $config_path_on_mgmt;
	}
	if (defined($api_port) && $api_port ne '') {
		$api_port_setting = "-p $api_port"; 
	}


	############### new ##################
	# $lib_path = "$base_path/checkpointR8x";
	# $get_config_bin = "$lib_path/get_config.py";
	# $enrich_config_bin = "$lib_path/enrich_config.py";
	# $get_cmd = "$python_bin $get_config_bin -a $api_hostname -w '$pwd' -l '$rulebase_names' -u $api_user $api_port_setting $ssl_verify $domain_setting -o '$cfg_dir/$obj_file_base'";
	# $enrich_cmd = "$python_bin $enrich_config_bin -a $api_hostname -w '$pwd' -l '$rulebase_names' -u $api_user $api_port_setting $ssl_verify $domain_setting -c '$cfg_dir/$obj_file_base'";

	############### old ##################
	$lib_path = $base_path;
	$get_config_bin = "$lib_path/fworch_get_config_cp_r8x_api.py";
	$enrich_config_bin = "$lib_path/fworch_get_config_cp_r8x_api.py";
	$get_cmd = "$python_bin $get_config_bin -m get -a $api_hostname -w '$pwd' -l '$rulebase_names' -u $api_user $api_port_setting $ssl_verify $domain_setting -o '$cfg_dir/$obj_file_base'";
	$enrich_cmd = "$python_bin $enrich_config_bin -m enrich -a $api_hostname -w '$pwd' -l '$rulebase_names' -u $api_user $api_port_setting $ssl_verify $domain_setting -o '$cfg_dir/$obj_file_base'";

	print("getting config with command: $get_cmd\n");
	$return_code = system($get_cmd); if ( $return_code != 0 ) { $fehler_count++; }
	print("enriching config with command:   $enrich_cmd\n");
	$return_code = system($enrich_cmd); if ( $return_code != 0 ) { $fehler_count++; }
	return ( $fehler_count, "$cfg_dir/$obj_file_base,$cfg_dir/$layer_name");
}


1;

__END__

=head1 NAME

parser - Perl extension for check point R8x API get and parse config

=head1 SYNOPSIS

  use CACTUS::FWORCH::import::checkpointR8x;

=head1 DESCRIPTION

Perl Module support for importing configs into database

=head2 EXPORT

  global variables

=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Cactus eSecurity, tmp@cactus.de

=cut
