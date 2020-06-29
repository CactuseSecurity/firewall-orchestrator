package CACTUS::ISO::import::parser;

use strict;
use warnings;
use IO::File;
use Getopt::Long;
use File::Basename;
use Time::HiRes qw(time);    # fuer hundertstelsekundengenaue Messung der Ausfuehrdauer
use Net::CIDR;
use CACTUS::ISO;
use CACTUS::ISO::import;
use Date::Calc qw(Add_Delta_DHMS);

require Exporter;
our @ISA = qw(Exporter);

our %EXPORT_TAGS = ( 'basic' => [ qw( &copy_config_from_mgm_to_iso &parse_config ) ] );

our @EXPORT  = ( @{ $EXPORT_TAGS{'basic'} } );
our $VERSION = '0.3';

# variblendefinition check point parser - global
# -------------------------------------------------------------------------------------------
my $GROUPSEP = $CACTUS::ISO::group_delimiter; 

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
	my $parser_py = "/usr/bin/python3 ./iso_parse_config_cp_r8x_api.py";

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
	if ( $return_code != 0 ) { print("ERROR in parse_config::users found: $return_code\n") }
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

sub get_ruleset_name_list {
	my $href_rulesetname = shift;
	my $result = '';
	
	while ( (my $key, my $value) = each %{$href_rulesetname}) {
		$result .= $value->{'dev_rulebase'} . ',';
    }
    if ($result =~ /^(.+?)\,$/) {
    	return $1;
    }
    return $result;
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
	my $cmd;
	my $return_code;
	my $fehler_count = 0;

#iso_parse_config_cp_r8x_api.py
	my $rulebase_names = get_ruleset_name_list($rulebase_names_hash_ref);
	# first extract password from $ssh_id_basename (normally containing ssh priv key)
	my $pwd = `cat $workdir/$CACTUS::ISO::ssh_id_basename`;
	chomp($pwd);
	if ( ${^CHILD_ERROR_NATIVE} ) { $fehler_count++; }
	if (!defined($api_port) || $api_port eq '') { $api_port = "443"; }
	my $api_bin = "/usr/bin/python3 ./iso_get_config_cp_r8x_api.py";
	$cmd = "$api_bin $api_hostname '$pwd' -l '$rulebase_names' -p $api_port > \"$cfg_dir/$obj_file_base\"";
	print("DEBUG - cmd = $cmd\n");
	$return_code = system($cmd); if ( $return_code != 0 ) { $fehler_count++; }

	return ( $fehler_count, "$cfg_dir/$obj_file_base,$cfg_dir/$layer_name");
}


1;

__END__

=head1 NAME

CACTUS::ISO::parser - Perl extension for IT Security Organizer check point R8x API access to config

=head1 SYNOPSIS

  use CACTUS::ISO::import::checkpointR8x;

=head1 DESCRIPTION

IT Security Organizer Perl Module
support for importing configs into ITSecOrg Database

=head2 EXPORT

  global variables

=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Tim Purschke, tmp@cactus.de

=head1 COPYRIGHT AND LICENSE

  Copyright (C) 2017 by Cactus eSecurity GmbH, Frankfurt, Germany

=cut
