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

$PSQLCMD_INIT -c "DROP DATABASE $FWORCHDB" 2>&1 | tee | $OUT
echo "creating db $FWORCHDB" 2>&1 | tee | $OUT
$DBCREATE_CMD -c "CREATE DATABASE $FWORCHDB" | $OUT
echo "creating fworch-db-model" | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/fworch-db-model.sql" 2>&1 | $OUT

echo "settings privileges" | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-user-textreader.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-grants.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-change-to-delete-cascade.sql" 2>&1 | $OUT

# delete .pgpass
. $FWORCHBINDIR/iso-pgpass-remove.sh
