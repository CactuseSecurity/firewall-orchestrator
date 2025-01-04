# Changes the comment in rule x to the current date in fortigate.cfg
# x = 52 by default, can be changed in ansible
# Created by alf

import fnmatch
import datetime
import os
import sys

# Define global variables that may be passed on the command line and their defaults if not
# example$ python3 write_date_to_comment.py uid "path"

uid = sys.argv[1] if len(sys.argv) >= 2 else 52
config_path = sys.argv[2] if len(sys.argv) >= 3 else "/home/fworchsample/sample-configs/fortinet_demo/fortigate.cfg"

with open(config_path, "r") as fin:
    data = fin.readlines()

rule_area_flag = False
uid_flag = False
current_line = 0
for line in data:
    if fnmatch.filter([line], 'config firewall policy\n'):
        rule_area_flag = True
    if fnmatch.filter([line], '    edit {}\n'.format(uid)):
        uid_flag = True
    if fnmatch.filter([line], '        set comments*') and uid_flag and rule_area_flag:
        data[current_line] = '        set comments "{}"\n'.format(datetime.datetime.now())
        break
    if fnmatch.filter([line], '    next\n') and uid_flag and rule_area_flag:
        data.insert(current_line, '        set comments "{}"\n'.format(datetime.datetime.now()))
        break
    if fnmatch.filter([line], '    next\n'):
        uid_flag = False
    if fnmatch.filter([line], 'end\n'):
        rule_area_flag = False
    current_line = current_line + 1

with open(config_path + "2.tmp", "w") as fout:
    data = "".join(data)
    fout.write(data)

os.rename(config_path + '2.tmp', config_path)
