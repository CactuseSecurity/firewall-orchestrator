#!/bin/sh
if [ -z "$1" ]
then
	if [ -z $FWORCHBASE ]
	then
	        FWORCHBASE="/usr/local/fworch"
			# echo "FWORCHBASE was not set. Using default directory $FWORCHBASE."
	fi
else
        FWORCHBASE=$1
fi
echo "using FWORCHBASE $FWORCHBASE"
FWORCHBINDIR=$FWORCHBASE/install/database/db-install-scripts
PATH=$PATH:$FWORCHBINDIR:$FWORCHBASE/importer

. $FWORCHBINDIR/iso-set-vars.sh $FWORCHBASE
. $FWORCHBINDIR/iso-pgpass-create.sh $FWORCHBASE
## Was ist PGPASS? Ein file mit Pathvariablen?

$PSQLCMD_INIT -c "DROP DATABASE $FWORCHDB" 2>&1 | tee | $OUT
## "/usr/bin/psql -h 127.0.0.1 -U dbadmin -d template1
# " -c "DROP DATABASE $FWORCHDB" 2>&1 | tee | $OUT
## Dies ist psql Befehl: -h "hostname" -U "username" -d "dbname" -c "command"
## Was ist template1?
## tee braucht datei, sonst passiert nichts
## der befehl leitet den stderror zum stdout und der wird input für $OUT
## OUT='logger -t fworch:db-init.sh -p local6.notice'
## FWORCHDB={{fworch_db_name}}
echo "creating db $FWORCHDB" 2>&1 | tee | $OUT
$DBCREATE_CMD -c "CREATE DATABASE $FWORCHDB" | $OUT
## /usr/bin/psql -h 127.0.0.1 -U dbadmin -d template1 -c "CREATE DATABASE $FWORCHDB"
echo "creating fworch-db-model" | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/fworch-db-model.sql" 2>&1 | $OUT
## /usr/bin/psql -h 127.0.0.1 -U dbadmin -d dbadmin -c "\i $SQLDIR/fworch-db-model.sql"
## \i executes a psql comman from the file following
## the file $SQLDIR/fworch-db-model.sql was moved to files/sql

echo "settings privileges" | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-user-textreader.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-grants.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-change-to-delete-cascade.sql" 2>&1 | $OUT

# delete .pgpass
. $FWORCHBINDIR/iso-pgpass-remove.sh
