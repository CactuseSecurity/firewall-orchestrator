
package CACTUS::FWORCH::import::parser;

use strict;
use warnings;
use IO::File;
use File::Find;

use CACTUS::FWORCH;
use CACTUS::FWORCH::import;
use Date::Calc qw(Add_Delta_DHMS Delta_DHMS);

require Exporter;
our @ISA = qw(Exporter);

our %EXPORT_TAGS = ( 'basic' => [ qw( &copy_config_from_mgm_to_iso &parse_config ) ] );

our @EXPORT = ( @{ $EXPORT_TAGS{'basic'} } );

# variblendefinition parser
# -------------------------------------------------------------------------------------------

our $fwobj_file_extension			= '.fwobj';
our $fwobj_file_pattern				= '\*\.fwobj';

our $forwarding_rule_file_extension	= '.fwrule';
our $forwarding_rule_file_pattern	= '\*\.fwrule';

our $rev_marker						= ',v'; 	# RCS files
our $rev_fwobj_file_extension		= '.fwobj,v';
our $rev_fwobj_file_pattern			= '\*\.fwobj\,v';

our $rev_rule_file_extension		= '.fwrule,v';
our $rev_rule_file_pattern			= '\*\.fwrule\,v';

our $debug_level = 0;

# our $local_rule_file_extension	= '.lfwrule7';
# our $local_rule_file_pattern	= '\*\.lfwrule7';

our $GROUPSEP				= "|";				# globale Einstellung fuer Seperator in Gruppen;
our $config_path_praefix;
our $last_import_time;
our $last_change_admin;
# our @UTC_diff_identifier = ();
our $UTC_diff;
our $scope; 

# Stati und anderes des Objektparsers
# -------------------------------------------------------------------------------------------
our $parse_obj_state 	= 0;	# moegliche Werte	0	kein status
								#			1	objectclass gestartet
								#			2	object gestartet
								#			3...5	diverse attribute & werte (kontextsensitiv)
our $parse_obj_type; 			# State 1 - aktuelle Objektklasse
our $parse_obj_id;				# State 2 - id des aktuellen objektes ( name__uid__uid )
our $parse_obj_name;				# State 2 - name des aktuellen objektes
our $parse_obj_attr;				# State 3 - Attribut
our $parse_obj_attr_value;		# State 3 - Wert des Attributes
our $parse_obj_attr_ext;			# State 4 - Attributerweiterung (kontextsensitiv)
our $parse_obj_attr_ext_value; 	# State 4 - Wert des erweiterten Attributes (kontextsensitiv)
our $parse_obj_neglist;			# Flag fuer negierte Liste
our $collecting_group_members = 0;	# flag
our $explicit_flag = 0;
our $create_explicit_element_flag = 0;
our $create_explicit_element_proto;
our $member_counter = 0;		# zaehlt #Gruppenmitglieder
our	$convert_simple_to_group_flag = 0;	# Flag, um bei Bedarf ein als simple angefangenes Objekt zur Gruppe zu machen
our $proto_of_explicit_svc_group;

# rule specific
our $parserule_rulenum;
our $parse_rule_field_details;
our $parse_rule_field;
our $config_files_str;

our $sublist_flag = 0;
our $rulelist_flag = 0;
our $rulelist_name = '';
our $indent = '';
our @subrulebases = ();	# local for checking on subrule lists

## parse_audit_log Funktion fuer phion noch nicht implementiert
sub parse_audit_log { }
	
############################################################
# copy_config_from_mgm_to_iso($ssh_private_key, $ssh_user, $ssh_hostname, $management_name, $obj_file_base, $cfg_dir, $rule_file_base)
# Kopieren der Config-Daten vom Management-System zum ITSecorg-Server
############################################################
sub copy_config_from_mgm_to_iso {
	my $ssh_user = shift;
	my $ssh_hostname = shift;
	my $management_name = shift;
	my $obj_file_base = shift;
	my $cfg_dir = shift;
	my $rule_file_base = shift;
	my $workdir = shift;
	my $auditlog		= shift;	# unused in phion
	my $prev_import_time= shift;	# unused in phion
	my $ssh_port		= shift;
	my $config_path_on_mgmt		= shift; # unused in phion
	
	my $cmd;
	my $return_code;
	my $fehler_count = 0;
		
	my $tar_archive = 'iso-phion-config.tgz';

	my $tar_cmd = 'tar cfz '.$tar_archive.' \`find . -type f ' .
		'-name "' . $fwobj_file_pattern . '" ' .
		'-o -name "' . $forwarding_rule_file_pattern . '" ' .
		'-o -name "' . $rev_fwobj_file_pattern . '" ' .
		'-o -name "' . $rev_rule_file_pattern . '" ' .
		' \`';

	if (!defined($ssh_port) || $ssh_port eq '') {
		$ssh_port = "22";
	}

	$cmd = "$ssh_bin -i $workdir/$CACTUS::FWORCH::ssh_id_basename -p $ssh_port $ssh_user\@$ssh_hostname $tar_cmd";
	$fehler_count += (system ($cmd) != 0);

	$cmd = "$scp_bin -i $workdir/$CACTUS::FWORCH::ssh_id_basename -P $ssh_port $ssh_user\@$ssh_hostname:$tar_archive $cfg_dir";
	$fehler_count += (system ($cmd) != 0);

	$cmd = "$ssh_bin -i $workdir/$CACTUS::FWORCH::ssh_id_basename -p $ssh_port $ssh_user\@$ssh_hostname rm $tar_archive";
	$fehler_count += (system ($cmd) != 0);

	$cmd = "cd $cfg_dir; tar xfz $tar_archive; rm $tar_archive";
	$fehler_count += (system ($cmd) != 0);
	
	find(\&collect_config_files, $cfg_dir);		# erzeugt einen String mit allen gefundenen Config-Files
	$UTC_diff = &get_UTC_diff_unix($ssh_user, $ssh_hostname, $ssh_port, "$workdir/$CACTUS::FWORCH::ssh_id_basename"); # hole UTC-Verschiebung mittels date-Befehl (globale Var setzen)
	return ($fehler_count,$config_files_str);
}

############################################################
# get_UTC_diff_unix($ssh_private_key, $ssh_user, $ssh_hostname, $management_name, $work_dir)
# liefert die Verschiebung in Stunden zu UTC
############################################################
sub get_UTC_diff_unix {
	my $ssh_user = shift;
	my $ssh_hostname = shift;
	my $ssh_port = shift;
	my $ssh_priv_key_file = shift;
	
	return (`$ssh_bin -i $ssh_priv_key_file -p $ssh_port $ssh_user\@$ssh_hostname date +\"%z\"` / 100); # Minuten "wegdividieren" (%z Format: +0200)
}

sub collect_config_files {
	if ( -f $File::Find::name  && $File::Find::name !~ /$rev_marker/ ) {
		if (defined($config_files_str)) {
			$config_files_str .= ("," . $File::Find::name);
		} else {
			$config_files_str = $File::Find::name;
		}
	}
}


#####################################################################################
# Start Parser 
#####################################################################################

sub parse_config {
	shift;   # ignore filename
	my $rulebase_file = shift;
	my $user_db_file = shift;
	my $rulebase_name = shift;
	my $output_dir = shift;
	my $debug = shift;
	my $mgm_name = shift;
	my $cfg_dir = shift;
	my $import_id = shift;
	my $audit_log_file= shift;
	my $last_import_time_local = shift;
	
	$debug_level = $debug;
	# setting global vars as find cannot take any parameters
	$config_path_praefix = $cfg_dir;
	$last_import_time = $last_import_time_local;
	
	# parse all files and fill structures (hashes, arrays)
	find(\&process_basic_object_files, $cfg_dir);
#	exit 1;
	find(\&process_forwarding_rule_files, $cfg_dir);
#	find(\&process_local_rule_files,$cfg_dir);
	&fix_bidir_rules();
	&add_basic_object_oids_in_rules_for_locally_defined_objects();
	&fix_rule_references();
	&link_subset_rules();
	&section_titles_correction();

	# write structures to file (debug: and screen)
#	print_results_monitor('objects');
#	print_results_monitor('rules');
	&print_results_files_objects($output_dir, $mgm_name, $import_id);
#	&print_results_files_users($output_dir, $mgm_name, $import_id);	
	&print_results_files_rules($output_dir, $mgm_name, $import_id);
	return 0; # done without errors
}

sub process_basic_object_files {
	if ($File::Find::name =~ /^$config_path_praefix\/(.*?)$/) {
		$scope = $1;
#		$flat_file =~ s/\//\_/g;
#		$scope = $flat_file;
		if ($scope =~ /FactoryDefault/ || 
			$scope =~ /^Revision/ ||
			$scope =~ /^zrepo/	) { # alle FactoryDefaults, Revisions und Revisions - ignorieren	
			if ($scope !~ /^Revision/) { print_debug ("ignoring basic element config file $scope ", $debug_level, 5); }
			return;
		} elsif ( -f $File::Find::name || -l $File::Find::name ) {
			if ( -e $File::Find::name ) {
				print_debug ("fwobj parser: processing file " . $File::Find::name, $debug_level, 1);	
#				if ($File::Find::name =~ /^$config_path_praefix\/(.*?)$/) { $scope = $1; }	
				&get_change_admin($File::Find::name, $last_import_time);
#				output_txt ("change_admin: $change_admin");
				&parse_basic_elements ($File::Find::name);
			} else {
				output_txt ("ERROR: iso-importer: phion.pm: cannot access file $File::Find::name\n", 2);			
			}
		}
	}
}

sub process_rule_files {
	my $extension = $_[0];
	
	if ($File::Find::name =~ /$extension$/) {
		if ($File::Find::name =~ /^$config_path_praefix\/(.*?)$/) {
			$scope = $1;
			$scope =~ s/\//\_/g;
#			$scope = $flat_file;
		}
		if ($scope =~ /FactoryDefault/ || 
			$scope =~ /^Revision/ ||
			$scope =~ /^zrepo/	) { # alle FactoryDefaults, Revisions und Revisions - ignorieren	
			if ($scope !~ /^Revision/) { print_debug ("ignoring ruleset file $scope", $debug_level, 3); }
			return;
		} elsif ( -f $File::Find::name || -l $File::Find::name ) {
			if ( -e $File::Find::name ) {
				print_debug ("rule parser: processing file " . $File::Find::name, $debug_level, 1);	
				@rulebases = (@rulebases, $scope);
				$parserule_rulenum = 0;
				&get_change_admin($File::Find::name, $last_import_time);
#				output_txt ("change_admin: $change_admin");
				&parse_rules ($File::Find::name);
			} else {
				output_txt ("ERROR: iso-importer: phion.pm: cannot access file $File::Find::name\n", 2);			
			}
		}
	}
}

sub conv_time_UTC {
	my $UTC_diff = shift;
	my $year = shift;
	my $month = shift;
	my $day = shift;
	my $hour = shift;
	my $min = shift;
	my $sec= shift;
	
	($year, $month, $day, $hour, $min, $sec) =  Add_Delta_DHMS( $year, $month, $day, $hour, $min, $sec, 0, $UTC_diff, 0, 0 );
	return sprintf("%04d-%02d-%02d %02d:%02d:%02d", $year, $month, $day, $hour, $min, $sec);
}

sub get_change_admin { # sets name of last_change admin for current config file in $last_change_admin global var ("" if none)
	my $cfg_file = $_[0];
	my $last_ch_time = $_[1];
	my $rev_file;
	my $line;
	my %change;
	my @change_arr = ();
	my $number_of_changes_found = 0;
	my $result_lc_admin = "";
	my $fehler = 0;
	
	if ($cfg_file =~ /^$config_path_praefix\/(.*)\/([a-zA-Z0-9\_\-\.]+)$/) {
		my $path_to_file = $1;
		my $basename = $2;
		$rev_file = "$config_path_praefix/Revision/$path_to_file/RCS/$basename,v";
		open (IN, $rev_file) or $fehler = "Cannot open file $rev_file for reading: $!";
		if ($fehler) {
			print ("WARNING: $fehler\n");
			close (IN);
		} else {
			PARSE_LOOP: while ( <IN> ) {
				$line = $_;					# Zeileninhalt merken
				if ( $line =~ /^date\s+(\d\d\d\d)\.(\d\d)\.(\d\d)\.(\d\d)\.(\d\d)\.(\d\d)\;\s+author\s+(\w+?)\_/ ) {
					my $year = $1; my $month = $2; my $day = $3; my $hour = $4; my $min = $5; my $sec = $6;
					my $sql_time = &conv_time_UTC ( $UTC_diff, $year, $month, $day, $hour, $min, $sec );
					if ($sql_time gt $last_ch_time) { # change time lies behind last import time - so proceed
						my $local_last_change_admin = $7;
						$change{"$sql_time.admin"} = $local_last_change_admin;
						@change_arr = (@change_arr, $sql_time);
						$number_of_changes_found ++;
					} else { last PARSE_LOOP; }
				}	
				if ( $line =~ /^next\s+\;$/ ) {	last PARSE_LOOP; }			
			} # end of while
			close (IN);
			if ($number_of_changes_found > 0) {
				my %seen = (); # only helper variable for unique operation below
				my @list_of_admins = values %change;
				my @unique_list_of_admins = grep {! $seen{$_}++} @list_of_admins;
				my $no_of_admins = $#unique_list_of_admins + 1;
				if ($no_of_admins == 1) {
					my $time = $change_arr[0];
					$result_lc_admin = $change{"$time.admin"};
				} else {
					output_txt ("warning: found $no_of_admins (>1) change-admin in $cfg_file since last import: " . join (':', @unique_list_of_admins), 2);
				}
			}
		}
		$last_change_admin = $result_lc_admin;
	}
	return;
}

sub process_forwarding_rule_files { &process_rule_files($forwarding_rule_file_extension); }
# sub process_local_rule_files { &process_rule_files($local_rule_file_extension); }


sub normalize_phion_proto {
	my $proto_str_in = shift;
	my $typ = '';
	my $proto = '';
			
	if (($proto_str_in eq 'echo')) 		{ $typ = 'simple'; $proto =  1;}
	elsif ($proto_str_in eq 'tcp') 		{ $typ = 'simple'; $proto =  6;}
	elsif (($proto_str_in eq 'udp')) 	{ $typ = 'simple'; $proto = 17;}
	elsif (($proto_str_in eq 'group')) 	{ $typ = 'group'; }
#	elsif (($proto_str_in eq 'other')) {  # from ServiceEntryOther: wait for proto one level below
	return ($typ, $proto);
}

sub normalize_phion_ports {
	my $port_in = shift;
	my $port = '';
	my $port_last = '';
	
	if ($port_in  =~ /^\s(.*?)$/) { $port_in = $1; } # remove space at beginning
	if ($port_in =~ /^(\d+)\-(\d+)$/) {
		$port = $1;		$port_last = $2;
	} elsif ($port_in =~ /^(\d+)\s(\d+)$/) { # TODO: das ist jetzt "very dirty", weil es eigentlich nur zwei Ports sind und nicht alle Ports von $1 bis $2
		$port = $1;		$port_last = $2;
	} elsif ($port_in =~ /^\*$/) {
		$port = 0; 		$port_last = 65535;
	} else {
		$port = $port_in;
	}	
	return ($port,$port_last);
}	

sub analyze_phion_ip {
	my $addr_in = shift;
	my ($addr, $addr_last, $type);
	if (!defined($addr_in)) {
		return (undef,undef,undef);
	}
	my ($net,$mask);
	($net,$mask) = split /\//, $addr_in;
#	print_debug ("analyze_phion_ip: start addr_in: '$addr_in'", $debug_level, 0);
	if (defined ($mask)) {
		my $max_mask;
		if ($net =~ /\:/) {
			$max_mask = 128;
		} else {
			$max_mask = 32;
		}
		if ($mask =~ /\d+/) {
			$mask = $max_mask - $mask;   # umrechnen von verdrehter phion maske auf normale maske
			if ($mask == 2 || $mask==3) { print_debug("warning mask=2 or 3: $net/$mask", $debug_level, 3); }
			$addr = "$net/$mask";
			$type = 'network';
		} else {
			print_debug("non numeric mask found in ip $addr_in: $mask", $debug_level, 0);
			return (undef,undef,undef);
		}
	} elsif ($addr_in =~ /(\d+\.\d+\.\d+\.)(\d+)\-([\d\.]+)/) {
		$addr = "$1$2";
		$addr_last = $3;
		if ($addr_last !~ /\./) { # second ip contains only last byte
			$addr_last = "$1$3";
		}
		$type = 'host'; # keep range type for later implementation
	} elsif ($addr_in =~ /[\d\.\:a-f]+/) {
		$addr = $addr_in;
		$addr_last = undef;
		$net = undef;
		$type = 'host';
	} else {
		print_debug ("analyze_phion_ip: found invalid ip address \'$addr_in\'", $debug_level, 0);
		return (undef,undef,undef);
	}
#	print_debug ("analyze_phion_ip: end addr: '$addr', type='$type'", $debug_level, 0);
	return ($type,$addr,$addr_last);
}

#****************************
# Verarbeitung / Aufbereitung der identifizierten Parameter und Values (kontextsensitiv)
#****************************
# store_results
sub store_results {
	if (!defined($parse_obj_type)) {
		print_debug ("error store_results: parse_obj_type not defined", $debug_level, 0);
		return;
	}
	if ($parse_obj_type ne 'rules') {
		if (!defined($parse_obj_id)) {
			print_debug ("error: parse_obj_id not defined; type: " . defined($parse_obj_type)?$parse_obj_type:'undefined' . ", name: " .
				defined($parse_obj_name)?$parse_obj_name:'undefined' . ", attr: " . defined($parse_obj_attr)?$parse_obj_attr:'undefined' . 
				", value: " . defined($parse_obj_attr_value)?$parse_obj_attr_value:'undefined', $debug_level, 0);
			return;
		}
		if ($parse_obj_state < 3) {
			print_debug("Warnung: store_results mit obj_state<3 aufgerufen", $debug_level, 1);
			return;
		}
	}
	my $debug_string = "name: " . (defined($parse_obj_name)? $parse_obj_name : 'undefined') . ", attr: " .
		(defined($parse_obj_attr)? $parse_obj_attr : 'undefined') . ", value: " . (defined($parse_obj_attr_value)? $parse_obj_attr_value : 'undefined');
	print_debug ($debug_string, $debug_level, 8);
	if ($parse_obj_type eq "netobj" || $parse_obj_type eq "connobj")	{ &store_results_nw_objects(); }
	elsif ($parse_obj_type eq "srvobj")	{ &store_results_nw_services(); }
	elsif ($parse_obj_type eq "rules")	{ &store_results_rules(); }
	else { 
		print_debug("store_results: found unknown parse_obj_type=$parse_obj_type",$debug_level,2);
	}
}

sub store_results_nw_objects {
	if (!defined ($network_objects{"$parse_obj_id.name"})) { 	# schon ein bekanntes network_object?
		@network_objects = (@network_objects, $parse_obj_id);
		$network_objects{"$parse_obj_id.name"}	= $parse_obj_name;
		$network_objects{"$parse_obj_id.scope"}	= $scope;
		$network_objects{"$parse_obj_id.typ"}	= 'simple';  # setting default values (for NetESet objects)
		$network_objects{"$parse_obj_id.type"}	= 'host';
		$network_objects{"$parse_obj_id.UID"}	= $parse_obj_id;  # default Wert nur wichtig fuer explizite Objekte
		$network_objects{"$parse_obj_id.last_change_admin"}	= $last_change_admin;
	}
	if ($convert_simple_to_group_flag) {
		# altes Objekt in Gruppe umwandeln
		$network_objects{"$parse_obj_id.typ"}	= 'group';
		$network_objects{"$parse_obj_id.type"}	= 'group';
		# zwischenspeichern der IP des ersten Elements
		my $addr = '';
		if (defined($network_objects{"$parse_obj_id.ipaddr"})) { # Speichern der Adresse des ersten Elements
			$addr = $network_objects{"$parse_obj_id.ipaddr"};
			$network_objects{"$parse_obj_id.ipaddr"} = '';
		}
		
		# Anlegen eines neuen Objekts fuer das erste Element
		my $obj_mbr_id = $parse_obj_id . '__' . $addr;		
		@network_objects = (@network_objects, $obj_mbr_id);
		$network_objects{"$obj_mbr_id.name"}	= $parse_obj_name . '__' . $addr;
		$network_objects{"$obj_mbr_id.scope"}	= $scope;
		$network_objects{"$obj_mbr_id.typ"}		= 'simple';
		$network_objects{"$obj_mbr_id.UID"}		= $parse_obj_id . '__' . $addr;
		$network_objects{"$obj_mbr_id.last_change_admin"}	= $last_change_admin;
		if ($addr =~ /\/32/ || $addr =~ /\.\d+$/ || $addr =~ /\:[\da-f]+$/i || $addr =~ /\/128/) {
			print_debug ("1 rewriting type to host '$addr', name=" . $network_objects{"$obj_mbr_id.name"} . ", orig type=" . $network_objects{"$parse_obj_id.type"}, $debug_level, 7);
			$network_objects{"$obj_mbr_id.type"} = 'host';
		} elsif ($addr eq '') {
			print_debug ("2 rewriting type to group '$addr', name=" . $network_objects{"$obj_mbr_id.name"} . ", orig type=" . $network_objects{"$parse_obj_id.type"}, $debug_level, 7);			
			$network_objects{"$obj_mbr_id.type"} = 'group';
		} elsif ($addr =~ /\/\d+$/) {
			print_debug ("3 rewriting type to network '$addr', name=" . $network_objects{"$obj_mbr_id.name"} . ", orig type=" . $network_objects{"$parse_obj_id.type"}, $debug_level, 7);			
			$network_objects{"$obj_mbr_id.type"} = 'network';			
		} else {
			print_debug ("4 leaving type as was '$addr', name=" . $network_objects{"$obj_mbr_id.name"} . ", orig type=" . $network_objects{"$parse_obj_id.type"}, $debug_level, 2);			
			$network_objects{"$obj_mbr_id.type"} = $network_objects{"$parse_obj_id.type"};	### old line
		}	
		$network_objects{"$obj_mbr_id.ipaddr"} = $addr;
		$network_objects{"$obj_mbr_id.ipaddr_last"} = $network_objects{"$parse_obj_id.ipaddr_last"};
		$network_objects{"$parse_obj_id.members"}		= $parse_obj_name . '__' . $addr; 		# jetzt das erste Element als einzigen Member hinzufuegen
		$network_objects{"$parse_obj_id.member_refs"}	= $obj_mbr_id;
		
		$convert_simple_to_group_flag = 0;		
	}	# das zweite Element wird anschliessend (bei addr-Zeile in state 5) normal als member hinzugefuegt
	# Daten im Hash ergaenzen
	elsif ($collecting_group_members) {
		if ($parse_obj_attr eq 'ref') {
			if (defined($network_objects{"$parse_obj_id.members"})) {
				$network_objects{"$parse_obj_id.members"} .= ( $GROUPSEP . $parse_obj_attr_value );
			} else {
				$network_objects{"$parse_obj_id.members"} = $parse_obj_attr_value;
				$network_objects{"$parse_obj_id.typ"} = 'group';
				$network_objects{"$parse_obj_id.type"} = 'group';
			}
		} elsif ($parse_obj_attr eq 'refid') {
			if (defined($network_objects{"$parse_obj_id.member_refs"})) {
				$network_objects{"$parse_obj_id.member_refs"} .= ( $GROUPSEP . $parse_obj_attr_value );
			} else {
				$network_objects{"$parse_obj_id.member_refs"} = $parse_obj_attr_value;
			}
		} elsif (($parse_obj_attr eq 'addr' || $parse_obj_attr eq 'addr6') && $parse_obj_attr_value ne '0.0.0.0') { # explizite Gruppe
			# create new object
			my ($tmp_addr, $tmp_addr_last, $tmp_type);
			my $parse_obj_member_name;
			my $parse_obj_member_id;
			
			($tmp_type,$tmp_addr,$tmp_addr_last) = analyze_phion_ip($parse_obj_attr_value);
			$parse_obj_member_name = $parse_obj_name . '__' . $tmp_addr;
			$parse_obj_member_id = $parse_obj_id . '__' . $tmp_addr;
			if (defined($tmp_addr_last)) {
				$parse_obj_member_name .= "-$tmp_addr_last";
				$parse_obj_member_id .= "-$tmp_addr_last";
			}
			$network_objects{"$parse_obj_member_id.type"} = $tmp_type;
			$network_objects{"$parse_obj_member_id.ipaddr"} = $tmp_addr;
			$network_objects{"$parse_obj_member_id.ipaddr_last"} = $tmp_addr_last;
			print_debug ("\nfound new explicit nw_obj group member: $parse_obj_member_id", $debug_level, 8);
			@network_objects = (@network_objects, $parse_obj_member_id);
			$network_objects{"$parse_obj_member_id.name"}	= $parse_obj_member_name;
			$network_objects{"$parse_obj_member_id.scope"}	= $scope;
			$network_objects{"$parse_obj_member_id.typ"}	= 'simple';
			$network_objects{"$parse_obj_member_id.UID"}	= $parse_obj_member_id;
			$network_objects{"$parse_obj_member_id.last_change_admin"}	= $last_change_admin;

			# jetzt die Gruppenreferenzen auf das neue Objekt setzen, sind ja schon definiert, da hier #members >= 2 
			$network_objects{"$parse_obj_id.member_refs"}	.= ( $GROUPSEP . $parse_obj_member_id );
			$network_objects{"$parse_obj_id.members"}		.= ( $GROUPSEP . $parse_obj_member_name );				
		} else {
			print_debug ("WARNING: store_nw_results called to add group member without match: $parse_obj_attr", $debug_level, 7);
		}
	} elsif (defined($parse_obj_attr_value)) {
		# Attribute aufarbeiten
		SWITCH_OBJ_ATTR: {
			if ($parse_obj_attr eq 'oid') {
				$network_objects{"$parse_obj_id.UID"} = $parse_obj_attr_value;
				last SWITCH_OBJ_ATTR;
			}
			if (($parse_obj_attr eq 'addr' || $parse_obj_attr eq 'addr6') && $parse_obj_attr_value ne '0.0.0.0') {
				$network_objects{"$parse_obj_id.typ"} = 'simple';
				($network_objects{"$parse_obj_id.type"},$network_objects{"$parse_obj_id.ipaddr"},$network_objects{"$parse_obj_id.ipaddr_last"}) =
					analyze_phion_ip($parse_obj_attr_value);
				$parse_obj_attr_value = $network_objects{"$parse_obj_id.ipaddr"};  #  necessary?
				last SWITCH_OBJ_ATTR;
			}
			if ($parse_obj_attr eq 'comment') {
				$network_objects{"$parse_obj_id.$parse_obj_attr"} = $parse_obj_attr_value; 	# stores only comments
			}
		}
	}
}

sub store_results_nw_services {
	if (!defined ($services{"$parse_obj_id.name"})) { 	# schon ein bekannter dienst?
		@services = (@services, $parse_obj_id);
		$services{"$parse_obj_id.name"} = $parse_obj_name;
		$services{"$parse_obj_id.scope"} = $scope;
		$services{"$parse_obj_id.type"} = 'group';
		$services{"$parse_obj_id.typ"} = 'group';
		$services{"$parse_obj_id.UID"}	= $parse_obj_id;  # default Wert nur wichtig fuer explizite Dienste
		$services{"$parse_obj_id.last_change_admin"} = $last_change_admin;
	}
	
	if ($convert_simple_to_group_flag) {
		# altes Objekt in Gruppe umwandeln
		$services{"$parse_obj_id.typ"}	= 'group';
		$services{"$parse_obj_id.type"}	= 'group';
		# zwischenspeichern des Zielports des ersten Elements
		my $proto = '';
		my $dport = '';
		my $dport_last = '';
		my $type = '';
		if (defined($services{"$parse_obj_id.port"})) { # Speichern der Daten des ersten Elements
			$dport = $services{"$parse_obj_id.port"};
			$services{"$parse_obj_id.port"} = '';
		}
		if (defined($services{"$parse_obj_id.port_last"})) {
			$dport_last = $services{"$parse_obj_id.port_last"};
			$services{"$parse_obj_id.port_last"} = '';
		}
		if (defined($services{"$parse_obj_id.ip_proto"})) {
			$proto = $services{"$parse_obj_id.ip_proto"};
			$services{"$parse_obj_id.ip_proto"} = '';
		}
		if (defined($services{"$parse_obj_id.type"})) {
			$type = $services{"$parse_obj_id.type"};
			$services{"$parse_obj_id.type"} = '';
		}
		my $svc_ext = "/$proto/$dport";	if ($dport_last ne '') { $svc_ext .= "-$dport_last"; }
		
		# Anlegen eines neuen Objekts fuer das erste Element
		my $obj_mbr_id = "$parse_obj_id/$scope/$svc_ext";		
		@services = (@services, $obj_mbr_id);
		$services{"$obj_mbr_id.name"}		= $parse_obj_name . $svc_ext;		
		$services{"$obj_mbr_id.scope"}		= $scope;
		$services{"$obj_mbr_id.typ"}		= 'simple';
		$services{"$obj_mbr_id.type"}		= $type;
		$services{"$obj_mbr_id.UID"}		= $obj_mbr_id;
		$services{"$obj_mbr_id.port"}		= $dport;
		$services{"$obj_mbr_id.port_last"}	= $dport_last;
		$services{"$obj_mbr_id.ip_proto"}	= $proto;
		$services{"$obj_mbr_id.last_change_admin"}	= $last_change_admin;

		# jetzt das erste Element als einzigen Member hinzufuegen
		$services{"$parse_obj_id.members"}		= $parse_obj_name . $svc_ext;
		$services{"$parse_obj_id.member_refs"}	= $obj_mbr_id;
		
		$convert_simple_to_group_flag = 0;		
	}	# das zweite Element wird anschliessend (bei addr-Zeile in state 5) normal als member hinzugefuegt
	# Daten im Hash ergaenzen
	elsif ($collecting_group_members) {
		if ($parse_obj_attr eq 'ref') {
			if (defined($services{"$parse_obj_id.members"})) {
				$services{"$parse_obj_id.members"} .= ( $GROUPSEP . $parse_obj_attr_value );
			} else {
				$services{"$parse_obj_id.members"} = $parse_obj_attr_value;
				$services{"$parse_obj_id.typ"} = 'group';
				$services{"$parse_obj_id.type"} = 'group';
			}
		} elsif ($parse_obj_attr eq 'refid') {
			if (defined($services{"$parse_obj_id.member_refs"})) {
				$services{"$parse_obj_id.member_refs"} .= ( $GROUPSEP . $parse_obj_attr_value );
			} else {
				$services{"$parse_obj_id.member_refs"} = $parse_obj_attr_value;
			}
		} elsif ($parse_obj_attr eq 'portLimit') { # explizite Gruppe
			# create new object
			my ($new_port, $new_port_last, $new_svc_ext, $new_proto);
			$new_proto = &normalize_phion_proto($proto_of_explicit_svc_group);
			($new_port, $new_port_last) = &normalize_phion_ports($parse_obj_attr_value);
			$new_svc_ext = "/$new_proto/$new_port";	if ($new_port_last ne '') { $new_svc_ext .= "-$new_port_last"; }
			my $parse_obj_member_name = $parse_obj_name . $new_svc_ext;
			my $parse_obj_member_id = "$parse_obj_id/$scope/$new_svc_ext";
			if (!defined($services{"$parse_obj_member_id.name"})) {
				@services = (@services, $parse_obj_member_id);
				$services{"$parse_obj_member_id.name"}		= $parse_obj_member_name;
				$services{"$parse_obj_member_id.scope"}		= $scope;
				$services{"$parse_obj_member_id.typ"}		= 'simple';
				$services{"$parse_obj_member_id.type"}		= (($parse_obj_attr_value =~ /\//)?'network':'host');
				$services{"$parse_obj_member_id.UID"}		= $parse_obj_member_id;
				$services{"$parse_obj_member_id.port"}		= $new_port;
				$services{"$parse_obj_member_id.port_last"}	= $new_port_last;
				$services{"$parse_obj_member_id.ip_proto"}	= $new_proto;
				$services{"$parse_obj_member_id.last_change_admin"}	= $last_change_admin;
			}

			# jetzt die Gruppenreferenzen auf das neue Objekt setzen, sind ja schon definiert, da hier #members >= 2 
			$services{"$parse_obj_id.member_refs"}	.= ( $GROUPSEP . $parse_obj_member_id );
			$services{"$parse_obj_id.members"}		.= ( $GROUPSEP . $parse_obj_member_name );				
		} else {
			print_debug ("WARNING: store_nw_results called to add group member without match: $parse_obj_attr", $debug_level, 7);
		}
	} elsif (defined($parse_obj_attr_value)) {
		# Attribute aufarbeiten
		SWITCH_OBJ_ATTR: {
			if ($parse_obj_attr eq 'oid') {
				$services{"$parse_obj_id.UID"} = $parse_obj_attr_value;
				last SWITCH_OBJ_ATTR;
			}			
			if ($parse_obj_attr eq 'portLimit') {
				($services{"$parse_obj_id.port"}, $services{"$parse_obj_id.port_last"}) =
					&normalize_phion_ports($parse_obj_attr_value);
				last SWITCH_OBJ_ATTR;
			}
			if (($parse_obj_attr eq 'botClientPort')) {
				$services{"$parse_obj_id.src_port"} = $parse_obj_attr_value;
				last SWITCH_OBJ_ATTR;
			}
			if (($parse_obj_attr eq 'topClientPort')) {
				$services{"$parse_obj_id.src_port_last"} = $parse_obj_attr_value;
				last SWITCH_OBJ_ATTR;
			}
			if ($parse_obj_attr eq 'sessionTimeout') {
				$services{"$parse_obj_id.timeout"} = $parse_obj_attr_value;
				last SWITCH_OBJ_ATTR;
			}
			if (($parse_obj_attr eq 'proto')) {
				$services{"$parse_obj_id.typ"} = 'simple';
				$services{"$parse_obj_id.ip_proto"} = $parse_obj_attr_value;
			}
			if (($parse_obj_attr eq 'type')) {
				$services{"$parse_obj_id.$parse_obj_attr"} = $parse_obj_attr_value;
				($services{"$parse_obj_id.typ"},$services{"$parse_obj_id.ip_proto"}) =
					&normalize_phion_proto($parse_obj_attr_value);
				last SWITCH_OBJ_ATTR;
			}
			# Als default das Attribut und den Wert sichern
			$services{"$parse_obj_id.$parse_obj_attr"} = $parse_obj_attr_value;
		}
	}
}

sub store_results_maplist { # ($nat_group_name, $nat_group_oid, $nat_group_comment, $mapping_details, $debug_level);
	my $nat_obj_name = shift;
	my $nat_obj_uid = shift;
	my $nat_obj_comment = shift;
	my $list_of_nat = shift;
	my $debug_level = shift;
	my $member_count = 0;
	my $remaining_nat_ip_line = $list_of_nat;
	
	if (!defined($debug_level)) { $debug_level = 0; }

	if (!defined($nat_obj_name) || !defined($nat_obj_uid)) { return; }
	if (!defined($nat_obj_comment)) { $nat_obj_comment = ''; }
	print_debug("entering store_results_maplist: nat obj name=$nat_obj_name, nat obj uid=$nat_obj_uid, comment=$nat_obj_comment, mapList=$list_of_nat", $debug_level, 5);
	while ($remaining_nat_ip_line =~ /^\s*(\d+\.\d+\.\d+\.\d+)\-\>\d+\.\d+\.\d+\.\d+\s?(.*)/) { # multiple ip nattings
		$parse_obj_attr_value = $1;	# incoming ip address
		$remaining_nat_ip_line = $2;
		$parse_obj_attr = 'addr';
		if ($member_count==0) {
			$parse_obj_name = $nat_obj_name;
#			$parse_obj_id = $nat_obj_uid;
#			problem: UID != oid but just the name of the object, needs to be this way for rule ref to work
			$parse_obj_id = $nat_obj_name;
			print_debug("		nat map storing nat group name=$parse_obj_name, value=$parse_obj_attr_value, attr=$parse_obj_attr, id=$parse_obj_id", $debug_level, 5);	
			&store_results(); # store group object as single ip host
			# turn object into group:
			$network_objects{"$nat_obj_name.typ"}  = 'group';
			$network_objects{"$nat_obj_name.type"} = 'group';
		}
		$parse_obj_name = "$nat_obj_name" . "_nat_ip_$parse_obj_attr_value";
		$parse_obj_id = $parse_obj_name . "_" . $nat_obj_uid;
		print_debug("		nat map storing nat single ip name=$parse_obj_name, value=$parse_obj_attr_value, attr=$parse_obj_attr, id=$parse_obj_id", $debug_level, 5);	
		&store_results(); # store object as single ip host
		# add object to group:
		if ($member_count==0) {
			$network_objects{"$nat_obj_name.member_refs"}	=  $parse_obj_id;
			$network_objects{"$nat_obj_name.members"}		=  $parse_obj_name;
			undef ($network_objects{"$nat_obj_name.ipaddr"});
		} else {
			$network_objects{"$nat_obj_name.member_refs"}	.= ( $GROUPSEP . $parse_obj_id );
			$network_objects{"$nat_obj_name.members"}		.= ( $GROUPSEP . $parse_obj_name );
		}				
		$member_count ++;
	}
	return;
}

#----------------------------------------
# Funktion parse_basic_elements
# Parameter: in_file: config file # Parameter: rulesetname, um nicht alle rulesets zu parsen
# zeilenweises Einlesen der Konfigurationsdatei (nw-services und nw-objects)
# Resultat: keins
#----------------------------------------

sub parse_basic_elements {
	my $in_file			= $_[0];
	my $ln 				= 0;		# line number
	my $line 			= '';
	my $last_line		= '';
	my ($nat_group_name, $nat_group_oid, $nat_group_comment);

	$parse_obj_state = 0;
	open (IN, $in_file) || die "$in_file konnte nicht geoeffnet werden.\n";
	LINE: while ( <IN> ) {
		$line = $_;					# Zeileninhalt merken
#		print ("line: $line\n");
		$last_line = $line;			# fuer end of line check
		$line =~ s/\x0D/\\r/g;  	# literal carriage return entfernen
		$line =~ s/\r\n/\n/g;		# handle cr,nl 
		chomp ($line);
		$ln++;						# Zeilennummer merken
	
########## parse obj state 1: main level for dividing into obj,svc, ... ################################################
		# state 1 start of type, z.B.  "	netobj={"
#		if ( /^\t([\w\-\.\_]+)\=\{$/ ) {
		if ( /^\t(netobj)\=\{$/ || /^\t(srvobj)\=\{$/ || /^\t(connobj)\=\{$/ ) {
			$parse_obj_state = 1;
			$parse_obj_type = $1;
			print_debug("up 0 (type: $parse_obj_type) => 1 at line no. $ln: $line", $debug_level, 3);
		} elsif ( /^\t\}$/ ) {
			$parse_obj_state = 0;
			undef ($parse_obj_type);
		}
########## parse obj state 2: main level for (new) element ################################################
		if (defined ($parse_obj_type)) {
			if ((/^\t\t([\w]+)\{$/)  && ( ($parse_obj_state == 2) || ($parse_obj_state == 1))) {
				$parse_obj_state = 2;
			}
			if (/^\t\t\}$/) { # Ende der Element-Definition
				$parse_obj_state = 1;
				undef ($parse_obj_name);
				undef ($parse_obj_id);
			}
	########## parse obj state 3: get element base data (name, comment, ...) ################################################
			if ((/^\t{3}(\w+)\=\{(.*?)$/)  && ( ($parse_obj_state == 3) || ($parse_obj_state == 2))) {
				$parse_obj_state = 3;
				$parse_obj_attr = $1;
				if ($2 ne '') {  # Ist der Wert in der aktuellen Zeile nicht leer?
					# value zwischenspeichern und untersuchen
					my $local_line = $2;
					# Ist der Wert in einer Zeile mit dem Parameter angegeben und nicht leer?						
					if ($local_line =~(/^(.+)\}$/)) {
						$parse_obj_attr_value = $1;
### maplist handling
						if ($parse_obj_type eq 'connobj') {
							if ($parse_obj_attr eq 'name') { $nat_group_name = $parse_obj_attr_value; }
							if ($parse_obj_attr eq 'oid') {	$nat_group_oid = $parse_obj_attr_value; }
							if ($parse_obj_attr eq 'comment') {	$nat_group_comment = $parse_obj_attr_value; }
							if ($parse_obj_attr eq 'mapList') {
#							 	storing dest nat object(s) from connection obj definition, assuming that mapList is always last statement of connobj definition
								&store_results_maplist($nat_group_name, $nat_group_oid, $nat_group_comment, $parse_obj_attr_value, $debug_level);
							}
							next LINE;
						}
### end of maplist handling
						if ($parse_obj_attr eq 'name') {
							$parse_obj_name = $parse_obj_attr_value;
							$parse_obj_id = $parse_obj_attr_value;	# Default-Wert fuer den Fall, dass keine OID vorhanden ist
						}   # found name of element
						if ($parse_obj_attr eq 'oid') { $parse_obj_id = $parse_obj_attr_value; }
						if ( ($parse_obj_attr eq 'comment') || ($parse_obj_attr eq 'oid') ) {
							store_results();
						}
						# Keine Gruppe/Liste geoeffnt, daher parse_obj_state auf Level 3 belassen
						undef ($parse_obj_attr);
					}
				} elsif ($parse_obj_attr eq 'list' || $parse_obj_attr eq 'neglist') {
					$parse_obj_neglist = 0;
					$member_counter = 0;
					if ($parse_obj_attr eq 'neglist') {
						$parse_obj_neglist = 1;
					}
					$parse_obj_state = 4;  # jetzt koennen beliebige Listen kommen (Laenge >=1)
				}
			}
			if ((/^\t{3}\}$/)) { 		# Ende des state 3
				$collecting_group_members = 0; # jetzt ist die Gruppe, sofern angefangen, zu Ende
				undef($parse_obj_attr);
			}
	########## parse obj state 4: Zwischenebene mit wenig Configdaten ################################################
			if ( (/^\t{4}(\w+)\{$/) ) {
				$parse_obj_state = 4;
				$parse_obj_attr_ext = $1;
				$convert_simple_to_group_flag = 0;				
				$member_counter ++;
				if ($member_counter == 2 && !$collecting_group_members) { # doch eine "explizite" Gruppe
					if (defined($parse_obj_name)) { print_debug ("found explicit group: $parse_obj_name", $debug_level, 5); }
					# rename existing object (group_name + addr / destport)
					# create group
					$convert_simple_to_group_flag = 1;
					&store_results();								# convert to group and add first (already read) group member
					$collecting_group_members = 1;
				}
				if (!((($parse_obj_attr_ext eq 'NetEntry' || $parse_obj_attr_ext eq 'NetRef') && $parse_obj_type eq 'netobj') || 
					(($parse_obj_attr_ext eq 'ServiceEntryTCP' || $parse_obj_attr_ext eq 'ServiceEntryUDP' || 
						$parse_obj_attr_ext eq 'ServiceRef' || $parse_obj_attr_ext eq 'ServiceEntryEcho' ||
						$parse_obj_attr_ext eq 'ServiceEntryOther')  # for other ip protos
					 && $parse_obj_type eq 'srvobj') )) {
					print_debug ("WARNING: found unknown or misplaced element $parse_obj_attr_ext in section $parse_obj_type", $debug_level, 7);
				}
				if (!$collecting_group_members && ($parse_obj_attr_ext eq 'NetRef' || $parse_obj_attr_ext eq 'ServiceRef')) {
					$collecting_group_members = 1;	# Gruppe faengt an
				}
				if ($parse_obj_attr_ext =~ 'Entry') {
					if ($parse_obj_attr_ext =~ /^ServiceEntry(.*?)$/ ) {
						# das IP-Protokoll extrahieren
						$parse_obj_attr = 'type';
						$parse_obj_attr_value = lc($1);
						$proto_of_explicit_svc_group = $parse_obj_attr_value;
						&store_results();
						$parse_obj_attr = '';		$parse_obj_attr_value = '';
					}
				}
			}
			# Ende des state 4
			if ((/^\t{4}\}$/) && ($parse_obj_state < 10))  {
				$parse_obj_state = 3;
				undef ($parse_obj_attr_ext); undef ($parse_obj_attr_ext_value);
			}
	########## parse obj state 5: get element details  ################################################
			if ((/^\t{5}\s?(\w+)\=\{(.*?)\}$/)  && ( ($parse_obj_state == 5) || ($parse_obj_state == 4))) {
				$parse_obj_state = 5;
				if ($2 ne '') {
					$parse_obj_attr = $1;
					$parse_obj_attr_value = $2;
					if ($parse_obj_attr eq 'addr' || $parse_obj_attr eq 'addr6' || $parse_obj_attr eq 'comment' ||
						$parse_obj_attr eq 'sessionTimeout' || $parse_obj_attr eq 'botClientPort' ||
						$parse_obj_attr eq 'topClientPort' || $parse_obj_attr eq 'portLimit' ||
						$parse_obj_attr eq 'proto' ||
						$parse_obj_attr eq 'ref' || $parse_obj_attr eq 'refid' ) {
						&store_results();
					}
				}
			}
			# Ende des state 5
			if ((/^\t{5}\}$/))  {
				$parse_obj_state = 4;
			}
		}
	} # end of while
	close (IN);
	print_debug ("$in_file closed",$debug_level,8);
	# check auf Vollstaendigkeit des Config-Files:
	if ($last_line =~ m/^\}(\n)?$/) { return 0; }
	else { return "ERROR: last line of config-file $in_file not correct: <$last_line>"; }
}

#####################################################

sub store_results_rules {
	if ($parse_obj_state==0) {	# nur am Ende eines Regelsatzes: Regelreihenfolge wegschreiben
		$rulebases{"$scope.ruleorder"} = join (',', @ruleorder);
		undef @ruleorder;
		# last change admin fuer gesamtes Regelwerk setzen
#		$rulebases{"$scope.last_change_admin"} = $last_change_admin; # wird derzeit nicht ausgewertet, nur pro Regel
	} elsif ($parse_obj_state==1) {	# nur am Ende einer Regel, ggf. UID setzen			
		$ruleorder[$parserule_rulenum] = $parserule_rulenum; # Regeln sind einfach von 1-n durchnummeriert
		if (!defined ($rulebases{"$scope.$parserule_rulenum.UID"})) { # UID setzen, falls noch nicht geschehen
			$rulebases{"$scope.$parserule_rulenum.UID"} = "$scope:" . $rulebases{"$scope.$parserule_rulenum.name"};
		}
		$rulebases{"$scope.$parserule_rulenum.last_change_admin"} = $last_change_admin; # last_change_admin pro Regel setzen
		$parserule_rulenum++;
		undef ($parse_obj_name);
		undef ($parse_obj_id);
	} else { 	# schon eine bekannte Regel?
		if (!defined ($rulebases{"$scope.$parserule_rulenum.name"})) {
			if (!defined($rulebases{"$scope.rulecount"})) {
				$rulebases{"$scope.rulecount"} = 1;
			} else {
				$rulebases{"$scope.rulecount"} += 1;
			}
			$rulebases{"$scope.$parserule_rulenum.name"} = $parse_obj_name;
			$rulebases{"$scope.$parserule_rulenum.src.op"} = '0';
			$rulebases{"$scope.$parserule_rulenum.dst.op"} = '0';
			$rulebases{"$scope.$parserule_rulenum.services.op"} = '0';
		}
		# Daten im Hash ergaenzen
		if (defined($parse_rule_field)) {
			if ($parse_obj_attr eq 'ref') {
				if (defined($rulebases{"$scope.$parserule_rulenum.$parse_rule_field"})) {
					$rulebases{"$scope.$parserule_rulenum.$parse_rule_field"} .= ( $GROUPSEP . $parse_obj_attr_value );
					$rulebases{"$scope.$parserule_rulenum.$parse_rule_field.refs"} .= $GROUPSEP; # ref_Feld ebenfalls erweitern um ein Element (falls ref_id leer)					
				} else {
					$rulebases{"$scope.$parserule_rulenum.$parse_rule_field"} = $parse_obj_attr_value;
					$rulebases{"$scope.$parserule_rulenum.$parse_rule_field.refs"} = ''
				}
			} elsif ($parse_obj_attr eq 'refid') {  # Annahme: ref_id kommt immer nach ref
				$rulebases{"$scope.$parserule_rulenum.$parse_rule_field.refs"} .= $parse_obj_attr_value;
			} elsif ($parse_obj_attr eq 'action') {
				print_debug ("found action: $parse_obj_attr_value", $debug_level, 7);
				if 		($parse_obj_attr_value eq 'ActionPass')		{ $parse_obj_attr_value = 'accept'; }
				elsif	($parse_obj_attr_value eq 'ActionDeny')		{ $parse_obj_attr_value = 'reject'; }
				elsif	($parse_obj_attr_value eq 'ActionBlock')	{ $parse_obj_attr_value = 'drop'; }
				elsif	($parse_obj_attr_value eq 'ActionRedirect')	{ $parse_obj_attr_value = 'redirect'; }
				elsif	($parse_obj_attr_value eq 'ActionMap')		{ $parse_obj_attr_value = 'map'; }
				$rulebases{"$scope.$parserule_rulenum.action"} = $parse_obj_attr_value;
			} elsif ($parse_obj_attr eq 'subsetName') {
				print_debug ("found action subsetName: $parse_obj_attr_value", $debug_level,7);
				$rulebases{"$scope.$parserule_rulenum.action.subsetName"} = $parse_obj_attr_value;			
			} else {
				print_debug ("group member attribute: $parse_obj_attr/$parse_obj_attr_value - ignoring", $debug_level, 7);
			}
		} elsif (defined($parse_obj_attr_value)) {
			if ($parse_obj_state < 5) { # rule top level
				# Attribute aufarbeiten
				SWITCH_OBJ_ATTR: {
					if ($parse_obj_attr eq 'oid') {
						$rulebases{"$scope.$parserule_rulenum.UID"} = $parse_obj_attr_value;
						last SWITCH_OBJ_ATTR;
					}
					if ($parse_obj_attr eq 'deactivated') {
						$rulebases{"$scope.$parserule_rulenum.disabled"} = $parse_obj_attr_value;
						last SWITCH_OBJ_ATTR;
					}
					if ($parse_obj_attr eq 'noLog') {
						if ($parse_obj_attr_value eq '0') {
							$rulebases{"$scope.$parserule_rulenum.track"} = 'log';
						} elsif ($parse_obj_attr_value eq '1') {
							$rulebases{"$scope.$parserule_rulenum.track"} = 'none';
						}
						last SWITCH_OBJ_ATTR;
					}
					if ($parse_obj_attr eq 'timeAllow') {
						$rulebases{"$scope.$parserule_rulenum.time"} = $parse_obj_attr_value;
						last SWITCH_OBJ_ATTR;
					}
					if ($parse_obj_attr eq 'comment') {
						$rulebases{"$scope.$parserule_rulenum.comments"} = $parse_obj_attr_value;
						last SWITCH_OBJ_ATTR;
					}
					$rulebases{"$scope.$parserule_rulenum.$parse_obj_attr"} = $parse_obj_attr_value;
				}
			}
			if ($parse_obj_attr eq 'bothWays' && $parse_obj_attr_value eq '1') { # bidirectional rule
				$rulebases{"$scope.$parserule_rulenum.bidir"} = $parse_obj_attr_value;			
			}
		}
	}
}

#----------------------------------------
# Funktion section_titles_correction
# Parameter: none
#----------------------------------------
# for each rule:
#	check if src/dst/srv are empty
#	add header_text 
#	add dummy src/dst/srv objects

sub section_titles_correction {
	my ($count, $rulebase_name);
	my @ruleorder;

	sub get_any_nwobj_refs {
		my $obj_id;
		foreach $obj_id (@network_objects) { if ($network_objects{"$obj_id.name"} eq 'World') { return $obj_id; } }
		return undef;
	}

	sub get_any_srvobj_refs {
		my $svc_id;
		foreach $svc_id (@services) {	if ($services{"$svc_id.name"} eq 'ALL') { return $svc_id; } }
		return undef;
	}

	sub is_phion_section_title {
		my $rulebase_name = shift;
		my $rule_number = shift;
	
		if (	!defined($rulebases{"$rulebase_name.$rule_number.src"}) &&
			!defined($rulebases{"$rulebase_name.$rule_number.dst"}) &&
			!defined($rulebases{"$rulebase_name.$rule_number.services"})) {
			return 1;
		} else {
			return 0;
		}
	}

	my $any_nwobj_ref = &get_any_nwobj_refs();
	my $any_srvobj_ref = &get_any_srvobj_refs();

	if (!defined($any_nwobj_ref) || !defined($any_srvobj_ref)) {
		print_debug ('error: no any objects found in config', $debug_level, 1);
	} else {
		foreach $rulebase_name (@rulebases) {
			if (defined($rulebases{"$rulebase_name.rulecount"}) && defined ($rulebases{"$rulebase_name.ruleorder"}) ) {
				@ruleorder = split(/\,/, $rulebases{"$rulebase_name.ruleorder"});
				$count = 0;		
				while ($count < $rulebases{"$rulebase_name.rulecount"}) {
					my $rule_number = $ruleorder[$count];
					print_debug ("rulebase: $rulebase_name, rule_no: $rule_number", $debug_level, 8);
					if (&is_phion_section_title($rulebase_name, $rule_number)) {
						# Section-Title vervollstaendigen
						$rulebases{"$rulebase_name.$ruleorder[$count].header_text"} = $rulebases{"$rulebase_name.$ruleorder[$count].name"};
						if (defined($rulebases{"$rulebase_name.$ruleorder[$count].UID"})) {
							$rulebases{"$rulebase_name.$ruleorder[$count].UID"} .=  $rulebases{"$rulebase_name.$ruleorder[$count].name"};							
						} else {
							$rulebases{"$rulebase_name.$ruleorder[$count].UID"} = $rulebase_name .  "." . $rulebases{"$rulebase_name.$ruleorder[$count].name"};
						}
						$rulebases{"$rulebase_name.$ruleorder[$count].action"} = 'drop';
						$rulebases{"$rulebase_name.$ruleorder[$count].src"} = 'World';
						$rulebases{"$rulebase_name.$ruleorder[$count].src.refs"} = $any_nwobj_ref;
						$rulebases{"$rulebase_name.$ruleorder[$count].dst"} = 'World';
						$rulebases{"$rulebase_name.$ruleorder[$count].dst.refs"} = $any_nwobj_ref;
						$rulebases{"$rulebase_name.$ruleorder[$count].services"} = 'ALL';
						$rulebases{"$rulebase_name.$ruleorder[$count].services.refs"} = $any_srvobj_ref;
					}
					$count ++;
				}
			}
		}
	}
}

sub fix_single_field_references {
	my $rulebase_name = shift;
	my $field = shift;
	my $rule_count = shift;
	my (@names, @names_original);
	my @refs = ();
	
	if (defined ($rulebases{"$rulebase_name.$rule_count.$field"}) && $rulebases{"$rulebase_name.$rule_count.$field"} ne '') {
		@names = split (/\|/, $rulebases{"$rulebase_name.$rule_count.$field"});
		@names_original = @names;
		my $idx=0; my $changed=0;
		if (defined $rulebases{"$rulebase_name.$rule_count.$field.refs"}) {
			@refs = split (/\|/, $rulebases{"$rulebase_name.$rule_count.$field.refs"});  # explicit use of | instead of $GROUPSEP
		} else {
			print_debug ("phion.pm WARNING (0): $rulebase_name, all refs undefined for rule $rule_count.$field.refs, name: " . $rulebases{"$rulebase_name.$rule_count.$field"}, $debug_level, 8);
		}
		ELEMENT: while ($idx<scalar(@names)) {
			if (!defined($refs[$idx]) || $refs[$idx] eq '') {  # no references are defined --> assume explicite object definition
				if ($field eq 'services') {
					if (defined ($services{"$names[$idx].name"}) && $services{"$names[$idx].name"} ne '') {
						print_debug ("phion.pm fixref WARNING (1): service in $rulebase_name, repairing ref to explicite $field object  " . $names[$idx], $debug_level, 6);
						$refs[$idx] = $names[$idx];
						$changed=1;
					} else { # even name is not set: delete element
						print_debug ("phion.pm WARNING (2): service in $rulebase_name, deleting broken references to incomplete $field object  " . $names[$idx], $debug_level, 6);
						splice (@refs,  $idx, 1);
						splice (@names, $idx, 1);
#						$idx--;
						next ELEMENT;
					}										
				} else { # network_objects
					if (defined ($network_objects{"$names[$idx].name"}) && $network_objects{"$names[$idx].name"} ne '') {  # no references are defined --> assume explicite object definition
						if ((defined ($network_objects{"$names[$idx].ipaddr"}) && $network_objects{"$names[$idx].ipaddr"} ne '') ||
							(defined ($network_objects{"$names[$idx].members"}) && $network_objects{"$names[$idx].members"} ne '')) {
							$refs[$idx] = $names[$idx];
							print_debug ("phion.pm fixref WARNING (3): $rulebase_name, fixing ref to $field object " . $names[$idx], $debug_level, 6);
						} else { # even name is not set: delete element
							print_debug ("phion.pm WARNING (4): $rulebase_name, deleting broken references to incomplete $field object " . $names[$idx], $debug_level, 6);
							splice (@refs,  $idx, 1);
							splice (@names, $idx, 1);
#							$idx--;
							next ELEMENT;
						}
					} else { # network objects: network_object in rule is not defined
						if ($names[$idx] =~ /(.*?)\:bwd$/) {	# NAT network objects in dst
							$names[$idx] = $1;
							if (defined($network_objects{"$names[$idx].name"})) {
								$refs[$idx] = $network_objects{"$names[$idx].UID"};
								print_debug ("phion.pm fixref WARNING (6): $rulebase_name, fixing ref to $field NAT object " . $names[$idx], $debug_level, 6);
							} else {
								if (defined($network_objects{"$names[$idx].name"}) || defined($network_objects{"$names[$idx].UID"})) {
									print_debug ("phion.pm fixref WARNING (7): undefined NAT object " . $names[$idx] . ", nwobj-name: ". $network_objects{"$names[$idx].name"} .
										", uid=" . $network_objects{"$names[$idx].UID"}, $debug_level, 6);
								} else {
									print_debug ("phion.pm fixref WARNING (8): undefined NAT object " . $names[$idx], $debug_level, 6);
								}
							}
							$changed = 1;
						} else { # if no nat object and not defined as nw object, then delete it
							print_debug ("phion.pm WARNING (5): $rulebase_name, deleting broken references to non-existant $field object " . $names[$idx], $debug_level, 6);
							splice (@refs, $idx, 1);
							splice (@names, $idx, 1);
#							$idx--;
							next ELEMENT;
						}
					}					
					$changed=1;
				}
			} else {
				# refs are defined - but are they pointing anyhere?
			}
=POD
			if ((defined($refs[$idx]) && $refs[$idx] ne '' && $field eq 'services' && !defined ($services{"$refs[$idx].UID"}))) { # ref ist defined but points to nowhere
				print ("phion.pm WARNING (10): $rulebase_name, removing broken references to incomplete $field object " . $names[$idx] . "\n");
				splice (@refs,  $idx, 1);
				splice (@names, $idx, 1);
				$idx--;
				$changed=1;
				next ELEMENT;
			}
			if ((defined($refs[$idx]) && $refs[$idx] ne '' && ($field eq 'src' || $field eq 'dst')) && !defined($network_objects{"$refs[$idx].UID"})) { # ref ist defined but points to nowhere
				print ("phion.pm WARNING (10): $rulebase_name, removing broken references to incomplete network $field object " . $names[$idx] . "\n");
				splice (@refs,  $idx, 1);
				splice (@names, $idx, 1);
				$idx--;
				$changed=1;
				next ELEMENT;
			}
=cut
			$idx++;
		}
		if ($changed) {
			print_debug ("changed list from " . join("$GROUPSEP", @names_original) . "\nto                " . join("$GROUPSEP", @names), $debug_level, 6);
			if (scalar(@names) == 0) { # if a whole field was deleted then delete the whole rule
				print_debug ("phion.pm fixref ERROR (6): $rulebase_name, deleted whole field $field object of rule no. $idx:\n" .
				"src: " . $rulebases{"$rulebase_name.$rule_count.src"} . "\n" .
				"dst: " . $rulebases{"$rulebase_name.$rule_count.dst"} . "\n" .
				"svc: " . $rulebases{"$rulebase_name.$rule_count.services"}, $debug_level, 7);
				return ("\n");
			}
		}
		return join("$GROUPSEP", @names) . "\n" . join("$GROUPSEP", @refs);
	} else {
		return ("\n");
	}	
}

sub delete_whole_rule_if_a_relevant_field_is_empty {
	my $rulebase_name = shift;
	my $rule_count = shift;
	my $to_be_deleted=0;
	my @ruleorder = split(/\,/, $rulebases{"$rulebase_name.ruleorder"});

	if (!defined($rulebases{"$rulebase_name.$ruleorder[$rule_count].src"}) || !defined($rulebases{"$rulebase_name.$ruleorder[$rule_count].dst"}) ||
		!defined($rulebases{"$rulebase_name.$ruleorder[$rule_count].services"})) {
		$to_be_deleted = 1;	
	} elsif ($rulebases{"$rulebase_name.$ruleorder[$rule_count].src"} eq '' || $rulebases{"$rulebase_name.$ruleorder[$rule_count].dst"} eq '' || $rulebases{"$rulebase_name.$ruleorder[$rule_count].services"} eq '') {
		$to_be_deleted = 1;
	}
	if ($to_be_deleted) {
		splice(@ruleorder, $rule_count, 1);
		print_debug("delete_whole_rule_if_a_relevant_field_is_empty: deleting in rulebase $rulebase_name rule no $rule_count, id=" .
			$rulebases{"$rulebase_name.$ruleorder[$rule_count].UID"}, $debug_level, 3);
#		$rule_count--;
		$rulebases{"$rulebase_name.rulecount"} = $rulebases{"$rulebase_name.rulecount"} - 1;
		$rulebases{"$rulebase_name.ruleorder"} = join (',', @ruleorder);
	}
	return $to_be_deleted;
}

sub fix_bidir_rules {	

	sub switch_rule_src_dst {
		my $dstkey = shift;
		my $src_tmp;
		
		$src_tmp = $rulebases{"$dstkey.src"};
		$rulebases{"$dstkey.src"} = $rulebases{"$dstkey.dst"};
		$rulebases{"$dstkey.dst"} = $src_tmp;
		$src_tmp = $rulebases{"$dstkey.src.refs"};
		$rulebases{"$dstkey.src.refs"} = $rulebases{"$dstkey.dst.refs"};
		$rulebases{"$dstkey.dst.refs"} = $src_tmp;
		$src_tmp = $rulebases{"$dstkey.src.op"};
		$rulebases{"$dstkey.src.op"} = $rulebases{"$dstkey.dst.op"};
		$rulebases{"$dstkey.dst.op"} = $src_tmp;
		if (defined($rulebases{"$dstkey.name"})) {
			$rulebases{"$dstkey.name"} .= ".bothWays_reverse";
		} else {
			$rulebases{"$dstkey.name"} = "bothWays_reverse";			
		}
	}

	my ($rule_count, $rulebase_name, $new_rule_number, $dstkey, $srckey, @ruleorder);

	foreach $rulebase_name (@rulebases) {
		if (defined($rulebases{"$rulebase_name.ruleorder"})) {
			@ruleorder = split(/\,/, $rulebases{"$rulebase_name.ruleorder"});
			$rule_count = 0;
			if (defined($rulebases{"$rulebase_name.rulecount"})) {
				while ($rule_count < $rulebases{"$rulebase_name.rulecount"}) {
					if (defined($rulebases{"$rulebase_name.$ruleorder[$rule_count].bidir"})) {
						print_debug ("debug: fix_bidir_rules found bidir rule: $rulebase_name.$ruleorder[$rule_count]", $debug_level, 3);
						$new_rule_number = $ruleorder[$rule_count]."reverse";
						$rulebases{"$rulebase_name.rulecount"} ++;  # Gesamtzahl der Regeln um eins erhoehen
						$rule_count ++;
						splice(@ruleorder, $rule_count, 0, $new_rule_number);	# fuegt neue Regel in ruleorder-array ein
						$srckey = "$rulebase_name." . $ruleorder[$rule_count-1];
						$dstkey = "$rulebase_name." . $new_rule_number;
						print_debug ("debug: phion.pm rulebase=$rulebase_name, srckey=$srckey, dstkey=$dstkey, rule_count=$rule_count, new_rule_number=$new_rule_number", $debug_level, 1);
						&copy_rule($srckey, $dstkey);
						&switch_rule_src_dst($dstkey);
						# now setting UID to a unique value for the case that a sublist is called more then once
						$rulebases{"$dstkey.UID"} =  $rulebases{"$dstkey.UID"} . '.' . 'reverse_direction_of_bothWays_rule';
					}	
					$rule_count++;
				}
			} else {
				print_debug ("warning: phion.pm found undefined rulecount for $rulebase_name", $debug_level, 3);
			}
		    $rulebases{"$rulebase_name.ruleorder"} = join(',', @ruleorder);
		} else {
			print_debug ("warning: phion.pm empty ruleset for rulebase $rulebase_name", $debug_level, 3);
		}
	}
}

#----------------------------------------
# for each rulebase:
#	search basic objects in rulebase that do not contain an OID
#	check if this object was defined anywhere
#	if yes, add name as OID
#   if no: delete object from rule and issue warning about broken ref
sub fix_rule_references {
	my ($rule_count, $rulebase_name);
	
	foreach $rulebase_name (@rulebases) {
		if (defined($rulebases{"$rulebase_name.ruleorder"})) {
			@ruleorder = split(/\,/, $rulebases{"$rulebase_name.ruleorder"});
			$rule_count = 0;
			if (defined($rulebases{"$rulebase_name.rulecount"})) {
				while ($rule_count < $rulebases{"$rulebase_name.rulecount"}) {
					($rulebases{"$rulebase_name.$ruleorder[$rule_count].src"},$rulebases{"$rulebase_name.$ruleorder[$rule_count].src.refs"}) =
						split(/\n/, &fix_single_field_references ($rulebase_name, "src", $ruleorder[$rule_count]));
					($rulebases{"$rulebase_name.$ruleorder[$rule_count].dst"},$rulebases{"$rulebase_name.$ruleorder[$rule_count].dst.refs"}) =
						split(/\n/, &fix_single_field_references ($rulebase_name, "dst", $ruleorder[$rule_count]));
					($rulebases{"$rulebase_name.$ruleorder[$rule_count].services"},$rulebases{"$rulebase_name.$ruleorder[$rule_count].services.refs"}) =
						split(/\n/, &fix_single_field_references ($rulebase_name, "services", $ruleorder[$rule_count]));
					if (!&delete_whole_rule_if_a_relevant_field_is_empty($rulebase_name, $rule_count)) {
						$rule_count++;
					}						
				}
			} else {
				print_debug ("warning: phion.pm found undefined rulecount for $rulebase_name", $debug_level, 3);
			}
		} else {
			print_debug ("warning: phion.pm empty ruleset for rulebase $rulebase_name", $debug_level, 3);
		}
	}
}

#----------------------------------------
# Funktion add_basic_object_oids_in_rules_for_locally_defined_objects
# Parameter: none
#----------------------------------------
# for each rulebase:
#	search basic objects in rulebase that do not contain an OID
#	check if this object was defined locally
#	if yes, add name as OID
sub add_basic_object_oids_in_rules_for_locally_defined_objects {
	my ($rule_count, $rulebase_name);

	sub fix_references {
		my $rulebase_name = shift;
		my $field = shift;
		my $rule_count = shift;
		my @names;
		my @refs = ();
		
		if (defined ($rulebases{"$rulebase_name.$rule_count.$field"}) && $rulebases{"$rulebase_name.$rule_count.$field"} ne '') {
			@names = split (/\|/, $rulebases{"$rulebase_name.$rule_count.$field"});
			my $idx=0; my $changed=0;
			if (defined $rulebases{"$rulebase_name.$rule_count.$field.refs"}) {
				@refs = split (/\|/, $rulebases{"$rulebase_name.$rule_count.$field.refs"});  # explicit use of | instead of $GROUPSEP
			} else {
				print_debug ("WARNING (1): $rulebase_name, ref undefined for rule $rule_count.$field.refs, name: " . $rulebases{"$rulebase_name.$rule_count.$field", $debug_level, 6});
			}
			while ($idx<scalar(@names)) {
				if (!defined($refs[$idx]) || $refs[$idx] eq '') {
					if ($field eq 'services') {
						if (defined ($services{"$names[$idx].name"}) && $services{"$names[$idx].name"} ne '') {
							$refs[$idx] = $names[$idx];
							$changed=1;
						} else {
							print_debug ("WARNING (2): $rulebase_name, still broken reference found to $field object " . $names[$idx], $debug_level, 6);
						}										
					} else {
						if (defined ($network_objects{"$names[$idx].name"}) && $network_objects{"$names[$idx].name"} ne '') {
							$refs[$idx] = $names[$idx];
							$changed=1;
						} else {
							print_debug ("WARNING (3): $rulebase_name, still broken reference found to $field object " . $names[$idx], $debug_level, 6);
						}					
					}
				}
				$idx++;
			}
			return join("$GROUPSEP", @refs);
		} else {
			print_debug ("WARNING (4): $rulebase_name, field ($field) not defined in rule# $rule_count, ID: " . $rulebases{"$rulebase_name.$rule_count.UID"}, $debug_level, 6);
			return ();
		}	
	}
	
	foreach $rulebase_name (@rulebases) {
		if (defined($rulebases{"$rulebase_name.ruleorder"})) {
			@ruleorder = split(/\,/, $rulebases{"$rulebase_name.ruleorder"});
			$rule_count = 0;
			if (defined($rulebases{"$rulebase_name.rulecount"})) {
				while ($rule_count < $rulebases{"$rulebase_name.rulecount"}) {
					$rulebases{"$rulebase_name.$ruleorder[$rule_count].src.refs"} = &fix_references ($rulebase_name, "src", $ruleorder[$rule_count]);
					$rulebases{"$rulebase_name.$ruleorder[$rule_count].dst.refs"} =	&fix_references ($rulebase_name, "dst", $ruleorder[$rule_count]);
					$rulebases{"$rulebase_name.$ruleorder[$rule_count].services.refs"} = &fix_references ($rulebase_name, "services", $ruleorder[$rule_count]);
					$rule_count++;
				}
			}
		}
	}
}

#----------------------------------------
# Funktion link_subset_rules
# Parameter: none
#----------------------------------------
# for each link:
#	convert rule to heading
#	insert subset
#	reorder rules (numbering)
sub link_subset_rules {
	my ($count, $key, $srckey, $sublist_name, $source_scope, $rulebase_name);
	my $new_rule_number;
	my @ruleorder;
	
	foreach $rulebase_name (@rulebases) {
		if ( my $sample_header_rule = &get_first_link_rule($rulebase_name) ) {	# if not - no need to insert headings
			my $sublist_offset = 5000000;
			my $total_sublist_rule_count = 0;
			my $insert_main_header_flag = 1;
			@ruleorder = split(/\,/, $rulebases{"$rulebase_name.ruleorder"});
			$count = 0;		
			while ($count < $rulebases{"$rulebase_name.rulecount"}) {
				if ($rulebases{"$rulebase_name.$ruleorder[$count].action"} eq 'ActionCascade') {
					my $name_of_header = $rulebases{"$rulebase_name.$ruleorder[$count].name"};
					$insert_main_header_flag = 0;
					# cascade-Regel zur Header-Regel umfunktionieren
					$rulebases{"$rulebase_name.$ruleorder[$count].action"} = 'drop';
					$rulebases{"$rulebase_name.$ruleorder[$count].header_text"} =
						$rulebases{"$rulebase_name.$ruleorder[$count].action.subsetName"};
					my $sublist_rule_count = 0;
					$sublist_name = $rulebases{"$rulebase_name.$ruleorder[$count].action.subsetName"};
					$source_scope = "${rulebase_name}__sublist__$sublist_name";

					while (defined($rulebases{"$source_scope.$sublist_rule_count.action"}) &&
							$rulebases{"$source_scope.$sublist_rule_count.action"} ne 'ActionCascadeBack' ) {
						$new_rule_number = $sublist_offset + $total_sublist_rule_count;
						$rulebases{"$rulebase_name.rulecount"} ++;
						$count ++; #	jetzt hochzaehlen, damit sublist hinter main-list landet
						splice(@ruleorder, $count, 0, $new_rule_number);	# fuegt neue Regel ein
						$key = "$rulebase_name." . $ruleorder[$count];
						$srckey = $source_scope . '.' . $sublist_rule_count;
						&copy_rule($srckey, $key);
						# now setting UID to a unique value for the case that a sublist is called more then once
						$rulebases{"$key.UID"} =  $rulebases{"$key.UID"} . '.' . $name_of_header;
						$sublist_rule_count ++;		$total_sublist_rule_count ++;
					}	
					# am Ende einer Sublist: Heading mit Titel 'main' einfuegen, sofern nicht gleich noch eine sublist folgt
					if (defined($ruleorder[$count+1])) {
						my $next_rule_no = $ruleorder[$count+1];
						if (defined($rulebases{"$rulebase_name.$next_rule_no.action"}) &&
							$rulebases{"$rulebase_name.$next_rule_no.action"} ne 'ActionCascade') {
							$insert_main_header_flag = 1;
							$count ++;
						}
					}
				    $rulebases{"$rulebase_name.ruleorder"} = join(',', @ruleorder);
				}
				if ($insert_main_header_flag) { # am Anfang oder nach Sublist "main"-header einfuegen
					$rulebases{"$rulebase_name.rulecount"} ++;
					$new_rule_number = $sublist_offset + $total_sublist_rule_count;
					splice(@ruleorder, $count, 0, $new_rule_number);	# fuegt neue Regel ein
					$key = "$rulebase_name." . $ruleorder[$count];
					&copy_rule($sample_header_rule, $key); # anschliessend die Daten abaendern:
					$rulebases{"$key.UID"} = "$rulebase_name.header.main.$count";
					$rulebases{"$key.header_text"} = 'main';
#					$rulebases{"$key.name"} = '';
					$rulebases{"$key.action"} = 'drop';
#					$rulebases{"$key.last_change_admin"} = $last_change_admin;
					$total_sublist_rule_count ++;	# pointer fuer insgesamt eingefuegte Regeln hochzaehlen
					$insert_main_header_flag = 0;
					$count ++; #	jetzt hochzaehlen, damit header nicht gleich bearbeitet wird
				}
				$count ++;
			}
		    $rulebases{"$rulebase_name.ruleorder"} = join(',', @ruleorder);
		}
	}
}

# Hilfsroutine zum Finden einer cascading-Regel als Vorlage fuer Header (Kopie)
sub get_first_link_rule {
	my $rulebase_name = shift;
	my $count = 0;		
	my @ruleorder;
	
	if (!defined($rulebases{"$rulebase_name.ruleorder"}) || !defined($rulebases{"$rulebase_name.rulecount"})) {
		return 0;
	}
	@ruleorder = split(/\,/, $rulebases{"$rulebase_name.ruleorder"});	
	while ($count < $rulebases{"$rulebase_name.rulecount"}) {
		if ($rulebases{"$rulebase_name.$ruleorder[$count].action"} eq 'ActionCascade') {
			print_debug ("found first link rule: $rulebase_name.$ruleorder[$count]", $debug_level, 7);
			return "$rulebase_name.$ruleorder[$count]";
		}
		$count ++;
	}
	return 0;
}

sub copy_rule {
	my $srckey = shift;
	my $key = shift;
	
	$rulebases{"$key.UID"}					= $rulebases{"$srckey.UID"};
	$rulebases{"$key.action"}				= $rulebases{"$srckey.action"};
	$rulebases{"$key.disabled"}				= $rulebases{"$srckey.disabled"};
	$rulebases{"$key.dst"}					= $rulebases{"$srckey.dst"};
	$rulebases{"$key.dst.op"}				= $rulebases{"$srckey.dst.op"};
	$rulebases{"$key.dst.refs"}				= $rulebases{"$srckey.dst.refs"};
	$rulebases{"$key.name"}					= $rulebases{"$srckey.name"};
	$rulebases{"$key.services"}				= $rulebases{"$srckey.services"};
	$rulebases{"$key.services.op"}			= $rulebases{"$srckey.services.op"};
	$rulebases{"$key.services.refs"}		= $rulebases{"$srckey.services.refs"};
	$rulebases{"$key.src"}					= $rulebases{"$srckey.src"};
	$rulebases{"$key.src.op"}				= $rulebases{"$srckey.src.op"};
	$rulebases{"$key.src.refs"}				= $rulebases{"$srckey.src.refs"};
	$rulebases{"$key.time"}					= $rulebases{"$srckey.time"};
	$rulebases{"$key.track"}				= $rulebases{"$srckey.track"};
	$rulebases{"$key.last_change_admin"}	= $rulebases{"$srckey.last_change_admin"};
	$rulebases{"$key.comments"}				= $rulebases{"$srckey.comments"};
}

#----------------------------------------
# Funktion parse_rules
# Parameter: in_file: config file
# zeilenweises Einlesen der Konfigurationsdatei (rules)
# Resultat: keins
#----------------------------------------
sub parse_rules {
	my $in_file			= $_[0];
	my $ln 				= 0;		# line number
	my $line 			= '';
	my $last_line		= '';
	my $scope_bu;
	
	undef ($parse_obj_state);
	open (IN, $in_file) || die "$in_file konnte nicht geoeffnet werden.\n";
	
	$indent = ''; $sublist_flag = 0; $rulelist_flag = 0; $rulelist_name = '';
	
	while ( <IN> ) {
		$line = $_;					# Zeileninhalt merken
		$last_line = $line;			# fuer end of line check
		$line =~ s/\x0D/\\r/g;  	# literal carriage return entfernen
		$line =~ s/\r\n/\n/g;		# handle cr,nl 
		chomp ($line);
		print_debug ("Zeile $ln, line: $line", $debug_level, 13);
		$ln++;
	
########## parse rule state 1: main level for dividing into obj,svc, ... ################################################
		# state 1 start of rule section 	rules={"

		if ( /^\tsublists\=\{$/ ) {
			$sublist_flag = 1;
			$indent = '\t\t';		# Variabler Einzug (leer bei rules=, enthaelt zwei Tabs in sublist=)
		}
		if ( $sublist_flag && /^\t\tRuleList\{$/ ) { $rulelist_flag = 1; $parserule_rulenum = 0; }
		if ( $rulelist_flag && /^\t\t\tname\=\{(.+?)\}$/ ) {
			$rulelist_name = $1;
			$rulebases{"$scope.$parserule_rulenum.UID"} = "$scope" . "__sublist__$rulelist_name";			
			$scope_bu = $scope;		
			$scope .= "__sublist__$rulelist_name";
		}
		
		if ( $rulelist_flag && /^${indent}\}$/ ) { # Ende einer Rulelist
			$rulelist_flag		= 0;
			$parse_obj_state	= 0;
			$parse_obj_type		= 'rules';			
			&store_results();
			$scope = $scope_bu;
			$rulelist_name		= '';
		}

		if ( /^${indent}\trules\=\{$/ ) {	# Beginn der Regel-Section (sowohl main als auch sublist)
			$parse_obj_state = 1;
			$parse_obj_type = 'rules';
		}
		if ( defined($parse_obj_state) && $parse_obj_state>=1 && /^${indent}\t\}$/ ) {	# end of 'rules=' or end of 'sublists=' resetting sublist flags and values
			$parse_obj_state	= 0;
			&store_results();
			undef ($parse_obj_type);
		}
		if ( defined($parse_obj_state) && $parse_obj_state>=1) {	# alle anderen Bereiche ausser "rules" ueberspringen
########## parse rule state 2: main level for (new) rule ################################################
			if ((/^${indent}\t\tRule\{$/)  && ( ($parse_obj_state == 2) || ($parse_obj_state == 1))) {
				$parse_obj_state = 2;
			}
			if (/^${indent}\t\t\}$/) { # Ende der Element-Definition
				$parse_obj_state = 1;
				&store_results();
			}
########## parse rule state 3: get element base data (name, comment, ...) ################################################
			if ((/^${indent}\t{3}(\w+)\=\{(.*?)$/)  && ( ($parse_obj_state == 3) || ($parse_obj_state == 2))) {
				$parse_obj_state = 3;
				$parse_obj_attr = $1;
				$explicit_flag = 0;
				if ($2 ne '') { # einzeilige Definition
					# value zwischenspeichern und untersuchen
					my $local_line = $2;
					# Ist der Wert in einer Zeile mit dem Parameter angegeben und nicht leer?						
					if ($local_line =~(/^(.+)\}$/)) {
						$parse_obj_attr_value = $1;
						if ($parse_obj_attr eq 'name') { $parse_obj_name = $parse_obj_attr_value; }
						if ($parse_obj_attr eq 'oid') { $parse_obj_id = $parse_obj_name . '__uid__' . $parse_obj_attr_value; }
						if (
							   ($parse_obj_attr eq 'comment')
							|| ($parse_obj_attr eq 'oid')
							|| ($parse_obj_attr eq 'deactivated')
							|| ($parse_obj_attr eq 'timeAllow')
							|| ($parse_obj_attr eq 'name')
							|| ($parse_obj_attr eq 'noLog')
						) {
							&store_results();
						}
						# Keine Gruppe/Liste geoeffnt, daher parse_obj_state auf Level 3 belassen
						undef ($parse_obj_attr);
					}
				} else {
					$parse_rule_field_details = $parse_obj_attr;
					if ($parse_rule_field_details eq 'src' || $parse_rule_field_details eq 'srcExplicit') {
						$parse_rule_field = 'src';
					}
					elsif ($parse_rule_field_details eq 'dst' || $parse_rule_field_details eq 'dstExplicit') {
						$parse_rule_field = 'dst';
					}
					elsif ($parse_rule_field_details eq 'srv' || $parse_rule_field_details eq 'srvExplicit') {
						$parse_rule_field = 'services';
					}
					elsif ($parse_rule_field_details eq 'action') {
						$parse_rule_field = 'action';
					}						
					else {
						undef ($parse_rule_field);
					}
					if ($parse_rule_field_details =~ /^...Explicit$/) {
						$explicit_flag = 1;
					}
					$parse_obj_state = 4;  # jetzt koennen beliebige Listen kommen (Laenge >=1)
				}
			}
			if ((/^${indent}\t{3}\}$/)) { 		# Ende des state 3
				undef ($parse_rule_field); # jetzt ist ein Gruppe, sofern angefangen, zu Ende
				undef($parse_obj_attr);
			}
########## parse rule state 4: Zwischenebene mit wenig Configdaten ################################################
			if ( (/^${indent}\t{4}(\w+)\{$/) ) {
				$parse_obj_state = 4;
				if (!defined($parse_rule_field)) {	# wenn in State 4 kein Feld gesetzt wurde: ignorieren
					if ($1 ne 'FilterGroupRef' && $1 ne 'DevGroupRef' && $1 ne 'PARPRef') {
						print_debug ("warning parsing rule state 4 - ignoring $1", $debug_level, 6);
					}
				} else {
					$parse_obj_attr_ext = $1;
					if (!(
							(
								($parse_rule_field eq 'src' || $parse_rule_field eq 'dst') &&
								($parse_obj_attr_ext eq 'NetEntry' || $parse_obj_attr_ext eq 'NetRef' || 
								 $parse_obj_attr_ext eq 'NetSet' || $parse_obj_attr_ext eq 'NetESet')
							) || 
							(
								($parse_rule_field eq 'services') &&
								($parse_obj_attr_ext eq 'ServiceRef' || $parse_obj_attr_ext eq 'ServiceSet')
							) || 
							(
								($parse_rule_field eq 'action') && ($parse_obj_attr_ext =~ /^Action/ )
							)
						)) {
						print_debug ("rule state 4 error: found unknown or misplaced element $parse_obj_attr_ext in rule section $parse_rule_field, " .
							"line: $ln, field: $parse_rule_field, attr_ext: $parse_obj_attr_ext", $debug_level, 1);
					} else { # extracting rule details
						$parse_rule_field_details = $1;
						$parse_obj_state = 5;
						if ($parse_rule_field eq 'action') {	# action sofort auswerten
							$parse_obj_attr = 'action';
							$parse_obj_attr_value = $parse_obj_attr_ext;
							&store_results();
						} # alles andere in state 5 eins tiefer
					}
				}
			}
			# Ende des state 4
			if ( (/^${indent}\t{4}\}$/) )  {
				$parse_obj_state = 3;
				undef ($parse_rule_field);
				undef ($parse_obj_attr_ext); undef ($parse_obj_attr_ext_value);
			}
	########## parse rule state 5: get element details  ################################################
#			if ( /^${indent}\t{5}(\w+)\=\{(.*?)\}?$/ && $parse_obj_state==5 ) {
			if ( /^${indent}\t{5}(\w+)\=\{(.*?)\}?$/ ) {
				if ($2 ne '') {		# einzeilige Definition - direkt auslesen
					$parse_obj_attr = $1;
					$parse_obj_attr_value = $2;
					if ($parse_obj_attr eq 'bothWays' && $parse_obj_attr_value eq '1') { # bidirectional rule
#						print_debug ("notice parsing rule state 5 - found bidir rule in line # $ln: $line", $debug_level, 2);
						&store_results();
					}					
					if ($parse_obj_attr eq 'name' || $parse_obj_attr eq 'comment' ||
						$parse_obj_attr eq 'subsetName'  || 
						$parse_obj_attr eq 'ref'  || $parse_obj_attr eq 'refid'	) {
						&store_results();
					}
				} else {
					$parse_obj_attr = $1;
					if ($parse_obj_attr eq 'list' || $parse_obj_attr eq 'neglist') {
						$parse_obj_state = 6;
					}
				}
			}
			# Ende des state 5
			if ((/^${indent}\t{5}\}$/))  {
				$parse_obj_state = 4;
				undef ($parse_rule_field);
			}
########## parse rule state 6: Zwischenebene mit wenig Configdaten ################################################
			if ( /^${indent}\t{6}(\w+)\{$/ && ($parse_obj_state == 5 || $parse_obj_state == 6) ) {
				$create_explicit_element_flag = 0;
				$parse_obj_attr_ext = $1;
				if (!defined($parse_rule_field)) { $parse_rule_field = ''; }
				if (!defined($parse_obj_attr_ext)) { $parse_obj_attr_ext = ''; }
				if (!(
						(
							($parse_rule_field eq 'src' || $parse_rule_field eq 'dst') &&
							($parse_obj_attr_ext eq 'NetEntry' || $parse_obj_attr_ext eq 'NetRef' || 
							 $parse_obj_attr_ext eq 'NetSet')
						) || 
						(
							($parse_rule_field eq 'services') &&
#							($parse_obj_attr_ext eq 'ServiceRef' || $parse_obj_attr_ext eq 'ServiceSet')
							($parse_obj_attr_ext eq 'ServiceRef' || $parse_obj_attr_ext eq 'ServiceEntryTCP' ||
							 $parse_obj_attr_ext eq 'ServiceEntryUDP' || $parse_obj_attr_ext eq 'ServiceEntryEcho' ||
							 $parse_obj_attr_ext eq 'ServiceEntryOther'
							)
						) 
				)) {
					if ( $parse_obj_attr_ext eq 'ConnRef' || $parse_obj_attr_ext eq 'ConnStd') {
					} else {
						print_debug ("state 6 error: found unknown or misplaced element $parse_obj_attr_ext in rule section $parse_rule_field" .
							"line: $ln, field: $parse_rule_field, attr_ext: $parse_obj_attr_ext", $debug_level, 1);
					}
				} else { # extracting rule details
					if ($explicit_flag && $parse_obj_attr_ext !~ /Ref$/) { # jetzt muss ein Basiselement angelegt werden
						$create_explicit_element_flag = 1;
						if ($parse_obj_attr_ext =~ /^ServiceEntry(.*?)$/ ) {
							# das IP-Protokoll extrahieren
							$create_explicit_element_proto = lc($1);
						}
					}
					$parse_obj_state = 7;
				}
			}
			# Ende des state 6
			if ( (/^${indent}\t{6}\}$/) )  {
				$parse_obj_state = 5;
#				undef ($parse_rule_field);
				$create_explicit_element_flag = 0;
				undef ($parse_obj_attr_ext); undef ($parse_obj_attr_ext_value);
			}
	########## parse rule state 7: get element list details  ################################################
			if ( /^${indent}\t{7}(\w+)\=\{\s?(.*?)\}$/ && $parse_obj_state==7 ) {
				if ($2 ne '') {		# einzeilige Definition - direkt auslesen
					$parse_obj_attr = $1;
					$parse_obj_attr_value = $2;
					if ($create_explicit_element_flag) {
						if ((($parse_obj_attr eq 'addr' || $parse_obj_attr eq 'addr6') && $parse_obj_attr_value ne '0.0.0.0') || $parse_obj_attr eq 'portLimit') {
							# adding new base element to structure
							$scope =~ /^(\d+)_(.+?)_(.+?)_(.+?)_(.+?)_(.+?)/;
							my $clusterserver; if (defined($4)) { $clusterserver = "-$4"; } else { $clusterserver = ""; }
							$parse_obj_name = $parse_obj_attr_value;
							if (defined($create_explicit_element_proto)) { # only use this for svc nor for ip
								$parse_obj_name = $create_explicit_element_proto . '_' . $parse_obj_name;
							}
							$parse_obj_name =~ s/\//_/ ; # replace '/' with '_' in name
							$parse_obj_id =  $parse_obj_name . '__uid__' . $parse_obj_name. $clusterserver;
							if ($parse_obj_attr eq 'addr' || $parse_obj_attr eq 'addr6') {
								$parse_obj_type = "netobj";
								&store_results();		# Element abspeichern				
								# hier werden vereinfachend alle anderen Parameter (comment, etc) ignoriert
							} elsif ($parse_obj_attr eq 'portLimit'
								# || $parse_obj_attr eq 'botClientPort'	|| $parse_obj_attr eq 'topClientPort')
							) {
								$parse_obj_type = "srvobj";
								&store_results();		# Element abspeichern
								if (defined($create_explicit_element_proto)) {
									$parse_obj_attr = 'type';
									$parse_obj_attr_value = $create_explicit_element_proto;
									&store_results();		# Protokoll hinterher
									undef($create_explicit_element_proto);
								}
								# hier werden vereinfachend alle anderen Parameter (Client_Port, Timeout) ignoriert
							}
							$parse_obj_type = "rules"; # und zurueck zum Regelparser
							
							# now storing the reference to the new element in the rule
							$parse_obj_attr = 'ref';	$parse_obj_attr_value = $parse_obj_name;
							&store_results();						
							$parse_obj_attr = 'refid';	$parse_obj_attr_value = $parse_obj_id;
							&store_results();						
						}
					} elsif ($parse_obj_attr eq 'ref' || $parse_obj_attr eq 'refid' ) {
						&store_results();
					}
				}
			}
##########################################################################################################
		}
	} # end of while
	close (IN);
	print_debug ("$in_file closed.",$debug_level, 8);
	
	# check auf Vollstaendigkeit des Config-Files:
	if ($last_line =~ m/^\}(\n)?$/) { return 0; }
	else { return "ERROR: last line of config-file $in_file not correct: <$last_line>"; }
}

1;
__END__

=head1 NAME

CACTUS::FWORCH::parser - Perl extension for fworch phion parser

=head1 SYNOPSIS

  use CACTUS::FWORCH::import::phion;

=head1 DESCRIPTION

fworch Perl Module support for importing configs into fworch Database

=head2 EXPORT

   &copy_config_from_mgm_to_iso - transfer phion MC rangetree fw rules to fworch
   &parse_config 				- parse phion MC config rangetree

=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Cactus eSecurity, tmp@cactus.de

=cut
