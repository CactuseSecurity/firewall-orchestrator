#!/bin/sh
# $Id: checkpoint-cp-config-locally.sh,v 1.1.2.9 2010-06-18 07:36:54 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/agents/Attic/checkpoint-cp-config-locally.sh,v $
# itsecorg import support script for copying check point config files in case of access problems
# to be run via cron every minute

# the following Check Point version dependent variables need to be set according to expert/root's "env" output
# here are the settings for R77:
VERSION=77
FWDIR=/opt/CPsuite-R$VERSION/fw1
CPDIR=/opt/CPshrd-R$VERSION
CPMDIR=$FWDIR
LD_LIBRARY_PATH=$CPDIR/lib:$FWDIR/lib:/opt/CPPIconnectra-R$VERSION/lib
export CPDIR CPMDIR FWDIR LD_LIBRARY_PATH

# from here version independent variables
user=itsecorg
DST=/home/itsecorg
OBJ=objects_5_0.C
RULE=rulebases_5_0.fws
AUDITLOG=auditlog.export
USR=fwauth.NDB
USREXP=cp-users.export
# UNUSED? USRGRPEXP=cp-user-groups.export
MV=/bin/mv
GREP=/bin/grep
CAT=/bin/cat
SLEEP=/bin/sleep
RM=/bin/rm
CHOWN=/bin/chown
KEYS=/home/itsecorg/.ssh/authorized_keys
TMPKEYS=/home/itsecorg/.ssh/authorized_keys.tmp
SRC=$FWDIR/conf
LOGDIR=$FWDIR/log

set_os_specific_cmds () {
        UNAME=/bin/uname
        OS_RESULT=$($UNAME)
        # Solaris:
        if [ "$OS_RESULT" = "SunOS" ]
        then
			group=other
			CP=/bin/cp
			PS="/bin/ps -efa"
			CMP=/bin/diff
			WC=/bin/wc
        fi
        # SPLAT:
        if [ "$OS_RESULT" = "Linux" ]
        then
			group=itsecorg
			CP="/bin/cp -u"
			PS="/bin/ps aux"
			CMP=/usr/bin/diff
			WC=/usr/bin/wc
        fi
}

cp_config_files_to_itsecorg_dir () {
        $CP $SRC/$OBJ $DST/$OBJ.cp
        $CP $SRC/$RULE $DST/$RULE.cp
#        $CP $LOGDIR/$AUDITLOG $DST/$AUDITLOG.cp
}

wait_for_scp_to_finish () {
        while [ $($PS | $GREP scp | $GREP itsecorg | $WC -l) != 0 ]
        do
                $SLEEP 1
        done
}

block_scp () {
        while [ $($PS | $GREP scp | $GREP itsecorg | $WC -l) != 0 ]
        do
                $SLEEP 1
        done
        mv $KEYS $TMPKEYS
}

accept_scp () {
        while [ $($PS | $GREP scp | $GREP itsecorg | $WC -l) != 0 ]
        do
                $SLEEP 1
        done
        mv $TMPKEYS $KEYS
}

move_if_changed () {
        wait_for_scp_to_finish
        if [ -f "$2" ]
        then
                CMP_RESULT=$($CMP "$1" "$2")
                if [ "$CMP_RESULT" = "" ]
                then
                        $RM "$1"
                else
                        wait_for_scp_to_finish
                        $MV "$1" "$2"
                fi
        else
                wait_for_scp_to_finish
                $MV "$1" "$2"
        fi
}

update_changed_config_files () {
	block_scp
	move_if_changed $DST/$OBJ.cp $DST/$OBJ
	move_if_changed $DST/$RULE.cp $DST/$RULE
#	move_if_changed $DST/$AUDITLOG.cp $DST/$AUDITLOG
	move_if_changed $DST/$USR.cp $DST/$USR
#	move_if_changed $DST/users.xml $DST/$USR
	accept_scp
}

set_file_access_rights () {
        $CHOWN $user:$group "$1"
}

set_all_file_access_rights () {
        set_file_access_rights $DST/$OBJ
        set_file_access_rights $DST/$RULE
#        set_file_access_rights $DST/$AUDITLOG
        set_file_access_rights $DST/$USR
}

start_audit_log_export () {
	# start export only if it is not already running
	if [ $($PS | grep "fw log" | grep -v grep | grep fw.adtlog | $WC -l) = 0 ]
	then 
		/bin/rm -f $LOGDIR/$AUDITLOG
		/bin/rm -f $DST/$AUDITLOG
		/bin/rm -f $DST/$AUDITLOG.cp	
		$FWDIR/bin/fw log -lf fw.adtlog >$LOGDIR/$AUDITLOG 2>/dev/null &
		/bin/sleep 100
	fi
}

export_users () {
	# keep old user file name for compat reasons
	$FWDIR/bin/fwm dbexport -f $DST/$USREXP.cp
	$FWDIR/bin/fwm dbexport -g -f $DST/$USR.cp
	$CAT $DST/$USREXP.cp >> $DST/$USR.cp
	$RM $DST/$USREXP.cp

#	Alternatively for externally defined Users (LDAP): export users to xml via Web Visualization Tool
#	see https://supportcenter.checkpoint.com/supportcenter/portal?eventSubmit_doGoviewsolutiondetails=&solutionid=sk30765
#	do not forget to change function update_changed_config_files above (users.xml) 
#	$DST/cpdb2web/cpdb2web -t users -s localhost -u ize0871 -o $DST
}

# main
set_os_specific_cmds
#start_audit_log_export
export_users
cp_config_files_to_itsecorg_dir
update_changed_config_files
set_all_file_access_rights
