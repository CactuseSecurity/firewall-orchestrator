#!/bin/sh
if [ ! $1 ]; then
	ISOBASE="/usr/share/itsecorg"
else
	ISOBASE=$1
fi
ISOBINDIR=$ISOBASE/install/database/db-install-scripts
PGPASS=$HOME/.pgpass
. $ISOBINDIR/iso-set-vars.sh
# creating .pgpass for current user to login to database
echo "$ISODBHOST:$ISODBPORT:$ISODB:$ISODBUSER:$ISODBPW" >$PGPASS
echo "$ISODBHOST:$ISODBPORT:$ISODB:$DBADMIN:$DBADMINPW" >>$PGPASS
echo "$ISODBHOST:$ISODBPORT:template1:$DBADMIN:$DBADMINPW" >>$PGPASS
/bin/chmod 600 $PGPASS
