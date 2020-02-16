Targetdir=/tmp/isotmp/1111
Sourcecfg=/home/tim/sting-manager.cfg
Dstcfg=/tmp/isotmp/1111/cfg/sting-manager.cfg
Bindir=/usr/share/itsecorg/importer
cd $Bindir
mkdir -p $Targetdir/cfg
cp $Sourcecfg $Dstcfg
#python iso_parse_config_cp_r8x_api.py -i 123 -f $Sourcecfg -u > $Targetdir/sting_users.csv
#python iso_parse_config_cp_r8x_api.py -i 123 -f $Sourcecfg -s > $Targetdir/sting_services.csv
#python iso_parse_config_cp_r8x_api.py -i 123 -f $Sourcecfg -n > $Targetdir/sting_netzobjekte.csv
#python iso_parse_config_cp_r8x_api.py -i 123 -f $Sourcecfg -r 'cactus_Security'  > $Targetdir/sting_rulebase.csv

./iso-importer-single.pl mgm_name=sting-manager -parse-only