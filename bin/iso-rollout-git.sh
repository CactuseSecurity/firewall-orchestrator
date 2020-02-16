#!/bin/bash
WS=/Users/tim/git/itsecorg
ISO_USR=itsecorg
ISOHOST=itsecorg
ISOHOME=/usr/share/itsecorg

ssh -l $ISO_USR $ISOHOST rm -rf $ISOHOME/importer $ISOHOME/web $ISOHOME/install
scp -prq $WS/* $ISO_USR@$ISOHOST:$ISOHOME/
#ssh -l $ISO_USR $ISOHOST "find $ISOHOME -type d -name CVS -exec rm -rf '{}' \; 2>/dev/null"