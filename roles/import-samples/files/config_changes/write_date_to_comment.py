# Changes the comment in rule 52 to the current date in fortigate.cfg
# Created by alf

import fnmatch
import datetime

with open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "rt") as fin:
    lines = fin.readlines()
with open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "wt") as fout:
    uid = 52
    uid_flag = False
    for line in fin:
        if fnmatch.filter([line], '*edit {}*'.format(uid)):
            uid_flag = True
        if fnmatch.filter([line], '*next*'):
            uid_flag = False
        if fnmatch.filter([line], '*set comments*') and uid_flag:
            line = '        set comments "{}"\n'.format(datetime.datetime.now())
        fout.write(line)
