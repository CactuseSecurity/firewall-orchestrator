#! /usr/bin/perl -w
use strict;
use lib '.';
use CACTUS::FWORCH;
use CACTUS::read_config;
use CGI qw(:standard);          
use Sys::Hostname;

my $isobase	= &CACTUS::read_config::read_config('TopDir');
my $importdir	= &CACTUS::read_config::read_config('ImportDir');
my $sleep_time	= &CACTUS::read_config::read_config('ImportSleepTime');
my $hostname_localhost = hostname();
my $importer_hostname = $hostname_localhost;	
my ($res, $mgm_name, $mgm_id, $fehler);

if ($#ARGV>=0) { if (defined($ARGV[0]) && is_numeric($ARGV[0])) { $sleep_time = $ARGV[0] * 1; } }

while (1) {
	output_txt("Import: another loop is starting... ");
	# get management systems from the database
	my $dbh1 = DBI->connect("dbi:Pg:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port","$fworch_srv_user","$fworch_srv_pw");
	if ( !defined $dbh1 ) { die "Cannot connect to database!\n"; }
	my $sth1 = $dbh1->prepare("SELECT mgm_id, mgm_name, do_not_import, importer_hostname from management LEFT JOIN stm_dev_typ USING (dev_typ_id)" .
			" WHERE NOT do_not_import AND NOT management.dev_typ_id in (12,13) ORDER BY mgm_name" );
			# do not import types 13 (checkpoint R8x) and 12 (fortimanager) as these are handled by new python/api importer
	if ( !defined $sth1 ) { die "Cannot prepare statement: $DBI::errstr\n"; }
	$res = $sth1->execute;
	my $management_hash = $sth1->fetchall_hashref('mgm_name');
	$sth1->finish;
	$dbh1->disconnect;
	# loop across all management systems
	foreach $mgm_name (sort keys %{$management_hash}) {
		$fehler = 0;
		output_txt("Import: looking at $mgm_name ... ");
		$mgm_id = $management_hash->{"$mgm_name"}->{"mgm_id"};
		if (defined($management_hash->{"$mgm_name"}->{"importer_hostname"})) {
			$importer_hostname = $management_hash->{"$mgm_name"}->{"importer_hostname"};	
		}
		if ($importer_hostname eq $hostname_localhost) {
			output_txt("Import: running on responsible importer $importer_hostname ... ");
			$fehler = system("$importdir/fworch-importer-single.pl mgm_id=$mgm_id");
			if ($fehler) {
				output_txt("Import error: $fehler");
			}
		}
	}
	output_txt("-------- Import module: going back to sleep for $sleep_time seconds --------\n");
	sleep $sleep_time;
}
