#!/bin/sh
if [ ! $1 ]; then
	FWORCHBASE="/usr/local/fworch"
else
	FWORCHBASE=$1
fi
FWORCHBINDIR=$FWORCHBASE/install/database/db-install-scripts
PGPASS=$HOME/.pgpass
. $FWORCHBINDIR/iso-set-vars.sh
# creating .pgpass for current user to login to database
echo "$FWORCHDBHOST:$FWORCHDBPORT:$FWORCHDB:$FWORCHDBUSER:$FWORCHDBPW" >$PGPASS
echo "$FWORCHDBHOST:$FWORCHDBPORT:$FWORCHDB:$DBADMIN:$DBADMINPW" >>$PGPASS
echo "$FWORCHDBHOST:$FWORCHDBPORT:template1:$DBADMIN:$DBADMINPW" >>$PGPASS
/bin/chmod 600 $PGPASS
