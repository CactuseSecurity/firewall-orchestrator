#!/bin/sh
# fworch import status writing script
# to be run via cron every minute

PHP=/usr/bin/php
FWORCHHOME=/usr/local/fworch
IMPORT_STATUS_DIR=/var/fworch
IMPORT_STATUS_FILE=import_status.txt
MKDIR=/bin/mkdir

$MKDIR -p $IMPORT_STATUS_DIR
$PHP $FWORCHHOME/web/htdocs/config/import_status_iframe.php --outputmode=text | sed 's/<br>/\n/g' > $IMPORT_STATUS_DIR/$IMPORT_STATUS_FILE