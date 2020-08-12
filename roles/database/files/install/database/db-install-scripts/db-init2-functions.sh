#!/bin/sh
# $Id: db-init2-functions-with-output.sh,v 1.1.2.1 2011-05-11 08:01:42 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/Attic/db-init2-functions-with-output.sh,v $
if [ ! $FWORCHBASE ]; then
        FWORCHBASE="/usr/local/fworch"
        echo "FWORCHBASE was not set, using default directory $FWORCHBASE."
fi
FWORCHBINDIR=$FWORCHBASE/install/database/db-install-scripts
PATH=$PATH:$FWORCHBINDIR:$FWORCHBASE/importer

echo "Make sure you have the correct values for database name, dbadmin password, ... set in iso-set-vars.sh"

. $FWORCHBINDIR/iso-set-vars.sh $FWORCHBASE
. $FWORCHBINDIR/iso-pgpass-create.sh $FWORCHBASE

# this script redefines all funtions (stored procedures) and views within the db
# can be aplied repeatedly

#echo "from here in fworch-user context"
echo "adding basic functions"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-basic-procs.sql"
echo "adding iso-import functions"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-import.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-import-main.sql"
echo "adding functions iso-obj-import and -refs"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-obj-import.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-obj-refs.sql"
echo "adding functions iso-svc-import, -refs"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-svc-import.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-svc-refs.sql"
echo "adding functions iso-usr-import, -refs"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-usr-import.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-usr-refs.sql"
echo "adding functions iso-rule-import and -refs"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-rule-import.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-rule-refs.sql"
echo "adding functions in iso-zone-import"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-zone-import.sql"
echo "adding functions for reporting: iso-report*.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-report.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-qa.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-report-basics.sql"
#echo "dropping views for documenting changes (as dbadmin): iso-views-drop.sql"
#$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-views-drop.sql"
echo "adding views for documenting changes (as fworch): iso-views.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-views.sql"
echo "settings grants: iso-grants.sql"
$PSQLCMD_CREATE_REST -c "\i $SQLDIR/iso-grants.sql"

# delete .pgpass
. $FWORCHBINDIR/iso-pgpass-remove.sh

# create commands to change owner of all db objects to fworch:
# pg_dump -U dbadmin -h localhost -s fworchdb | grep -i 'owner to' | grep -v 'PROCEDURAL LANGUAGE' | sed -e 's/OWNER TO .*;/OWNER TO fworch;/i'

# create commands to drop all functions:
# pg_dump -U dbadmin -h localhost -s fworchdb | grep -i 'create function' | sed -e 's/CREATE/DROP/' | sed -e 's/RETURNS.*/CASCADE;/'
# create commands to drop all views (does not work without manual modifications because of dependencies):
# pg_dump -U dbadmin -h localhost -s fworchdb | grep -i 'create view' | sed -e 's/CREATE/DROP/' | sed -e 's/AS/CASCADE;/'