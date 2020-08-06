# !/usr/bin/perl -w
# $Id: juniper.pm,v 1.1.2.12 2012-03-24 13:57:03 tim Exp $
# $Source: /home/cvs/iso/package/importer/CACTUS/FWORCH/import/Attic/juniper.pm,v $

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

## parse_audit_log Funktion fuer JunOS (noch) nicht implementiert
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
	my $obj_ip = shift;
	my $act_obj_zone = shift;
	my $act_obj_comm = shift;
	my $debuglevel = shift;
	my $act_obj_nameZ = '';
	my $act_obj_ipaddr = '';
	my $act_obj_ipaddr_last = '';
	my $act_obj_mask = '';
	my $act_obj_type = '';
	my $act_obj_loc = '';
	my $act_obj_color = 'black';
	my $act_obj_sys = '';
	my $act_obj_uid = '';
	my $ipv6 = 0;
		
	print_debug("add_nw_obj called with name=$act_obj_name, ip=$obj_ip, zone=$act_obj_zone", $debuglevel, 5);
	($act_obj_ipaddr, $act_obj_mask) = split (/\//, $obj_ip);
	if ($obj_ip =~/\:/) { $ipv6 = 1; }
	print_debug("split: ip=$act_obj_ipaddr, mask=$act_obj_mask", $debuglevel, 4);	
#	$act_obj_nameZ = "${act_obj_name}__zone__$act_obj_zone";	# Zone in Feldindex mit aufgenommen
	$act_obj_nameZ = "${act_obj_name}";	# CHANGE: Zone nicht in Feldindex aufgenommen
	if (!defined ($network_objects{"$act_obj_nameZ.name"})) {
		@network_objects = (@network_objects, $act_obj_nameZ);
		$network_objects{"$act_obj_nameZ.name"} = $act_obj_name;
		$network_objects{"$act_obj_nameZ.UID"} = $act_obj_nameZ;
		if ((!$ipv6 && $act_obj_mask==32) || ($ipv6 && $act_obj_mask==128)) { $network_objects{"$act_obj_nameZ.type"} = 'host' }
		else { $network_objects{"$act_obj_nameZ.type"} = 'network' }
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
# add_nw_obj_group_member
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub add_nw_obj_group_member{
	my $group_name = shift;
	my $member_name = shift;
	my $act_obj_zone = shift;
	my $comment = shift;
	my $debuglevel = shift;

	my $act_obj_name = '';
	my $group_nameZ = '';
	my $act_obj_mbr = '';
	my $act_obj_fkt = '';
	my $act_obj_color = 'black';
	my $act_obj_comm = '';
	my $mbrlst = '';
	my $mbr_ref_lst = '';

#	$group_nameZ = "${group_name}__zone__$act_obj_zone";	# Zone in Feldindex mit aufgenommen
	$group_nameZ = "${group_name}";	# CHANGE: Zone nicht in Feldindex aufgenommen
	if (!defined ($network_objects{"$group_nameZ.name"})) {
		@network_objects = (@network_objects, $group_nameZ);
		$network_objects{"$group_nameZ.name"} = $group_name;
		$network_objects{"$group_nameZ.UID"} = $group_nameZ;
		print_debug ("added group $group_nameZ", $debuglevel, 5);
		$network_objects{"$group_nameZ.zone"} = $act_obj_zone;		# neues Feld fuer Zone
		$network_objects{"$group_nameZ.type"} = 'group';
		$network_objects{"$group_nameZ.color"} = $act_obj_color;
	}
#		die ("reference to undefined network object $member_name found in group $group_name, zone: $act_obj_zone");
	if (defined($network_objects{"$group_nameZ.members"})) {
		$mbrlst = $network_objects{"$group_nameZ.members"};
		$mbr_ref_lst = $network_objects{"$group_nameZ.member_refs"};
	}
	if ( $mbrlst eq '' ) {
		$mbrlst = $member_name;
#		$mbr_ref_lst = $member_name . "__zone__$act_obj_zone";
		$mbr_ref_lst = $member_name; #CHANGE
	}
	else {
		$mbrlst = "$mbrlst|$member_name";
#		$mbr_ref_lst = "$mbr_ref_lst|$member_name" . "__zone__$act_obj_zone";
		$mbr_ref_lst = "$mbr_ref_lst|$member_name";  #CHANGE
	}
	$network_objects{"$group_nameZ.members"} = $mbrlst;
	$network_objects{"$group_nameZ.member_refs"} = $mbr_ref_lst;
	if (defined($comment) && $comment ne '') { $network_objects{"$group_nameZ.comments"} = $comment; }
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
	my $comment = shift;	
	my $debuglevel = shift
	my @range;
	my $act_obj_typ = 'simple';
	my $act_obj_type = '';
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
			$services{"$act_obj_name.ip_proto"} = '';
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
sub add_nw_service_obj_grp { # ($application_name, $members, $members_proto, $members_uid, $comment, $debuglevel_main);
	my $act_obj_name = shift;
	my $act_obj_members = shift;
	my $comment = shift;
	my $debuglevel = shift;
	my $act_obj_typ = 'group';
	my $act_obj_type = '';
	my $act_obj_proto = '';
	my $act_obj_src_last = '';
	my $act_obj_dst_last = '';
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
	if (defined($comment) && $comment ne '') { $services{"$act_obj_name.comments"} = $comment; } else { $services{"$act_obj_name.comments"} = ''; }
	$services{"$act_obj_name.typ"} = $act_obj_typ;
	$services{"$act_obj_name.type"} = $act_obj_type;
	$services{"$act_obj_name.rpc_port"} = $act_obj_rpc;
	$services{"$act_obj_name.UID"} = $act_obj_name;
}

sub junos_split_list {  # param1 = list of objects (network or service)
	my $list = shift;
	my $debug_level = shift;
	my $orig_list = $list;
	
#	if ($list =~ /^\[\s([\w\-\_\/\.\d\s]+?)\s\]$/) { # standard list format: [ x1 x2 x3 ]
	if ($list =~ /^\[\s(.+?)\s\]$/) { # standard list format: [ x1 x2 x3 ]
		$list = $1;
		$list = join('|', split(/\s+/, $list));
#	} elsif ($list =~ /[\w\-\_\/\.\d]+/) {
	} elsif ($list =~ /[^\s]+/) {
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
	my $comment = shift;
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
#		$rulebases{"$rulebase_name.$rule_no.src.refs"} .= ("$src" . "__zone__$from_zone");
		$rulebases{"$rulebase_name.$rule_no.src.refs"} .= ("$src");  # CHANGE
	}
	$rulebases{"$rulebase_name.$rule_no.dst"} = '';
	foreach my $dst (split(/\|/, &junos_split_list($destination, $debuglevel))) {
		if ($rulebases{"$rulebase_name.$rule_no.dst"} ne '') {
			$rulebases{"$rulebase_name.$rule_no.dst"} .= '|';
			$rulebases{"$rulebase_name.$rule_no.dst.refs"} .= '|';
		}
		$rulebases{"$rulebase_name.$rule_no.dst"} .= "$dst";
#		$rulebases{"$rulebase_name.$rule_no.dst.refs"} .= ("$dst" . "__zone__$to_zone");
		$rulebases{"$rulebase_name.$rule_no.dst.refs"} .= ("$dst"); # CHANGE
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
	$rulebases{"$rulebase_name.$rule_no.name"} = $policy_name;		# kein Aequivalent zu CP rule_name
	$rulebases{"$rulebase_name.$rule_no.time"} = '';
	if (defined($comment) && $comment ne '') { $rulebases{"$rulebase_name.$rule_no.comments"} = $comment; }
	else  { $rulebases{"$rulebase_name.$rule_no.comments"} = $policy_name; }
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
	my $simulate = 0;

	if ($simulate) {  # get config from std ssh server, not from native junos system
		$cmd = "echo \"applications {\" > $cfg_dir/$obj_file_base";
		if (system ($cmd)) { $fehler_count++; }
		$cmd = "$scp_bin $scp_batch_mode_switch -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname:predef_svc_junos $cfg_dir/$obj_file_base.1";
		if (system ($cmd)) { $fehler_count++; }
		$cmd = "cat $cfg_dir/$obj_file_base.1 >> $cfg_dir/$obj_file_base";
		if (system ($cmd)) { $fehler_count++; }
		$cmd = "echo \"}\" >> $cfg_dir/$obj_file_base";
		if (system ($cmd)) { $fehler_count++; }
		$cmd = "$scp_bin $scp_batch_mode_switch -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname:junos_config $cfg_dir/$obj_file_base.1";
		if (system ($cmd)) { $fehler_count++; }
		$cmd = "cat $cfg_dir/$obj_file_base.1 >> $cfg_dir/$obj_file_base";
		if (system ($cmd)) { $fehler_count++; }
		$cmd = "rm $cfg_dir/$obj_file_base.1";
		if (system ($cmd)) { $fehler_count++; }
	} else {
		if (system ("echo \"applications {\" > $cfg_dir/$obj_file_base")) { $fehler_count++; }
		$cmd = "$ssh_bin -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname show configuration groups junos-defaults applications > $cfg_dir/$obj_file_base.1";
		if (system ($cmd)) { $fehler_count++; }
		if (system ("cat $cfg_dir/$obj_file_base.1 >> $cfg_dir/$obj_file_base")) { $fehler_count++; }
		if (system ("echo \"}\" >> $cfg_dir/$obj_file_base")) { $fehler_count++; }
		$cmd = "$ssh_bin -i $workdir/$CACTUS::FWORCH::ssh_id_basename $ssh_user\@$ssh_hostname show config > $cfg_dir/$obj_file_base.1";
		if (system ($cmd)) { $fehler_count++; }
		if (system ("cat $cfg_dir/$obj_file_base.1 >> $cfg_dir/$obj_file_base")) { $fehler_count++; }
		if (system ("rm $cfg_dir/$obj_file_base.1")) { $fehler_count++; }
	}
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
#	parsing device name(s)
		if ($line=~ /^\s+host\-name\s+(.+?)\;$/ && ($context eq 'groups/node/system' || $context eq 'system')) {
			$mgm_name = $1;
			@nodes = ($mgm_name, @nodes);
			&print_debug("parse_mgm_name: found hostname: $mgm_name",$debuglevel_main,1);
			next NEW_LINE; 
		}
	# cluster node
		if ($line =~ /^groups\s\{$/ && $context eq '') { $context = 'groups'; next NEW_LINE; }
		if ($line =~ /^\s+node(\d+)\s\{$/ && $context eq 'groups') { $context .= "/node"; next NEW_LINE;  }
		if ($line =~ /^\s+system\s\{$/ && $context eq 'groups/node') { $context .= "/system"; &print_debug("found system line: $line",$debuglevel_main,5); next NEW_LINE;  }
		if ($line=~ /^\}$/ && $context eq 'groups/node/system') {  # in case of cluster: add both node names to make up the cluster name
			$context = '';
			$mgm_name = join('_', sort (@nodes));
			@rulebases = ($mgm_name);
			$rulebase_name = $mgm_name;
			&print_debug("parse_mgm_name: found hostname combined: $mgm_name and setting rulebase_name to $mgm_name",$debuglevel_main,1);
			return $mgm_name; 
		}
	# single node
		if ($line =~ /^system\s\{$/ && $context eq '') { $context = "system"; &print_debug("found system line: $line",$debuglevel_main,5); next NEW_LINE;  }
		if ($line=~ /^\}$/ && $context eq 'system') {  # in case of single system
			$context = '';
			@rulebases = ($mgm_name);
			$rulebase_name = $mgm_name;
			&print_debug("parse_mgm_name: found hostname of single system: $mgm_name and setting rulebase_name to $mgm_name",$debuglevel_main,1);
			return $mgm_name; 
		}
	}
	&print_debug("ERROR: end of parse_mgm_name: at end without match (mgm_name=$mgm_name)",$debuglevel_main,-1);
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
		$source_port, $destination_port, $uuid, $members, $members_uid, $members_proto, $rpc, $comment);
	my $line = '';
	my $context = '';
	my @nodes= ();

	&print_debug("entering parse_config_base_objects =======================================================",$debug,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		&print_debug("pcbo-line: $line", $debug, 9);
#		if ($line =~ /^\s+\}$/ && $context eq 'security/address-book') { $context = &switch_context($context, 'security', $debug); next NEW_LINE; }
#		if ($line =~ /^\s+\}$/ && $context eq 'security/address-book/global') { $context = &switch_context($context, 'security/address-book', $debug); next NEW_LINE; }
#		if ($line=~ /^\}$/) { $context = &switch_context($context, '', $debug); next NEW_LINE; }
#		if ($line=~ /^\}$/ && $context eq 'applications/application') { $context = &switch_context($context, 'applications', $debug); next NEW_LINE; }
		if ($line=~ /^\}$/ && $context ne 'applications/application' && $context ne 'applications/application-set'
			&& $context ne'') { $context = &switch_context($context, '', $debug); next NEW_LINE; }
			# added ne 'applications/application' to enable parsing of predefined services in the beginning of the file
#	parsing zones
		if ($line =~ /^security\s\{$/ && $context eq '') { $context = &switch_context($context, 'security', $debug); next NEW_LINE; }
		if ($line =~ /^\s+zones\s\{$/ && $context eq 'security') { $context = &switch_context($context, 'security/zones', $debug); next NEW_LINE; }
		if ($line =~ /^\s+address-book\s\{$/ && $context eq 'security') { $context = &switch_context($context, 'security/address-book', $debug); next NEW_LINE; }
		if ($line =~ /^\s+global\s\{$/ && $context eq 'security/address-book') {
				$context = &switch_context($context, 'security/address-book/global', $debug);
				$zone = 'global'; 
				next NEW_LINE; 
		}
#		if ($line=~ /^\s+security\-zone\s([\w\.\_\-]+)\s\{$/ && ($context eq 'security/zones' || $context eq 'security/zones/address-book')) { 
		if ($line=~ /^\s+security\-zone\s([\w\.\_\-]+)\s\{$/) { 
			$zone = $1; &add_zone ($zone, $debug); 
			print_debug("found zone $zone", $debug, 2);
			next NEW_LINE; 
		}
#	parsing network objects
		if ($line=~ /^\s+address\-book\s\{$/ && $context eq 'security/zones') { $context = &switch_context($context, 'security/zones/address-book', $debug); next NEW_LINE; }
		# old comment syntax /* xxx */
		if ($line=~ /^\s+\/\*\s(.*?)\s\*\/$/ && $context eq 'security/zones/address-book') { $comment = $1; next NEW_LINE; }
		# new comment syntax description "xx yy zz";	
		if ($line=~ /^\s+description\s\"?(.+?)\"?\;$/ && 
			($context eq 'security/zones/address-book' || $context eq 'security/address-book/global' || $context eq 'applications/application')) {
			$comment = $1;
			print_debug("found comment $comment", $debug, 4);
			next NEW_LINE;
		}
		# start of comment brackets (containing only obj name)
		if ($line=~ /^\s+address\s(.+?)\s\{$/ && ($context eq 'security/zones/address-book' || $context eq 'security/address-book/global')) {
			$obj_name = $1;
			print_debug("found object name $obj_name", $debug, 4);
			next NEW_LINE;
		}
		# ip address only within comment brackets
		if ($line=~ /^\s+([\d\:\.\/]+)\;$/ && ($context eq 'security/zones/address-book' || $context eq 'security/address-book/global')) {
			$obj_ip = $1;
			print_debug("found object ip $obj_ip for obj $obj_name", $debug, 4);
			next NEW_LINE;
		}
		if ($line=~ /^\s+\}$/ && ($context eq 'security/zones/address-book' || $context eq 'security/address-book/global')) {
			if (defined($obj_name) && defined($obj_ip) && defined($comment)) {
				print_debug("found obj $obj_name with ip $obj_ip in zone $zone; comment=$comment", $debug, 4);
				&add_nw_obj ($obj_name, $obj_ip, $zone, $comment, $debug);
				undef($obj_name); undef($obj_ip); undef($comment);
				next NEW_LINE; 
			}
		}
		if ($line=~ /^\s+address\s(.+?)\s([\d\:\.\/]+)\;$/ && ($context eq 'security/zones/address-book' || $context eq 'security/address-book/global')) {
			$obj_name = $1;
			$obj_ip = $2;
			print_debug("found obj $obj_name with ip $obj_ip in zone $zone", $debug, 4);
			&add_nw_obj ($obj_name, $obj_ip, $zone, $comment, $debug);
			undef($obj_name); undef($obj_ip); undef($comment);
			next NEW_LINE; 
		}
#	parsing network services (applications)
		if ($line =~ /^applications\s\{$/ && $context eq '') {
			$context = &switch_context($context, 'applications', $debug);
			next NEW_LINE;
		}
		if ($line=~ /^\s*\/\*\s(.*?)\s\*\/$/ && $context eq 'applications') { $comment = $1; }
		if ($line =~ /^\s*application\s(.+?)\s\{/ && $context eq 'applications') {
			$application_name = $1;
			print_debug("found application $application_name", $debug, 4);
			$context = &switch_context($context, 'applications/application', $debug);
			next NEW_LINE; 
		}
		if ($line =~ /^\s*application\s(.+?)\sprotocol\s(.+?)\;$/ && $context eq 'applications') {	# sonderfall: single-line-service-definition
			$application_name = $1;
			&add_nw_service_obj ($1, $2, '', '1-65535', '', '', '', '', $comment, $debug);
			print_debug("found application $application_name in single line definition", $debug, 4);
			undef ($comment);
			next NEW_LINE; 
		}
		if ($line =~ /^\s+protocol\s(\w+?)\;$/ && $context eq 'applications/application') { $proto = $1; next NEW_LINE;}
		if ($line =~ /^\s+source-port\s(.+?)\;$/ && $context eq 'applications/application') { $source_port = $1; next NEW_LINE; }
		if ($line =~ /^\s+destination-port\s(.+?)\;$/ && $context eq 'applications/application') { $destination_port = $1; next NEW_LINE; }
		if ($line =~ /^\s+uuid\s(.+?)\;$/ && $context eq 'applications/application') { $uuid = $1; next NEW_LINE; }
		if ($line =~ /^\s+rpc\-program\-number\s([\d\-]+?)\;$/ && $context eq 'applications/application') { $rpc = $1; next NEW_LINE;}
		if ($line =~ /^\s+icmp\-(.+?)\s(\d+)\;$/ && $context eq 'applications/application') { $icmp_art = $1; $icmp_nummer = $2; next NEW_LINE; }			
		if ($line =~ /^\s*\}$/ && $context eq 'applications/application') {
			# service wegschreiben
			&print_debug("before calling add_nw_service_obj: for application $application_name", $debug, 5);
			&add_nw_service_obj ($application_name, $proto, $source_port, $destination_port, $uuid, $rpc, $icmp_art, $icmp_nummer, $comment, $debug);
			$context = &switch_context($context, 'applications', $debug);
			undef($application_name); undef($proto); undef($source_port); undef($destination_port); undef($uuid); undef($rpc); undef($icmp_art); undef($icmp_nummer);
			undef ($comment);
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
	my ($zone, $address_group_name, $obj_name, $obj_ip, $group_member_name, $application_name, $proto, $icmp_art, $icmp_nummer, $source_port, $destination_port, $uuid, $members, $members_uid, $members_proto, $rpc);
	my $line = '';
	my $context = '';
	my $comment;
	my @nodes= ();
	
	&print_debug("entering parse_config_group_objects =======================================================",$debug,2);
	NEW_LINE: foreach $line (@config_lines) {
		chomp($line);
		&print_debug("pcgo-line: $line", $debug, 9);
		if ($line=~ /^application\s/ && $context eq 'applications') {
			 $context = &switch_context($context, 'applications/application', $debug); next NEW_LINE;
		} 
		if ($line=~ /^\}$/ && $context eq 'applications/application') { $context = &switch_context($context, 'applications', $debug); next NEW_LINE; }
		if ($line=~ /^\}$/ && $context ne 'applications' && $context ne 'applications/application' && $context ne 'applications/application-set'
			&& $context ne '') { $context = &switch_context($context, '', $debug); next NEW_LINE; }
#		if ($line=~ /^\}$/) { $context = &switch_context($context, '', $debug); next NEW_LINE; }
#	parsing zones
		if ($line =~ /^security\s\{$/ && $context eq '') { $context = &switch_context($context, 'security', $debug);  next NEW_LINE; }
		if ($line =~ /^\s+zones\s\{$/ && $context eq 'security') { $context = &switch_context($context, 'security/zones', $debug);  next NEW_LINE; }
		if ($line=~ /^\s+security\-zone\s([\w\.\_\-]+)\s\{$/ && ($context eq 'security/zones' || $context eq 'security/zones/address-book'))  {
			$zone = $1;
			print_debug("entering zone $zone, context=$context", $debug, 4);
			next NEW_LINE; 
		}
#	parsing network objects
		if ($line=~ /^\s+address\-book\s\{$/ && ($context eq 'security/zones')) { $context = &switch_context($context, 'security/zones/address-book', $debug); next NEW_LINE; }
		if ($line=~ /^\s+address\-book\s\{$/ && ($context eq 'security' || $context eq 'security')) {
			$context = &switch_context($context, 'security/address-book', $debug); next NEW_LINE;
		}
		if ($line=~ /^\s+(.+?)\s\{$/ && $context eq 'security/address-book') {
			$zone = $1;
			$context = &switch_context($context, "security/address-book/$zone", $debug); 
			next NEW_LINE; 
		}
#	parsing network object groups
		if ($line=~ /^\s+\/\*\s(.*?)\s\*\/$/ && $context eq 'security/zones/address-book') { $comment = $1; next NEW_LINE; }
		if ($line=~ /^\s+address\-set\s(.+?)\s\{$/ && ($context eq 'security/zones/address-book' || $context eq 'security/address-book/global')) {
			$address_group_name = $1;
			print_debug("entering network obj group $address_group_name in zone $zone, context=$context", $debug, 4);
			$context = &switch_context($context, 'security/zones/address-book/address-set', $debug);
			next NEW_LINE; 
		}
		if ($line=~ /^\s+description\s\"?(.+?)\"?\;$/ && ($context eq 'security/zones/address-book/address-set' || $context eq 'security/address-book/global')) {
			$comment = $1;
			next NEW_LINE; 
		}
		if ($line=~ /^\s+address\s(.+?)\;$/ && $context eq 'security/zones/address-book/address-set') {
			$group_member_name = $1;
			print_debug("found address group member of group $address_group_name: $group_member_name in zone $zone", $debug, 4);
			&add_nw_obj_group_member ($address_group_name, $group_member_name, $zone, $comment, $debug);
			undef($group_member_name); undef($comment);
			next NEW_LINE; 
		}
		if ($line=~ /^\s+\}$/ && $context eq 'security/zones/address-book/address-set') { $context = &switch_context($context, 'security/zones/address-book', $debug); next NEW_LINE; }
		if ($line=~ /^\}$/ && ($context =~ '^security')) { $context = &switch_context($context, '', $debug); next NEW_LINE; }
#	parsing network services
		if ($line =~ /^applications\s\{$/ && $context eq '') { $context = &switch_context($context, 'applications', $debug); next NEW_LINE; }
#	parsing service groups
		if ($line =~ /^\s*application\-set\s(.+?)\s\{$/ && $context eq 'applications') {
			$application_name = $1;
			$members = '';
			print_debug("found application-set $application_name", $debug, 6);
			$context = &switch_context($context, 'applications/application-set', $debug);
			next NEW_LINE;
		}
		if ($line =~ /^\s+application\s([\w\-\.\_]+?)\;/ && $context eq 'applications/application-set') {
			if ($members ne '') { $members .= '|'; }
			$members .= "$1";
			print_debug("found application $1 in application-set $application_name", $debug, 6);
			next NEW_LINE;
		}
#	parsing term quasi service groups
		if ($line =~ /^\s+application\s(.+?)\s\{/ && $context eq 'applications') {
			$application_name = $1;
			print_debug("found application $application_name", $debug, 4);
			$context = &switch_context($context, 'applications/application', $debug);
			next NEW_LINE; 
		}
		if ($line =~ /^\s+term\sterm0\sprotocol\s(\w+?)\suuid\s(.+?)\;$/ && $context eq 'applications/application') {  # parse this line twice!	
			$members = ''; $members_uid = ''; $members_proto = '';
			$context = &switch_context($context, 'applications/application-set', $debug);
			print_debug("pcgo: found term, switchting to group mode ($application_name)", $debug, 6);
		}
		if ($line =~ /^\s+term\sterm\d+\sprotocol\s\w+\suuid\s(.+?)\;$/ && $context eq 'applications/application-set') {
			my $term_uuid = $1;
			if (defined($junos_uuids{"$term_uuid"})) {
				print_debug("pcgo: found single resolvable uuid ref (group: $application_name, uuid: $term_uuid) for application " . $junos_uuids{"$term_uuid"}, $debug, 6);
				if ($members ne '') { $members .= '|'; }
				$members .= $junos_uuids{"$term_uuid"};
			} else {
				print_debug("pcgo: ignoring unresolvable uuid ref ($application_name): $term_uuid", $debug, 6);
			}
			next NEW_LINE;
		}			
		if ($line =~ /^\s*\}$/ && $context eq 'applications/application') {	$context = &switch_context($context, 'applications', $debug); } # ignore simple applications
		if ($line =~ /^\s*\}$/ && $context eq 'applications/application-set') {
			$context = &switch_context($context, 'applications', $debug);
			# service group wegschreiben
			&add_nw_service_obj_grp ($application_name, $members, $comment, $debug);
			undef($comment);
			next NEW_LINE;
		}
		if ($line =~ /^\}$/ && $context eq 'applications') { $context = &switch_context($context, '', $debug); next NEW_LINE; }			
	}
	return 0;
}

sub cfg_file_complete { 	# check auf Vollstaendigkeit des Config-Files:
	my $debug_level = shift;
	my $ln_cnt = $#config_lines;
	my $cfg_file_complete = 1;	

	while ($config_lines[$ln_cnt] =~ /^\s*$/ ) { $ln_cnt -- ; }		# ignore empty lines at the end
	if ($config_lines[$ln_cnt] !~ /^\s*\{primary\:node\d+\}$/ && $config_lines[$ln_cnt] !~ /^\}$/) {
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
	my $comment;

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
		if ($line=~ /^\s+\/\*\s(.*?)\s\*\/$/ && $context eq 'security/policies/zone') { $comment = $1; }
#		if ($line=~ /^\s+\#\s(.*)$/ && $context eq 'security/policies/zone') { $comment = $1; }
		if ($line=~ /^\s+(inactive)?\:?\s?policy\s(.+?)\s\{$/ && $context eq 'security/policies/zone') {
			$disabled = $1; if (!defined($disabled)) { $disabled = ''; }
			$policy_name = $2;
			$context = &switch_context($context, 'security/policies/zone/policy', $debug);
			&print_debug("entering policy $policy_name (zone $from_zone to $to_zone)",$debug,2);
			next NEW_LINE;
		}
#	match part of rule
		# new comment syntax description "xx yy zz";	
		if ($line=~ /^\s+description\s\"?(.+?)\"?\;$/ && $context eq 'security/policies/zone/policy') {
			$comment = $1;
			print_debug("found rule comment $comment", $debug, 4);
			next NEW_LINE;
		}

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
			if (!defined($track) || $track eq '') { $track = $found_track; }
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
			$rule_no = &add_rule ($rule_no, $from_zone, $to_zone, $policy_name, $disabled, $source, $destination, $application, $action, $track, $comment, $debug);
			$context = &switch_context($context, 'security/policies/zone', $debug);
			$track = ''; undef($policy_name); undef($comment);
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
		my $device_type=8; # junos 10.x fest gesetzt		# move to config
		&read_predefined_services($device_type, $debuglevel_main);		# schreibt die predefined services in @services und %services
		my $mgm_name_in_config = &parse_mgm_name($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name, $config_dir, $import_id);
		&parse_config_base_objects  ($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name, $config_dir, $import_id); # zones, simple network and service objects  
		push @zones, "global"; 	# Global Zone immer hinzufuegen
		foreach my $zone (@zones) {	object_address_add("any", "0.0.0.0", "0.0.0.0", $zone, "any-obj for zone $zone added by fworch"); &print_debug(""); } #		Any-Objekte fuer alle Zonen einfuegen
		&parse_config_group_objects ($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name, $config_dir, $import_id); # groups are parsed in separate cycle to ensure that all base objects are complete
#		&resolve_service_uuid_references ($debuglevel_main);
		&parse_config_rules ($in_file_main, $fworch_workdir, $debuglevel_main, $mgm_name_in_config, $config_dir, $import_id); # finally parsing the rule base, ignoring the rulebase name in fworch config

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
support for importing configs into fworch Database

=head2 EXPORT

  global variables


=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Tim Purschke, tmp@cactus.de
