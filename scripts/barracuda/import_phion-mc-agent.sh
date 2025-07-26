#!/bin/sh
{
  /usr/bin/id
  /bin/date
} >> /var/tmp/itsecorg.log 2>&1;
/bin/cp -u -r /opt/phion/rangetree/configroot/* /var/phion/home/itsecorg/ >> /var/tmp/itsecorg.log 2>&1;
/bin/chown -R itsecorg:users /var/phion/home/itsecorg >> /var/tmp/itsecorg.log 2>&1;
