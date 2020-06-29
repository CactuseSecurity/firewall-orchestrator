#!/bin/bash
ISOHOME=/tmp/itsecorg-rollout
WS=/Users/tim/git
ISO_USR=60320

sudo rm -rf $ISOHOME
sudo mkdir -p $ISOHOME
sudo cp -pr $WS/itsecorg $ISOHOME/
sudo chown -R $ISO_USR:$ISO_USR $ISOHOME
sudo find $ISOHOME -type d -name .git -exec rm -rf '{}' \;  2>/dev/null
sudo find $ISOHOME -type f -name .project -exec rm -rf '{}' \;  2>/dev/null
sudo find $ISOHOME -type f -name .DS_Store -exec rm -rf '{}' \;  2>/dev/null
sudo find $ISOHOME -type f -name .includepath -exec rm -rf '{}' \;  2>/dev/null
cd $ISOHOME || exit;  sudo tar cvfz iso.tgz itsecorg
