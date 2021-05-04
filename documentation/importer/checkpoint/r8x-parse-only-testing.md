# how to test with a config file without contact to the original manager

```
fworch@ubu18test:~$ python3 importer/checkpointR8x/parse_config.py -f /tmp/isotmp/3/cfg/xxx11.cfg -r 'gMgmt-Cluster_Internet-internal Security' -d 1
2021-05-04 06:04:07,232 - DEBUG - debug_level: 1
2021-05-04 06:04:07,418 - DEBUG - parse_config - args
f:/tmp/isotmp/3/cfg/xxx11.cfg
i: 0
m: <mgmt-name>
r: gMgmt-Cluster_Internet-internal Security
n: False
s: False
u: False
d: 1
"0"%"0"%"gMgmt-Cluster_Internet-internal Security"%%"false"%"False"%"Any"%"97aeb369-9aea-11d5-bd16-0090272ccb30"%"False"%"Any"%"97aeb369-9aea-11d5-bd16-0090272ccb30"%"False"%"Any"%"97aeb369-9aea-11d5-bd16-0090272ccb30"%"Accept"%"Log"%"Policy Targets"%"Any"%""%%"24d3149b-7be1-49a3-addb-190e8271baac"%"FW INTERNAL COMMUNICATION"%%%
"0"%"1"%"gMgmt-Cluster_Internet-internal Security"%%"False"%"False"%"grp_P_HO_FW_Internet-Innen"%"70d30477-ccbc-49f0-a92f-2622b97361be"%"False"%"grp_P_HO_TIME-Server"%"5179bf0b-b27c-44cd-aeb2-2ca4bb49daf2"%"False"%"gntp-udp"%"ce66b076-0cad-11de-8299-00000000dfdf"%"Accept"%"Log"%"Cluster_Internet-Innen_global"%"Any"%"diverse Dienste #08.03.2011 REU ON#SIM-2011-20"%"HO-NTP"%"936c6067-a6c1-4bb6-9767-ffba54536fa6"%%%
```
