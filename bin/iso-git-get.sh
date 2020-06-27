#!/bin/bash
iso_target=$HOME/dev/iso-run
cd
rm -rf $iso_target
mkdir -p $iso_target $HOME/bin
cd $iso_target
git clone isodev:/home/git/itsecorg
# set link to scripts
rm $HOME/bin/iso-git-get.sh
ln -s $iso_target/itsecorg/bin/iso-git-get.sh $HOME/bin/iso-git-get.sh
#sudo cp $iso_target/itsecorg/ansible/inventory/hosts /etc/ansible/hosts
