# Changes the comment in rule x to the current date in fortigate.cfg
# x = 52 by default, can be changed in config_changes_main.py
# Created by alf

import fnmatch
import datetime
import os

# Default variables if run as individual script
if __name__ == "__main__":
    config_path = "/home/isosample/sample-configs/fortinet_demo/fortigate.cfg"
    uid = 52
# Variables if called by config_changes_main.py
else:

    import sys

    config_path = sys.argv[1]
    uid = sys.argv[2]

with open(config_path, "r") as fin:
    data = fin.readlines()

rule_area_flag = False
uid_flag = False
set_comment_flag = False
current_line = 0
for line in data:
    if fnmatch.filter([line], 'config firewall policy\n'):
        rule_area_flag = True
    if fnmatch.filter([line], '    edit {}\n'.format(uid)):
        uid_flag = True
    if fnmatch.filter([line], '        set comments*') and uid_flag and rule_area_flag:
        line = '        set comments "{}"\n'.format(datetime.datetime.now())
        set_comment_flag = True
        break
    if fnmatch.filter([line], '    next\n') and uid_flag and rule_area_flag and not set_comment_flag:
        data.insert(current_line, '        set comments "{}"\n'.format(datetime.datetime.now()))
        set_comment_flag = True
        break
    if fnmatch.filter([line], '    next\n'):
        uid_flag = False
    if fnmatch.filter([line], 'end\n'):
        rule_area_flag = False
    current_line = current_line + 1

with open(config_path + ".tmp", "w") as fout:
    data = "".join(data)
    fout.write(data)

os.rename(config_path + '.tmp', config_path)
