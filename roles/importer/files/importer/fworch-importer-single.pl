#! /usr/bin/perl -w

use strict;
use lib '.';
use CACTUS::FWORCH;					  # base functions and variables for fworch db access
use CACTUS::FWORCH::import;			  # import functions, parsers
use CGI qw(:standard);				  # provides argument handling
use Time::HiRes qw(time tv_interval); # provides exact measurement of import execution time
use File::Path qw(make_path rmtree);
use File::Find;
use CACTUS::read_config;

##############################################

sub empty_rule_files { # deletes a rule file and creates an empty rule file instead
	if ($File::Find::name =~ /\_rulebase\.csv$/) {
		system("rm '$File::Find::name'");
		system("touch '$File::Find::name'");
	}
}

sub empty_config_files { # deletes a csv config file and creates an empty csv file instead
	if ($File::Find::name =~ /\.csv$/) {
		system("rm '$File::Find::name'");
		system("touch '$File::Find::name'");
		print ("emptying csv config file '" . $File::Find::name . "'\n");
	} else {
		print ("leaving config file '" . $File::Find::name . "' untouched\n");
	}
}

my ($user, $output, $dev_name, $cfg_typ, $admin, $hersteller);
my ($cmd, $dauer, $first_start_time, $start_time, $sqlcode, $fields);
my ($current_import_id, $dev_id, $dev_typ_id);
my $changes = '';
my $error_str_local = '';	my $error_count_global = 0;		my $error_count_local = 0;	my $error_level;
my ($template, $obj_file, $obj_file_base, $rule_file, $rule_file_base, $user_file, $user_file_base, $config_files, $config_files_str,
	$cmd_str, $is_netscreen,$ssh_hostname,$ssh_user,$ssh_private_key, $ssh_public_key, $ssh_port, $config_path_on_mgmt);
my ($sqldatafile, $csv_zone_file, $csv_obj_file, $csv_svc_file, $csv_usr_file, $csv_auditlog_file, $csv_rule_file, $logfiles,
	$show_all_import_errors, $fullauditlog, $clear_all_rules, $clear_whole_mgm_config, $no_md5_checks);
my $prev_imp_id;
my $prev_imp_time = "2000-01-01 00:00:00";	# management has never been imported --> default value
my $do_not_copy = 0;
my $use_scp = 0;
my $no_cleanup = 0;
my $debug_level=0;
my $configfile="";
my $csvonly = 0;


$first_start_time = time; $start_time = $first_start_time;

my ($mgm_id, $mgm_name) = 
	&evaluate_parameters((defined(scalar param("mgm_id")))?scalar param("mgm_id"):'', (defined(scalar param("mgm_name")))?scalar param("mgm_name"):'');
if (defined(param("-show-only-first-import-error"))) { $show_all_import_errors = 0; } else { $show_all_import_errors = 1; }
if (defined(param("-fullauditlog"))) { $fullauditlog = 1; } else { $fullauditlog = 0; }
if (defined(param("-clear-all-rules"))) { $clear_all_rules = 1; } else { $clear_all_rules = 0; }
if (defined(param("-clear-management"))) { $clear_whole_mgm_config = 1; } else { $clear_whole_mgm_config = 0; }
if (defined(param("-no-md5-checks"))) { $no_md5_checks = 1; } else { $no_md5_checks = 0; }
if (defined(param("-do-not-copy"))) { $do_not_copy = 1; $no_md5_checks = 1; }  # assumes that config has already been copied to fworch importer
if (defined(param("-use-scp"))) { $use_scp = 1; $no_md5_checks = 1; }  # assumes that config has already been copied to fworch importer
if (defined(param("-no-cleanup"))) { $no_cleanup = 1; $no_md5_checks = 1; }  # assumes that config has already been copied to fworch importer
if (defined(param("-debug"))) { $debug_level = param("-debug"); }
if (defined(param("-configfile"))) { $configfile = param("-configfile"); }
if (defined(param("-csvonly"))) { $csvonly = 1; $do_not_copy = 1; $no_md5_checks = 1; }	# for testing - only run db import from existing csv files

# set basic parameters (read from import.conf)
my $fworch_workdir	= &read_config('fworch_workdir') . "/$mgm_id";
my $archive_dir	= &read_config('archive_dir');
my $cfg_dir	= "${fworch_workdir}/cfg";
my $audit_log_file = "auditlog.export"; 
my $bin_path	= &read_config('simple_bin_dir') . "/";
my $save_import_results_to_file = &read_config('save_import_results_to_file');
my $new_md5sum = 1;
my $stored_md5sum_of_last_import = 2;
my $rulebases;

# get import info
($error_count_local, $error_str_local, $mgm_name, $dev_typ_id, $obj_file_base,$obj_file,$user_file_base,$user_file,$rule_file_base,$rule_file,
		$csv_zone_file, $csv_obj_file, $csv_svc_file, $csv_usr_file, $csv_auditlog_file,
		$ssh_hostname,$ssh_user,$ssh_private_key,$ssh_public_key,$hersteller,$is_netscreen, $config_files, $ssh_port, $config_path_on_mgmt) =
		&get_import_infos_for_mgm($mgm_id, $fworch_workdir, $cfg_dir);
$error_count_global = &error_handler_add(undef, $error_level = 5, "mgm-id-not-found: $mgm_id", $error_count_local, $error_count_global);

# check if device is a legacy device, otherwise exit here without doing anything
if (grep {$_ eq $dev_typ_id} (11,12,13)) {
	output_txt("Management $mgm_name (mgm_id=$mgm_id, dev_typ_id=$dev_typ_id): not a legacy device type, skipping\n");
	exit (0);
}

my $import_was_already_running = (&is_import_running($mgm_id))?1:0;
my $initial_import_flag = &is_initial_import($mgm_id);
$current_import_id  = &insert_control_entry($initial_import_flag,$mgm_id);	# set import lock


output_txt ("current_import_id=$current_import_id");
$error_count_global = &error_handler_add($current_import_id, $error_level = 3, "set-import-lock-failed", !defined($current_import_id), $error_count_global);
$error_count_global = &error_handler_add($current_import_id, $error_level = 2, "import-already-running: $mgm_name (ID: $mgm_id)",
	$import_was_already_running, $error_count_global);

if (!$error_count_global) {
	require "CACTUS/FWORCH/import/$hersteller.pm"; 	# load the matching parser at run time
	$rulebases = &get_rulebase_names($mgm_id, $CACTUS::FWORCH::dbdriver, $fworch_database, $fworch_srv_host, $fworch_srv_port, $fworch_srv_user, $fworch_srv_pw);
	if (!$do_not_copy && !$csvonly) {
		rmtree($fworch_workdir); make_path($fworch_workdir,{mode => 0700}); make_path($cfg_dir,{mode => 0700});
		$error_count_global = &error_handler_add($current_import_id, $error_level = 2, "copy-ssh-keys-failed",
				$error_count_local = &put_ssh_keys_in_place ($fworch_workdir, $ssh_public_key, $ssh_private_key), $error_count_global);
		if (!$initial_import_flag) {
			$prev_imp_id	= exec_pgsql_cmd_return_value("SELECT get_last_import_id_for_mgmt($mgm_id)");
			$prev_imp_time	= exec_pgsql_cmd_return_value("SELECT start_time FROM import_control WHERE control_id=$prev_imp_id AND successful_import");
		}
		# 1) read names of rulebases of each device from database
		# copy config data from management system to fworch import system
		if ($configfile ne "") {
			system ("${bin_path}scp $configfile $cfg_dir/$obj_file_base");
		}
		else {
			($error_count_local, $config_files_str) =
				&CACTUS::FWORCH::import::parser::copy_config_from_mgm_to_iso 
					($ssh_user, $ssh_hostname, $mgm_name, $obj_file_base, $cfg_dir, $rule_file_base,
						$fworch_workdir, $audit_log_file, $prev_imp_time, $ssh_port, $config_path_on_mgmt, $rulebases, $debug_level);	# TODO: add use_scp parameter
		}
		if ($error_count_local) {
			if ($is_netscreen) { 		# file-check wg. Netscreen-Return-Code eingebaut
				my $file_size = -s "$cfg_dir/$obj_file_base";
				if (!defined ($file_size) || $file_size==0) {
					$error_count_global = &error_handler_add($current_import_id, $error_level = 3, "netscreen-copy-config-failed: $mgm_name, $error_str_local",
						$error_count_local=1, $error_count_global);
				}
			} else {
				$error_count_global = &error_handler_add($current_import_id, $error_level = 3, "copy-config-failed: $mgm_name, $error_str_local",
					$error_count_local=1, $error_count_global);
			}
		}
		# check if config has changed
		$stored_md5sum_of_last_import = exec_pgsql_cmd_return_value ("SELECT last_import_md5_complete_config FROM management WHERE mgm_id=$mgm_id");
		if (!defined($stored_md5sum_of_last_import)) { $stored_md5sum_of_last_import = -1; }
		# clear stored md5 hash in any case to force import during next run even if transfer of config fails this time
		&exec_pgsql_cmd_no_result("UPDATE management SET last_import_md5_complete_config='cleared' WHERE mgm_id=$mgm_id"); 
		&iconv_config_files_2_utf8($config_files_str, $fworch_workdir);	# convert config files from latin1 to utf-8
		
		$new_md5sum = &calc_md5_of_files($config_files_str, $fworch_workdir); # fworch_workdir is here tmpdir
		$error_count_global = &error_handler_add ($current_import_id, $error_level = 3, "calc-md5sum-failed", (defined($new_md5sum))?0:1, $error_count_global);
	} # end of do_not_copy / $csvonly
	if (!$error_count_global) {
		if ($new_md5sum ne $stored_md5sum_of_last_import || $no_md5_checks) {
			if (!$csvonly) 
			{
				$start_time = time(); # parse start time
				output_txt("---------------------------------------------------------------------------\n"); 
				output_txt("Starting import of management: $mgm_name\n");
				# 2) parse config
				$error_count_local = &CACTUS::FWORCH::import::parser::parse_config ($obj_file, $rule_file, $user_file, $rulebases, $fworch_workdir, $debug_level, $mgm_name, $cfg_dir,
										$current_import_id, "$cfg_dir/$audit_log_file", $prev_imp_time, $fullauditlog, $debug_level);
				if ($error_count_local) { 
					$error_count_global = &error_handler_add(	$current_import_id, $error_level = 3, "parse-$error_count_local", $error_count_local=1, $error_count_global);
					if (defined($current_import_id)) 
					{
						$error_count_local = &exec_pgsql_cmd_no_result("SELECT remove_import_lock($current_import_id)");
					}
					$error_count_global = &error_handler_add
						($current_import_id, $error_level = 3, "remove-import-lock-failed: $error_count_local", $error_count_local, $error_count_global);
				}
				output_txt("Parsing done in " . sprintf("%.2f",(time() - $start_time)) . " seconds");
			}
			if (!$error_count_global) {
				if (!$csvonly) 
				{
					&set_last_change_time($last_change_time_of_config, $current_import_id); # Zeit eintragen, zu der die letzte Aenderung an der Config vorgenommen wurde (tuning)
				}
				# starting import from csv to database
				# 3a) fill import tables with bulk copy cmd from csv
				if ($clear_all_rules) # clear rule csv files, if we want to enforce an initial import
				{ 
					find(\&empty_rule_files,$fworch_workdir); 
					print("clearing rule files\n");
				} 
				if ($clear_whole_mgm_config)  # clear all data to force initial import
				{
					find(\&empty_config_files,$fworch_workdir); 
					print("clearing all csv files\n");
				}
				if (-e $csv_usr_file) { &iconv_2_utf8($csv_usr_file, $fworch_workdir); }		# utf-8 conversion of user data

				# if $csvonly is set: replace import id in all csv files with $current_import_id
				if ($csvonly) 
				{
					my @rulebase_basenames = split(/,/, get_local_ruleset_name_list($rulebases));
					my @rulebase_fullnames = ();
					for my $filename (@rulebase_basenames) {
						@rulebase_fullnames = (@rulebase_fullnames, $fworch_workdir . '/' . $filename . '_rulebase.csv' );
					}
					for my $csvfile (($csv_zone_file, $csv_obj_file, $csv_svc_file, $csv_usr_file, @rulebase_fullnames)) {
						# print ("replacing import_id in csvfile=$csvfile\n");
						if (-e $csvfile) {
							replace_import_id_in_csv($csvfile, $current_import_id);
						}
					}
				}

				$error_count_local = &fill_import_tables_from_csv($dev_typ_id,$csv_zone_file, $csv_obj_file, $csv_svc_file, $csv_usr_file, $rulebases, $fworch_workdir, $csv_auditlog_file);

				# 3b) if an error occured, import everything in single sql statement steps to be able to spot the error
				if ($error_count_local) { 
					$error_count_global = &error_handler_add ($current_import_id, $error_level = 3, "first problems while filling database: $error_count_local",
						$error_count_local, $error_count_global);
					if ($show_all_import_errors) {

						&fill_import_tables_from_csv_with_sql ($dev_typ_id,$csv_zone_file, $csv_obj_file, $csv_svc_file, $csv_usr_file, $rulebases, $fworch_workdir, $csv_auditlog_file);

					}
				}
				# 4) wrapping up
				if (!$error_count_global) {  # import ony when no previous errors occured
					$error_count_local = 0;						
					if (!&exec_pgsql_cmd_return_value("SET client_min_messages TO NOTICE; SELECT import_all_main($current_import_id)")) {
						$error_count_local = 1;
						print("first import run found errors; re-running import with DEBUG option\n");
						&exec_pgsql_cmd_return_value("SET client_min_messages TO DEBUG1; SELECT import_all_main($current_import_id)");
						print("second import run with debugging completed\n");
					} else {
						print("found no errors during import\n");
					}
					$error_count_global = &error_handler_add ($current_import_id, $error_level = 3, "",	$error_count_local, $error_count_global);
					$changes = &exec_pgsql_cmd_return_value("SELECT show_change_summary($current_import_id)");
					# updating md5sum
					if (!$error_count_global) { &exec_pgsql_cmd_no_result("UPDATE management SET last_import_md5_complete_config='$new_md5sum' WHERE mgm_id=$mgm_id"); }
				}
			}
			# output results and set import status
			if ($changes ne '') { output_txt("Changes: $changes\n"); }
			if ($error_count_global) { output_txt ("Import of management $mgm_name (mgm_id=$mgm_id, import_id=$current_import_id): FOUND $error_count_global error(s)\n", 2); }
			else {
				my $changes_found = 0;
				if ($changes ne '') { $changes_found = 1; }
				output_txt ("Import of management $mgm_name (mgm_id=$mgm_id, import_id=$current_import_id)" .
						", total time: " . sprintf("%.2fs",(time() - $first_start_time)) . "): no errors found" . (($changes_found )?"":", no changes found") . "\n");
				my $sql_cmd = 'UPDATE import_control SET successful_import=TRUE' . (($changes_found)? ', changes_found=TRUE':'') . ' WHERE control_id=' . $current_import_id;
				&exec_pgsql_cmd_no_result($sql_cmd); # if no error occured - set import_control.successful_import to true
			}
		} else {
			output_txt("Management $mgm_name (mgm_id=$mgm_id), no changes in configuration files (MD5)\n");
			&exec_pgsql_cmd_no_result("DELETE FROM import_control WHERE control_id=$current_import_id"); # remove imports with unchanged data
			&exec_pgsql_cmd_no_result("UPDATE management SET last_import_md5_complete_config='$new_md5sum' WHERE mgm_id=$mgm_id");
		}
	}
	# Cleanup and statistics
	if (defined($current_import_id)) 
	{
		&exec_pgsql_cmd_no_result("SELECT remove_import_lock($current_import_id)");   # this sets import_control.stop_time to now()
	}
	&clean_up_fworch_db($current_import_id);
	if (defined($save_import_results_to_file) && $save_import_results_to_file && ($error_count_global || $changes ne '')) { # if changes or errors occured: move config & csv to archive
		system ("${bin_path}mkdir -p $archive_dir; cd $fworch_workdir; ${bin_path}tar cfz $archive_dir/${current_import_id}_`${bin_path}date +%F_%T`_mgm_id_$mgm_id.tgz .");
	}
	#`cp -f $fworch_workdir/cfg/*.cfg /var/itsecorg/fw-config/`; # special backup for several configs - dos-box
	if (!$no_cleanup) { rmtree $fworch_workdir; }
} else {
	if (defined($current_import_id)) 
	{
		&exec_pgsql_cmd_no_result("SELECT remove_import_lock($current_import_id)");   # this sets import_control.stop_time to now()
	}
}
exit ($error_count_global);
