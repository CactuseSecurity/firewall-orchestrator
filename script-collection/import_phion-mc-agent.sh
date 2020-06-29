#!/bin/sh
# $Id: phion-mc-agent.sh,v 1.1.2.1 2007-12-14 18:47:25 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/agents/Attic/phion-mc-agent.sh,v $
/usr/bin/id >> /var/tmp/itsecorg.log 2>&1;
/bin/date >> /var/tmp/itsecorg.log 2>&1;
/bin/cp -u -r /opt/phion/rangetree/configroot/* /var/phion/home/itsecorg/ >> /var/tmp/itsecorg.log 2>&1;
/bin/chown -R itsecorg:users /var/phion/home/itsecorg >> /var/tmp/itsecorg.log 2>&1;




