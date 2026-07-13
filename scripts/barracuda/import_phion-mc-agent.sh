#!/bin/sh
LOGFILE=/var/phion/home/itsecorg/itsecorg.log
{
  /usr/bin/id
  /bin/date
} >> "$LOGFILE" 2>&1;
/bin/cp -u -r /opt/phion/rangetree/configroot/* /var/phion/home/itsecorg/ >> "$LOGFILE" 2>&1;
/bin/chown -R itsecorg:users /var/phion/home/itsecorg >> "$LOGFILE" 2>&1;
