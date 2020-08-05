# $Id: read_config.pm,v 1.1.2.1 2007-12-13 08:48:49 tim Exp $
# $Source: /home/cvs/iso/package/importer/CACTUS/Attic/read_config.pm,v $

package CACTUS::read_config;

use strict;
use warnings;
use IO::File;
require Exporter;
our @ISA = qw(Exporter);

our %EXPORT_TAGS = (
	'basic' => [
		qw( &read_config )
	]
);

our @EXPORT  = ( @{ $EXPORT_TAGS{'basic'} } );
our $VERSION = '1.2';

############################################################
# read one file into string
############################################################
sub read_file_into_string {
	my $conf_file = shift;
	my ($line, $lines);

	my $INFILE = new IO::File ("< $conf_file") or die "cannot open file $conf_file\n";
	$lines = '';

	while ($line = <$INFILE>) { $lines .= $line; }
	$INFILE->close;
	return $lines;
}
############################################################
# read parameter from config file
############################################################
sub read_config {
	my $param = shift;
	my $confdir =  '/usr/local/fworch/etc';
	my $result;

	my $global_conf_lines	= &read_file_into_string ("$confdir/iso.conf");
	my $import_conf_lines	= &read_file_into_string ("$confdir/import.conf");
	my $all_conf_lines	= $global_conf_lines . '\n' . $import_conf_lines;

	my @config_lines = split (/\n/, $all_conf_lines);

	foreach my $line (@config_lines) {
		if ($line =~ /(.*?)\#/) { $line = $1; }	# remove comments
		if ($line !~ /^$/ && $line =~ /^\s*$param\s+(.*?)\s*$/) {
			$result = $1;
#			print ("found matching config line for $param. Param: .$1.\n");
		}
	}
	if (!defined($result)) { print ("warning: config parameter $param neither found in global nor in import config file\n"); }
	return $result;
}

1;
__END__

=head1 NAME

read_config - Perl extension for IT Security Organizer

=head1 SYNOPSIS

  use CACTUS::read_config;

=head1 DESCRIPTION

IT Security Organizer Perl Module
support for reading config files

=head2 EXPORT

  Basic functions
    read_config(parameter to read from file)

=head1 SEE ALSO

  behind the door


=head1 AUTHOR

  Tim Purschke, tmp@cactus.de

=head1 COPYRIGHT AND LICENSE

  Copyright (C) 2005-2007 by Cactus eSecurity GmbH, Frankfurt, Germany

=cut
