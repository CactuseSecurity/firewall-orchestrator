package CACTUS::FWORCH::import::parser;

use strict;
use warnings;
use Time::HiRes qw(time); # fuer hundertstelsekundengenaue Messung der Ausfuehrdauer
use CACTUS::FWORCH;
use CACTUS::FWORCH::import;
use CACTUS::read_config;

require Exporter;
our @ISA = qw(Exporter);

our %EXPORT_TAGS = ( 'basic' => [ qw( &copy_config_from_mgm_to_iso &parse_config ) ] );

our @EXPORT = ( @{ $EXPORT_TAGS{'basic'} } );
our $VERSION = '0.3';

our @config_lines;	# array der Zeilen des Config-Files im Originalzustand (kein d2u etc.)
our $parser_mode;	# Typ des Config-Formates (basic, data)
our $rule_order = 0; 	# Reihenfolge der Rules im Configfile
our $rulebase_name;

#####################################
# add_nw_obj
# param1: obj_name
# param2: obj_ip (with netmask in form 1.2.3.0/24 or ::/0)
# param3: obj zone
# param4: debuglevel [0-?]
#####################################
sub add_nw_obj {
	my $act_obj_name = shift;
	my $obj_ip = shift;
	my $obj_type = shift;
	my $act_obj_zone = shift;
	my $act_obj_comm = shift;
	my $act_obj_ipaddr_last = shift;
	my $uid_postfix = shift;
	my $debuglevel = shift;
	my $act_obj_nameZ = '';
	my $act_obj_ipaddr = '';
	my $act_obj_mask = '';
	my $act_obj_type = '';
	my $act_obj_loc = '';
	my $act_obj_color = 'black';
	my $act_obj_sys = '';
	my $act_obj_uid = '';
	my $ipv6 = 0;
	
	if (!defined($act_obj_ipaddr_last)) { $act_obj_ipaddr_last = ''; }
	if (!defined($obj_type)) { $obj_type = ''; }
	if (!defined($act_obj_comm)) { $act_obj_comm = ''; }
	print_debug("add_nw_obj called with name=$act_obj_name, ip=$obj_ip, type=$obj_type, obj_ipaddr_last=$act_obj_ipaddr_last, zone=$act_obj_zone", $debuglevel, 4);
#	if ($obj_type ne 'ip_range') {
		($act_obj_ipaddr, $act_obj_mask) = split (/\//, $obj_ip);
		if ($obj_ip =~/\:/) { $ipv6 = 1; }
		print_debug("split: ip=$act_obj_ipaddr, mask=$act_obj_mask", $debuglevel, 7);	
#	}
	$act_obj_nameZ = "${act_obj_name}$uid_postfix";	# ipv6_uid_postfix in Feldindex mit aufgenommen, um kollidierende Namen zu verhindern
	if (!defined ($network_objects{"$act_obj_nameZ.name"})) {
		@network_objects = (@network_objects, $act_obj_nameZ);
		$network_objects{"$act_obj_nameZ.name"} = $act_obj_name;
		$network_objects{"$act_obj_nameZ.UID"} = $act_obj_nameZ;
		if ($obj_type ne 'ip_range') {
			if ((!$ipv6 && $act_obj_mask==32) || ($ipv6 && $act_obj_mask==128)) { $obj_type = 'host' }
			else { $obj_type = 'network'; }
		}
		$network_objects{"$act_obj_nameZ.type"} = $obj_type; 
		$network_objects{"$act_obj_nameZ.netmask"} = $act_obj_mask ;
		$network_objects{"$act_obj_nameZ.zone"} = $act_obj_zone;		# neues Feld fuer Zone
		$network_objects{"$act_obj_nameZ.ipaddr"} = $act_obj_ipaddr;
		$network_objects{"$act_obj_nameZ.ipaddr_last"} = $act_obj_ipaddr_last;
		$network_objects{"$act_obj_nameZ.color"} = $act_obj_color;
		$network_objects{"$act_obj_nameZ.comments"} = $act_obj_comm;
		$network_objects{"$act_obj_nameZ.location"} = $act_obj_loc;
		$network_objects{"$act_obj_nameZ.sys"} = $act_obj_sys;
	} else {
		print_debug ("found duplicate object definition for network object $act_obj_name in zone $act_obj_zone", $debuglevel, -1);
	}
}


#####################################
# add_nw_service_obj 
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub add_nw_service_obj {  # ($application_name, $proto, $source_port, $destination_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $debug_level_main)
	my $act_obj_name = shift;
	my $act_obj_proto = shift;
	my $act_obj_src = shift;
	my $act_obj_dst = shift;
	my $act_obj_uid = shift;
	my $act_obj_rpc = shift;
	my $icmp_art = shift;
	my $icmp_nummer = shift;
	my $comment = shift;	
	my $debuglevel = shift
	my @range;
	my $act_obj_typ = 'simple';
	my $act_obj_type = 'simple';
	my $act_obj_src_last = '';
	my $act_obj_dst_last = '';
	my $act_obj_time = '';
	my $act_obj_time_std = '';
	my $act_obj_color = 'black';

	if (!defined ($services{"$act_obj_name.name"})) {
		@services = (@services, $act_obj_name);
		$services{"$act_obj_name.name"} = $act_obj_name;
		if (defined($act_obj_src)) {
			@range = split ( /-/, $act_obj_src);
			$services{"$act_obj_name.src_port"} = $range[0];
			if (defined($range[1])) {
				$services{"$act_obj_name.src_port_last"} = $range[1];
			} else {
				$services{"$act_obj_name.src_port_last"} = '';				
			}
		}
		if (defined($act_obj_dst)) {
			@range = split ( /-/, $act_obj_dst);
			$services{"$act_obj_name.port"} = $range[0];
			if (defined($range[1])) {
				$services{"$act_obj_name.port_last"} = $range[1];
			} else {
				$services{"$act_obj_name.port_last"} = '';				
			}
		}
		if (defined($act_obj_proto)) {
			$services{"$act_obj_name.ip_proto"} = get_proto_number($act_obj_proto)
		} else {
			# $services{"$act_obj_name.ip_proto"} = '';
		}
		$services{"$act_obj_name.timeout"} = $act_obj_time;
		$services{"$act_obj_name.color"} = $act_obj_color;
		if (defined($comment) && $comment ne '') { $services{"$act_obj_name.comments"} = $comment; } else { $services{"$act_obj_name.comments"} = ''; }
		$services{"$act_obj_name.typ"} = $act_obj_typ;
		$services{"$act_obj_name.type"} = $act_obj_type;
		if (defined($act_obj_rpc)) {
			$services{"$act_obj_name.rpc_port"} = $act_obj_rpc;
		} else {
			$services{"$act_obj_name.rpc_port"} = '';				
		}
		$services{"$act_obj_name.UID"} = $act_obj_name;		
		print_debug("add_nw_service_obj: added application $act_obj_name", $debuglevel, 4);
	} else {
		print_debug("add_nw_service_obj: warning duplicate defintion of service $act_obj_name", $debuglevel, 1);
	}
	return;
}

#####################################
# add_nw_obj_grp 
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub add_nw_obj_grp { # ($obj_name, $members, $comment, $objgrp_uid, $debuglevel_main);
	my $act_obj_name = shift;
	my $act_obj_members = shift;
	my $act_obj_zone = shift;
	my $comment = shift;
	my $uid = shift;
	my $uid_postfix = shift;
	my $debuglevel = shift;
	my $act_obj_color = 'black';
	my $act_obj_nameZ;
	my $members_refs_local = ''; 
		
	print_debug("add_nw_obj_grp called with name=$act_obj_name", $debuglevel, 3);
#	$act_obj_nameZ = "${act_obj_name}";	# CHANGE: Zone nicht in Feldindex aufgenommen
	$act_obj_nameZ = "${act_obj_name}$uid_postfix";	# ipv6_uid_postfix in Feldindex mit aufgenommen, um kollidierende Namen zu verhindern
	if (!defined ($network_objects{"$act_obj_nameZ.name"})) {
		@network_objects = (@network_objects, $act_obj_nameZ);
		$network_objects{"$act_obj_nameZ.name"} = $act_obj_name;
#		if (defined($uid)) {
#			$network_objects{"$act_obj_nameZ.UID"} = $uid . $uid_postfix; # $act_obj_nameZ;
#		} else {
		$network_objects{"$act_obj_nameZ.UID"} = $act_obj_nameZ;			
#		}
		$network_objects{"$act_obj_nameZ.type"} = 'group';
		$network_objects{"$act_obj_nameZ.zone"} = $act_obj_zone;		# neues Feld fuer Zone
		$network_objects{"$act_obj_nameZ.color"} = $act_obj_color;
		$network_objects{"$act_obj_nameZ.comments"} = $comment;
		$network_objects{"$act_obj_nameZ.members"} = &fortinet_split_list($act_obj_members);
		if ($uid_postfix ne '') { # only add ipv6 postfix for non-ipv4 objects
			if ($act_obj_members =~ /^\"(.*?)\"$/) { # standard list format: "x1" "x2" "x3"
				my @tmp_list = split(/\"\s\"/, $1);
				foreach my $member_local (@tmp_list) {
					$members_refs_local .= '"' . $member_local . $uid_postfix . '" ';
				}
				if ($members_refs_local =~ /^(.*?)\s$/) { $members_refs_local = $1; }
				$network_objects{"$act_obj_nameZ.member_refs"} = &fortinet_split_list($members_refs_local);
			} else {
				print_debug ("add_nw_obj_grp::non-matching members format found $act_obj_name, members: $act_obj_members", $debuglevel, -1);
			}
		} else {
			$network_objects{"$act_obj_nameZ.member_refs"} = &fortinet_split_list($act_obj_members);
		}
	} else {
		print_debug ("found duplicate object definition for network object $act_obj_name in zone $act_obj_zone", $debuglevel, -1);
	}
}

#####################################
# add_nw_service_obj_grp 
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub add_nw_service_obj_grp { # ($application_name, $members, $members_proto, $members_uid, $comment, $debuglevel_main);
	my $act_obj_name = shift;
	my $act_obj_members = shift;
	my $comment = shift;
	my $debuglevel = shift;
	my $act_obj_typ = 'group';
	my $act_obj_type = 'group';
	my $act_obj_proto = '';
	my $act_obj_src_last = '';
	my $act_obj_dst_last = '';
	my $act_obj_time = '';
	my $act_obj_time_std = '';
	my $act_obj_color = 'black';
	my $act_obj_rpc = '';
	my $mbrlst = '';
	
	@services = (@services, $act_obj_name);
	$services{"$act_obj_name.name"} = $act_obj_name;
	$services{"$act_obj_name.members"} = &fortinet_split_list($act_obj_members);
	$services{"$act_obj_name.member_refs"} = &fortinet_split_list($act_obj_members);
	print_debug("adding service group $act_obj_name with members $act_obj_members", $debuglevel, 6);
	$services{"$act_obj_name.ip_proto"} = $act_obj_proto;
	$services{"$act_obj_name.timeout"} = $act_obj_time;
	$services{"$act_obj_name.color"} = $act_obj_color;
	if (defined($comment) && $comment ne '') { $services{"$act_obj_name.comments"} = $comment; } else { $services{"$act_obj_name.comments"} = ''; }
	$services{"$act_obj_name.typ"} = $act_obj_typ;
	$services{"$act_obj_name.type"} = $act_obj_type;
	$services{"$act_obj_name.rpc_port"} = $act_obj_rpc;
	$services{"$act_obj_name.UID"} = $act_obj_name;
}

sub fortinet_split_list {  # param1 = list of objects (network or service)
	my $list = shift;
	my $debug_level = shift;
	my $orig_list = $list;
	
	if ($list =~ /^\"(.*?)\"$/) { # standard list format: "x1" "x2" "x3"
		$list = $1;
		$list = join('|', split(/\"\s\"/, $list));
	} else {
		print_debug("warning in fortinet_split_list: orig_list=$orig_list; found no match for object list", $debug_level, 1);
	}
#	print_debug("fortinet_split_list: orig_list=$orig_list, result=$list", $debug_level, 5);
	return $list;
}

#####################################
# add_rule 
# param1: 
# debuglevel [integer]
#####################################
sub add_rule { # ($rule_no, $from_zone, $to_zone, $policy_id, $disabled, $source, $destination, $application, $action, $track, $debuglevel_main)
	my $rule_no = shift;
	my $from_zone = shift;
	my $to_zone = shift;
	my $policy_id = shift;
	my $disabled = shift;
	my $source = shift;
	my $destination = shift;
	my $service = shift;
	my $action = shift;
	my $track = shift;
	my $comment = shift;
	my $policy_name = shift;
	my $svc_neg = shift;
	my $src_neg = shift;
	my $dst_neg = shift;
	my $uid_postfix = shift;
	my $debuglevel = shift;
	my $rule_id;


#	print_debug ("add_rule: rulebase_name=$rulebase_name, rulecount=" . $rulebases{"$rulebase_name.rulecount"}, $debuglevel, 4);
	$rulebases{"$rulebase_name.rulecount"} = $rule_no + 1;	# Anzahl der Regeln wird sukzessive hochgesetzt
#	$rule_id = "from_zone__$from_zone" . "__to_zone__$to_zone" . "__$rule_id";
	$ruleorder[$rule_no] = $rule_no;

	if (!defined($track) || $track eq '') { $track = 'none'; }
	if (length($track)<3) { print_debug ("warning, short track: <$track>", $debuglevel, 1); }
	
	$rulebases{"$rulebase_name.$rule_no.src"} = '';
	foreach my $src (split(/\|/, &fortinet_split_list($source, $debuglevel))) {
		if ($rulebases{"$rulebase_name.$rule_no.src"} ne '') {
			$rulebases{"$rulebase_name.$rule_no.src"} .= '|';
			$rulebases{"$rulebase_name.$rule_no.src.refs"} .= '|';
		}
		$rulebases{"$rulebase_name.$rule_no.src"} .= "$src";
#		$rulebases{"$rulebase_name.$rule_no.src.refs"} .= ("$src" . "__zone__$from_zone");
		$rulebases{"$rulebase_name.$rule_no.src.refs"} .= ("$src$uid_postfix");  # CHANGE
	}
	$rulebases{"$rulebase_name.$rule_no.dst"} = '';
	foreach my $dst (split(/\|/, &fortinet_split_list($destination, $debuglevel))) {
		if ($rulebases{"$rulebase_name.$rule_no.dst"} ne '') {
			$rulebases{"$rulebase_name.$rule_no.dst"} .= '|';
			$rulebases{"$rulebase_name.$rule_no.dst.refs"} .= '|';
		}
		$rulebases{"$rulebase_name.$rule_no.dst"} .= "$dst";
#		$rulebases{"$rulebase_name.$rule_no.dst.refs"} .= ("$dst" . "__zone__$to_zone");
		$rulebases{"$rulebase_name.$rule_no.dst.refs"} .= ("$dst$uid_postfix"); # CHANGE
	}
		
	$rulebases{"$rulebase_name.$rule_no.services"} = '';
	foreach my $svc (split(/\|/, &fortinet_split_list($service, $debuglevel))) {
		if ($rulebases{"$rulebase_name.$rule_no.services"} ne '') {
			$rulebases{"$rulebase_name.$rule_no.services"} .= '|';
			$rulebases{"$rulebase_name.$rule_no.services.refs"} .= '|';
		}
		$rulebases{"$rulebase_name.$rule_no.services"} .= "$svc";
		$rulebases{"$rulebase_name.$rule_no.services.refs"} .= "$svc";
	}
	
	$rulebases{"$rulebase_name.$rule_no.id"} = $policy_id;
	$rulebases{"$rulebase_name.$rule_no.ruleid"} = $policy_id;
	$rulebases{"$rulebase_name.$rule_no.order"} = $rule_no;
	if ($disabled eq 'inactive') { $rulebases{"$rulebase_name.$rule_no.disabled"} = '1'; }
	else { $rulebases{"$rulebase_name.$rule_no.disabled"} = '0'; }
	$rulebases{"$rulebase_name.$rule_no.src.zone"} = $from_zone;
	$rulebases{"$rulebase_name.$rule_no.dst.zone"} = $to_zone;
	$rulebases{"$rulebase_name.$rule_no.services.op"} = $svc_neg;
	$rulebases{"$rulebase_name.$rule_no.src.op"} = $src_neg;
	$rulebases{"$rulebase_name.$rule_no.dst.op"} = $dst_neg;
	$rulebases{"$rulebase_name.$rule_no.action"} = $action;
	$rulebases{"$rulebase_name.$rule_no.track"} = $track;
	$rulebases{"$rulebase_name.$rule_no.install"} = '';		# set hostname verwenden ?
	$rulebases{"$rulebase_name.$rule_no.name"} = $policy_name;
	$rulebases{"$rulebase_name.$rule_no.time"} = '';
	if (defined($comment) && $comment ne '') { $rulebases{"$rulebase_name.$rule_no.comments"} = $comment; }
	$rulebases{"$rulebase_name.$rule_no.UID"} = $policy_id;
	$rulebases{"$rulebase_name.$rule_no.header_text"} = '';
	print_debug ("added_rule: rulebase_name=$rulebase_name, from_zone: " . $from_zone . ", to_zone: " . $to_zone, $debuglevel, 4);
	return $rule_no+1;	
}

############################################################
# add_zone ($new_zone)
############################################################
sub add_zone {
	my $new_zone = shift;
	my $debug = shift;
	my $is_there = 0;
	foreach my $elt (@zones) { if ($elt eq $new_zone) { $is_there = 1; last; } }
	if (!$is_there) { push @zones, $new_zone; &print_debug("adding new zone: $new_zone", $debug, 4); }
}

############################################################
# object_address_add  (name, ip, mask, zone, comment)
############################################################
sub object_address_add {
	my $act_obj_name   = $_[0];
	my $act_obj_ipaddr = $_[1];
	my $act_obj_mask = $_[2];
	my $act_obj_zone = $_[3];
	my $act_obj_comm = $_[4];
	my @params;
	my $act_obj_nameZ = '';
	my $act_obj_ipaddr_last = '';
	my $act_obj_type = 'simple';
	my $act_obj_loc = '';
	my $act_obj_color = 'black';
	my $act_obj_sys = '';
	my $act_obj_uid = '';
		
#	$act_obj_nameZ = "${act_obj_name}__zone__$act_obj_zone";	# Zone in Feldindex mit aufgenommen
	$act_obj_nameZ = "${act_obj_name}";	# CHANGE Zone nicht in Feldindex aufgenommen
	if (!defined ($network_objects{"$act_obj_nameZ.name"})) {
		@network_objects = (@network_objects, $act_obj_nameZ);
		$network_objects{"$act_obj_nameZ.name"} = $act_obj_name;
	} elsif (defined ($network_objects{"$act_obj_nameZ.name"})) {
		print "sub ns_object_address NET_OBJECT: $act_obj_nameZ ist bereits definiert.\n";
	} else {
		print "sub ns_object_address NET_OBJECT: $act_obj_nameZ ist undefiniert.\n";
	}
	my $subnetbits = &calc_subnetmask ($act_obj_mask);
	if ($subnetbits==32) { $network_objects{"$act_obj_nameZ.type"} = 'host' };
	if ($subnetbits<32)  { $network_objects{"$act_obj_nameZ.type"} = 'network' };
	$network_objects{"$act_obj_nameZ.netmask"} = $act_obj_mask ;
	$network_objects{"$act_obj_nameZ.zone"} = $act_obj_zone;		# neues Feld fuer Zone
	$network_objects{"$act_obj_nameZ.ipaddr"} = $act_obj_ipaddr;
	$network_objects{"$act_obj_nameZ.ipaddr_last"} = $act_obj_ipaddr_last;
	$network_objects{"$act_obj_nameZ.color"} = $act_obj_color;
	$network_objects{"$act_obj_nameZ.comments"} = $act_obj_comm;
	$network_objects{"$act_obj_nameZ.location"} = $act_obj_loc;
	$network_objects{"$act_obj_nameZ.sys"} = $act_obj_sys;
	$network_objects{"$act_obj_nameZ.UID"} = $act_obj_nameZ;
}

############################################################
# copy_config_from_mgm_to_iso($ssh_private_key, $ssh_user, $ssh_hostname, $management_name,
# $obj_file_base, $cfg_dir, $rule_file_base)
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
	my $debug_level   = shift;
	my $cmd;
	my $fehler_count = 0;
	my $result;

	if ($config_path_on_mgmt ne '') {	# not a real fortigate but a standard ssh server
		$cmd = "$scp_bin $scp_batch_mode_switch -i $workdir/${CACTUS::FWORCH::ssh_id_basename} $ssh_user\@$ssh_hostname:$config_path_on_mgmt/fortigate.cfg $cfg_dir/$obj_file_base";
	} else { # standard fortigate
		$cmd = "$ssh_bin -o StrictHostKeyChecking=no -i $workdir/${CACTUS::FWORCH::ssh_id_basename} $ssh_user\@$ssh_hostname show full-configuration > $cfg_dir/$obj_file_base";	# fortigate
		# adding "-o StrictHostKeyChecking=no" to allow for failover of fortinet machines:
	}
	#print_debug("copy_config_from_mgm_to_iso cmd=$cmd", $debuglevel, 4);
	if (system ($cmd)) { $fehler_count++; }
	return ($fehler_count, "$cfg_dir/$obj_file_base" );
}

sub sort_rules_and_add_zone_headers {
	my $debug = shift;
	my $anzahl_regeln;
	my $count;
	my $zone_string;
	my @rule_zones = ();

	# Nachbereitung Regeln: Sortierung nach a) Zonen b) $ruleorder
 	if (!defined($rulebases{"$rulebase_name.rulecount"})) {
 		 $anzahl_regeln = 0;
 	} else {
 		$anzahl_regeln = $rulebases{"$rulebase_name.rulecount"};
 	}
	for ($count=0; $count<$anzahl_regeln; $count++) {
		$zone_string = $rulebases{"$rulebase_name.$ruleorder[$count].src.zone"};
		$zone_string .= " : ";
		$zone_string .= $rulebases{"$rulebase_name.$ruleorder[$count].dst.zone"};
		push @rule_zones, $zone_string
	}
	my @idx = ();
	my $item;
	for (@rule_zones) {
		($item) = $_;
		push @idx, $item;
	}

	@ruleorder = @ruleorder[ sort { $idx[$a] cmp $idx[$b] } 0 .. $anzahl_regeln-1 ];
	@rule_zones = @rule_zones[ sort { $idx[$a] cmp $idx[$b] } 0 .. $anzahl_regeln-1 ];

	# Nachbereitung Regeln: Header-Regeln vor Zonenwechsel einfuegen
	my $new_zone_string;
	my $old_zone_string = "";
	my $rule_header_count = 1;
	my $rule_header_offset = &CACTUS::read_config::read_config('rule_header_offset') * 1;
	my $new_rule_id;
	for ($count = 0; $count < $anzahl_regeln; $count++) {
		$new_zone_string = $rule_zones[$count];
		if ($new_zone_string ne $old_zone_string) { # insert header rule
			$new_rule_id = $rule_header_offset+$rule_header_count++;
			(my $src_zone, my $dst_zone) = split (/ : /, $new_zone_string);
			splice(@ruleorder,$count,0,$new_rule_id); # fuegt neue Regel ein
			splice(@rule_zones,$count,0,$new_zone_string); 
			$anzahl_regeln++;
		    $rulebases{"$rulebase_name.rulecount"} = $anzahl_regeln;
			$rulebases{"$rulebase_name.$ruleorder[$count].id"} = $new_rule_id;
			$rulebases{"$rulebase_name.$ruleorder[$count].header_text"} = $new_zone_string;
			$rulebases{"$rulebase_name.$ruleorder[$count].UID"} = $new_rule_id;
			$rulebases{"$rulebase_name.$ruleorder[$count].src"} = "all";
			$rulebases{"$rulebase_name.$ruleorder[$count].src.refs"} = "all";
			$rulebases{"$rulebase_name.$ruleorder[$count].dst"} = "all";
			$rulebases{"$rulebase_name.$ruleorder[$count].dst.refs"} = "all";
			$rulebases{"$rulebase_name.$ruleorder[$count].services"} = "all";
			$rulebases{"$rulebase_name.$ruleorder[$count].action"} = "deny";
			$rulebases{"$rulebase_name.$ruleorder[$count].src.zone"} = $src_zone;
			$rulebases{"$rulebase_name.$ruleorder[$count].dst.zone"} = $dst_zone;
			$rulebases{"$rulebase_name.$ruleorder[$count].disabled"} = '0';
			$rulebases{"$rulebase_name.$ruleorder[$count].src.op"} = '0';
			$rulebases{"$rulebase_name.$ruleorder[$count].dst.op"} = '0';
			$rulebases{"$rulebase_name.$ruleorder[$count].services.op"} = '0';
		}
		$old_zone_string = $new_zone_string;
	}
=cut
	# moving any:any section down to the end
	my $destination_rule_no;
	for ($count = $anzahl_regeln-1; $count >= 0; $count--) {
		if ($rule_zones[$count] eq 'any : any') {			# cut rule within any:any section and paste at the end

			$destination_rule_no = $anzahl_regeln-1;
			
			# this is the header rule to be moved to the top of the any:any section:
			while ($rule_zones[$destination_rule_no] eq 'any : any' && $rulebases{"$rulebase_name.$ruleorder[$count].header_text"} ne '') { 
				$destination_rule_no--;
			}
			
			# this is a deny rule (and no section header) to be moved to the bottom of the any:any section:
			if ($rulebases{"$rulebase_name.$ruleorder[$count].action"} eq 'deny' && $rulebases{"$rulebase_name.$ruleorder[$count].header_text"} eq '') {
				$destination_rule_no = $anzahl_regeln-1;
			}

			# make sure that standard any:any rules are inserted before deny rules
			while ($rule_zones[$destination_rule_no] eq 'any : any' && $rulebases{"$rulebase_name.$ruleorder[$destination_rule_no].action"} eq 'deny'  &&
				$rulebases{"$rulebase_name.$ruleorder[$count].action"} ne 'deny' &&
				$rulebases{"$rulebase_name.$ruleorder[$count].header_text"} eq '') { 
				$destination_rule_no--;
			}
			
			if ($count ne $destination_rule_no) { # avoid moving on the same spot
				&print_debug("moving any : any rule with id " . $rulebases{"$rulebase_name.$ruleorder[$count].id"} .
					" from pos $count to pos $destination_rule_no", $debug, 2);
				splice( @ruleorder, $destination_rule_no, 0, splice( @ruleorder, $count, 1 ) );
				splice( @rule_zones, $destination_rule_no, 0, splice( @rule_zones, $count, 1 ) );
			}
		}
	}
=cut
	# moving any:any section down to the end
	my ($destination_rule_no, $first_any_rule, $last_any_rule, $number_of_any_rules);
	for ($count=0; $count<$anzahl_regeln; $count++) {
		if (!defined($first_any_rule) && $rule_zones[$count] eq 'any : any') { $first_any_rule = $count; }
		if (defined($first_any_rule) && !defined($last_any_rule) && $rule_zones[$count] ne 'any : any') { $last_any_rule = $count-1; }
	}
	if (defined($first_any_rule) && !defined($last_any_rule)) { $last_any_rule = $number_of_any_rules-1; }
	if (defined($first_any_rule)) {	# only move any:any rules if they exist
		$number_of_any_rules = $last_any_rule - $first_any_rule + 1;
		$destination_rule_no = $anzahl_regeln-$number_of_any_rules;
		&print_debug("moving any:any rules from pos $first_any_rule to pos $last_any_rule to the bottom ($destination_rule_no)", $debug, 2);
		splice( @ruleorder, $destination_rule_no, 0, splice( @ruleorder, $first_any_rule, $number_of_any_rules ) );
		splice( @rule_zones, $destination_rule_no, 0, splice( @rule_zones, $first_any_rule, $number_of_any_rules ) );
	}
}

sub parse_mgm_name { # params: $debug_level
	# since fortiOS 5.6 the hostname is not part of the config?!
	my $debuglevel_main = shift;
	my $mgm_name = '';
	my $line = '';
	my $context = '';
	my @nodes= ();

	&print_debug("entering parse_mgm_name",$debuglevel_main,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		print_debug("parse_mgm_name::parsing line $line", $debuglevel_main, 9);
#	parsing device name(s) --- set hostname "xxxx"
		if ($line =~ /^config\ssystem\sglobal/ && $context eq '') { $context = "system global"; &print_debug("found system line: $line",$debuglevel_main,5); next NEW_LINE;  }
		if ($line=~ /^\s+set\s+hostname\s+\"(.+?)\"$/ && ($context eq 'system global')) {
			$mgm_name = $1;
			@nodes = ($mgm_name, @nodes);
			&print_debug("parse_mgm_name: found hostname: $mgm_name",$debuglevel_main,1);
			next NEW_LINE; 
		}
		if ($line=~ /^end$/ && $context eq 'system global') {
			$context = '';
			return $mgm_name; 
		}
	}
	&print_debug("ERROR: end of parse_mgm_name: at end without match (mgm_name=$mgm_name)",$debuglevel_main,-1);
	return 0;
}

sub parse_vdom_names { # ($debug_level)
	my $debuglevel_main = shift;
	my $line = '';
	my $context = '';
	my @vdom= ();

	&print_debug("entering parse_vdom_names",$debuglevel_main,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		print_debug("parse_vdom_names::parsing line $line", $debuglevel_main, 9);
		if ($line=~ /^config\svdom$/ && $context eq '') { $context = 'vdom'; next NEW_LINE; }
		if ($line=~ /^edit\s(.+?)$/ && $context eq 'vdom') { @vdom = (@vdom, $1); next NEW_LINE; }
		if ($line=~ /^end$/ && $context eq 'vdom') { $context = ''; return join(',', @vdom); }
	}
	&print_debug("end of parse_vdom_names: at end without any vdom match",$debuglevel_main,2);
	return '';
}

# the following function does only parse simple objects without groups. Groups are parsed in a second run using function parse_config_group_objects
sub parse_config_base_objects { # ($debug_level, $mgm_name)
	sub transform_port_list_to_artificial_services {
		# params: ($destination_port, $proto, $application_name, $source_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $comment, $debug)
		my $destination_port = shift;
		my $proto = shift;
		my $application_name = shift;
		my $source_port = shift;
		my $uuid = shift;
		my $rpc = shift;
		my $icmp_art = shift;
		my $icmp_nummer = shift;
		my $comment = shift;
		my $debug = shift;
		my $members = '';
		
		foreach my $current_port (split (/\s/, $destination_port)) {
			if ($current_port =~ /^(.+?)\:(.+?)$/) { # handle case: set tcp-portrange 0-65535:0-65535
				$current_port = $1;
				$source_port = $2;
			}
			my $member = "${application_name}_FWORCH-multiGRP-${current_port}_$proto";
			$members .= '"' . $member . '" ';
			print_debug("adding arty service ${application_name}::$member with dst-port $current_port", $debug, 7);
			&add_nw_service_obj ($member, $proto, $source_port, $current_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $comment, $debug);
		}
		$members =~ s/\s$//;	# remove trailing blank
		return $members
	}

	my $debug = shift;
	my $mgm_name = shift;
	my ($zone, $address_group_name, $obj_type, $obj_name, $obj_ip, $obj_ip_last, $obj_netmask, $group_member_name, $application_name, $group_name, $group_members, $proto, $icmp_art, $icmp_nummer,
		$source_port, $destination_port, $uuid, $members, $members_uid, $members_proto, $rpc, $comment, $uid_postfix);
	my $line = '';
	my $context = '';
	my @nodes= ();
	my $v6flag = 0;

	&print_debug("entering parse_config_base_objects =======================================================",$debug,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		&print_debug("pcbo-line: $line", $debug, 9);
		if ($line =~ /^end$/ && ($context eq 'firewall address' || $context eq 'firewall addrgrp' || $context eq 'firewall vip')) { 
			&print_debug("switching back to top level, resetting uid_postfix", $debug, 6);
			$context = &switch_context($context, '', $debug); $v6flag = 0; $uid_postfix = ''; next NEW_LINE; 
		}
		if ($line =~ /^config firewall (multicast\-)?address(6?)$/ && $context eq '') {
			my $mc = $1;
			my $v6 = $2;
			$v6flag = 0; $uid_postfix = ''; 
			if (defined($mc) && $mc eq 'multicast-') { $uid_postfix = '.multicast.uid'; }
			if (defined($v6) && $v6 eq '6') { 
				$v6flag = 1; 
				if ($uid_postfix eq '.multicast.uid') { $uid_postfix = '.multicast.ipv6.uid'; } else { $uid_postfix = '.ipv6.uid'; } 
			}
			&print_debug("switching to firewall address, uid_postfix=$uid_postfix", $debug, 6);
			$context = &switch_context($context, 'firewall address', $debug); 
			next NEW_LINE; 
		}
		if ($line =~ /^config firewall addrgrp(6?)$/ && $context eq '') {
			if (defined($1) && $1 eq '6') { $v6flag = 1; $uid_postfix = ".ipv6.uid"; } else { $v6flag = 0; $uid_postfix = ''; }
			&print_debug("switching to firewall addrgrp, v6_uid_postfix=$uid_postfix", $debug, 6);
			$context = &switch_context($context, 'firewall addrgrp', $debug); 
			next NEW_LINE; 
		}
		if ($line =~ /^\s+edit\s\"(.+)"$/ && ($context eq 'firewall address' || $context eq 'firewall addrgrp')) {
			$obj_name = $1;
			print_debug("found object name $obj_name", $debug, 6);
			$context = &switch_context($context, $context . ' single object', $debug); 
			next NEW_LINE; 
		}
		if ($line =~ /^\s+next$/ && $context eq 'firewall address single object') {
			if (!defined($zone)) { $zone = 'global'; }			
			if (!defined($obj_ip)) { 
				if ($v6flag==1) {
					$obj_ip = '::'; $obj_netmask = '0'; 					
				} else {
					$obj_ip = '0.0.0.0'; $obj_netmask = '0.0.0.0'; 
				}
			}
			if (!defined($obj_ip_last)) { $obj_ip_last = ''; }
			if (!defined($obj_type)) { $obj_type = ''; }
			if ($obj_type eq 'interface-subnet')
			{ 
				# interface-subnet is not CIDR conform, therefore we change the netmask to a single host
				$obj_type = 'host';
				if ($v6flag==1)
				{
					$obj_netmask = '128';
				}
				else
				{
					$obj_netmask = '255.255.255.255';
				}
			}
			if (!defined($comment)) { $comment = ''; }
			if (!defined($obj_netmask)) { $obj_netmask = '255.255.255.255'; }
			if (!$v6flag) { $obj_netmask = &calc_subnetmask($obj_netmask); }
			print_debug("adding nw_obj type=$obj_type, name=$obj_name with ip $obj_ip/$obj_netmask, obj_ip_last=$obj_ip_last, zone=$zone", $debug, 5);
			&add_nw_obj ($obj_name, "$obj_ip/$obj_netmask", $obj_type, $zone, $comment, $obj_ip_last, $uid_postfix, $debug);
			undef($obj_name); undef($obj_ip); undef($comment); undef($zone); undef($obj_ip_last); undef($obj_type); undef($obj_netmask);
			$context = &switch_context($context, 'firewall address', $debug); 
			next NEW_LINE; 
		}
		# adding zone 
		if ($line=~ /^\s+set\sassociated\-interface\s\"(.+?)\"$/ && ($context eq 'firewall address single object' || $context eq 'firewall addrgrp single object')) { 
			$zone = $1; &add_zone ($zone, $debug); 
			print_debug("found zone $zone", $debug, 2);
			next NEW_LINE; 
		}
		if ($line =~ /^\s+set\suuid\s(\w+)$/ && ($context eq 'firewall address single object' || $context eq 'firewall addrgrp single object' || 
				$context eq 'firewall vip single object')) {
			$uuid = $1;
			print_debug("found object uid $uuid", $debug, 4);
			next NEW_LINE; 
		}
		if ($line =~ /^\s+set\stype\s([\w\-]+)$/ && $context eq 'firewall address single object') {
			$obj_type = $1;
			if ($obj_type eq 'multicastrange' || $obj_type eq 'iprange') { $obj_type = 'ip_range'; }
			print_debug("found object type $obj_type", $debug, 4);
			next NEW_LINE; 
		}
		if ($line =~ /^\s+set\scomment\s[\'\"](.*?)[\'\"]$/ && ($context eq 'firewall address single object' || $context eq 'firewall addrgrp single object'
				|| $context eq 'firewall vip single object')) {
			if (defined($comment) && $comment ne '') { $comment .= "; $1"; } else { $comment = $1; }
			print_debug("found object comment $comment", $debug, 4);
			next NEW_LINE; 
		}
		if ($line =~ /^\s+set\s(wildcard\-)?fqdn\s\"(.*?)\"$/ && ($context eq 'firewall address single object' || $context eq 'firewall addrgrp single object')) {
			my ($wcard, $fqdn);
			if (defined($1)) { $wcard = $1; } else { $wcard = ''; }
			$fqdn = $2;
			if (defined($comment) && $comment ne '') { $comment .= "; ${wcard}fqdn $fqdn"; } else { $comment = "${wcard}fqdn $fqdn"; }
			print_debug("found fqdn object $comment", $debug, 4);
			next NEW_LINE; 
		}
		 
		if ($line =~ /^\s+set\sassociated\-interface\s\"(\w+)\"$/ && $context eq 'firewall address single object') {
			$zone = $1;
			print_debug("found object zone $zone", $debug, 4);
			next NEW_LINE; 
		}
		if ($line =~ /^\s+set\ssubnet\s(.+?)\s(.+?)$/ && $context eq 'firewall address single object') {
			$obj_ip = $1;
			$obj_netmask = $2;
			print_debug("found object ip $obj_ip with mask $obj_netmask", $debug, 4);
			next NEW_LINE; 
		}
########### multicast/ip range ipv4 network objects #####################
		if ($context eq 'firewall address single object' && $line =~ /^\s+set\sstart\-ip\s([\d\.]+)$/) {
			$obj_ip = $1;
			print_debug("found ip range start-ip $obj_ip, context=$context", $debug, 6);
			next NEW_LINE; 
		}
		if ($context eq 'firewall address single object' && $line =~ /^\s+set\send\-ip\s([\d\.]+)$/) {
			$obj_ip_last = "$1";
			print_debug("found ip range end-ip $obj_ip_last, context=$context", $debug, 6);
			next NEW_LINE; 
		}
########### NAT network objects #####################
		if ( $context eq '' && $line =~ /^config firewall vip$/) { $context = &switch_context($context, 'firewall vip', $debug); next NEW_LINE; }

		if ($context eq 'firewall vip' && $line =~ /^\s+edit\s\"(.+)"$/) {
			$obj_name = $1;
			print_debug("found vip object name $obj_name", $debug, 4);
			$context = &switch_context($context, $context . ' single object', $debug); 
			next NEW_LINE; 
		}
		if ($context eq 'firewall vip single object' && $line =~ /^\s+set\sextip\s([\d\.]+)\-?(.*?)$/) {
			$obj_ip = $1;
			print_debug("found nat object (starting) ip $obj_ip", $debug, 5);
			if (defined($2) && $2 ne '') { $obj_ip_last = "$2"; $obj_type = 'ip_range';
				print_debug("found nat object (ending) ip $obj_ip_last", $debug, 5);
			} else { $obj_type = 'host'; }
			next NEW_LINE; 
		}
		if ($line =~ /^\s+set\stype\s(\w+?)\-nat$/ && $context eq 'firewall vip single object') {
			$obj_type = $1;
			print_debug("found object type $obj_type", $debug, 4);
			$obj_type = 'host';		# dirty - resolve this to ip range type?
			next NEW_LINE; 
		}		
		if ($line=~ /^\s+set\sextintf\s\"(.+?)\"$/ && $context eq 'firewall vip single object') { 
#			$zone = $1;
#			if ($zone ne 'any') {
#				&add_zone ($zone, $debug); 
#				print_debug("found zone $zone", $debug, 2);
#			} else {
#				$zone = 'global';
#			}
			# above is not working with latest NAT example; so simply defining all VIPs as in zone global 
			$zone = 'global';
			next NEW_LINE; 
		}
		if ($line =~ /^\s+next$/ && $context eq 'firewall vip single object') {
			if (!defined($zone)) { $zone = 'global'; }			
			$obj_netmask = '255.255.255.255';
			print_debug("found nat obj $obj_name with ip $obj_ip in zone $zone", $debug, 5);
			&add_nw_obj ($obj_name, $obj_ip . '/' . &calc_subnetmask ($obj_netmask), $obj_type, $zone, $comment, $obj_ip_last, $uid_postfix, $debug);
			undef($obj_name); undef($obj_ip); undef($comment); undef($zone);
			$context = &switch_context($context, 'firewall vip', $debug); 
			next NEW_LINE; 
		}
		
#################### network object group ####################################
		if ($line =~ /^\s+set\smember\s(.+)$/ && $context eq 'firewall addrgrp single object') {
			$members = $1;
			print_debug("found object member string $members", $debug, 4);
			next NEW_LINE; 
		}
		if ($line =~ /^\s+next$/ && $context eq 'firewall addrgrp single object') {
			# address group wegschreiben
			if (!defined($zone)) { $zone = 'global'; }			
			&add_nw_obj_grp ($obj_name, $members, $zone, $comment, $uuid, $uid_postfix, $debug);
			undef($obj_name); undef($members); undef($comment);
			$context = &switch_context($context, 'firewall addrgrp', $debug);
			next NEW_LINE;
		}
		
###########  service section
# --------------- parsing network services ----------------
		if ($line =~ /^config firewall service custom$/ && $context eq '') {
			$context = &switch_context($context, 'firewall service custom', $debug);
			next NEW_LINE;
		}
		if ($line =~ /^end$/ && $context eq 'firewall service custom') {
			$context = &switch_context($context, '', $debug);
			next NEW_LINE;
		}
		if ($line =~ /\s+edit\s\"(.+?)\"$/ && $context eq 'firewall service custom') {
			$application_name = $1;
			print_debug("found application $application_name", $debug, 5);
			$context = &switch_context($context, 'firewall service custom single', $debug);
			next NEW_LINE; 
		}
		if ($line=~ /^\s+set\scomment\s[\'\"](.+?)[\'\"]$/ && $context eq 'firewall service custom single') { $comment = $1; }
#		if ($line =~ /^\s+icmp\-(.+?)\s(\d+)\;$/ && $context eq 'applications/application') { $icmp_art = $1; $icmp_nummer = $2; next NEW_LINE; }			

		if ($line =~ /\s+set\sprotocol\sICMP$/ && $context eq 'firewall service custom single') {
			$proto = 'icmp';
			print_debug("found icmp based service $application_name", $debug, 5);
			next NEW_LINE; 
		}
#			set tcp-portrange 22:1024-65535 143-145:100-200 22123
#			set udp-portrange 68 889 789-891
#			rebuild this into a group of services with single ports
		if ($line =~ /\s+set\s(udp|tcp)\-portrange\s(.+?)$/ && 
			($context eq 'firewall service custom single' || $context eq 'firewall service single multi-port group')) {
			my $old_proto = $proto;
			my $old_destination_port = $destination_port;
			$proto = $1;
			$destination_port = $2;
			if (defined($old_proto)) { # this is not the first portrange line for this service
				$context = &switch_context($context, 'firewall service single multi-port group', $debug);
				print_debug("starting multi-port firewall service group due to multi-proto service $application_name", $debug, 5);
				# transform old portrange into group
				$members = &transform_port_list_to_artificial_services($old_destination_port, $old_proto, $application_name, 
							$source_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $comment, $debug);
				print_debug("arty service 1st of 2 lines $application_name members: $members", $debug, 7);
				$members .= " " . &transform_port_list_to_artificial_services($destination_port, $proto, $application_name, 
							$source_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $comment, $debug);
				print_debug("arty service 1st & 2nd line $application_name members: $members", $debug, 7);
			} elsif ($destination_port =~ /\s/) {  # more than one port separated by space - but first portrange line encountered
				print_debug("starting multi-port firewall service group single proto $application_name", $debug, 5);
				$context = &switch_context($context, 'firewall service single multi-port group', $debug);
				$members = &transform_port_list_to_artificial_services($destination_port, $proto, $application_name, 
							$source_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $comment, $debug);
				print_debug("arty service 1st line $application_name members: $members", $debug, 5);
			} else { # single port range
				print_debug("starting vanilla service $application_name", $debug, 6);
				# nothing else todo except for checking for source port 
				if ($destination_port =~ /^(.+?)\:(.+?)$/) { # handle port ranges including source ports: set tcp-portrange 0-65535:0-65535
					$destination_port = $1;
					$source_port = $2;
				}
			}
			next NEW_LINE; 
		}
		if ($line =~ /\s+next$/ && $context eq 'firewall service custom single') {  # service wegschreiben
			&print_debug("before calling add_nw_service_obj: for application $application_name", $debug, 5);
			&add_nw_service_obj ($application_name, $proto, $source_port, $destination_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $comment, $debug);
			$context = &switch_context($context, 'firewall service custom', $debug);
			undef($application_name); undef($proto); undef($source_port); undef($destination_port); undef($uuid); undef($rpc); undef($icmp_art); undef($icmp_nummer);
			undef ($comment);
			next NEW_LINE;
		}
# -------------- parsing service groups ------------------
		if ($line =~ /^config firewall service group$/ && $context eq '') {
			print_debug("starting firewall service group", $debug, 6);
			$context = &switch_context($context, 'firewall service group', $debug);
			next NEW_LINE;
		}
		if ($line =~ /^\s+edit\s\"(.+?)"$/ && $context eq 'firewall service group') {
			$context = &switch_context($context, 'firewall service single group', $debug);
			$application_name = $1;
			next NEW_LINE;
		}
		if ($line =~ /^\s+set\smember\s(.+)$/ && $context eq 'firewall service single group') {
			$members = $1;
			print_debug("found service members $members in service group $application_name", $debug, 6);
			next NEW_LINE;
		}
		if ($line =~ /^\s+set\scomment\s[\'\"](.*?)[\'\"]$/ && ($context eq 'firewall service single group')) {
			$comment = $1;
			print_debug("found service group comment $comment", $debug, 4);
			next NEW_LINE; 
		}
#	closig statements
		if ($line =~ /^end$/ && $context eq 'firewall service group') {	$context = &switch_context($context, '', $debug); }
		if ($line =~ /^\s+next$/ && ($context eq 'firewall service single group' || $context eq 'firewall service single multi-port group')) {
			if ($context eq 'firewall service single group') {
				$context = &switch_context($context, 'firewall service group', $debug);
			} else { # moving from multi-port group back to non-group environment (context='firewall service single multi-port group')
				$context = &switch_context($context, 'firewall service custom', $debug);				
			}
			if (!defined($members)) { print_debug("warning: application group $application_name: members undefined", $debug, 1); }
			print_debug("adding service group $application_name with members: $members", $debug, 5);
			# service group wegschreiben
			&add_nw_service_obj_grp ($application_name, $members, $comment, $debug);
			undef($application_name); undef($proto); undef($source_port); undef($destination_port); undef($uuid); undef($rpc); undef($icmp_art); undef($icmp_nummer);
			undef ($comment); undef ($members);
			next NEW_LINE;
		}
	}
	return 0;
}


sub cfg_file_complete { 	# check auf Vollstaendigkeit des Config-Files:
	my $debug_level = shift;
	my $ln_cnt = $#config_lines;
	my $cfg_file_complete = 1;	


	while ($config_lines[$ln_cnt] =~ /^\s*$/ ) { $ln_cnt -- ; }		# ignore empty lines at the end
	if ($config_lines[$ln_cnt-2] !~ /^end/ && $config_lines[$ln_cnt-1] !~ /^$/ && $config_lines[$ln_cnt] !~ /^\w+ [\$\#] \.$/) {
		$cfg_file_complete = 0;
		print_debug ("ERROR: expected end of config to be end, blank line, prompt with fw name. Instead got: " . 
			$config_lines[$ln_cnt-2] . $config_lines[$ln_cnt-1] . $config_lines[$ln_cnt], $debug_level, -1);
	}
	return $cfg_file_complete;
}

sub switch_context {
	my $old_level = shift;
	my $new_level = shift;
	my $debug_level = shift;	
	print_debug("switching context from $old_level to $new_level", $debug_level, 4);
	return $new_level;
}

sub parse_config_rules  { # ($debuglevel_main, $mgm_name_in_config)
	my $debug = shift;
	my $mgm_name = shift;
	my ($source, $destination, $application, $from_zone, $to_zone, $policy_name, $track, $uid_postfix, $comment);
	my $line = '';
	my $context = '';
	my $disabled ='';
	my $action = 'deny';
	my $rule_no = 0;
	my ($policy_id, $policy_uid);
	my $list;
	my $list_typ;
	my $svc_neg = 0;
	my $src_neg = 0;
	my $dst_neg = 0;
	my $policy_type;
	
	&print_debug("entering parse_config_rules =======================================================",$debug,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		&print_debug("pcr: $line", $debug, 9);
		if ($line =~ /^config firewall policy(.*?)$/ && $context eq '') {  # match all policy types 4, 6, 4to6 6to4, ...
			$policy_type = $1;
			$context = &switch_context($context, 'firewall policy', $debug);  
			next NEW_LINE; 
		}
		if ($line =~ /^\s+edit\s(\d+)$/ && $context eq 'firewall policy') { 
			$context = &switch_context($context, 'firewall policy single rule', $debug);
			$policy_id = $1;
			# to avoid duplicate policy ids (v6 and v4 may have clashing ids)
			if ($policy_type ne '') { $policy_id = "ipv$policy_type.$policy_id"; }
			&print_debug("found policy $policy_id",$debug,2);
			next NEW_LINE; 
		}
		if ($line=~ /^\s+set\ssrcintf\s\"(.+?)\"$/ && $context eq 'firewall policy single rule') {
			$from_zone = $1;
			&add_zone ($from_zone, $debug); 
			&print_debug("found from zone $from_zone",$debug,5);
			next NEW_LINE; 
		}
		if ($line=~ /^\s+set\sdstintf\s\"(.+?)\"$/ && $context eq 'firewall policy single rule') {
			$to_zone = $1;
			&add_zone ($to_zone, $debug); 
			&print_debug("found to zone $to_zone",$debug,5);
			next NEW_LINE; 
		}
		# negated rule parts:
		if ($line=~ /^\s+set\s(service|srcaddr|dstaddr)\-negate\senable$/ && $context eq 'firewall policy single rule') {
			my $negation = $1;
			if ($negation eq 'service') { $svc_neg= '1'; }
			if ($negation eq 'srcaddr') { $src_neg= '1'; }
			if ($negation eq 'dstaddr') { $dst_neg= '1'; }
			&print_debug("found negation $negation",$debug,5); 
			next NEW_LINE; 
		}		
		
		if ($line=~ /^\s+set\sname\s\"(.*?)\"$/ && $context eq 'firewall policy single rule') {
			$policy_name = $1;
			&print_debug("found rule name $policy_name",$debug,5); 
			next NEW_LINE; 
		}		
		if ($line=~ /^\s+set\scomments\s\"(.*?)\"$/ && $context eq 'firewall policy single rule') {
			$comment = $1;
			&print_debug("found single line rule comment $comment",$debug,5); 
			next NEW_LINE; 
		}
		if ($line=~ /^\s+set\scomments\s\"(.*?)$/ && $context eq 'firewall policy single rule') { 
			$comment = $1; 
			$context = &switch_context($context, 'firewall policy single rule multi-line comment', $debug);
			&print_debug("found start of multi-line rule comment $comment",$debug,5); 
			next NEW_LINE; 
		}
		if ($line=~ /^(.*?)(\"?)$/ && $context eq 'firewall policy single rule multi-line comment') {
			$comment .= "\n$1"; 
			&print_debug("found more lines of multi-line comment $comment",$debug,5); 
			if (defined($2) && $2 eq '"') { $context = &switch_context($context, 'firewall policy single rule', $debug); }
			next NEW_LINE; 
		}
		if ($line=~ /^\s+set\sstatus\s(en|dis)able$/ && $context eq 'firewall policy single rule') {
			if ($1 eq 'en') { $disabled=''; } else { $disabled = "inactive" } 
			&print_debug("found rule dis/enable statement: $disabled",$debug,5); 
			next NEW_LINE;
		}
		if ($line=~ /^\s+set\suuid\s(.*?)$/ && $context eq 'firewall policy single rule') {
			$policy_uid = $1;
			&print_debug("found policy uid: $policy_uid",$debug,5); 
			next NEW_LINE;
		}

		if ($line =~ /^\s+set (src|dst)addr\s(.+)$/ && $context eq 'firewall policy single rule') {
			$list_typ = $1;
			$list = $2;
			if ($list_typ eq 'src') { $source = $list; }
			if ($list_typ eq 'dst') { $destination = $list; }
			&print_debug("found src/dst list ($list_typ): $list",$debug,5); 
			next NEW_LINE;
		}
		if ($line =~ /^\s+set\sservice\s(.*?)$/ && $context eq 'firewall policy single rule') {
			$application = $1;
			&print_debug("found service: $application",$debug,5); 
			next NEW_LINE;
		}
#	action / track part of rule (then)
		if ($line=~ /^\s+set\saction\s(accept|deny|reject)$/ && $context eq 'firewall policy single rule') { 
			$action = $1; 
			&print_debug("found action: $action",$debug,5); 
			next NEW_LINE; 
		}
		if ($line=~ /^\s+set\slogtraffic[\s|-](disable|utm|all|start enable)$/ && $context eq 'firewall policy single rule') {
			my $found_track = $1;
			if ($found_track eq 'disable') { $found_track = 'none'; }
			if (!defined($track) || $track eq '') { $track = $found_track; }
			elsif ($found_track eq 'start enable') { $track .= ' start'; } 
			&print_debug("found track: $track",$debug,5); 
			next NEW_LINE;
		}
#closing sections
		if ($line =~ /^end$/ && $context eq 'firewall policy') { 
			$context = &switch_context($context, '', $debug);  
			&print_debug("found end of policy section, stopping rule parsing.",$debug,5); 
			next NEW_LINE;
#			config file may contain more than one "config firewall policy" section
		}
		if ($line =~ /^\s+next$/ && $context eq 'firewall policy single rule') { 
			if (!defined($track)) { $track=''; }
			if (!defined($policy_name)) { $policy_name=''; }
			if (!defined($from_zone)) { $from_zone=''; }
			if (!defined($to_zone)) { $to_zone=''; }
			if (!defined($policy_name)) { $policy_name=''; }
			if ($policy_type ne '') { $uid_postfix = ".ipv$policy_type.uid"; } else { $uid_postfix = ''; }
			&print_debug("found rule from $from_zone to $to_zone, name=$policy_name, disabled=$disabled, src=$source, dst=$destination, svc=$application, action=$action, track=$track", $debug,3);
			# Regel wegschreiben

			# using UID as uid
			# $rule_no = &add_rule ($rule_no, $from_zone, $to_zone, $policy_uid, $disabled, $source, $destination, $application,
			# 	$action, $track, $comment, $policy_name, $svc_neg, $src_neg, $dst_neg, $uid_postfix, $debug);

			# using id as uid
			$rule_no = &add_rule ($rule_no, $from_zone, $to_zone, $policy_id, $disabled, $source, $destination, $application,
				$action, $track, $comment, $policy_name, $svc_neg, $src_neg, $dst_neg, $uid_postfix, $debug);

			$context = &switch_context($context, 'firewall policy', $debug);
			# reset all values after a rule has been parsed:
			$action='deny'; # default value for action = deny (not contained in config!)
			$disabled=''; $source=''; $destination=''; $application=''; $uid_postfix='';
			undef($track); undef($policy_name); undef($comment); undef($from_zone); undef($to_zone);
			$svc_neg = 0; $src_neg = 0; $dst_neg = 0;
			
			next NEW_LINE;
		} # end of rule
	}
	&sort_rules_and_add_zone_headers($debug);
    $rulebases{"$rulebase_name.ruleorder"} = join(',', @ruleorder);
	return 0;
}
 
sub purge_non_vdom_config_lines { # ($vdom_name)
	my $vdom_name = shift;
	my $debug = shift;
 	my $ln_cnt = 0;
	my $found_correct_vdom = 0;
	my @vdom_config_lines;
	my ($start_of_vdom, $end_of_vdom);

	$end_of_vdom = $#config_lines;	# if vdom is the last vdom in file
	LINE: while (!$found_correct_vdom && $ln_cnt<$#config_lines) {
		if (!defined($start_of_vdom) &&
			$config_lines[$ln_cnt] =~ /^config vdom$/ && 
			$config_lines[$ln_cnt+1] =~ /^edit ${vdom_name}$/ 
			# && $config_lines[$ln_cnt+2] =~ /^config system settings$/
			#&& $config_lines[$ln_cnt+2] =~ /^config system /
			&& $config_lines[$ln_cnt+2] !~ /^next$/		# filter out vdom defining lines at the very begining
			)
		{
			$start_of_vdom = $ln_cnt+2;
			$ln_cnt++ ; 
		}
		if (defined($start_of_vdom) &&
			$config_lines[$ln_cnt] =~ /^config vdom$/ && 
			$config_lines[$ln_cnt+1] =~ /^edit .+?$/ 
			#&& $config_lines[$ln_cnt+2] =~ /^config system /
			&& $config_lines[$ln_cnt+2] !~ /^next$/
			)
		{
			$end_of_vdom = $ln_cnt;
			$ln_cnt = $#config_lines;
		}
		$ln_cnt++ ; 
	}
	@vdom_config_lines = splice(@config_lines, $start_of_vdom, $end_of_vdom-$start_of_vdom+1);
	&print_debug("found vdom $vdom_name from line $start_of_vdom to line $end_of_vdom",$debug,1); 
	@config_lines = @vdom_config_lines;
}

sub remove_pagination { # removes --More-- ^M         ^M     
	my $debug = shift;           
 	my $ln_cnt = 0;

	&print_debug("start of pagination removal",$debug,3);	
	while ($ln_cnt<$#config_lines) {
		if ($config_lines[$ln_cnt] =~ /^\-\-More\-\-\s\r\s+\r(\s*.+)$/) {
			$config_lines[$ln_cnt] = $1;
		}
		$ln_cnt++;
	}
	&print_debug("end of pagination removal",$debug,3);
}
 
#####################################################################################
# MAIN

sub parse_config { # ($obj_file, $rule_file, $rulebases, $user, $fworch_workdir, $debug_level, $mgm_name, $config_dir, $import_id)
	my $in_file_main = shift;
	shift; shift; shift; # $rule_file und $rulebases und $user nicht verwendet
	my $fworch_workdir = shift;
	my $debuglevel_main = shift;
	my $mgm_name = shift;
	my $config_dir = shift;
	my $import_id = shift;
	my $debug_level   = shift;
	my $vdom_name;

	# initializing global variables:
	@services = ();
	@network_objects = ();
	&print_debug ("in_file_main=$in_file_main, fworch_workdir=$fworch_workdir, debuglevel_main=$debuglevel_main, mgm_name=$mgm_name, config_dir=$config_dir, import_id=$import_id", $debuglevel_main, 6);

	open (IN, $in_file_main) || die "$in_file_main konnte nicht geoeffnet werden.\n";
	@config_lines = <IN>;	# sichern Config-array fuer spaetere Verwendung
	close (IN);
	&remove_pagination($debuglevel_main);

	if (!&cfg_file_complete($debuglevel_main)) { return "incomplete-config-file-$mgm_name"; }	
	else {
#		my $mgm_name_in_config = &parse_mgm_name($debuglevel_main);			
		@rulebases = ($mgm_name);
		$rulebase_name = $mgm_name;
		&print_debug("parse_mgm_name: found hostname of single system: $mgm_name and setting rulebase_name to $mgm_name",$debuglevel_main,1);
		
		my $vdom_names = &parse_vdom_names($debuglevel_main);
		if ($vdom_names ne '' && $mgm_name =~ /.+?\_\_\_(.+)$/ ) { # file contains multiple vdoms and mgm_name also contains xxx___vdom_name
			$vdom_name = $1;
			&print_debug ("rulebase_name: $rulebase_name, mgm_name=$mgm_name, mgm_name_in_config=$mgm_name, vdom_name:$vdom_name", $debuglevel_main, 2);
			&purge_non_vdom_config_lines ($vdom_name,$debuglevel_main);	# removes all lines that do not belong to correct vdom from @config_lines
		}
		&parse_config_base_objects  ($debuglevel_main, $mgm_name); # zones, simple network and service objects  
		push @zones, "global"; 	# Global Zone immer hinzufuegen
		&parse_config_rules ($debuglevel_main, $mgm_name); # finally parsing the rule base, ignoring the rulebase name in fworch config

		&print_results_files_objects($fworch_workdir, $mgm_name, $import_id);
		&print_results_files_rules  ($fworch_workdir, $mgm_name, $import_id);
		&print_results_files_zones  ($fworch_workdir, $mgm_name, $import_id);
#		print_results_monitor('objects');
#		print_results_monitor('rules');
	}
	return 0;
}
	
1;
__END__

=head1 NAME

CACTUS::FWORCH::parser - Perl extension for fworch fortinet parser

=head1 SYNOPSIS

  use CACTUS::FWORCH::import::fortinet;

=head1 DESCRIPTION

fworch Perl Module support for importing configs into fworch Database

=head2 EXPORT

  global variables


=head1 SEE ALSO

  behind the door

=head1 AUTHOR

Cactus eSecurity, tmp@cactus.de

=cut
