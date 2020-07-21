# Changes the comment in rule 52 to the current date in fortigate.cfg
# Created by alf
# 2020-07-19 tmp: minor fixes to make changes instant using os.rename to avoid parallel writes, added close
# TODO: eliminate fixed uid 52 to make it more universal?

import fnmatch
import datetime
import os

cfg_file = "/home/isosample/sample-configs/fortinet_demo/fortigate.cfg"

with open(cfg_file, "rt") as fin:
    lines = fin.readlines()
    fin.close()
with open(cfg_file + '.tmp', "wt") as fout:
    uid = 52
    uid_flag = False
    for line in lines:
        if fnmatch.filter([line], '*edit {}*'.format(uid)):
            uid_flag = True
        if fnmatch.filter([line], '*next*'):
            uid_flag = False
        if fnmatch.filter([line], '*set comments*') and uid_flag:
            line = '        set comments "{}"\n'.format(datetime.datetime.now())
        fout.write(line)
fout.close()

os.rename(cfg_file + '.tmp', cfg_file)
