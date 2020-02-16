#!/bin/sh
# $Id: 20070831-iso-view-deletes-corrections-patch.sh,v 1.1.2.2 2007-12-13 10:47:32 tim Exp $
# $Source: /home/cvs/iso/package/install/migration/20070831-view-corrections-for-deletes/Attic/20070831-iso-view-deletes-corrections-patch.sh,v $

ISOHOME=/usr/local/itsecorg
DBHOST=localhost
DBNAME=isov1
DBUSER=dbadmin
DBADMINPW=
PSQL_EXEC=psql
DBPORT=5432
DBEXEC="$PSQL_EXEC -h $DBHOST -d $DBNAME -U $DBUSER"
PGPASS=$HOME/.pgpass

echo "$DBHOST:$DBPORT:$DBNAME:$DBUSER:$DBADMINPW" >$PGPASS
/bin/chmod 600 $PGPASS

$DBEXEC -c "\i $ISOHOME/install/migration/20070831-view-corrections-for-deletes/20070831-iso-view-deletes-correction-part-1.sql"
$DBEXEC -c "\i $ISOHOME/install/database/iso-report-basics.sql"
$DBEXEC -c "\i $ISOHOME/install/database/iso-views.sql"
$DBEXEC -c "\i $ISOHOME/install/database/iso-rule-import.sql"
$DBEXEC -c "\i $ISOHOME/install/database/iso-obj-import.sql"
$DBEXEC -c "\i $ISOHOME/install/database/iso-svc-import.sql"
$DBEXEC -c "\i $ISOHOME/install/database/iso-usr-import.sql"
$DBEXEC -c "\i $ISOHOME/install/migration/20070831-view-corrections-for-deletes/20070831-iso-view-deletes-correction-part-2.sql"

rm $PGPASS