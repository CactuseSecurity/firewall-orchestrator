#!/bin/bash

NAME_="dicovery-fgtmgr.sh"
SYNOPSIS_="$NAME_ [-d] [-H <Host-IP/name>] [-U <username>] [-K <SSH-Key>]"
REQUIRES_="standard GNU commands"
VERSION_="0.1"
DATE_="2021-06-24"
AUTHOR_="Holger Dost <hod@cactus.de>"
PURPOSE_="extracts information from the fortimanager"
EXIT_ERROR=3
EXIT_BUG=3
EXIT_SUCCESS=0


KEY='/usr/local/fworch/.ssh/id_rsa_forti'
USER='itsecorg'
SERVER='1.1.1.1'
REMCOM='diagnose dvm device list'
DEBUGMODE=0
GREP="/bin/fgrep"
AWK="/usr/bin/awk"
HEAD="/usr/bin/head"
 

usage () {
   echo >&2 "$NAME_ $VERSION_ - $PURPOSE_
Usage: $SYNOPSIS_
Requires: $REQUIRES_
Example: discovery-fgtmgr.sh -d -H 1.1.1.1 -U testuser -K .ssh/id_rsa_testkey
"
   exit 1
}
 
shopt -s extglob

while getopts 'dhH:C:D:' OPTION ; do
   case $OPTION in
      h) usage $EXIT_SUCCESS
         ;;
      d) DEBUGMODE=1
         ;;
      H) SERVER="$OPTARG"
         ;;
      U) USER="$OPTARG"
         ;;
      K) KEY="$OPTARG"
         ;;
      \?)echo "unknown option \"-$OPTARG\"." >&2
         usage $EXIT_ERROR
         ;;
      :) echo "option \"-$OPTARG\" argument missing" >&2
         usage $EXIT_ERROR
         ;;
     *) echo "bug ..." >&2
         usage $EXIT_BUG
         ;;
   esac
done

# : ${SERVER:='1.1.1.1'}
# : ${USER:='itsecorg'}
# : ${KEY:='/usr/local/fworch/.ssh/id_rsa_forti'}
 
DEBUG () {
      if [ $DEBUGMODE -gt 0 ]; then
         #printf "$1\n"
         printf '%s\n' "$1"
      fi
}
DEBUGWOLF () {
      if [ $DEBUGMODE -gt 0 ]; then
         printf '%s' "$1"
      fi
}

REMRES=`ssh -i ${KEY} ${USER}@${SERVER} "${REMCOM}" | egrep "fmg/faz|vdom|^TYPE" | grep -v 'root flags'`
LINECOUNT=0
FMGLINECOUNT=0
while read line; do
  ((LINECOUNT++))
  #DEBUG "$line"
  if [[ "$line" =~ "fmg/faz" ]]; then
    ((FMGLINECOUNT++))
    IFS=' '; read -ra FMGLINE <<< $line
    FMGVALCOUNT=0
    for FMGVAL in "${FMGLINE[@]}"; do
      ((FMGVALCOUNT++))
      FMG[${FMGLINECOUNT},${FMGVALCOUNT}]=$FMGVAL
      DEBUGWOLF "${FMG[${FMGLINECOUNT},${FMGVALCOUNT}]},"
    done
    DEBUG ""
    # array für die Ausgabezeilen bauen, oder die Zeile direkt ausgeben
  fi
  if [[ "$line" =~ "vdom" ]]; then
    ((VDOMLINECOUNT++))
    IFS=' '; read -ra VDOMLINE <<< $line
    VDOMVALCOUNT=0
    for VDOMVAL in "${VDOMLINE[@]}"; do
      ((VDOMVALCOUNT++))
      VDOM[${FMGLINECOUNT},${VDOMLINECOUNT},${VDOMVALCOUNT}]=$VDOMVAL
      DEBUGWOLF "${VDOM[${FMGLINECOUNT},${VDOMLINECOUNT},${VDOMVALCOUNT}]},"
    done
    DEBUG ""
    # wenn vdoms existieren obige zeile ergänzen, auch mehrfach
  fi
done <<< "$REMRES"
FMGLINECOUNTMAX=$FMGLINECOUNT

echo "${#FMG[@]}"
echo "${#VDOM[@]}"
#printf "${FMG[${FMGLINECOUNT},${FMGVALCOUNT}]}
