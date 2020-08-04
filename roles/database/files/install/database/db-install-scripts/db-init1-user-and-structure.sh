#!/bin/sh
if [ -z "$1" ]
then
	if [ -z $ISOBASE ]
	then
	        ISOBASE="/usr/share/itsecorg"
			# echo "ISOBASE was not set. Using default directory $ISOBASE."
	fi
else
        ISOBASE=$1
fi
echo "using ISOBASE $ISOBASE"
ISOBINDIR=$ISOBASE/install/database/db-install-scripts
PATH=$PATH:$ISOBINDIR:$ISOBASE/importer

. $ISOBINDIR/iso-set-vars.sh $ISOBASE
. $ISOBINDIR/iso-pgpass-create.sh $ISOBASE

$PSQLCMD_INIT -c "DROP DATABASE $ISODB" 2>&1 | tee | $OUT
echo "creating db $ISODB" 2>&1 | tee | $OUT
$DBCREATE_CMD -c "CREATE DATABASE $ISODB" | $OUT
echo "creating itsecorg-db-model" | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/itsecorg-db-model.sql" 2>&1 | $OUT

echo "settings privileges" | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-user-textreader.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-grants.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-change-to-delete-cascade.sql" 2>&1 | $OUT

# delete .pgpass
. $ISOBINDIR/iso-pgpass-remove.sh
