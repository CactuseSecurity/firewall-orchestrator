#!/usr/bin/perl -w
# $Id: netscreen.pm,v 1.1.2.17 2013-02-04 21:12:35 tim Exp $
# $Source: /home/cvs/iso/package/importer/CACTUS/ISO/import/Attic/netscreen.pm,v $

package CACTUS::ISO::import::parser;

use strict;
use warnings;
use Time::HiRes qw(time); # fuer hundertstelsekundengenaue Messung der Ausfuehrdauer
use CACTUS::ISO;
use CACTUS::ISO::import;
use CACTUS::read_config;
# use Scalar::Util;

require Exporter;
our @ISA = qw(Exporter);

our %EXPORT_TAGS = ( 'basic' => [ qw( &copy_config_from_mgm_to_iso &parse_config ) ] );

our @EXPORT = ( @{ $EXPORT_TAGS{'basic'} } );
our $VERSION = '0.3';

our @config_lines;	# array der Zeilen des Config-Files im Originalzustand (kein d2u etc.)
our $parser_mode;	# Typ des Config-Formates (basic, data)
our $rule_order = 0; 	# Reihenfolge der Rules im Configfile
our $rulebase_name;

## parse_audit_log Funktion fuer netscreen noch nicht implementiert

sub parse_audit_log { }

######################################
# remove_unwanted_chars
# param1: debuglevel [0-?]
# gobal var in and out: @config_lines 
#####################################
sub remove_unwanted_chars {
	my $debuglevel = shift;
	my $line;
	my @cleaned_lines = ();

	LINE: foreach $line (@config_lines) {
		if ($line =~ /^\s*$/) { # ignore empty lines
		} else { # remove non-printable characters
			my $line_orig = $line;
			$line =~ s/[^[:print:]]+//g;
			if ($line =~ /^\s+(.+?)$/) { $line = $1; }
			if ($line =~ /^(.+?)\s+$/) { $line = $1; }
			$line .= "\n";
			@cleaned_lines = (@cleaned_lines, "$line");
#			if ($line_orig ne $line) { print ("cleaned config line containing non-printable characters.\nOriginal line: $line_orig to: $line"); }
		}
	}
	@config_lines = @cleaned_lines;
}

######################################
# ns_mode_check
# bestimmt den Typ des Config-Formates
# param1: input-filename
# param2: debuglevel [0-?]
#####################################
sub ns_mode_check {
	my $debuglevel = shift;
	my $mode = '';
	my $line_num_for_check = 10;	# number of lines for testing the config format
	my $i = 0;
   # Setzen des Parsermodes
   # umschalten des Betriebsmodus anhand der ersten $line_num_for_check Zeile des uebergebenen Konfigurationsfiles
	MODE_DEF: {
		for ($i = 0; $i < $line_num_for_check; ++$i) {
			if ($config_lines[$i]=~/^u?n?set\s/) {
				$mode = 'basic';
				last MODE_DEF;
			}
			elsif ($config_lines[$i]=~/^\(DM/) {
				$mode = 'data';
				last MODE_DEF;
			}
			else {
				if ($mode !~ /basic/ or /data/) {
					$mode = 'unknown';
				}
			}
			if ($debuglevel >= 7){
				print ("sub ns_mode_check line_$i, mode: $mode : $config_lines[$i]\n");
			}
		}
		$parser_mode = $mode;
		if ($mode =~/unknown/) {
			print "unknown format in config file - exiting!\n";
			return 1;
		}
	}
	if ($debuglevel >= 1){
		print ("sub ns_mode_check line_$i, mode: $mode : $config_lines[$i]\n");
	}
	return 0;
}


#####################################
# is_domain_name
# param1: string
# returns true if string is domain name and no ip
#####################################
sub is_domain_name {
	my @parts = split(/\./, shift);
	if (scalar(@parts) > 1) {
		if (Scalar::Util::looks_like_number($parts[$#parts])) {
			return 0;
		} else {
			return 1; 
		}
	}
	return 0;
}

#####################################
# split_lines_to_parameter
# param1: input-line
# param2: debuglevel [0-?]
# Separator=space, Strings in double quotes
#####################################
sub split_lines_to_parameter {
   my $line = $_[0];
	my $debuglevel = $_[1];
	my $i=0;
	my $l=0;
	my $k=$i;
	my @params;
	my @parameter = ();

	$parameter[$k] ='';
	$line =~ s/\n//;
	$line =~ s/\r//;
	@params = split (/ /,$line);
	while ($i <= $#params) {
		if ($params[$i] =~ /^".+"$/) { 
			$parameter[$k] = "$params[$i]";
			$parameter[$k] =~ s/"//g;
			$k++;
			$i++;
		}
		elsif ($params[$i] =~ /^"/) { 
			do {
				if (defined($parameter[$k])) {
					$parameter[$k] = "$parameter[$k] $params[$i]";
				} else {
					$parameter[$k] = "$params[$i]";
				}
			}
			while ($params[$i++] !~ /"$/ and $i <= $#params);
			$parameter[$k] =~ s/^\s//;
			$parameter[$k] =~ s/"//g;
			$k++;
		}
		else {
			$parameter[$k] = "$params[$i]";
			$k++;
			$i++;
		}
	}
	@parameter = @parameter;
}


#####################################
# ns_object_address
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub ns_object_address {
   my $line = $_[0];
	my $debuglevel = $_[1];
	my $i=0;
	my $l=0;
	my @params;
	my $act_obj_zone = '';
	my $act_obj_name = '';
	my $act_obj_nameZ = '';
	my $act_obj_ipaddr = '';
	my $act_obj_ipaddr_last = '';
	my $act_obj_mask = '';
	my $act_obj_comm = '';
	my $act_obj_type = '';
	my $act_obj_loc = '';
	my $act_obj_color = 'black';
	my $act_obj_sys = '';
	my $act_obj_uid = '';
	my $test = '';
		
	@params = split_lines_to_parameter ($line,$debuglevel);
	$act_obj_zone		=	$params[2];
	$act_obj_name		=	$params[3];
	$act_obj_ipaddr		=	$params[4];
	$act_obj_mask		=	$params[5];
	if ($params[6]) { 
		$act_obj_comm	=	$params[6];
	} else {
		if ($act_obj_mask !~ /\d+\.\d+\.\d+\.\d+/) {	# 5. Parameter ist keine Netzmask, sondern Kommentar (bei v6-Objekten)
			$act_obj_comm = $act_obj_mask;
			($test, $act_obj_mask) = split(/\//, $act_obj_ipaddr);
		}
	}
	if (&is_domain_name($act_obj_ipaddr)) {
		if (!defined($act_obj_comm) || $act_obj_comm eq '') {
			$act_obj_comm = "domain object: $act_obj_ipaddr";
		} else {
			$act_obj_comm	.=	", domain object: $act_obj_ipaddr";
		}
		$act_obj_ipaddr = '0.0.0.1/32';
	}
	
	$act_obj_nameZ = "${act_obj_name}__zone__$act_obj_zone";	# Zone in Feldindex mit aufgenommen
	OBJECT_DEF_GROUP: {
		if (!defined ($network_objects{"$act_obj_nameZ.name"})) {
			@network_objects = (@network_objects, $act_obj_nameZ);
			$network_objects{"$act_obj_nameZ.name"} = $act_obj_name;
			$network_objects{"$act_obj_nameZ.UID"} = $act_obj_nameZ;
			if ($debuglevel >= 5){	print "ns_object_address: found new network object $act_obj_name\n"; }
		}
		else {
			last OBJECT_DEF_GROUP;
		}
	}
	my $is_ipv6 = ($act_obj_ipaddr =~ /\:/);
	my $subnetbits;
	if ($is_ipv6) {
		if (defined($act_obj_mask)) {
			$subnetbits = $act_obj_mask;
		} else {
			my $addr;
			($addr, $subnetbits) = split (/\//, $act_obj_ipaddr);
			if (!defined($subnetbits)) {
				$subnetbits = 128;			
			}
		}
	} else {
		$subnetbits = &calc_subnetmask ($act_obj_mask);
	}
#	print ("debug: ip_addr = $act_obj_ipaddr, mask=$act_obj_mask, subnetbits = $subnetbits\n");
	if (!$is_ipv6) {
		if (!defined($act_obj_mask)) {
			$act_obj_mask = '255.255.255.255';			
		}
		if (!defined($subnetbits) || $subnetbits==32) { $network_objects{"$act_obj_nameZ.type"} = 'host'; }
		else { $network_objects{"$act_obj_nameZ.type"} = 'network'; }
	} else {
		if ($subnetbits==128) { $network_objects{"$act_obj_nameZ.type"} = 'host'; }
		else { $network_objects{"$act_obj_nameZ.type"} = 'network'; }
	}
	$network_objects{"$act_obj_nameZ.netmask"} = $act_obj_mask ;
	$network_objects{"$act_obj_nameZ.zone"} = $act_obj_zone;		# neues Feld fuer Zone
	$network_objects{"$act_obj_nameZ.ipaddr"} = $act_obj_ipaddr;
	$network_objects{"$act_obj_nameZ.ipaddr_last"} = $act_obj_ipaddr_last;
	$network_objects{"$act_obj_nameZ.color"} = $act_obj_color;
	$network_objects{"$act_obj_nameZ.comments"} = $act_obj_comm;
	$network_objects{"$act_obj_nameZ.location"} = $act_obj_loc;
	$network_objects{"$act_obj_nameZ.sys"} = $act_obj_sys;
}

#####################################
# ns_object_address_add
# param1: name
# param2: ip
#####################################
sub ns_object_address_add {
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
	my $subnetbits;
		
	$act_obj_nameZ = "${act_obj_name}__zone__$act_obj_zone";	# Zone in Feldindex mit aufgenommen
	if (!defined ($network_objects{"$act_obj_nameZ.name"})) {
		@network_objects = (@network_objects, $act_obj_nameZ);
		$network_objects{"$act_obj_nameZ.name"} = $act_obj_name;
	} elsif (defined ($network_objects{"$act_obj_nameZ.name"})) {
		print "sub ns_object_address NET_OBJECT: $act_obj_nameZ ist bereits definiert.\n";
	} else {
		print "sub ns_object_address NET_OBJECT: $act_obj_nameZ ist undefiniert.\n";
	}
	my $is_ipv6 = ($act_obj_ipaddr =~ /\:/);
	if ($is_ipv6) {
		if (defined($act_obj_mask)) {
			$subnetbits = $act_obj_mask;
		} else {
			$subnetbits = 128;			
		}
	} else {
		$subnetbits = &calc_subnetmask ($act_obj_mask);
	}
	if (!$is_ipv6) {
		if (!defined($act_obj_mask)) {
			$act_obj_mask = '255.255.255.255';			
		}
		if ($subnetbits==32) { $network_objects{"$act_obj_nameZ.type"} = 'host'; }
		else { $network_objects{"$act_obj_nameZ.type"} = 'network'; }
	} else {
		if ($subnetbits==128) { $network_objects{"$act_obj_nameZ.type"} = 'host'; }
		else { $network_objects{"$act_obj_nameZ.type"} = 'network'; }
	}
	if (&is_domain_name($act_obj_ipaddr)) {
		if (!defined($act_obj_comm) || $act_obj_comm eq '') {
			$act_obj_comm = "domain object: $act_obj_ipaddr";
		} else {
			$act_obj_comm	.=	", domain object: $act_obj_ipaddr";
		}
		$act_obj_ipaddr = '0.0.0.1/32';
	}

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

#####################################
# ns_object_group_address
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub ns_object_group_address {
   my $line = $_[0];
	my $debuglevel = $_[1];
	my $i=0;
	my $l=0;
	my @params;
	my $act_obj_zone = '';
	my $act_obj_name = '';
	my $act_obj_nameZ = '';
	my $act_obj_mbr = '';
	my $act_obj_fkt = '';
	my $act_obj_color = 'black';
	my $act_obj_comm = '';
	my $mbrlst = '';
	my $mbr_ref_lst = '';

	@params = split_lines_to_parameter ($line,$debuglevel);
	$act_obj_zone		=	$params[3];
	$act_obj_name		=	$params[4];
	if ($params[6]) { $act_obj_mbr	=	$params[6]; }
	if ($params[5]) { $act_obj_fkt	=	$params[5]; }

	$act_obj_nameZ = "${act_obj_name}__zone__$act_obj_zone";	# Zone in Feldindex mit aufgenommen
	OBJECT_DEF: {
		if (!defined ($network_objects{"$act_obj_nameZ.name"})) {
			if ($debuglevel >= 5){	print "ns_object_group_address: found new network object group $act_obj_name\n"; }
			@network_objects = (@network_objects, $act_obj_nameZ);
			$network_objects{"$act_obj_nameZ.name"} = $act_obj_name;
			$network_objects{"$act_obj_nameZ.UID"} = $act_obj_nameZ;
		}
		else {
			last OBJECT_DEF;
		}
	}
	$network_objects{"$act_obj_nameZ.zone"} = $act_obj_zone;		# neues Feld fuer Zone
	$network_objects{"$act_obj_nameZ.type"} = 'group';
	$network_objects{"$act_obj_nameZ.color"} = $act_obj_color;
	if ( $act_obj_fkt =~ /add/ ) {
		if (defined($network_objects{"$act_obj_nameZ.members"})) {
			$mbrlst = $network_objects{"$act_obj_nameZ.members"};
			$mbr_ref_lst = $network_objects{"$act_obj_nameZ.member_refs"};
		}
		if ( $mbrlst eq '' ) {
			$mbrlst = $act_obj_mbr;
			$mbr_ref_lst = $act_obj_mbr . "__zone__$act_obj_zone";
		}
		else {
			$mbrlst = "$mbrlst|$act_obj_mbr";
			$mbr_ref_lst = "$mbr_ref_lst|$act_obj_mbr" . "__zone__$act_obj_zone";
		}
		$network_objects{"$act_obj_nameZ.members"} = $mbrlst;
		$network_objects{"$act_obj_nameZ.member_refs"} = $mbr_ref_lst;
	}
}

#####################################
# ns_object_service
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub ns_object_service {
	my $line = $_[0];
	my $debuglevel = $_[1];
	my $i=0;
	my $l=0;
	my @params;
	my @range;
	my $act_obj_typ = '';
	my $act_obj_type = '';
	my $act_obj_name = '';
	my $act_obj_proto = '';
	my $act_obj_src = '';
	my $act_obj_src_last = '';
	my $act_obj_dst = '';
	my $act_obj_dst_last = '';
	my $act_obj_comm = '';
	my $act_obj_time = '';
	my $act_obj_time_std = '';
	my $act_obj_color = 'black';
	my $act_obj_rpc = '';
	my $act_obj_uid = '';
	my $extention = 0;

	@params = split_lines_to_parameter ($line,$debuglevel);
	$act_obj_name		=	$params[2];
	$extention			=	$params[3];
	$act_obj_typ		=	'simple';
	$act_obj_type		=	$params[4];
	$act_obj_src		=	$params[6];
	$act_obj_dst		=	$params[8];
	if ($params[10]) { 
		my $time1 = $params[10];
		if ($time1 eq 'never') {
			$time1 = 43200;  # ca. 1 Monat sollte als utopisches Maximum reichen
		}
		$act_obj_time = 60 * $time1;
	}
	if ($extention =~ /protocol/) {
		if (!defined ($services{"$act_obj_name.name"})) {
			if ($debuglevel >= 5){	print "ns_object_service: found new service object $act_obj_name\n"; }
			@services = (@services, $act_obj_name);
			$services{"$act_obj_name.name"} = $act_obj_name;
			$services{"$act_obj_name.extention"} = $extention;
		}
		else {
			if ( $services{"$act_obj_name.comments"} =~ /netscreen_predefined_service/ ) {
			} else {
				print "sub ns_object_services SERVICE: $act_obj_name ist bereits definiert.\n";
			}
		}
	}
	elsif ($extention =~ /\+/) {   #   MultiDienste (+service)
		my $group_name = $act_obj_name;
		if (!defined ($services{"$act_obj_name.name"})) {
			print ("Fehler: +Service $act_obj_name ohne vorherige Definition\n");
		} else {
			if ($services{"$act_obj_name.typ"} ne 'group') { # ersten Dienst erst in Gruppe umwandeln
				# dazu erstmal den alten Dienst duplizieren (mit no=0)
				my $first_name = $act_obj_name . "_netscreen_PLUS_1";
				$services{"$first_name.name"} = $first_name;
				$services{"$first_name.src_port"} = $services{"$act_obj_name.src_port"};
				$services{"$first_name.src_port_last"} = $services{"$act_obj_name.src_port_last"};
				$services{"$first_name.port"} = $services{"$act_obj_name.port"};
				$services{"$first_name.port_last"} = $services{"$act_obj_name.port_last"};
				$services{"$first_name.ip_proto"} = $services{"$act_obj_name.ip_proto"};
				$services{"$first_name.timeout"} = $services{"$act_obj_name.timeout"};
				$services{"$first_name.color"} = $services{"$act_obj_name.color"};
				$services{"$first_name.comments"} = $services{"$act_obj_name.comments"};
				$services{"$first_name.typ"} = $services{"$act_obj_name.typ"};
				$services{"$first_name.type"} = $services{"$act_obj_name.type"};
				if (defined($services{"$act_obj_name.rpc_port"})) {
					$services{"$first_name.rpc_port"} = $services{"$act_obj_name.rpc_port"};
				}
				$services{"$first_name.UID"} = $first_name;
				push @services, $first_name;
				
				# jetzt den Ursprungsdienst in eine Gruppe umwandeln
				$services{"$group_name.typ"} = 'group';
				$services{"$group_name.members"} = "$first_name";
				$services{"$group_name.member_refs"} = "$first_name";
				$services{"$group_name.member_zahl"} = 1;
				undef (	$services{"$group_name.src_port"} );
				undef (	$services{"$group_name.src_port_last"} );
				undef (	$services{"$group_name.port"} );
				undef (	$services{"$group_name.port_last"} );
				undef (	$services{"$group_name.ip_proto"} );
			}
			$services{"$group_name.member_zahl"} = $services{"$group_name.member_zahl"} + 1;
			my $member_zahl = $services{"$group_name.member_zahl"};
			my $new_plus_svc_name = $group_name . "_netscreen_PLUS_" . $services{"$group_name.member_zahl"};
			push @services, $new_plus_svc_name;
			$act_obj_name = $new_plus_svc_name;
			$services{"$act_obj_name.name"} = $act_obj_name;
			$services{"$act_obj_name.UID"} = $act_obj_name;
			$services{"$act_obj_name.extention"} = $extention;
			$services{"$group_name.members"} .= "|$new_plus_svc_name";
			$services{"$group_name.member_refs"} .= "|$new_plus_svc_name";
		}
	} else { 
		print "?\n";
		print "$line\n";
	}
	if ($act_obj_type =~ /rpc/) {
#		print "RPC-line: $line; obj_type: $act_obj_type, act_obj_src: $act_obj_src\n";
		$services{"$act_obj_name.src_port"} = 0;
		$services{"$act_obj_name.src_port_last"} = 65535;
		$services{"$act_obj_name.port"} = 0;
		$services{"$act_obj_name.port_last"} = 65535;
		$services{"$act_obj_name.typ"} ='rpc';
#		$services{"$act_obj_name.rpc_nr"} = $act_obj_src;
		$act_obj_rpc = $act_obj_src;
#		$act_obj_rpc = $act_obj_src;
#		if ($line =~ /protocol ms-rpc (uuid .*)$/) {
#			my $uuid = $1;
#			print "    found in RPC-line: $uuid\n";
#			$services{"$act_obj_name.rpc_nr"} = $1;
#			$services{"$act_obj_name.svc_prod_specific"} = $1;
#		}
	} else {	
		@range = split ( /-/, $act_obj_src);
		$services{"$act_obj_name.src_port"} = $range[0];
		$services{"$act_obj_name.src_port_last"} = $range[1];
		@range = split ( /-/, $act_obj_dst);
		$services{"$act_obj_name.port"} = $range[0];
		$services{"$act_obj_name.port_last"} = $range[1];
	}
	$services{"$act_obj_name.ip_proto"} = get_proto_number($act_obj_type);
	$services{"$act_obj_name.timeout"} = $act_obj_time;
	$services{"$act_obj_name.color"} = $act_obj_color;
	$services{"$act_obj_name.comments"} = $act_obj_comm;
	$services{"$act_obj_name.typ"} = $act_obj_typ;
	$services{"$act_obj_name.type"} = $act_obj_type;
	$services{"$act_obj_name.rpc_port"} = $act_obj_rpc;
	$services{"$act_obj_name.UID"} = $act_obj_name;
}

#####################################
# ns_object_group_service
# param1: input-line
# param2: debuglevel [0-?]
#####################################
sub ns_object_group_service {
   my $line = $_[0];
	my $debuglevel = "";
	my $i=0;
	my $l=0;
	my @params;
	my @range;
	my $act_obj_typ = 'group';
	my $act_obj_type = '';
	my $act_obj_mbr = '';
	my $act_obj_fkt = '';
	my $act_obj_name = '';
	my $act_obj_proto = '';
	my $act_obj_src = '';
	my $act_obj_src_last = '';
	my $act_obj_dst = '';
	my $act_obj_dst_last = '';
	my $act_obj_comm = '';
	my $act_obj_time = '';
	my $act_obj_time_std = '';
	my $act_obj_color = 'black';
	my $act_obj_rpc = '';
	my $act_obj_uid = '';
	my $mbrlst = '';
	my $test = '';

	if (defined($_[1])) {
		$debuglevel = $_[1];
	}
	@params = split_lines_to_parameter ($line,$debuglevel);
	$act_obj_name		=	$params[3];
	if ($params[5]) { 
		$act_obj_mbr	=	$params[5];
	}
	if ($params[4]) { 
		$act_obj_fkt	=	$params[4];
	}
			
	if (!defined ($services{"$act_obj_name.name"})) {
		@services = (@services, $act_obj_name);
		$services{"$act_obj_name.name"} = $act_obj_name;
	}
	if ( $act_obj_fkt =~ /add/ ) {
		if (defined($services{"$act_obj_name.members"})) {
			$mbrlst = $services{"$act_obj_name.members"};
		}
		if ( $mbrlst eq '' ) {
			$mbrlst = $act_obj_mbr;
		}
		else {
			$mbrlst = "$mbrlst|$act_obj_mbr";
		}
		$services{"$act_obj_name.members"} = $mbrlst;
		$services{"$act_obj_name.member_refs"} = $mbrlst;
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

#####################################
# ns_rules_initial
# param1: input-line [string]
# param2: rule_order, [integer] 	 Reihenfolge der Rules im Configfile
#debuglevel [integer]
#####################################
sub ns_rules_initial {
	my $line = $_[0];
	my $order = $_[1];
	my $debuglevel = $_[2];
	my $i=0;
	my $l=0;
	my $act_rule_id = -1;
	my $act_rule_ruleid = -1;
	my $act_rule_name	=	'';
	my	$act_rule_Szone	=	'';
	my	$act_rule_Dzone	=	'';
	my	$act_rule_src	=	'';
	my	$act_rule_dst	=	'';
	my	$act_rule_srv	=	'';
	my $act_rule_act	=	'';
	my	$act_rule_track	=	'';
	my	$act_rule_count	=	'';
	my @params;
	my @range;
	my $from_zone_idx = 5;
	my ($nat_idx, $action_idx);

	# Handling von dst MIP-IPs, muster: set policy id 8 from "Untrust" to "Trust"  "Any" "MIP(12.7.2.0/24)" "ANY" permit log count
	if ($line =~ /set policy id \d+ (name .*?)?from \"([\w\_\-\.]+)\" to \"([\w\_\-\.]+)\"\s+\".*?\"\s+\"MIP\(([\d\.]+)\/?(\d+)?\)\"/) {
		my $mip_ip = $4;
		my $dst_zone = $3;
		my $netmask_bits = $5;
		my $netmask_string = '';
		if (!defined($netmask_bits) || $netmask_bits eq '') { $netmask_bits = 32; }
		my $netmask_dotted = &convert_mask_to_dot_notation($netmask_bits); 
		if ($netmask_bits<32) { $netmask_string = "/$netmask_bits"; }
		if ($debuglevel>1) { print("ns_rules_initial: found dst MIP. MIP($mip_ip$netmask_string) in zone $dst_zone\n"); }
		&ns_object_address_add("MIP($mip_ip$netmask_string)", "$mip_ip", $netmask_dotted, $dst_zone, 'Virtual MIP, generated by ITSecOrg from interface MIP definition');
	}
=cut
	# Handling von src MIP-IPs (wenn es sowas gibt), muster: set policy id 8 from "Untrust" to "Trust" "MIP(12.7.2.27)" "Any" "ANY" permit log count
	if ($line =~ /set policy id (name .*?)?\d+ from \"([\w\_\-\.]+)\" to \"([\w\_\-\.]+)\"\s+\"MIP\(([\d\.]+)\/?(\d+)?\)\"\s+\".*?\"/) {
		my $mip_ip = $4;
		my $src_zone = $2;
		my $netmask_bits = $5;
		my $netmask_string = '';
		if (!defined($netmask_bits) || $netmask_bits eq '') { $netmask_bits = 32; }
		my $netmask_dotted = &convert_mask_to_dot_notation($netmask_bits); 
		if ($netmask_bits<32) { $netmask_string = "/$netmask_bits"; }
		&ns_object_address_add("MIP($mip_ip$netmask_string)", "$mip_ip", $netmask_dotted, $src_zone, 'Virtual MIP, generated by ITSecOrg from interface MIP definition');
	}
=cut
	$rulebases{"$rulebase_name.rulecount"} = $order + 1;	# Anzahl der Regeln wird sukzessive hochgesetzt
	@params = split_lines_to_parameter ($line,$debuglevel);
	$act_rule_id		=	$params[3];
	$act_rule_ruleid	=	$act_rule_id;
	$ruleorder[$order] = $act_rule_id; 
	if ($params[4] =~ /^name$/) {
		$act_rule_name	=	$params[5];
		$from_zone_idx = 7;
	}
	$nat_idx = $from_zone_idx + 7;
	$action_idx = $nat_idx;  # Normalfall ohne NAT
	$act_rule_Szone			=	$params[$from_zone_idx+0];
	$act_rule_Dzone			=	$params[$from_zone_idx+2];
	$act_rule_src			=	$params[$from_zone_idx+4];
	$act_rule_dst			=	$params[$from_zone_idx+5];
	my $act_rule_src_refs	=	"${act_rule_src}__zone__$act_rule_Szone";
	my $act_rule_dst_refs	=	"${act_rule_dst}__zone__$act_rule_Dzone";
	$act_rule_srv			=	$params[$from_zone_idx+6];
	if ($params[$nat_idx] =~ /^nat$/) {
		my $nat_type = "initial";
		my $line_after_nat = "initial";
		my $found_nat = 0;
		
		if ($line =~ /(.*?)\s+nat\s+(.*)/) { $line_after_nat = $2;	}

		if (!$found_nat && $line_after_nat =~ /^dst ip [\d\.]+ port \d+ .*/) { # nat dst ip 10.132.160.12 port 44316 permit log
			$action_idx += 6; $found_nat = 1; $nat_type = '7 dst nat with port';
		} 
		if (!$found_nat && $line_after_nat =~ /^dst ip [\d\.]+ [\d\.]+ .*/) { # nat dst ip 2.52.20.0 2.52.21.255 permit log
			$action_idx += 5; $found_nat = 1; $nat_type = '8 dst nat with ip range'; 
		}
		if (!$found_nat && $line_after_nat =~ /^dst ip [\d\.]+ .*/) { # nat dst ip 10.132.160.12 permit log
			$action_idx += 4; $found_nat = 1; $nat_type = '1 dst nat simple';
		} 
		if (!$found_nat && $line_after_nat =~ /^src dst ip [\d\.]+ port \d+ .*/) { # nat src dst ip 192.168.0.4 port 22 permit log
			$action_idx += 7; $found_nat = 1; $nat_type = '9 src & dst & port nat';
		} 		
		if (!$found_nat && $line_after_nat =~ /^src dip\-id \d+ dst ip [\d\.]+ port \d+ .*/) { # nat src dip-id 32 dst ip 10.132.160.12 [port 222] permit log
			$action_idx += 9; $found_nat = 1; $nat_type = '6 src & dst & dip nat & port nat';
		} 
		if (!$found_nat && $line_after_nat =~ /^src dip\-id \d+ dst ip [\d\.]+ .*/) { # nat src dip-id 32 dst ip 10.132.160.12 permit log
			$action_idx += 7; $found_nat = 1; $nat_type = '2 src & dst & dip nat';
		} 
		if (!$found_nat && $line_after_nat =~ /^src dst ip [\d\.]+ .*/) {#  nat src dst ip 172.20.128.1
			$action_idx += 5; $found_nat = 1; $nat_type = '3 src dst nat';
		}
		if (!$found_nat && $line_after_nat =~ /^src dip\-id \d+ .*/) {#  nat src dip-id 4 permit log
			$action_idx += 4;  $found_nat = 1; $nat_type = '4 src & dip-id';
		}
		if (!$found_nat && $line_after_nat =~ /^src .*/) { # nat src permit  --> einfachster Fall der src nat ohne weitere Parameter
			$action_idx += 2; $found_nat = 1; $nat_type = '5 simple src nat'; 
		}
	}
	if (defined($params[$action_idx+0])) { $act_rule_act	=	$params[$action_idx+0]; }
	my $track_idx = $action_idx+1;
	if (defined($act_rule_act) && $act_rule_act eq 'tunnel') { # vpn regel
		$act_rule_act .= " $params[$action_idx+1]";
		$track_idx = $action_idx+2; # name des tunnels auslassen
	}
	if (defined($params[$track_idx]) && $params[$track_idx] =~ /auth|webauth/) {
		$act_rule_act .= " $params[$track_idx]";
		$track_idx ++;
	}
	my @track_types = qw(log alert count alarm);
	while (defined($params[$track_idx])) {  # collecting track info
		my $pattern_to_find = $params[$track_idx];
		if (grep(/$pattern_to_find/, @track_types)) {
			if (defined($act_rule_track) && length($act_rule_track)>0) {
				$act_rule_track .= " " . $params[$track_idx];
			} else { 
				$act_rule_track = $params[$track_idx];
			}
		}
		$track_idx ++;
	}
	if (!defined($act_rule_track) || $act_rule_track eq '') { $act_rule_track = 'none'; }
	if (length($act_rule_track)<3) {
		print ("warning, track short: <$act_rule_track>\n$line\n");
	}
	if ($debuglevel >= 5){	print "ns_rules_initial: found new policy $act_rule_id\n"; }
=cut
	if (!defined ($rulebases{"$rulebase_name.$act_rule_id.id"})) {
		if ((($debuglevel >= 4)&&($debuglevel<=10))||(($debuglevel >= 14)&&($debuglevel<=20))){
			print "sub ns_rules_initial RULE: $act_rule_id neu definiert.\n";
		}
	}
	elsif (defined ($rulebases{"$rulebase_name.$act_rule_id.id"})) {
		if ((($debuglevel >= 4)&&($debuglevel<=10))||(($debuglevel >= 14)&&($debuglevel<=20))){
			print "sub ns_rules_initial RULE: $act_rule_id ist bereits definiert.\n";
		}
	}
	else {
		if ((($debuglevel >= 4)&&($debuglevel<=10))||(($debuglevel >= 14)&&($debuglevel<=20))){
			print "sub ns_rules_initial RULE: $act_rule_id ist undefiniert.\n";
		}
	}
=cut
	$rulebases{"$rulebase_name.$act_rule_id.id"} = $act_rule_id;
	$rulebases{"$rulebase_name.$act_rule_id.ruleid"} = $act_rule_ruleid;
	$rulebases{"$rulebase_name.$act_rule_id.order"} = $order;
	$rulebases{"$rulebase_name.$act_rule_id.disabled"} = '0';
	$rulebases{"$rulebase_name.$act_rule_id.src.zone"} = $act_rule_Szone;
	$rulebases{"$rulebase_name.$act_rule_id.src"} = $act_rule_src;
	$rulebases{"$rulebase_name.$act_rule_id.src.refs"} = $act_rule_src_refs;
	$rulebases{"$rulebase_name.$act_rule_id.dst.zone"} = $act_rule_Dzone;
	$rulebases{"$rulebase_name.$act_rule_id.dst"} = $act_rule_dst;
	$rulebases{"$rulebase_name.$act_rule_id.dst.refs"} = $act_rule_dst_refs;
	$rulebases{"$rulebase_name.$act_rule_id.services.op"} = '0';
	$rulebases{"$rulebase_name.$act_rule_id.src.op"} = '0';
	$rulebases{"$rulebase_name.$act_rule_id.dst.op"} = '0';
	$rulebases{"$rulebase_name.$act_rule_id.services"} = $act_rule_srv;
	$rulebases{"$rulebase_name.$act_rule_id.services.refs"} = $act_rule_srv;
	$rulebases{"$rulebase_name.$act_rule_id.action"} = $act_rule_act;
	$rulebases{"$rulebase_name.$act_rule_id.track"} = $act_rule_track;
	$rulebases{"$rulebase_name.$act_rule_id.install"} = '';		# set hostname verwenden ?
	$rulebases{"$rulebase_name.$act_rule_id.name"} = '';		# kein Aequivalent zu CP rule_name
	$rulebases{"$rulebase_name.$act_rule_id.time"} = '';
	$rulebases{"$rulebase_name.$act_rule_id.comments"} = $act_rule_name;
	$rulebases{"$rulebase_name.$act_rule_id.UID"} = $act_rule_id;
	$rulebases{"$rulebase_name.$act_rule_id.header_text"} = '';
	
}

#####################################
# ns_rules_extended
# param1: debuglevel [integer]
# keine Hashuebergabe - zu fehleranfaellig bei meinem stil - arbeite mit globalem hash
# Aufruf darf erst erfolgen, wenn ns_rules_initial alle Rules erfasst hat
# Rules werden nur ergaenzt, fall sRule-id nicht existiert -> Fehlermeldung
#####################################
sub ns_rules_extended {
	my	$debuglevel = $_[0];
	my	$line = '';
	my	$act_rule_id = -1;
	my	$act_rule_src	=	'';
	my	$act_rule_dst	=	'';
	my	$act_rule_srv	=	'';
	my @params;
	
	foreach $line (@config_lines) {
		if ($debuglevel >= 9) { print $line; }
		@params = split_lines_to_parameter ($line,$debuglevel);
		
		if (($line =~/^set policy id \d+/) && ($#params == 3)) {
			$act_rule_id = $params[3]*1;
		} elsif (($line =~/^exit/) && ($act_rule_id > -1)) {
			$act_rule_id = -1;
		}
		if ($act_rule_id > -1) {
			if ($line=~ /^set service / && $#params == 2) {
#				if (!defined($rulebases{"$rulebase_name.$act_rule_id.services"})) { $rulebases{"$rulebase_name.$act_rule_id.services"} = ''; }
#				if (!defined($rulebases{"$rulebase_name.$act_rule_id.services.refs"})) { $rulebases{"$rulebase_name.$act_rule_id.services.refs"} = ''; }
				$act_rule_srv = $rulebases{"$rulebase_name.$act_rule_id.services"};
				$act_rule_srv = "$act_rule_srv|$params[2]";
				$rulebases{"$rulebase_name.$act_rule_id.services"} = $act_rule_srv;
				$rulebases{"$rulebase_name.$act_rule_id.services.refs"} = $act_rule_srv;
			}
			if ($line=~ /^set src-address/ && $#params == 2) {
				if ($line =~ /set src-address\s+\"MIP\(([\d\.]+)\/?(\d+)?\)\"/) {
					my $src_zone = $rulebases{"$rulebase_name.$act_rule_id.src.zone"};
					my $mip_ip = $1;
					my $netmask_bits = $2;
					my $netmask_string = '';
					my $is_ipv6 = ($mip_ip =~ /\:/);
					if ((!defined($netmask_bits) || $netmask_bits eq '') && $is_ipv6) { $netmask_bits = 128; }
					if ((!defined($netmask_bits) || $netmask_bits eq '') && !$is_ipv6) { $netmask_bits = 32; }
					my $netmask_dotted = &convert_mask_to_dot_notation($netmask_bits); 
					if ($is_ipv6 && $netmask_bits<128) { $netmask_string = "/$netmask_bits"; }
					if (!$is_ipv6 && $netmask_bits<32) { $netmask_string = "/$netmask_bits"; }
					&ns_object_address_add("MIP($mip_ip$netmask_string)", "$mip_ip", $netmask_dotted, $src_zone, 'Virtual MIP, generated by ITSecOrg from interface MIP definition');
				}
				$act_rule_src = $rulebases{"$rulebase_name.$act_rule_id.src"};
				my $act_rule_src_refs = $rulebases{"$rulebase_name.$act_rule_id.src.refs"};
				my $zone = $rulebases{"$rulebase_name.$act_rule_id.src.zone"};
				$act_rule_src = "$act_rule_src|$params[2]";
				$act_rule_src_refs .= "|$params[2]__zone__$zone";
				$rulebases{"$rulebase_name.$act_rule_id.src"} = $act_rule_src;
				$rulebases{"$rulebase_name.$act_rule_id.src.refs"} = $act_rule_src_refs;
				# keine Konsistenzpruefung bzgl. Zone, da die Konsistenz der Konfig unter diesem Gesichtspunkt vorausgesetzt wird
			}
			if ($line=~ /^set dst-address/ && $#params == 2) {
				# Handling von dst MIP-IPs, muster: set dst-address "MIP(89.19.225.170)"
				if ($line =~ /set dst-address\s+\"MIP\(([\d\.]+)\/?(\d+)?\)\"/) {
					my $dst_zone = $rulebases{"$rulebase_name.$act_rule_id.dst.zone"};
					my $mip_ip = $1;
					my $netmask_bits = $2;
					my $netmask_string = '';
					if (!defined($netmask_bits) || $netmask_bits eq '') { $netmask_bits = 32; }
					my $netmask_dotted = &convert_mask_to_dot_notation($netmask_bits); 
					if ($netmask_bits<32) { $netmask_string = "/$netmask_bits"; }
					&ns_object_address_add("MIP($mip_ip$netmask_string)", "$mip_ip", $netmask_dotted, $dst_zone, 'Virtual MIP, generated by ITSecOrg from interface MIP definition');
				}
				$act_rule_dst = $rulebases{"$rulebase_name.$act_rule_id.dst"};
				my $act_rule_dst_refs = $rulebases{"$rulebase_name.$act_rule_id.dst.refs"};
				my $zone = $rulebases{"$rulebase_name.$act_rule_id.dst.zone"};
				$act_rule_dst = "$act_rule_dst|$params[2]";
				$act_rule_dst_refs .= "|$params[2]__zone__$zone";
				$rulebases{"$rulebase_name.$act_rule_id.dst"} = $act_rule_dst;
				$rulebases{"$rulebase_name.$act_rule_id.dst.refs"} = $act_rule_dst_refs;
				# keine Konsistenzpruefung bzgl. Zone, da die Konsistenz der Konfig unter diesem Gesichtspunkt vorausgesetzt wird
			}
		}
	}
}

############################################################
# ns_add_zone ($new_zone)
############################################################
sub ns_add_zone {
	my $new_zone = shift;
	my $debug_level = shift;
	my $is_there = 0;
	foreach my $elt (@zones) { if ($elt eq $new_zone) { $is_there = 1; last; } }
	if (!$is_there) {
		push @zones, $new_zone;
		if ($debug_level >= 2){	print "ns_add_zone: found new zone $new_zone\n"; }
	}
}

############################################################
# read_predefined_services(
############################################################
sub read_predefined_services {
	my $device_type = shift;
	my $predefined_service_string = shift;
	my $predef_svc;
	my ($svc_name,$ip_proto,$port,$port_end,$timeout,$comment,$typ,$group_members);

	$predef_svc = exec_pgsql_cmd_return_value ("SELECT dev_typ_predef_svc FROM stm_dev_typ WHERE dev_typ_id=$device_type");
	my @predef_svc = split /\n/, $predef_svc;
	foreach my $svc_line (@predef_svc) {
		($svc_name,$ip_proto,$port,$port_end,$timeout,$comment,$typ,$group_members) = split /;/, $svc_line;
		$services{"$svc_name.name"}			= $svc_name;
		$services{"$svc_name.port"}			= $port;
		$services{"$svc_name.port_last"}	= $port_end;
		$services{"$svc_name.ip_proto"}		= $ip_proto;
		$services{"$svc_name.timeout"}		= $timeout;
		$services{"$svc_name.color"}		= "black";
		$services{"$svc_name.comments"}		= "$predefined_service_string, $comment";
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

#	the scp command has to be used for tests with non-netscreen devices
#	$cmd = "$scp_bin $scp_batch_mode_switch -i $workdir/$CACTUS::ISO::ssh_id_basename $ssh_user\@$ssh_hostname:ns_sys_config $cfg_dir/$obj_file_base";
	$cmd = "$ssh_client_screenos -z $ssh_hostname -t netscreen -i $workdir/$CACTUS::ISO::ssh_id_basename -c 'get config' -u $ssh_user -d 0 -o $cfg_dir/$obj_file_base";
	if (system ($cmd)) { $fehler_count++; }
	return ($fehler_count, "$cfg_dir/$obj_file_base" );
}

sub sort_rules_and_add_zone_headers {
	my $anzahl_regeln = $rulebases{"$rulebase_name.rulecount"};
	my $count;
	my $zone_string;
	my @rule_zones = ();

	# Nachbereitung Regeln: Sortierung nach a) Zonen b) $ruleorder

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
			$rulebases{"$rulebase_name.$ruleorder[$count].src"} = "Any";
			$rulebases{"$rulebase_name.$ruleorder[$count].dst"} = "Any";
			$rulebases{"$rulebase_name.$ruleorder[$count].services"} = "ANY";
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


# check auf Vollstaendigkeit des Config-Files:
sub is_cfg_file_complete {
	my $ln_cnt = $#config_lines;
	my $cfg_file_complete = 1;	
	while ($config_lines[$ln_cnt] =~ /^\s*$/ ) { $ln_cnt -- ; }		# ignore empty lines at the end
	if ($config_lines[$ln_cnt] !~ /^.*set multicast\-group\-policy/) {
		if ( $config_lines[$ln_cnt] =~ /^exit.?$/ || $config_lines[$ln_cnt] =~ /^set zone \"?.+?\"? ip-classification /) {
			# last non-empty line must either be "exit" or multicast-related
			$ln_cnt -- ;
			if ( $config_lines[$ln_cnt] !~ /^(un)?set .*?route.*?$/ && $config_lines[$ln_cnt] !~ /^set zone \"?.+?\"? ip-classification /) {
				# assuming that last config part deals with routing 
				$cfg_file_complete = 0;
				print ("expected last line to deal with routing. Instead got: " . $config_lines[$ln_cnt] . "\n");
			}
		} else {
			$cfg_file_complete = 0;
			print ("expected last line to either be 'exit' or mutlicast-related. Instead got: " . $config_lines[$ln_cnt] . "\n");
		}
	}
	return $cfg_file_complete;
}

sub parse_users { # not implemented yet
	my $line = shift;
	if ( $line=~ /^set user.*/ ) {
		# print "found user: $line\n";
		# noch auszuprogrammieren
		# set user "oe560" uid 1
		# set user "oe560" type  auth
		# set user "oe560" hash-password "asdf askdfaslkdfjalsjfd"
		# set user "oe560" "enable"
	}
}

#####################################################################################
# MAIN

sub parse_config {
	# ($obj_file, $rule_file, $rulebases, $iso_workdir, $debug_level)
	my $in_file_main = shift;
	shift; 
	shift; # $rule_file und $user nicht verwendet
	my $dev_info_hash = shift;  # fuer netscreen gibt es pro management immer nur genau ein device
	my $iso_workdir = shift;
	my $debuglevel_main = shift;
	my $mgm_name = shift;
	my $config_dir = shift;
	my $import_id = shift;	
	my $line = '';
	my $ln_cnt = $#config_lines;
	my @vsys_lines = ();
	my $vsys_started = 0;
	my $dev_name = 'undefined_dev_name';
	
	# Initializing
	
	@services = ();
	@network_objects = ();
	my $device_type=3; # netscreen v5.1 fest gesetzt, TODO: move to config
	my $predefined_service_string = "netscreen_predefined_service";	# move to config

	if ($debuglevel_main >= 2){	print "in_file_main:$in_file_main!\ndebuglevel_main:$debuglevel_main!\n"; }
	&read_predefined_services($device_type, $predefined_service_string); # schreibt die predefined services in @services und %services
	
	open (IN, $in_file_main) || die "$in_file_main konnte nicht geoeffnet werden.\n";
	@config_lines = <IN>;
	close (IN);
	
	&remove_unwanted_chars ($debuglevel_main);
		
	if (&ns_mode_check($debuglevel_main)) { return "unknown-netscreen-config-file-mode-$mgm_name"; }
	
	if (!&is_cfg_file_complete()) { return "incomplete-config-file-$mgm_name"; }
	else {
		foreach $line (@config_lines) {		#	extract all zone information (global zones can be used in vsys config)
			if ( $line =~ /^set zone id \d+ "(.+)"/ || $line =~ /^set zone "(.+)" vrouter / || $line =~ /^set interface ".+" zone "(.+)"/ ) {
				&ns_add_zone ($1, $debuglevel_main);
			}
		}	
		while ( (my $key, my $value) = each %{$dev_info_hash} ) { $dev_name = $value->{'dev_name'}; } # nur ein device pro netscreen management
#		print ("searching for vsys with dev_name $dev_name, mgm_name=$mgm_name\n");
		@rulebases = ($dev_name); $rulebase_name = $dev_name;
		if ($dev_name ne '')  {  # vsys config erwartet
			foreach $line (@config_lines) {
				if ($line =~ /^set vsys \"?$dev_name\"? /i) {	# start of right vsys definition
					$vsys_started = 1;
				} elsif ($line =~ /^set vsys \"?/) { $vsys_started = 0; }
				elsif ($vsys_started) {
					@vsys_lines = (@vsys_lines, $line);
				}
			}	
			if ($#vsys_lines>0) { @config_lines = @vsys_lines; }	#	only copy vsys config if correct vsys has been found
		} 
		LINE: foreach $line (@config_lines) {
			if ($debuglevel_main >= 9) { print "main debug line: $line"; }
			if ($line=~ /^set hostname ([\w\.\_\-]+)/) { $mgm_name = $1; @rulebases = ($mgm_name); $rulebase_name = $mgm_name; }  # wird auch in ns_rules_initial verwendet
			$line=~ (/^set address/) && do {&ns_object_address ($line,$debuglevel_main);};
			$line=~ (/^set group address/) && do {&ns_object_group_address ($line,$debuglevel_main);};
			if ($line=~ /^set service.+?protocol.*/ || $line=~ /^set service.+? \+ .*/) { ns_object_service ($line, $debuglevel_main); }
			if ($line=~ /^set group service.*/) { ns_object_group_service ($line,$debuglevel_main); }
###########	Start Regelparser
			if ($line=~ /^set policy id \d+ (application|attack)/ ) {	next LINE; } # ignore it
			if ($line=~ /^set policy id (\d+) (name|from)/ ) {		# Standardfall: eine Regeldefinition beginnt
				if ($debuglevel_main >= 5){	print "parse_config: found new policy $1\n"; }
				&ns_rules_initial ($line,$rule_order,$debuglevel_main);
				$rule_order ++;
			}
			if ( $line=~ /^set policy id (\d+) disable.*/ ) {		# Regel deaktiviert
				if (defined($rulebases{"$rulebase_name.$1.id"})) { $rulebases{"$rulebase_name.$1.disabled"} = '1'; }
				else { print ("Fehler: noch nicht definierte Regel disabled: $line\n"); }
			}
###########	Ende Regelparser
			&parse_users($line);
		}
		&ns_rules_extended ($debuglevel_main); # file noch einmal durchgehen und diesmal nach Ergaenzungen der Regeln mit svc, src oder dst suchen
		# Any-Objekte fuer alle Zonen einfuegen
		push @zones, "Global"; 	# Global Zone immer hinzufuegen
		foreach my $zone (@zones) {
			&ns_object_address_add("Any", "0.0.0.0", "0.0.0.0", $zone, "Any-Obj for Zone added by ITSecOrg"); 
			&ns_object_address_add("Any-IPv4", "0.0.0.0", "0.0.0.0", $zone, "Any-IPv4-Obj for Zone added by ITSecOrg"); 
			&ns_object_address_add("Any-IPv6", "::", "0", $zone, "Any-IPv6-Obj for Zone added by ITSecOrg"); 
		}
		&sort_rules_and_add_zone_headers ();
		# neu: Wegschreiben der Regelreihenfolge
	    $rulebases{"$rulebase_name.ruleorder"} = join(',', @ruleorder);
#		print_results_monitor('objects');
#		print_results_monitor('rules');
		print_results_files_objects($iso_workdir, $mgm_name, $import_id);
		print_results_files_rules  ($iso_workdir, $mgm_name, $import_id);
		print_results_files_zones  ($iso_workdir, $mgm_name, $import_id);
		return 0;
	}
}

1;
__END__

=head1 NAME

CACTUS::ISO::parser - Perl extension for IT Security Organizer netscreen parser

=head1 SYNOPSIS
 use CACTUS::ISO::import::netscreen;

=head1 DESCRIPTION

IT Security Organizer Perl Module
support for importing configs into ITSecOrg Database

=head2 EXPORT

  global variables


=head1 SEE ALSO

  behind the door

=head1 AUTHOR

  Holger Dost, Tim Purschke, tmp@cactus.de


