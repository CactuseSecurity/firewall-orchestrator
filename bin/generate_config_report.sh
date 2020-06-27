#!/bin/sh
# $Id: write_config_report.sh,v 1.1.2.9 2012-03-24 13:57:21 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/Attic/write_config_report.sh,v $
# itsecorg cli script to export a config to html files 
# to be run via cron
# calls php script reportin_tables_config_cli.php

PHP=/usr/bin/php
MKDIR=/bin/mkdir 
ISOHOME=/usr/share/itsecorg
REPORT_SCRIPT=web/htdocs/reporting_tables_config_cli.php
STAMM=$ISOHOME/web/htdocs/
REPORT_OUT_DIR=/var/itsecorg/reports
REPORT_FORMAT=html
DATE=$(/bin/date +%F-%H%M%S)

show_syntax () {
	echo "usage: $0 [-v] -d <device-id|device-name> [-f <format of report>] [-c <client-id>] [-t <time of report>] [-o <output dir of report>]" 
	echo "-v verbose output"
	echo "-d either specify device-id or device-name"
	echo "-f format of report defaults to $REPORT_FORMAT"
	echo "		possible values are: html, simple.html, junos, csv, ARS.csv"
	echo "-t time of report defaults to now() = $DATE"
	echo "-c client-id defaults to none = no filtering"
	echo "-o output dir defaults to $REPORT_OUT_DIR"
}

# handle command line parameters
vflag=off
while getopts vd:f:c:t:o: opt
do
    case "$opt" in
      v)  vflag=on;;
      f)  REPORT_FORMAT="$OPTARG";;
      d)  DEVICE_ID="$OPTARG";;
      c)  CLIENT_ID="$OPTARG";;
      t)  DATE="$OPTARG";;
      o)  REPORT_OUT_DIR="$OPTARG";;
      \?) show_syntax; exit 1;;	# unknown flag
		
    esac
done
shift $(expr $OPTIND - 1)

REPORT_NAME=$DATE-itsecorg-configreport-dev-$DEVICE_ID.$REPORT_FORMAT

if [ "$DEVICE_ID" = "" ]; then
	echo "ERROR: no device id specified"
	show_syntax
	exit 1
fi

if [ "$vflag" = "on" ]; then
	echo "generating report for device $DEVICE_ID, report date = $DATE, outfile = $REPORT_OUT_DIR/$REPORT_NAME"
fi

$MKDIR -p "$REPORT_OUT_DIR"
$PHP $ISOHOME/$REPORT_SCRIPT \
	--dev_id="$DEVICE_ID" \
	--reportdate="$DATE" \
	--client="$CLIENT_ID" \
	--stamm=$STAMM \
	--reportformat="$REPORT_FORMAT" \
	--mgm_filter='TRUE' \
	>"$REPORT_OUT_DIR"/"$REPORT_NAME"
