# !/usr/bin/perl -w
# $Id: cisco.pm,v 1.1.2.2 2011-05-30 07:42:18 tim Exp $
# $Source: /home/cvs/iso/package/importer/CACTUS/FWORCH/import/Attic/cisco.pm,v $

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
our %junos_uuids = ();

## parse_audit_log Funktion fuer ASA (noch) nicht implementiert
sub parse_audit_log { }

#####################################
# add_nw_obj
# param1: obj_name
# param2: obj_ip (with netmask)
# param3: obj zone
# param4: debuglevel [0-?]
#####################################
sub add_nw_obj {
	my $act_obj_name = shift;
	my $act_obj_ipaddr = shift;
	my $act_obj_mask = shift;
	my $debuglevel = shift;
	my $act_obj_ipaddr_last = '';
	my $act_obj_comm = '';
	my $act_obj_type = '';
	my $act_obj_loc = '';
	my $act_obj_color = 'black';
	my $act_obj_sys = '';
	my $act_obj_uid = '';
		
	if (!defined($act_obj_mask) || $act_obj_mask eq '') { $act_obj_mask = "32"; }
	elsif ($act_obj_mask =~ /\./) { $act_obj_mask = &calc_subnetmask($act_obj_mask); if ($act_obj_mask ne "32") { $act_obj_name = $act_obj_name . '_mask_' . $act_obj_mask; } }
	# otherwise leave mask as is (assuming int between 0 and 32)
	print_debug("add_nw_obj called with name=$act_obj_name, ip=$act_obj_ipaddr", $debuglevel, 5);
	if (!defined ($network_objects{"$act_obj_name.name"})) {
		@network_objects = (@network_objects, $act_obj_name);
		$network_objects{"$act_obj_name.name"} = $act_obj_name;
		$network_objects{"$act_obj_name.UID"} = $act_obj_name;
		if ($act_obj_mask==32) { $network_objects{"$act_obj_name.type"} = 'host' };
		if ($act_obj_mask<32)  { $network_objects{"$act_obj_name.type"} = 'network' };
		$network_objects{"$act_obj_name.netmask"} = $act_obj_mask ;
		$network_objects{"$act_obj_name.ipaddr"} = $act_obj_ipaddr;
		$network_objects{"$act_obj_name.ipaddr_last"} = $act_obj_ipaddr_last;
		$network_objects{"$act_obj_name.color"} = $act_obj_color;
		$network_objects{"$act_obj_name.comments"} = $act_obj_comm;
		$network_objects{"$act_obj_name.location"} = $act_obj_loc;
		$network_objects{"$act_obj_name.sys"} = $act_obj_sys;
	} else {
		print_debug ("found duplicate object definition for network object $act_obj_name", $debuglevel, 6); # no error with cisco configs
	}
}

#####################################
# add_nw_obj_group_member
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub add_nw_obj_group_member{
	my $group_name = shift;
	my $member_name = shift;
	my $debuglevel = shift;

	my $act_obj_name = '';
	my $act_obj_mbr = '';
	my $act_obj_fkt = '';
	my $act_obj_color = 'black';
	my $act_obj_comm = '';
	my $mbrlst = '';
	my $mbr_ref_lst = '';

	if (!defined ($network_objects{"$group_name.name"})) {
		@network_objects = (@network_objects, $group_name);
		$network_objects{"$group_name.name"} = $group_name;
		$network_objects{"$group_name.UID"} = $group_name;
		print_debug ("added group $group_name", $debuglevel, 5);
		$network_objects{"$group_name.type"} = 'group';
		$network_objects{"$group_name.color"} = $act_obj_color;
	}
#		die ("reference to undefined network object $member_name found in group $group_name, zone: $act_obj_zone");
	if (defined($network_objects{"$group_name.members"})) {
		$mbrlst = $network_objects{"$group_name.members"};
		$mbr_ref_lst = $network_objects{"$group_name.member_refs"};
	}
	if ( $mbrlst eq '' ) {
		$mbrlst = $member_name;
		$mbr_ref_lst = $member_name;
	}
	else {
		$mbrlst = "$mbrlst|$member_name";
		$mbr_ref_lst = "$mbr_ref_lst|$member_name";
	}
	$network_objects{"$group_name.members"} = $mbrlst;
	$network_objects{"$group_name.member_refs"} = $mbr_ref_lst;
	return;
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
	my $debuglevel = shift
	my @range;
	my $act_obj_typ = 'simple';
	my $act_obj_type = '';
	my $act_obj_src_last = '';
	my $act_obj_dst_last = '';
	my $act_obj_comm = '';
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
			$services{"$act_obj_name.ip_proto"} = '';
		}
		$services{"$act_obj_name.timeout"} = $act_obj_time;
		$services{"$act_obj_name.color"} = $act_obj_color;
		$services{"$act_obj_name.comments"} = $act_obj_comm;
		$services{"$act_obj_name.typ"} = $act_obj_typ;
		$services{"$act_obj_name.type"} = $act_obj_type;
		if (defined($act_obj_rpc)) {
			$services{"$act_obj_name.rpc_port"} = $act_obj_rpc;
		} else {
			$services{"$act_obj_name.rpc_port"} = '';				
		}
		$services{"$act_obj_name.UID"} = $act_obj_name;		
		if (defined($act_obj_uid) && $act_obj_uid ne '') { $junos_uuids{"$act_obj_uid"} = $act_obj_name; }	#	collect uid refs
		print_debug("add_nw_service_obj: added application $act_obj_name", $debuglevel, 4);
	} else {
		print_debug("add_nw_service_obj: warning duplicate defintion of service $act_obj_name", $debuglevel, 1);
	}
	return;
}

#####################################
# add_nw_service_obj_grp 
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub add_nw_service_obj_grp { # ($application_name, $members, $members_proto, $members_uid, $debuglevel_main);
	my $act_obj_name = shift;
	my $act_obj_members = shift;
	my $debuglevel = shift;
	my $act_obj_typ = 'group';
	my $act_obj_type = '';
	my $act_obj_proto = '';
	my $act_obj_src_last = '';
	my $act_obj_dst_last = '';
	my $act_obj_comm = '';
	my $act_obj_time = '';
	my $act_obj_time_std = '';
	my $act_obj_color = 'black';
	my $act_obj_rpc = '';
	my $mbrlst = '';
	
	if (!defined ($services{"$act_obj_name.name"})) {
		@services = (@services, $act_obj_name);
		$services{"$act_obj_name.name"} = $act_obj_name;
		print_debug("adding service group $act_obj_name", $debuglevel, 5);
	} else {
		print_debug("re-defining service group $act_obj_name", $debuglevel, 5);		
	}
	if (defined($act_obj_members) && $act_obj_members ne '') {
		$services{"$act_obj_name.members"} = $act_obj_members; # simple group case
		$services{"$act_obj_name.member_refs"} = $act_obj_members; # simple group case
		print_debug("adding service group $act_obj_name with members $act_obj_members", $debuglevel, 5);
	} else {
		print_debug("no members defined", $debuglevel, 1);
	}
	$services{"$act_obj_name.ip_proto"} = $act_obj_proto;
	$services{"$act_obj_name.timeout"} = $act_obj_time;
	$services{"$act_obj_name.color"} = $act_obj_color;
	$services{"$act_obj_name.comments"} = $act_obj_comm;
	$services{"$act_obj_name.typ"} = $act_obj_typ;
	$services{"$act_obj_name.type"} = $act_obj_type;
	$services{"$act_obj_name.rpc_port"} = $act_obj_rpc;
	$services{"$act_obj_name.UID"} = $act_obj_name;
}

sub junos_split_list {  # param1 = list of objects (network or service)
	my $list = shift;
	my $debug_level = shift;
	my $orig_list = $list;
	
	if ($list =~ /^\[\s([\w\-\_\/\.\d\s]+?)\s\]$/) { # standard list format: [ x1 x2 x3 ]
		$list = $1;
		$list = join('|', split(/\s/, $list));
	} elsif ($list =~ /[\w\-\_\/\.\d]+/) {
		# found single object, no changes necessary
	} else {
		print_debug("warning in junos_split_list: orig_list=$orig_list; found no match for object list", $debug_level, 1);
	}
#	print_debug("junos_split_list: orig_list=$orig_list, result=$list", $debug_level, 5);
	return $list;
}

#####################################
# add_rule 
# param1: 
# debuglevel [integer]
#####################################
sub add_rule { # ($rule_no, $from_zone, $to_zone, $policy_name, $disabled, $source, $destination, $application, $action, $track, $debuglevel_main)
	my $rule_no = shift;
	my $from_zone = shift;
	my $to_zone = shift;
	my $policy_name = shift;
	my $disabled = shift;
	my $source = shift;
	my $destination = shift;
	my $service = shift;
	my $action = shift;
	my $track = shift;
	my $debuglevel = shift;
	my $rule_id;

#	print_debug ("add_rule: rulebase_name=$rulebase_name, rulecount=" . $rulebases{"$rulebase_name.rulecount"}, $debuglevel, 4);
	$rulebases{"$rulebase_name.rulecount"} = $rule_no + 1;	# Anzahl der Regeln wird sukzessive hochgesetzt
	$rule_id = "from_zone__$from_zone" . "__to_zone__$to_zone" . "__$policy_name";
	$ruleorder[$rule_no] = $rule_no;

	if (!defined($track) || $track eq '') { $track = 'none'; }
	if (length($track)<3) { print_debug ("warning, short track: <$track>", $debuglevel, 1); }
	
	$rulebases{"$rulebase_name.$rule_no.src"} = '';
	foreach my $src (split(/\|/, &junos_split_list($source, $debuglevel))) {
		if ($rulebases{"$rulebase_name.$rule_no.src"} ne '') {
			$rulebases{"$rulebase_name.$rule_no.src"} .= '|';
			$rulebases{"$rulebase_name.$rule_no.src.refs"} .= '|';
		}
		$rulebases{"$rulebase_name.$rule_no.src"} .= "$src";
		$rulebases{"$rulebase_name.$rule_no.src.refs"} .= ("$src" . "__zone__$from_zone");
	}
	$rulebases{"$rulebase_name.$rule_no.dst"} = '';
	foreach my $dst (split(/\|/, &junos_split_list($destination, $debuglevel))) {
		if ($rulebases{"$rulebase_name.$rule_no.dst"} ne '') {
			$rulebases{"$rulebase_name.$rule_no.dst"} .= '|';
			$rulebases{"$rulebase_name.$rule_no.dst.refs"} .= '|';
		}
		$rulebases{"$rulebase_name.$rule_no.dst"} .= "$dst";
		$rulebases{"$rulebase_name.$rule_no.dst.refs"} .= ("$dst" . "__zone__$to_zone");
	}
		
	$rulebases{"$rulebase_name.$rule_no.services"} = '';
	foreach my $svc (split(/\|/, &junos_split_list($service, $debuglevel))) {
		if ($rulebases{"$rulebase_name.$rule_no.services"} ne '') {
			$rulebases{"$rulebase_name.$rule_no.services"} .= '|';
			$rulebases{"$rulebase_name.$rule_no.services.refs"} .= '|';
		}
		$rulebases{"$rulebase_name.$rule_no.services"} .= "$svc";
		$rulebases{"$rulebase_name.$rule_no.services.refs"} .= "$svc";
	}
	
	$rulebases{"$rulebase_name.$rule_no.id"} = $rule_id;
	$rulebases{"$rulebase_name.$rule_no.ruleid"} = $rule_id;
	$rulebases{"$rulebase_name.$rule_no.order"} = $rule_no;
	if ($disabled eq 'inactive') { $rulebases{"$rulebase_name.$rule_no.disabled"} = '1'; }
	else { $rulebases{"$rulebase_name.$rule_no.disabled"} = '0'; }
	$rulebases{"$rulebase_name.$rule_no.src.zone"} = $from_zone;
	$rulebases{"$rulebase_name.$rule_no.dst.zone"} = $to_zone;
	$rulebases{"$rulebase_name.$rule_no.services.op"} = '0';
	$rulebases{"$rulebase_name.$rule_no.src.op"} = '0';
	$rulebases{"$rulebase_name.$rule_no.dst.op"} = '0';
	$rulebases{"$rulebase_name.$rule_no.action"} = $action;
	$rulebases{"$rulebase_name.$rule_no.track"} = $track;
	$rulebases{"$rulebase_name.$rule_no.install"} = '';		# set hostname verwenden ?
	$rulebases{"$rulebase_name.$rule_no.name"} = '';		# kein Aequivalent zu CP rule_name
	$rulebases{"$rulebase_name.$rule_no.time"} = '';
	$rulebases{"$rulebase_name.$rule_no.comments"} = $policy_name;
	$rulebases{"$rulebase_name.$rule_no.UID"} = $rule_id;
	$rulebases{"$rulebase_name.$rule_no.header_text"} = '';
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
	if (!$is_there) { push @zones, $new_zone; &print_debug("adding new zone: $new_zone", $debug, 1); }
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
		
	$act_obj_nameZ = "${act_obj_name}__zone__$act_obj_zone";	# Zone in Feldindex mit aufgenommen
	if (!defined ($network_objects{"$act_obj_nameZ.name"})) {
		@network_objects = (@network_objects, $act_obj_nameZ);
		$network_objects{"$act_obj_nameZ.name"} = $act_obj_name;
	} elsif (defined ($network_objects{"$act_obj_nameZ.name"})) {
		print "sub ns_object_address NET_OBJECT: $act_obj_nameZ ist bereits definiert.\n";
	} else {
		print "sub ns_object_address NET_OBJECT: $act_obj_nameZ ist undefiniert.\n";
	}
	my $subnetbits = calc_subnetmask ($act_obj_mask);
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
# read_predefined_services(
############################################################
sub read_predefined_services {
	my $device_type = shift;
#	my $predefined_service_string = shift;
	my $debug_level = shift;
	my $predef_svc;
	my ($svc_name,$ip_proto,$port,$port_end,$timeout,$comment,$typ,$group_members);

	$predef_svc = exec_pgsql_cmd_return_value ("SELECT dev_typ_predef_svc FROM stm_dev_typ WHERE dev_typ_id=$device_type");
	my @predef_svc = split /\n/, $predef_svc;
	
	&print_debug("inserting pre-defined services for junos: $predef_svc",$debug_level,8);
	
	foreach my $svc_line (@predef_svc) {
		($svc_name,$ip_proto,$port,$port_end,$timeout,$comment,$typ,$group_members) = split /;/, $svc_line;
		$services{"$svc_name.name"}			= $svc_name;
		$services{"$svc_name.port"}			= $port;
		$services{"$svc_name.port_last"}	= $port_end;
		$services{"$svc_name.ip_proto"}		= $ip_proto;
		$services{"$svc_name.timeout"}		= $timeout;
		$services{"$svc_name.color"}		= "black";
#		$services{"$svc_name.comments"}		= "$predefined_service_string, $comment";
		$services{"$svc_name.comments"}		= $comment;
		$services{"$svc_name.typ"}			= $typ;
		$services{"$svc_name.type"}			= "";
		$services{"$svc_name.rpc_port"}		= "";
		$services{"$svc_name.UID"}			= $svc_name;
		$services{"$svc_name.members"}		= $group_members;
		$services{"$svc_name.member_refs"}	= $group_members;
		push @services, $svc_name;
	}
	return;
}

sub resolve_service_uuid_references {	# ($debuglevel_main);
	my $debug = shift;
	
	foreach my $nw_svc (@services) {
		&print_debug("resolve_service_uuid_references: checking service $nw_svc, typ=" . $services{"$nw_svc.typ"} . "type=" . $services{"$nw_svc.type"}, $debug, 5);
		if ($services{"$nw_svc.typ"} eq 'group') {
			&print_debug("resolve_service_uuid_references: checking service group $nw_svc", $debug, 5);
			my @members = split (/\|/, $services{"$nw_svc.member_refs"});
			my $member_string = '';
			my $change_flag = 0;
			foreach my $member (@members) {
				&print_debug("resolve_service_uuid_references: checking member $member", $debug, 5);
				if ($member =~ /^[a-f0-9]\-[a-f0-9]\-[a-f0-9]\-[a-f0-9]\-[a-f0-9]$/) {
					my $old_member = $member;
					$change_flag = 1;
					$member = $junos_uuids{"$member"};
					&print_debug("resolve_service_uuid_references: replacing uuid $old_member with $member", $debug, 5);
				}
				if ($member_string ne '') { $member_string .= '|'; }
				$member_string .= $member;
			}
			if ($change_flag) {
				$services{"$nw_svc.members"} = $member_string;
				$services{"$nw_svc.member_refs"} = $member_string;
			}
		}
	}	
	return;
}

############################################################
# copy_config_from_mgm_to_iso($ssh_private_key, $ssh_user, $ssh_hostname, $management_name,
# $obj_file_base, $cfg_dir, $rule_file_base)
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
	my $cmd;
	my $fehler_count = 0;
	my $result;

	$cmd = "$scp_bin $scp_batch_mode_switch -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname:asa_config $cfg_dir/$obj_file_base";	# dummy
#	$cmd = "$ssh_bin -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname show config > $cfg_dir/$obj_file_base";	# Cisco ASA - noch offen
	if (system ($cmd)) { $fehler_count++; }
	return ($fehler_count, "$cfg_dir/$obj_file_base" );
}

sub sort_rules_and_add_zone_headers {
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
			(my $src_zone, my $dst_zone) = split / : /, $new_zone_string;
			splice(@ruleorder,$count,0,$new_rule_id); # fuegt neue Regel ein
			splice(@rule_zones,$count,0,$new_zone_string); 
			$anzahl_regeln++;
		    $rulebases{"$rulebase_name.rulecount"} = $anzahl_regeln;
			$rulebases{"$rulebase_name.$ruleorder[$count].id"} = $new_rule_id;
			$rulebases{"$rulebase_name.$ruleorder[$count].header_text"} = $new_zone_string;
			$rulebases{"$rulebase_name.$ruleorder[$count].UID"} = $new_rule_id;
			$rulebases{"$rulebase_name.$ruleorder[$count].src"} = "any";
			$rulebases{"$rulebase_name.$ruleorder[$count].dst"} = "any";
			$rulebases{"$rulebase_name.$ruleorder[$count].services"} = "any";
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
}

sub parse_mgm_name { # ($obj_file, $fworch_workdir, $debug_level, $mgm_name, $config_dir, $import_id)
	my $in_file_main = shift;
	my $fworch_workdir = shift;
	my $debuglevel_main = shift;
	my $mgm_name = shift;
	my $config_dir = shift;
	my $import_id = shift;
	my $line = '';
	my $context = '';
	my @nodes= ();

	&print_debug("entering parse_mgm_name",$debuglevel_main,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		if ($line=~ /^hostname\s+(.+?)$/ && ($context eq '')) {
			$mgm_name = $1;
			@nodes = ($mgm_name, @nodes);
			&print_debug("parse_mgm_name: found hostname: $mgm_name",$debuglevel_main,1);
			$context = '';
			@rulebases = ($mgm_name);
			$rulebase_name = $mgm_name;
			&print_debug("parse_mgm_name: found hostname of single system: $mgm_name and setting rulebase_name to $mgm_name",$debuglevel_main,1);
			return $mgm_name; 
		}
	}
	&print_debug("ERROR: end of parse_mgm_name: at end without match (mgm_name=$mgm_name)",$debuglevel_main,-1);
}

sub extract_ip_addresses_from_string {
	my $input_string = shift;
	my $debug = shift;
	my @list_of_ips = ();
	my $network_part;
	my $ip;

	print_debug("acl extract_ip_addresses_from_string entering with: input=$input_string", $debug, 4);
	while ($input_string =~ /^.*?(h?o?s?t?)\s?(\d+\.\d+\.\d+\.\d+)(.*)$/) { # at least one explicit ip address included
		my $rest = $6;
		my $host = $1;
		my $ip = $2;
		if (defined($host) && $host ne '') {
			@list_of_ips = (@list_of_ips, "$ip/32");
		} else {
			if (defined($network_part)) {
				@list_of_ips = (@list_of_ips, "$network_part/" . &calc_subnetmask($ip));
				undef($network_part);
			} else {
				$network_part = $ip;
			}
		}
		$input_string = $rest;
	}
	return join(',', @list_of_ips);
}

# result: ($application_name, $proto, $destination_port, $icmp_art)
sub extract_services_from_string {
	my $input_string = shift;
	my $debug = shift;
	my @list_of_svcs = ();
	my $svc;
	my $proto;
	my $destination_port;
	my $icmp_art;
	my $application_name;
	
	# cases:
	# icmp object-group VPNConc 21.253.174.192 255.255.255.240 echo-reply
	# udp object-group kraft_proxies any eq domain
	# tcp host 82.122.108.51 host 46.162.37.17 eq 6614
	# esp any object-group VPNConc

	print_debug("acl extract_services_from_string entering with: input=$input_string", $debug, 4);
	if ($input_string =~ /^.*?(tcp|udp)\s(.*?)\seq\s(.*)$/) {
		$proto = $1;
		$destination_port = $3;
		$icmp_art = '';
		$application_name = "${proto}_$destination_port";
		@list_of_svcs = (@list_of_svcs, "$application_name/$proto/$destination_port/$icmp_art");
	}
=cut
	# das funktioniert noch nicht, da der icmp type nicht trivial zu erkennen ist
	if ($input_string =~ /^.*?(icmp)\s(.*?)\s([\w\-]+)$/) {
		$proto = 'icmp';
		$destination_port = '';
		$icmp_art = $3;
		$application_name = "${proto}_$icmp_art";
		@list_of_svcs = (@list_of_svcs, "$application_name/$proto/$destination_port/$icmp_art");
	}
=cut
	return join(',', @list_of_svcs);
}

# the following function does only parse simple objects without groups. Groups are parsed in a second run using function parse_config_group_objects
sub parse_config_base_objects { # ($obj_file, $fworch_workdir, $debug_level, $mgm_name, $config_dir, $import_id)
	my $in_file_main = shift;
	my $fworch_workdir = shift;
	my $debug = shift;
	my $mgm_name = shift;
	my $config_dir = shift;
	my $import_id = shift;
	my ($zone, $address_group_name, $obj_name, $obj_ip, $group_member_name, $application_name, $group_name, $group_members, $proto, $icmp_art, $icmp_nummer,
		$source_port, $destination_port, $uuid, $members, $members_uid, $members_proto, $rpc);
	my $line = '';
	my $context = '';
	my @nodes= ();
	my $obj_mask;

	&print_debug("entering parse_config_base_objects =======================================================",$debug,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		&print_debug("pcbo-line: $line", $debug, 9);
# 	parsing both nw nad svc objects
		if ($line =~ /^access\-list\s.+?extended\s(permit|deny)\s(.+)$/ && ($context eq '' || $context eq 'network-object-group' || $context eq 'service-object-group' || $context eq 'acl-extended')) {
			my $acl = $2;
			$context = &switch_context($context, 'acl-extended', $debug);
			print_debug("acl match part: $acl", $debug, 7);
			foreach my $ip (split(/,/, &extract_ip_addresses_from_string($acl, $debug))) {
				($obj_ip, $obj_mask) = split (/\//, $ip);
				$obj_name = $obj_ip;
				print_debug("acl expl network definition: found network $obj_name (raw: $ip) with ip $obj_ip and mask $obj_mask", $debug, 7);
				&add_nw_obj ($obj_name, $obj_ip, $obj_mask, $debug);
			}
			foreach my $svc (split(/,/, &extract_services_from_string($acl, $debug))) {
				($application_name, $proto, $destination_port, $icmp_art) = split (/\//, $svc);
				print_debug("acl expl service definition: found name=$application_name, proto=$proto, dest_port=$destination_port,icmp_art=$icmp_art", $debug, 4);
				&add_nw_service_obj ($application_name, $proto, $source_port, $destination_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $debug);
				undef($application_name); undef($source_port); undef($destination_port); undef($uuid); undef($rpc); undef($icmp_art); undef($icmp_nummer);
			}
			next NEW_LINE;
		}
#	parsing network objects
		if ($line =~ /^object\-group\snetwork\s(.+?)$/ && ($context eq '' || $context eq 'network-object-group' || $context eq 'service-object-group')) {
			$context = &switch_context($context, 'network-object-group', $debug);
			next NEW_LINE;
		}	
		if ($line=~ /^\s+network\-object\s(host\s)?([\d\.]+)\s?([\d\.]*)$/ && $context eq 'network-object-group') {
			$obj_name = $2;
			$obj_ip = $2;
			$obj_mask = $3;
			print_debug("found obj $obj_name with ip $obj_ip and mask $obj_mask", $debug, 4);
			&add_nw_obj ($obj_name, $obj_ip, $obj_mask, $debug);
			undef($obj_name); undef($obj_ip); undef($obj_mask);
			next NEW_LINE; 
		}
#	parsing network services
		if ($line =~ /^object\-group\sservice\s(.+?)\s(.+?)$/ && ($context eq '' || $context eq 'network-object-group' || $context eq 'service-object-group')) {
			$context = &switch_context($context, 'service-object-group', $debug);
			$proto = $2;
			next NEW_LINE;
		}
		if ($line =~ /^object\-group\sicmp\-type\s(.+?)$/ && ($context eq '' || $context eq 'network-object-group' || $context eq 'service-object-group')) {
			$context = &switch_context($context, 'service-object-group', $debug);
			$proto = 'icmp';
			next NEW_LINE;
		}
		if ($line=~ /^\s+port\-object\seq\s(.+?)$/ && $context eq 'service-object-group') {
			print_debug("found svc proto $proto", $debug, 4);
			$destination_port = $1;
			$application_name = "${proto}_$destination_port";
			print_debug("found svc obj $application_name with destination port $destination_port", $debug, 4);
			&add_nw_service_obj ($application_name, $proto, $source_port, $destination_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $debug);
			undef($application_name); undef($source_port); undef($destination_port); undef($uuid); undef($rpc); undef($icmp_art); undef($icmp_nummer);
			#  undef($proto); --> needed for following services in same group
			next NEW_LINE;
		}
		if ($line=~ /^\s+icmp\-object\s(.+?)$/ && $context eq 'service-object-group') {
			$icmp_art= $1;
			print_debug("found icmp obj $icmp_art", $debug, 4);
			$application_name = "${proto}_$icmp_art";
			print_debug("found svc icmp obj $application_name with type $icmp_art", $debug, 4);
			&add_nw_service_obj ($application_name, $proto, $source_port, $destination_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $debug);
			undef($application_name); undef($source_port); undef($destination_port); undef($uuid); undef($rpc); undef($icmp_art); undef($icmp_nummer);
			#  undef($proto); --> needed for following services in same group
			next NEW_LINE;
		}
	}
	return 0;
}

sub parse_config_group_objects { # ($obj_file, $fworch_workdir, $debug_level, $mgm_name, $config_dir, $import_id)
	my $in_file_main = shift;
	my $fworch_workdir = shift;
	my $debug = shift;
	my $mgm_name = shift;
	my $config_dir = shift;
	my $import_id = shift;
	my ($zone, $address_group_name, $act_obj_mask, $obj_name, $obj_ip, $group_member_name, $application_name);
	my ($comment, $proto, $icmp_art, $icmp_nummer, $source_port, $destination_port, $uuid, $members, $members_uid, $members_proto, $rpc, $service_group_name);
	my $line = '';
	my $context = '';
	my @nodes= ();
	
	&print_debug("entering parse_config_group_objects =======================================================",$debug,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		&print_debug("pcgo-line: $line", $debug, 9);
		if ($line =~ /^\s+description (.*)$/) { $comment = $1; } # general collection of description
#	parsing network object groups
		if ($line =~ /^object\-group\snetwork\s(.+?)$/ && ($context eq '' || $context eq 'network-object-group' || $context eq 'service-object-group')) {
			$context = &switch_context($context, 'network-object-group', $debug);
			$address_group_name = $1;
			print_debug("found address group $address_group_name", $debug, 4);
			next NEW_LINE;
		}	
		if ($line=~ /^\s+network\-object\s(host\s)?([\d\.]+)\s?([\d\.]*)$/ && $context eq 'network-object-group') {
			$group_member_name = $2;
			$act_obj_mask = $3;
			if (!defined($act_obj_mask) || $act_obj_mask eq '') { $act_obj_mask = "32"; }
			else { $act_obj_mask = &calc_subnetmask($act_obj_mask); if ($act_obj_mask ne "32") { $group_member_name = $group_member_name . '_mask_' . $act_obj_mask; } }
			print_debug("found address group member of group $address_group_name: $group_member_name", $debug, 4);
			&add_nw_obj_group_member ($address_group_name, $group_member_name, $debug);
			undef($group_member_name);
			next NEW_LINE; 
		}
#	parsing service groups
		if ($line =~ /^object\-group\sservice\s(.+?)\s(.+?)$/ && ($context eq '' || $context eq 'network-object-group' || $context eq 'service-object-group')) {
			$context = &switch_context($context, 'service-object-group', $debug);
			$proto = $2;
			$service_group_name = $1;
			$members = '';
			next NEW_LINE;
		}
		if ($line=~ /^\s+port\-object\seq\s(.+?)$/ && $context eq 'service-object-group') {
			$destination_port = $1;
			$application_name = "${proto}_$destination_port";
			if ($members ne '') { $members .= '|'; }
			$members .= "$application_name";
			print_debug("found application $application_name in service group $service_group_name", $debug, 6);
			next NEW_LINE;
		}
		if ($line=~ /^\s+icmp\-object\s(.+?)$/ && $context eq 'service-object-group') {
			$icmp_art= $1;
			print_debug("found icmp obj $icmp_art", $debug, 4);
			$application_name = "${proto}_$icmp_art";
			if ($members ne '') { $members .= '|'; }
			$members .= "$application_name";
			print_debug("found application $application_name in service group $service_group_name", $debug, 6);
			next NEW_LINE;
		}
		if ($context eq 'service-object-group' && $line =~ /^\w/) {
			print_debug("adding service group $service_group_name", $debug, 6);
			&add_nw_service_obj_grp ($service_group_name, $members, $comment, $debug);
			undef($comment);
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
	if ($config_lines[$ln_cnt] !~ /^\:\send$/) {
		$cfg_file_complete = 0;
		print_debug ("ERROR: expected last line to contain either primary node info or top level curly bracket. Instead got: " . $config_lines[$ln_cnt], $debug_level, -1);
	}
	return $cfg_file_complete;
}

sub switch_context {
	my $old_level = shift;
	my $new_level = shift;
	my $debug_level = shift;	
	print_debug("switching context from $old_level to $new_level", $debug_level, 8);
	return $new_level;
}

 sub parse_config_rules  { # ($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name_in_config, $config_dir, $import_id)
	my $in_file_main = shift;
	my $fworch_workdir = shift;
	my $debug = shift;
	my $mgm_name = shift;
	my $config_dir = shift;
	my $import_id = shift;
	my ($zone, $action, $source, $destination, $application, $from_zone, $to_zone, $policy_name, $disabled, $track);
	my $line = '';
	my $context = '';
	my $rule_no = 0;
	my $list;
	my $list_typ;

	&print_debug("entering parse_config_rules =======================================================",$debug,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		&print_debug("pcr: $line", $debug, 9);
#	rules start
		if ($line =~ /^security\s\{$/ && $context eq '') { $context = &switch_context($context, 'security', $debug);  next NEW_LINE; }
		if ($line =~ /^\s+policies\s\{$/ && $context eq 'security') { $context = &switch_context($context, 'security/policies', $debug);  next NEW_LINE; }
		if ($line=~ /^\s+from\-zone\s(.+?)\sto\-zone\s(.+?)\s\{$/ && $context eq 'security/policies') {
			$context = &switch_context($context, 'security/policies/zone', $debug); 
			$from_zone = $1;
			$to_zone = $2;
			&print_debug("entering policy from zone $from_zone to zone $to_zone",$debug,2);
			next NEW_LINE; 
		}
		if ($line=~ /^\s+(inactive)?\:?\s?policy\s(.+?)\s\{$/ && $context eq 'security/policies/zone') {
			$disabled = $1; if (!defined($disabled)) { $disabled = ''; }
			$policy_name = $2;
			$context = &switch_context($context, 'security/policies/zone/policy', $debug);
			&print_debug("entering policy $policy_name (zone $from_zone to $to_zone)",$debug,2);
			next NEW_LINE;
		}
#	match part of rule
		if ($line=~ /^\s+match\s\{$/ && $context eq 'security/policies/zone/policy') { $context = &switch_context($context, 'security/policies/zone/policy/match', $debug); next NEW_LINE; }
		if ($line =~ /^\s+(source|destination)\-address\s(.+?)\;?$/ && $context eq 'security/policies/zone/policy/match') {
			$list_typ = $1;
			$list = $2;
			if ($list_typ eq 'source') { $source = $list; }
			if ($list_typ eq 'destination') { $destination = $list; }
#			&print_debug("pcr: nw-list=$list", $debug, 9);
			if ($list =~ /^\[/ && $list !~ /\]$/) { $context = &switch_context($context, "security/policies/zone/policy/match/$list_typ", $debug); }
			next NEW_LINE;
		}
		if ($line =~ /^\s+application\s(.+?)\;?$/ && $context eq 'security/policies/zone/policy/match') {
			$application = $1;
#			&print_debug("pcr: appl-list=$application", $debug, 9);
			if ($application =~ /^\[/ && $application !~ /\]$/) { $context = &switch_context($context, 'security/policies/zone/policy/match/application', $debug); }
			next NEW_LINE;
		}
		if ($line=~ /^\s+\}$/ && $context eq 'security/policies/zone/policy/match') { $context = &switch_context($context, 'security/policies/zone/policy', $debug); next NEW_LINE; }
#	dealing with multiple source, destination or application lines of rules
		if ($context =~ /^security\/policies\/zone\/policy\/match\/(\w+)$/) {
			$list_typ = $1;
			my $multi_line;
			if ($line =~ /\s*(.*?)\;?$/) { $multi_line = $1; }
			if ($list_typ eq 'source') { $source .= " $multi_line"; }
			if ($list_typ eq 'destination') { $destination.= " $multi_line"; }
			if ($list_typ eq 'application') { $application .= " $multi_line"; }
			if ($multi_line =~ /\]$/) { $context = &switch_context($context, "security/policies/zone/policy/match", $debug); undef($list_typ); undef($list); }	# last line reached
			next NEW_LINE;
		}

#	action / track part of rule (then)
		if ($line=~ /^\s+then\s\{$/ && $context eq 'security/policies/zone/policy') { $context = &switch_context($context, 'security/policies/zone/policy/action', $debug); next NEW_LINE; }
		if ($line=~ /^\s+(permit|deny|reject)\;$/ && $context eq 'security/policies/zone/policy/action') { $action = $1; next NEW_LINE; }
		if ($line=~ /^\s+(permit|deny|reject)\s\{$/ && $context eq 'security/policies/zone/policy/action') {
			$action = $1;
			$context = &switch_context($context, 'security/policies/zone/policy/action/action_parameter', $debug);
			next NEW_LINE;
		}  # ignoring further action parameters
		if ($line=~ /^\s+(log|count)\s\{$/ && $context eq 'security/policies/zone/policy/action') {
			my $found_track = $1;
			if ($track eq '') { $track = $1; }
			elsif ($found_track eq 'count' && $track eq 'log') { $track = 'log count'; } 
			$context = &switch_context($context, 'security/policies/zone/policy/action/action_parameter', $debug);
			next NEW_LINE;
		}  # ignoring further tracking parameters
=cut
		track=log wird in diesem Fall nicht gefunden:
                   permit {            
                        destination-address {
                            drop-untranslated;
                        }               
                    }                   
                    log {               
                        session-close;  
                    }            
=cut
		if ($line=~ /^\s+.+?\{$/ && $context eq 'security/policies/zone/policy/action/action_parameter') {
			$context = &switch_context($context, 'security/policies/zone/policy/action/action_parameter/null', $debug);
			next NEW_LINE;
		}
#closing sections
		if ($line=~ /^\s+.+?\{$/ && $context eq 'security/policies/zone/policy/action/action_parameter/null') {
			$context = &switch_context($context, 'security/policies/zone/policy/action/action_parameter', $debug);
			next NEW_LINE;
		}
		if ($line=~ /^\s+\}$/ && $context eq 'security/policies/zone/policy/action/action_parameter') { $context = &switch_context($context, 'security/policies/zone/policy/action', $debug); next NEW_LINE; }
		if ($line=~ /^\s+\}$/ && $context eq 'security/policies/zone/policy/action') { $context = &switch_context($context, 'security/policies/zone/policy', $debug); next NEW_LINE; }
		if ($line=~ /^\s+\}$/ && $context eq 'security/policies/zone/policy') {
			if (!defined($track)) { $track=''; }
			&print_debug("found rule from $from_zone to $to_zone, name=$policy_name, disabled=$disabled: src=$source, dst=$destination, svc=$application, action=$action, track=$track",$debug,2);
			# Regel wegschreiben
			$rule_no = &add_rule ($rule_no, $from_zone, $to_zone, $policy_name, $disabled, $source, $destination, $application, $action, $track, $debug);
			$context = &switch_context($context, 'security/policies/zone', $debug);
			$track = ''; undef($policy_name);
			next NEW_LINE;
		} # end of rule
		if ($line=~ /^\s+\}$/ && $context eq 'security/policies/zone') { $context = switch_context($context, 'security/policies', $debug); undef($from_zone); undef($to_zone); next NEW_LINE; }
		if ($line=~ /^\s+\}$/ && $context eq 'security/policies') { $context = &switch_context($context, 'security', $debug); next NEW_LINE; }
		if ($line=~ /^\}$/) { $context = &switch_context($context, '', $debug); next NEW_LINE; }
	}
	&sort_rules_and_add_zone_headers ();
    $rulebases{"$rulebase_name.ruleorder"} = join(',', @ruleorder);
	return 0;
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

	# initializing global variables:
	@services = ();
	@network_objects = ();
	&print_debug ("in_file_main=$in_file_main, fworch_workdir=$fworch_workdir, debuglevel_main=$debuglevel_main, mgm_name=$mgm_name, config_dir=$config_dir, import_id=$import_id", $debuglevel_main, 6);

	open (IN, $in_file_main) || die "$in_file_main konnte nicht geoeffnet werden.\n";
	@config_lines = <IN>;	# sichern Config-array fuer spaetere Verwendung
	close (IN);

	if (!&cfg_file_complete($debuglevel_main)) { return "incomplete-config-file-$mgm_name"; }	
	else {
#		my $device_type=9; # ASA 8.x fest gesetzt		# TODO move to config
#		&read_predefined_services($device_type, $debuglevel_main);		# schreibt die predefined services in @services und %services
		my $mgm_name_in_config = &parse_mgm_name($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name, $config_dir, $import_id);
		&parse_config_base_objects  ($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name, $config_dir, $import_id); # zones, simple network and service objects  
		push @zones, "global"; 	# Global Zone immer hinzufuegen
		foreach my $zone (@zones) {	object_address_add("any", "0.0.0.0", "0.0.0.0", $zone, "any-obj for Zone added by ITSecOrg"); &print_debug(""); } #		Any-Objekte fuer alle Zonen einfuegen
		&parse_config_group_objects ($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name, $config_dir, $import_id); # groups are parsed in separate cycle to ensure that all base objects are complete
#		&resolve_service_uuid_references ($debuglevel_main);
		&parse_config_rules ($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name_in_config, $config_dir, $import_id); # finally parsing the rule base, ignoring the rulebase name in itsecorg config

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

CACTUS::FWORCH::parser - Perl extension for IT Security Organizer netscreen parser

=head1 SYNOPSIS

  use CACTUS::FWORCH::import::netscreen;

=head1 DESCRIPTION

IT Security Organizer Perl Module
support for importing configs into ITSecOrg Database

=head2 EXPORT

  global variables


=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Holger Dost, Tim Purschke, tmp@cactus.de


