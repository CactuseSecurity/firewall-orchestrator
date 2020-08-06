#! /bin/sh
# $Id: db-dump-isov1.sh,v 1.1.2.1 2007-12-14 18:50:57 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/backup-scripts/Attic/db-dump-isov1.sh,v $
# /etc/init.d/import_itsecorg-main-rc stop  -- no need to stop the importer
DATUM=$(/bin/date +"%F")
ZEIT=$(/bin/date +"%c")
PG_BIN=/usr/bin
PG_DATA=/etc/postgresql/9.6/main
ISO_HOME=/usr/share/itsecorg
ISO_DB=isodb
DUMPDIR=$ISO_HOME/var/db/db-dumps
BACKUP_USER=tim  # unix user
PG_BACKUP_USER=dbbackup
DB_HOST=127.0.0.1 # do not use localhost as this defaults to ipv6
TEMPLOG=$ISO_HOME/var/itsecorg-backup.log
/usr/bin/logger -t fworch -p local6.notice "$0 info: Start $ZEIT"
/bin/echo "running $0 ${DATUM}"
/bin/mkdir -p $DUMPDIR
echo "executing: $PG_BIN/pg_dump -U $PG_BACKUP_USER -Fc -h $DB_HOST $ISO_DB 2>$TEMPLOG | /bin/gzip -c >$DUMPDIR/${DATUM}_itsecorg_db_dump_$ISO_DB.Fc.gz"
$PG_BIN/pg_dump -U $PG_BACKUP_USER -Fc -h $DB_HOST $ISO_DB 2>$TEMPLOG | /bin/gzip -c >$DUMPDIR/"${DATUM}"_itsecorg_db_dump_$ISO_DB.Fc.gz
while read -r log;
do
	/usr/bin/logger -t fworch -p local6.notice "$log"
done < $TEMPLOG
/bin/rm $TEMPLOG
sudo -u postgres pg_dumpall -g >$DUMPDIR/"${DATUM}"_dump_all_users.sql
/bin/tar cvfz $DUMPDIR/"${DATUM}"_iso_etc_dir.tgz $ISO_HOME/etc $PG_DATA/pg_hba.conf $PG_DATA/postgresql.conf
# /etc/init.d/itsecorg-import start
ZEIT=$(/bin/date +"%c")
/usr/bin/logger -t fworch -p local6.notice "db-backup info: Stop $ZEIT"

# customer specific user settings
/bin/chown $BACKUP_USER $DUMPDIR/"${DATUM}"_itsecorg_db_dump_$ISO_DB.Fc.gz
/bin/chown $BACKUP_USER $DUMPDIR/"${DATUM}"_iso_etc_dir.tgz
/bin/chown $BACKUP_USER $DUMPDIR/"${DATUM}"_dump_all_users.sql
