#!/bin/sh
# $Id: iso-set-vars.sh,v 1.1.2.3 2007-12-15 19:44:20 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/Attic/iso-set-vars.sh,v $

PATH=$PATH:/bin:/usr/bin

OUT='logger -t ITSecOrg:db-init.sh -p local6.notice'
ERROR_OUT='logger -t ITSecOrg:db-init.sh -p local6.error'
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

ISODBPW="§(FUIOD§($/)EU_.s MjsfEIUFIDFUDFI"
DBADMINPW="§(FUIOD§($/) 9239dsf2 EUEIUFIDFUDFI"
ADMINPW="§(FUIOD§($/)EUEIUFIdioasd9  dsaf21=DFUDFI"
DBDIR=$ISOBASE/install/database
PATH=$PATH:$ISOBINDIR:$ISOBASE/importer
ISOBINDIR=$DBDIR/db-install-scripts
SQLDIR=$DBDIR/stored-procedures
DATADIR=$DBDIR/csv-data
SAMPLEDIR=$ISOBASE/sample-data
ISODB=isodb
ISODBUSER=itsecorg
ISODBPORT=5432
ISODBHOST=127.0.0.1
#ISODBHOST=localhost # could end up in ipv6 connection
DBADMIN=dbadmin
DBBACKUPUSER=dbbackup   # no auth, direct access (trust)
#POSTGRESBIN=/usr/share/pgsql/bin  # manual compilation
POSTGRESBIN=/usr/bin # package installation
CREATELANG_BIN=$POSTGRESBIN/createlang
PSQLBIN=$POSTGRESBIN/psql
DBCREATEBIN=$POSTGRESBIN/createdb
PSQLCMD_INIT="$PSQLBIN -h $ISODBHOST -U $DBADMIN -d template1"
PSQLCMD_INIT_USER="$PSQLBIN -h $ISODBHOST -U $DBADMIN -d $ISODB"
PSQLCMD_CREATE_REST="$PSQLBIN -h $ISODBHOST -U $DBADMIN -d $ISODB"
#DBCREATE_CMD="$DBCREATEBIN -h $ISODBHOST -U $DBADMIN"
DBCREATE_CMD="$PSQLBIN -h $ISODBHOST -U $DBADMIN -d template1"
CREATE_LANG_CMD="$CREATELANG_BIN -h $ISODBHOST -U $DBADMIN -d $ISODB plpgsql"
PSQLCMD="$PSQLBIN -h $ISODBHOST -U $ISODBUSER -d $ISODB"

PSQLCMD_CREATE_DBADMIN="createuser -s -e dbadmin -w"