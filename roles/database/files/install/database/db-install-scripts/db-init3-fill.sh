#!/bin/sh

if [ ! "$FWORCHBASE" ]; then
        FWORCHBASE="/usr/local/fworch"
        echo "FWORCHBASE was not set, using default diretory $FWORCHBASE."
fi
FWORCHBINDIR=$FWORCHBASE/install/database/db-install-scripts
PATH=$PATH:$FWORCHBINDIR:$FWORCHBASE/importer

. $FWORCHBINDIR/iso-set-vars.sh $FWORCHBASE
. $FWORCHBINDIR/iso-pgpass-create.sh $FWORCHBASE

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
. $FWORCHBINDIR/iso-pgpass-remove.sh
