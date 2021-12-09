package CACTUS::FWORCH::import::parser;

use strict;
use warnings;
use IO::File;
use Getopt::Long;
use File::Basename;
use CACTUS::FWORCH;
use CACTUS::FWORCH::import;

require Exporter;
our @ISA = qw(Exporter);

our %EXPORT_TAGS = ( 'basic' => [ qw( &copy_config_from_mgm_to_iso &parse_config ) ] );

our @EXPORT  = ( @{ $EXPORT_TAGS{'basic'} } );
our $VERSION = '0.3';

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
	my $debug_level   = shift;
	my $result;
	my $cmd;
	my $return_code = 0;
	# my $parser_py = "/usr/bin/python3 ./fworch_parse_config_cp_r8x_api.py";
	my $parser_py = "/usr/bin/python3 ./checkpointR8x/parse_config.py";
	my $users_csv = "$output_dir/${mgm_name}_users.csv";
	my $users_delimiter = "%"; # value is defined in parser_py = ./fworch_parse_config_cp_r8x_api.py !!!


# parsing rulebases
	my $local_rulebase_names = get_local_ruleset_name_list($rulebase_name);
	my $global_rulebase_names = get_global_ruleset_name_list($rulebase_name);
	my @local_rulebase_name_ar = split /,/, $local_rulebase_names;
	my @global_rulebase_name_ar = split /,/, $global_rulebase_names;
	my $rulebase_with_slash;
	my $rulebase_name_sanitized;
	for (my $i=0; $i<scalar(@local_rulebase_name_ar); $i++) {
		if (defined($global_rulebase_name_ar[$i]) && $global_rulebase_name_ar[$i] ne "") {
			$rulebase_with_slash = $global_rulebase_name_ar[$i].'/'.$local_rulebase_name_ar[$i];
			$rulebase_name_sanitized = $global_rulebase_name_ar[$i].'__'.$local_rulebase_name_ar[$i];
		}
		else {
			$rulebase_with_slash = $local_rulebase_name_ar[$i];
			$rulebase_name_sanitized = $local_rulebase_name_ar[$i];
		}

		$cmd = "$parser_py -m $mgm_name -i $import_id -r \"$rulebase_with_slash\" -f \"$object_file\" -d $debug_level > \"$output_dir/${rulebase_name_sanitized}_rulebase.csv\"";
#		print("DEBUG - cmd = $cmd\n");
		$return_code = system($cmd); 
		if ( $return_code != 0 ) { print("ERROR in parse_config found: $return_code\n") }

	}
# 	foreach my $rulebase (@local_rulebase_name_ar) {
# 		my $rulebase_name_sanitized = join('__', split /\//, $rulebase);
# 		$cmd = "$parser_py -m $mgm_name -i $import_id -r \"$rulebase\" -f \"$object_file\" -d $debug_level > \"$output_dir/${rulebase_name_sanitized}_rulebase.csv\"";
# #		print("DEBUG - cmd = $cmd\n");
# 		$return_code = system($cmd); 
# 		if ( $return_code != 0 ) { print("ERROR in parse_config found: $return_code\n") }
# 	}
# parsing users
	$cmd = "$parser_py -m $mgm_name -i $import_id -u -f \"$object_file\" -d $debug_level > \"$output_dir/${mgm_name}_users.csv\"";
#	print("DEBUG - cmd = $cmd\n");
	$return_code = system($cmd); 
	# system("ls -l $output_dir");
	if ( $return_code != 0 ) { print("ERROR in parse_config::users found: $return_code\n") }
	
	# in case of no users being returned, remove users_csv file
	if (-r $users_csv) {
		my $empty_flag = 0;
		open FH, $users_csv;
		my $firstline = <FH>;
		if (defined($firstline)) {
			# print ("firstline=$firstline###\n");
			if(index($firstline,$users_delimiter)==-1) {
					#print ("test: empty_flag=$empty_flag\n");
					$empty_flag = 1;
			}
		}
		close FH;
		if ($empty_flag == 1){
			# print ("unlinking users_csv file $users_csv\n");
			unlink $users_csv;
		}
	}
	
# parsing svc objects
	$cmd = "$parser_py -m $mgm_name -i $import_id -s -f \"$object_file\" -d $debug_level > \"$output_dir/${mgm_name}_services.csv\"";
#	print("DEBUG - cmd = $cmd\n");
	$return_code = system($cmd); 
	if ( $return_code != 0 ) { print("ERROR in parse_config::services found: $return_code\n") }
# parsing nw objects
	$cmd = "$parser_py -m $mgm_name -i $import_id -n -f \"$object_file\" -d $debug_level > \"$output_dir/${mgm_name}_netzobjekte.csv\"";
#	print("DEBUG - cmd = $cmd\n");
	$return_code = system($cmd); 
	if ( $return_code != 0 ) { print("ERROR in parse_config::network_objects found: $return_code\n") }
	return $return_code;
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
	my $debug_level     = shift;
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

	my $rulebase_names = get_local_ruleset_name_list($rulebase_names_hash_ref);
	if ( ${^CHILD_ERROR_NATIVE} ) { $fehler_count++; }

	if ( -r "$workdir/${CACTUS::FWORCH::ssh_id_basename}.pub" ) {
		$ssl_verify = "-s $workdir/${CACTUS::FWORCH::ssh_id_basename}.pub";
	}
	if (defined($config_path_on_mgmt) && $config_path_on_mgmt ne '') {
		$domain_setting = "-D " . $config_path_on_mgmt;
	}
	if (defined($api_port) && $api_port ne '') {
		$api_port_setting = "-p $api_port"; 
	}

	$lib_path = "$base_path/checkpointR8x";
	$get_config_bin = "$lib_path/get_basic_config.py";
	$enrich_config_bin = "$lib_path/enrich_config.py";
	$get_cmd = "$python_bin $get_config_bin -a $api_hostname -w '$workdir/$CACTUS::FWORCH::ssh_id_basename' -l '$rulebase_names' -u $api_user $api_port_setting $ssl_verify $domain_setting -o '$cfg_dir/$obj_file_base' -d $debug_level";
	$enrich_cmd = "$python_bin $enrich_config_bin -a $api_hostname -w '$workdir/$CACTUS::FWORCH::ssh_id_basename' -l '$rulebase_names' -u $api_user $api_port_setting $ssl_verify $domain_setting -c '$cfg_dir/$obj_file_base' -d $debug_level";
	
	if ($debug_level>0) {
		print("getting config with command: $get_cmd\n");
	}
	$return_code = system($get_cmd); if ( $return_code != 0 ) { $fehler_count++; }
	if ($debug_level>0) {
		print("enriching config with command:   $enrich_cmd\n");
	}
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

Perl Module support for importing configs into database - in this case only empty shell calling python code

=head2 EXPORT

  global variables

=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Cactus eSecurity, tmp@cactus.de

=cut
