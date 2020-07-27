#!/bin/sh

if [ ! "$ISOBASE" ]; then
        ISOBASE="/usr/share/itsecorg"
        echo "ISOBASE was not set, using default diretory $ISOBASE."
fi
ISOBINDIR=$ISOBASE/install/database/db-install-scripts
PATH=$PATH:$ISOBINDIR:$ISOBASE/importer

. $ISOBINDIR/iso-set-vars.sh $ISOBASE
. $ISOBINDIR/iso-pgpass-create.sh $ISOBASE

### now inserting data
echo "adding basic data (stm_color)" | $OUT
$PSQLCMD -c "\copy stm_color (color_name,color_rgb) FROM '$DATADIR/color.csv' DELIMITER ';' CSV" 2>&1 | $OUT
echo "adding basic data (error)" | $OUT
$PSQLCMD -c "\copy error (error_id,error_lvl,error_txt_ger,error_txt_eng) FROM '$DATADIR/error.csv' DELIMITER ';' CSV" 2>&1 | $OUT
echo "adding basic data (ip_proto)" | $OUT
$PSQLCMD -c "\copy stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) FROM '$DATADIR/ip-protocol-list.csv' DELIMITER ';' CSV" 2>&1 | $OUT
echo "adding basic data (iso-fill-stm - mixed)" | $OUT
$PSQLCMD -c "\i $SQLDIR/iso-fill-stm.sql" 2>&1 | $OUT

# delete .pgpass
. $ISOBINDIR/iso-pgpass-remove.sh
