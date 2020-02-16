#!/bin/sh
# $Id: write_import_status_file.sh,v 1.1.2.3 2011-05-19 11:17:41 tim Exp $
# $Source: /home/cvs/iso/package/install/bi​n/Attic/write_import_status_file.​sh,v $
# itsecorg import status writing script
# to be run via cron every minute

PHP=/usr/bin/php
ISOHOME=/usr/share/itsecorg
IMPORT_STATUS_DIR=/var/itsecorg
IMPORT_STATUS_FILE=import_status.txt
MKDIR=/bin/mkdir

$MKDIR -p $IMPORT_STATUS_DIR
$PHP $ISOHOME/web/htdocs/config/import_status_iframe.php --outputmode=text | sed 's/<br>/\n/g' > $IMPORT_STATUS_DIR/$IMPORT_STATUS_FILE