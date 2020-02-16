#!/bin/sh
# $Id: db-init1-structure.sh,v 1.1.2.3 2009-12-02 15:03:44 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/Attic/db-init1-structure.sh,v $
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

echo "make sure you have the correct values for database name, dbadmin password, ... set in iso-set-vars.sh"
echo "press <CTRL-C> to abort installation within 5 seconds"
/bin/sleep 5

. $ISOBINDIR/iso-set-vars.sh $ISOBASE
. $ISOBINDIR/iso-pgpass-create.sh $ISOBASE

# careful: this script actually destroys all existing data within the database
# echo "creating dbadmin user"
# $PSQLCMD_CREATE_DBADMIN
# psql --command "CREATE USER dbadmin WITH SUPERUSER PASSWORD 'st8chel';"

echo "dropping db $ISODB, may fail but fallback (drop tables) in place" | $OUT
$PSQLCMD_INIT -c "DROP DATABASE $ISODB" 2>&1 | tee | $OUT
/bin/sleep 3			# waiting for connection to template to finish
echo "creating dbusers"  2>&1 | tee | $OUT	# creating basic groups and users
$PSQLCMD_INIT -c "create user \"$ISODBUSER\" with password '$ISODBPW'" #   2>&1 | $OUT
$PSQLCMD_INIT -c "create user \"admin\" WITH PASSWORD '$ADMINPW' IN GROUP secuadmins, isoadmins;"
#$PSQLCMD_INIT -c "create user \"test\" WITH PASSWORD '$xxxPW' IN GROUP secuadmins;"


# the following lines are only needed, if no DB restore is done 
echo "creating db $ISODB" 2>&1 | tee | $OUT
#$DBCREATE_CMD --owner $ISODBUSER $ISODB | $OUT
$DBCREATE_CMD -c "CREATE DATABASE $ISODB" | $OUT
echo "adding language plpgsql" 2>&1 | tee | $OUT
$CREATE_LANG_CMD 2>&1 | $OUT
echo "creating itsecorg-db-model" | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/itsecorg-db-model.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "insert into isoadmin (isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username,isoadmin_password_must_be_changed) VALUES (11,'Firewall','Orchestration Admin','admin',false);" 2>&1 | $OUT
#$PSQLCMD_CREATE_REST -c "insert into isoadmin (isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username,isoadmin_password_must_be_changed) VALUES (12,'demo','test user','test',false);" 2>&1 | $OUT
echo "settings privileges" | $OUT

$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-user-textreader.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-grants.sql" 2>&1 | $OUT
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-change-to-delete-cascade.sql" 2>&1 | $OUT

# delete .pgpass
. $ISOBINDIR/iso-pgpass-remove.sh
