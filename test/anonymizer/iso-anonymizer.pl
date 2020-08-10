#! /usr/bin/perl -w
# -------------------------------------------------------------------------------------------
# iso-anonymizer.pl
# run like this: 
# ./iso-anonymizer.pl [-txt-subst-file=/var/tmp/strings.txt] [-net="192.168.0.0/16"] <config-file1 config-file2 ...> 
# -------------------------------------------------------------------------------------------
require 5.006_000; # Needed for NetAddr::IP and file handler
require Exporter;
use strict;
use warnings;
use CGI qw(:standard);
use NetAddr::IP; # NetAddr::IP installation: apt-get install libnetaddr-ip-perl
use Carp;
use Time::HiRes qw(time tv_interval); # for exact recording of script execution time

my ($cfg_file, $line);
our @ISA = qw(Exporter);
my $infile;
my $txt_subst_file;
my $net="10.0.0.0/8";
my $outfile;
my %anonymized_ip;	
my %anonymized_text;
my $ano_txt = "IsoAAAA";	# starting pattern - needs to be alpha chars only for incrementing to work
my $ano_suffix = '.iso-anonymized';

sub remove_keys {
	my $line = shift;
	my @pattern_list = ();
	my $replacement = '\'removed by iso-anonymizer\'';
	# fortinet:
	@pattern_list = (@pattern_list,'^(\s*set passwd )','^(\s*set password )','^(\s*set ssh\-public\-key1 )','^(\s*set ssh\-public\-key2 )',
		'^(\s*set ssh\-public\-key3 )');
	# screenos:
	@pattern_list = (@pattern_list,'^(set admin password )','^(set admin user \".+?\" password )','^(set nsrp auth password )',
		'^(set ike gateway \".+?\" address .+? Main outgoing-interface \".+?\" preshare )','^(set auth\-server \".+?\" radius secret )');
	foreach my $pattern (@pattern_list) { if ($line =~ /$pattern/) { $line = "$1$replacement\n"; } }
	return $line;
}

sub remove_keys_multiline {
	my $line = shift;
	my @pattern_list = ();
	my $replacement = '\'removed by iso-anonymizer\'';
	# fortinet:
	@pattern_list = (@pattern_list,'^(\s*set private\-key )', '^(\s*set certificate )', '^(\s*set ca )', '^(\s*set csr )');
	foreach my $pattern (@pattern_list) { if ($line =~ /$pattern/) {  
#		print ("found multi start: $line"); 
		$line = "$1$replacement\n"; 
	} }
	return $line;
}

sub create_string_subst_hash {
	my $txt_subst_file_local = shift;
	open( my $txt_file, $txt_subst_file_local ) or croak "Unable to open $txt_subst_file_local: $!\n";
	while (my $line = <$txt_file>) {
		chomp ($line);
		$anonymized_text{$line} = $ano_txt;
		# adding separator chars (_-) contained in pattern again:
		if ($line =~ /.*?([\_\-])$/) { $anonymized_text{$line} .= $1; }	
		if ($line =~ /^([\_\-]).*?/) { $anonymized_text{$line} = $1 . $anonymized_text{$line}; }	
		++$ano_txt;
	}
	close ($txt_file);
	return;
}
sub _in_range { return 0 <= $_[0] && $_[0] <= 255; }

sub find_ipaddrs (\$&) {
    my($r_text, $callback) = @_;
    my $addrs_found = 0;
	my $regex = qr<(\d+)\.(\d+)\.(\d+)\.(\d+)(\/\d\d?)?>;

    $$r_text =~ s{$regex}{
        my $orig_match = join '.', $1, $2, $3, $4;
        if (defined($5) && $5 ne '') { $orig_match .= '/32'; }
        if ((my $num_matches = grep { _in_range($_) } $1, $2, $3, $4) == 4) {
            $addrs_found++;
            my $ipaddr = NetAddr::IP->new($orig_match);
            $callback->($ipaddr, $orig_match);
        } else {
            $orig_match;
        }
    }eg;
    return $addrs_found;
}

sub show_help {
	print ("---------------------------------------------------------------\n");
	print ("iso-anonyimzer (c) 2016 by Cactus eSecurity (https://cactus.de)\n");
	print ("---------------------------------------------------------------\n");
	print ("iso-anonyimzer can be used to substitute any occurence of ip addresses in a set of text files consistently.\n");
	print ("Might be helpful for anonymizing configuration files of routers, firewalls, etc. before handing them to third parties\n");
	print ("Consistently means that one ip is always substituted by the same destination ip address.\n");
	print ("All subnets, where identified as such, are replaced by /32 subnets. Does currently only handle IPv4 addresses.\n");
	print ("Additionally strings (e.g. customer names, etc.) can be (also consistently) replaced with generated anonymous strings starting with $ano_txt.\n");
	print ("Make sure that the string patterns do not contain any text that needs to stay unchanged in the output file.\n");
	print ("Note that anonymizing is performed consistently across all files. So if you need this multiple file consistency, \n");
	print ("make sure to anonymize all relevant files in a single run.\n");
	print ("\nSyntax:\n");
	print ("iso-anonymizer -help -txt-subst-file=<subst-filename> -net=<ip-subnet> -remove-keys <infile1> <infile2> ... <infilen>\n");
	print ("-help : displays this text (also when called without parameters)\n");
	print ("-txt-subst-file=<subst-filename> : optional, if parameter is set, substitutes all strings listed in <subst-filename> (one string per line)\n"); 
	print ("-net=<ip-subnet> : optional, defaults to '10.0.0.0/8' - ip subnet that is used for ip address substitution\n");
	print ("-remove-keys=1 : optional, removes keys, certificates and passwords/hashes appearig in config files in common formats \n");
	print ("<infile1> <infile2> ... <infilen> : list of files to anonymize\n\n");
	print ("Example:\n");
	print ("iso-anonymizer -txt-subst-file=subst-strings.txt -net=192.168.88.0/24 file1.cfg file2.cfg file3.cfg\n\n");
}

sub anonymize {
	my $infile = shift;
	my $net = shift;
	my $outfile = shift;
	my $remove_keys = shift;
	
	my $ip = NetAddr::IP->new("$net");
	my $ln_count = 0;
	my $multiline_remove = 0;
	my $multiline_close_pattern = '^\-+END(.*?)\-+[\"\']';
	my $multiline_counter = 0;
	my $multiline_limit = 70;	# certificates

	open( my $ifh, $infile ) or croak "Unable to open $infile: $!\n";
	open( my $ofh, ">$outfile" ) or croak "Unable to open $outfile: $!\n" ;

	LINE: while (my $line = <$ifh>) {
		$ln_count++;
		if ($remove_keys && $multiline_remove) {
			$multiline_counter++;
#			print ("line: $line");
			if ($line =~ $multiline_close_pattern || $multiline_counter > $multiline_limit) {
				if ($line !~ $multiline_close_pattern) { print ("WARNING: did not find closing line for multiline text block in line $ln_count\n"); }
#				print ("stopping multiline removal in line no $ln_count: $line"); 
				$multiline_remove = 0; $multiline_counter = 0; 
			}
			next LINE;
		} 
		find_ipaddrs($line, sub {
			my($ipaddr, $orig) = @_;
			if ($orig =~ /^2[45][0258]\./) { # found netmask (assuming IPs starting with 24x.* and 25x.* are netmasks)
				return $anonymized_ip{$orig} if exists $anonymized_ip{$orig};
				$anonymized_ip{$orig} = "255.255.255.255"; # changing all netmask to /32 to avoid invalid cidrs
				return $anonymized_ip{$orig};
			} elsif ($orig eq '0.0.0.0') { 	# leave /0 netmask alone
				return $ipaddr->addr;
			} else {  
				my $netmask = '';
				if ($orig =~ /(.+?)\/32$/) {
					$orig = $1;
					$netmask = '/32';
				}
				return $anonymized_ip{$orig} . $netmask if exists $anonymized_ip{$orig};
				# if found ip has not yet an anonymous equivalent in hash - create new ip
				++$ip;
				$anonymized_ip{$orig} = $ip->addr;
				return $anonymized_ip{$orig} . $netmask;
			}
		});
		if (defined($txt_subst_file) && $txt_subst_file ne '') { # obfuscating text
			my $regex_all_texts = join("|", map {quotemeta} keys %anonymized_text);
			$line =~ s/($regex_all_texts)/$anonymized_text{$1}/go;
		}
		if ($remove_keys) {
			$line = &remove_keys($line);
			my $orig_line = $line;
			$line = &remove_keys_multiline($line);
			if ($orig_line ne $line) { $multiline_remove = 1; }
		}
		print $ofh $line;
	}
	close ($ifh); close ($ofh); return;	
}

###########################
# main start
###########################

my $start_time = time();
my $query = CGI->new;
my $total_filesize = 0;
my $remove_keys = 0;

if ((defined($ARGV[0]) && $ARGV[0] eq "-help") || scalar($query->param)==0) { &show_help(); exit 0; }
if (defined(param("-txt-subst-file"))) { $txt_subst_file = param("-txt-subst-file"); &create_string_subst_hash($txt_subst_file); } 
	else { $txt_subst_file = ''; print ("no -txt-subst-file specified, not doing any string anonymizing\n"); }
if (defined(param("-net"))) { $net = param("-net"); } else { print ("no -net parameter specified, using default net $net\n"); }
if (defined(param("-remove-keys"))) { $remove_keys = 1; print ("Removing keys/passwords from files.\n"); }

# treating all params not starting with - as files to anonymize
# do not re-anonymize files with .anonymized extension and do not anonymize binary files
foreach my $file (@ARGV) { 
	if ($file !~ /^-/ && $file !~ /.*?$ano_suffix$/ && -T $file) {
		$total_filesize += -s $file;
		print ("anonymizing: $file ... "); 
		&anonymize($file, $net, $file . $ano_suffix, $remove_keys);
		print ("result file = $file$ano_suffix\n"); 
	} else { if ($file !~ /^-/) { print ("ignoring file $file\n"); } }
}

# Generating statistics
my @ki=keys(%anonymized_ip);
my @kt=keys(%anonymized_text);
my $duration = time() - $start_time;
print("Anonymized " . ($#ki+1) . " ip addresses and " . ($#kt+1) . " strings in " . sprintf("%.1f",$duration) . " seconds");
printf(" (total %.2f MB, %.2f Mbytes/second).\n", $total_filesize/1000000, $total_filesize/$duration/1000000);
my $anonet = NetAddr::IP->new($net);
if ($anonet->num()<($#ki+1)) { 
	print("WARNING: generated "  . ($#ki+1) . " anonymized ip addresses (more than available in " . $anonet .
		" which can only hold " . $anonet->num() . " IP addresses).\n");
	print ("   Suggest to use bigger subnet if you need uniqueness of IP addresses.\n"); 
}

=head1 NAME

iso-anonymizer.pl - replace IP addresses with anonymized IPs as well as text with anonymized text in plain text files

=head1 SYNOPSIS
  ./iso-anonymizer.pl [-txt-subst-file=/var/tmp/strings.txt] [-net="192.168.0.0/16"] <config-file1 config-file2 ...> 

=head1 DESCRIPTION

This is a script for 
a) replacing IP addresses in plain text with anonymized equivalents from 
the network range supplied.

b) replacing strings in a file with anonymized strings

Input is a number of ASCII files (all parameters not starting with -)
IP addresses as well as strings are replaced  one-for-one throughout 
all text files, so once an IP address has an anonymized equivalent, 
it stays that way. 

This is useful if you need to use production configuration data for testing.
E.g. from firewalls but do not want to expose the production data on a
test system. This way you can protect an organization's 
identity at the same time.

Caveats: 
- currently only implemented for IPv4
- beware of anonymizing common strings; e.g. "INT" when handling database dumps is part of keyword CONSTRAINT
  use slightly longer strings like "INT_" instead

Params:
- The network range used for replacement, is set to "10.0.0.0/8" if omitted.
- For each file <infile> supplied an anonymized file called 
  <infile>.anonymized is created.

The second argument is a network address, which should be given in
CIDR notation, and really represents a range of IP addresses from
which we can draw from while doing the IP address substitutions (Note
that the use of NetAddr::IP means that we will never overflow this
range - but it will wrap around if we increment it enough). Using an
RFC1918 private address range is a good idea.

Note that the script tries to handle network addresses so that 
network address and netmask (both given in 255.255.255.x notation
as well as a.b.c.d/xy notation) will match by simply setting 
all netmasks to /32. 

=head1 EXAMPLES
./iso-anonymizer.pl -net=172.20.0.0/21 -txt-subst-file=/var/tmp/strings.txt /var/tmp/firewall17.cfg /var/tmp/router9.cfg

 tim@lacantha:$ sudo perl iso-anonymizer.pl -txt-subst-file=strings.txt /var/tmp/netscreen1.cfg
 no net specified, using default net 10.0.0.0/8
 anonymizing: /var/tmp/netscreen1.cfg ... result file = /var/tmp/netscreen1.cfg.anonymized
 Anonymized 20197 ip addresses and 150 strings in 31.1 seconds (0.46 Mbytes/second).
 tim@lacantha:~$ 
 
Anonymizing a whole (ASCII) Postgresql database:
  # creating an ASCII dump of the database:
  pg_dump -U dbadmin -d fworchdb -W >/var/tmp/fworch_db.dump.sql
  # or as postgres user:  pg_dump -d fworchdb >/var/tmp/fworch_db.dump.sql
  # turn binary .Fc dump into ascii (only necessary if you do not already have an ascii dump): pg_restore /var/tmp/fworch_db.dump.Fc >/var/tmp/fworch_db.dump.sql
  # anonymizing:
  iso-anonymizer.pl -txt-subst-file=/var/tmp/strings.txt /var/tmp/fworch_db.dump.sql
  # restoring anonymized database:
  psql --set ON_ERROR_STOP=on targetdb </var/tmp/fworch_db.dump.sql

=head1 TODO
- reliably replace network address by networks with consistent netmasks
  (currently all networks are reduced to a /32 netmask)

=head1 AUTHOR
Tim Purschke E<lt>tmp@cactus.deE<gt>

=head1 COPYRIGHT AND LICENSE
Copyright (C) 2016 by Cactus eSecurity GmbH

=head1 SEE ALSO
Behind the door

=cut