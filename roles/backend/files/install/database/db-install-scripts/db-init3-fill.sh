#!/bin/sh
# $Id: db-init3-fill.sh,v 1.1.2.2 2007-12-13 10:47:31 tim Exp $
# $Source: /home/cvs/iso/package/install/bin/Attic/db-init3-fill.sh,v $
if [ ! "$ISOBASE" ]; then
        ISOBASE="/usr/share/itsecorg"
        echo "ISOBASE was not set, using default diretory $ISOBASE."
fi
ISOBINDIR=$ISOBASE/install/database/db-install-scripts
PATH=$PATH:$ISOBINDIR:$ISOBASE/importer

. $ISOBINDIR/iso-set-vars.sh $ISOBASE
. $ISOBINDIR/iso-pgpass-create.sh $ISOBASE


#echo "Careful: this script modifies the database by adding basic data to it (do not run against existing db)"
#echo "press <CTRL-C> to abort installation within 10 seconds"
#sleep 10

### now inserting data
echo "adding basic data (stm_color)" | $OUT
$PSQLCMD -c "\copy stm_color (color_name,color_rgb) FROM '$DATADIR/color.csv' DELIMITER ';' CSV" 2>&1 | $OUT
echo "adding basic data (error)" | $OUT
$PSQLCMD -c "\copy error (error_id,error_lvl,error_txt_ger,error_txt_eng) FROM '$DATADIR/error.csv' DELIMITER ';' CSV" 2>&1 | $OUT
echo "adding basic data (ip_proto)" | $OUT
$PSQLCMD -c "\copy stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) FROM '$DATADIR/ip-protocol-list.csv' DELIMITER ';' CSV" 2>&1 | $OUT
echo "adding basic data (text_msg)" | $OUT
$PSQLCMD -c "\copy text_msg (text_msg_id,text_msg_ger,text_msg_eng) FROM '$DATADIR/text_msg.csv' DELIMITER ';' CSV" 2>&1 | $OUT
echo "adding basic data (iso-fill-stm - mixed)" | $OUT
$PSQLCMD -c "\i $SQLDIR/iso-fill-stm.sql" 2>&1 | $OUT
#echo "adding network specific data: sample-data/iso-fill-samples.sql|user.sql" | $OUT
#$PSQLCMD -c "\i $SAMPLEDIR/iso-fill-samples.sql" 2>&1 | $OUT
#$PSQLCMD_INIT_USER -c "\i $SAMPLEDIR/iso-user.sql" 2>&1 | $OUT

# delete .pgpass
#. $ISOBINDIR/iso-pgpass-remove.sh
