#! /usr/bin/perl -w
use strict;
use lib '.';
use CACTUS::FWORCH;
use CACTUS::read_config;
use CGI qw(:standard);          

my ($res, $mgm_name, $mgm_id, $fehler);

# get management systems from the database
my $dbh1 = DBI->connect("dbi:Pg:dbname=$fworch_database;host=$fworch_srv_host;port=$fworch_srv_port","$fworch_srv_user","$fworch_srv_pw");
if ( !defined $dbh1 ) { die "Cannot connect to database!\n"; }
my $sth1 = $dbh1->prepare("SELECT mgm_id, mgm_name from management LEFT JOIN stm_dev_typ USING (dev_typ_id)" .
		" ORDER BY mgm_name" );
if ( !defined $sth1 ) { die "Cannot prepare statement: $DBI::errstr\n"; }
$res = $sth1->execute;
my $management_hash = $sth1->fetchall_hashref('mgm_name');
$sth1->finish;
$dbh1->disconnect;

my $isobase	= &CACTUS::read_config::read_config('TopDir');
my $importdir	= &CACTUS::read_config::read_config('ImportDir');

# Schleife ueber alle Managementsysteme: Loeschen und wieder einlesen aller Configs
foreach $mgm_name (sort keys %{$management_hash}) {
	$mgm_id = $management_hash->{"$mgm_name"}->{"mgm_id"};
	$fehler = system("$importdir/iso-importer-single.pl mgm_id=$mgm_id -no-md5-checks -clear-management");
	$fehler = system("$importdir/iso-importer-single.pl mgm_id=$mgm_id -no-md5-checks");
}
exit(0);
