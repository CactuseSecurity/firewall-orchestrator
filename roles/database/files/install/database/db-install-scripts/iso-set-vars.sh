#!/bin/sh
# $Id: iso-set-vars.sh,v 1.1.2.3 2007-12-15 19:44:20 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/Attic/iso-set-vars.sh,v $

PATH=$PATH:/bin:/usr/bin

OUT='logger -t fworch:db-init.sh -p local6.notice'
ERROR_OUT='logger -t fworch:db-init.sh -p local6.error'
if [ -z "$1" ]
then
	if [ -z "$FWORCHBASE" ]
	then
	        FWORCHBASE="/usr/local/fworch"
			# echo "FWORCHBASE was not set. Using default directory $FWORCHBASE."
	fi
else
        FWORCHBASE=$1
fi
echo "using FWORCHBASE $FWORCHBASE"

FWORCHDBPW="§(FUIOD§($/)EU_.s MjsfEIUFIDFUDFI"
DBADMINPW="§(FUIOD§($/) 9239dsf2 EUEIUFIDFUDFI"
ADMINPW="§(FUIOD§($/)EUEIUFIdioasd9  dsaf21=DFUDFI"
DBDIR=$FWORCHBASE/install/database
PATH=$PATH:$FWORCHBINDIR:$FWORCHBASE/importer
FWORCHBINDIR=$DBDIR/db-install-scripts
SQLDIR=$DBDIR/stored-procedures
DATADIR=$DBDIR/csv-data
SAMPLEDIR=$FWORCHBASE/sample-data
FWORCHDB=fworchdb
FWORCHDBUSER=fworch
FWORCHDBPORT=5432
FWORCHDBHOST=127.0.0.1
#FWORCHDBHOST=localhost # could end up in ipv6 connection
DBADMIN=dbadmin
DBBACKUPUSER=dbbackup   # no auth, direct access (trust)
#POSTGRESBIN=/usr/share/pgsql/bin  # manual compilation
POSTGRESBIN=/usr/bin # package installation
PSQLBIN=$POSTGRESBIN/psql
DBCREATEBIN=$POSTGRESBIN/createdb
PSQLCMD_INIT="$PSQLBIN -h $FWORCHDBHOST -U $DBADMIN -d template1"
PSQLCMD_INIT_USER="$PSQLBIN -h $FWORCHDBHOST -U $DBADMIN -d $FWORCHDB"
PSQLCMD_CREATE_REST="$PSQLBIN -h $FWORCHDBHOST -U $DBADMIN -d $FWORCHDB"
#DBCREATE_CMD="$DBCREATEBIN -h $FWORCHDBHOST -U $DBADMIN"
DBCREATE_CMD="$PSQLBIN -h $FWORCHDBHOST -U $DBADMIN -d template1"
PSQLCMD="$PSQLBIN -h $FWORCHDBHOST -U $FWORCHDBUSER -d $FWORCHDB"
