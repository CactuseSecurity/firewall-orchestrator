# Created by alf
# Enlarges rule 60 by a random srcadress in fortigate.cfg

import random
import string
import fnmatch
import os
import sys
import re

# Define global variables that may be passed on the command line and their defaults if not
# example$ python3 enlarge_rule.py uid "path"

input_index = sys.argv[1] if len(sys.argv) >= 2 else 0
input_index = int(input_index) % 59
config_path = sys.argv[2] if len(sys.argv) >= 3 else "/home/fworchsample/sample-configs/fortinet_demo/fortigate.cfg"
uid = -1

# First step: create functions that build a random ip address and uuid


def random_octet():
    return str(random.randrange(0, 255))


def random_ip():
    return random_octet() + '.' + random_octet() + '.' + random_octet() + '.' + random_octet()


def random_uuid():
    s = ''.join(random.choices(string.ascii_lowercase + string.digits, k=32))
    return s[:8] + '-' + s[8:12] + '-' + s[12:16] + '-' + s[16:20] + '-' + s[20:]


# Second step: extract rule uid using the input_index

with open(config_path, "r") as fin:
    data = fin.readlines()

rule_area_flag = False
rule_mapping_count = 0

for line in data:
    if fnmatch.filter([line], 'config firewall policy\n'):
        rule_area_flag = True
    elif fnmatch.filter([line], '    edit *\n') and rule_area_flag:
        uid = int(re.findall(r'\d+', line)[0])
        if rule_mapping_count == input_index:
            break
        else:
            rule_mapping_count = rule_mapping_count + 1
    elif fnmatch.filter([line], 'end\n') and rule_area_flag:
        break


# Third step: build new network object in "config firewall address"

ip_address = random_ip()
uuid = random_uuid()
count = 1
line_to_insert_at = -1

for line in data:
    if fnmatch.filter([line], 'config firewall address\n'):
        line_to_insert_at = count
        break
    else:
        count = count + 1

if line_to_insert_at > -1:
    data.insert(line_to_insert_at, '# start recognition comment for auto-delete function, ID={}\n'.format(uid))
    data.insert(line_to_insert_at + 1, '    edit "{}"\n'.format(ip_address))
    data.insert(line_to_insert_at + 2, '        set uuid {}\n'.format(uuid))
    data.insert(line_to_insert_at + 3, '        set subnet {} 255.255.255.255\n'.format(ip_address))
    data.insert(line_to_insert_at + 4, '        set comment "Automatically built for test purposes"\n')
    data.insert(line_to_insert_at + 5, '    next\n')
    data.insert(line_to_insert_at + 6, '# end recognition comment for auto-delete function, ID={}\n'.format(uid))

# Fourth step: add new object to source address of rule uid

rule_area_flag = False
uid_flag = False
delete_unused_network_objects = False
replace_counter = 0

for line in data:
    if fnmatch.filter([line], 'config firewall policy\n'):
        rule_area_flag = True
    if fnmatch.filter([line], '    edit {}\n'.format(uid)):
        uid_flag = True
    if fnmatch.filter([line], '        set srcaddr*') and rule_area_flag and uid_flag:
        if fnmatch.filter([line], '        set srcaddr "all"\n'):
            data[replace_counter] = '        set srcaddr "{}"\n'.format(ip_address)
        else:
            data[replace_counter] = line.rstrip() + ' "{}"\n'.format(ip_address)
        if len(data[replace_counter]) > 95:
            data[replace_counter] = '        set srcaddr "{}"\n'.format(ip_address)
            delete_unused_network_objects = True
        break
    if fnmatch.filter([line], '    next\n'.format(uid)):
        uid_flag = False
    if fnmatch.filter([line], 'end\n'):
        rule_area_flag = False
    replace_counter = replace_counter + 1

# Last step: write everything back to the config file
# If delete_unused_network_objects is set delete all new objects except for the most recent

with open(config_path + ".tmp", "w") as fout:
    if delete_unused_network_objects:
        delete_flag = False
        object_count = 0
        for line in data:
            last_comment_line_flag = False
            if line == '# start recognition comment for auto-delete function, ID={}\n'.format(uid):
                delete_flag = True
                object_count = object_count + 1
            if line == '# end recognition comment for auto-delete function, ID={}\n'.format(uid):
                delete_flag = False
                last_comment_line_flag = True
            if object_count < 2:
                fout.write(line)
            elif not (delete_flag or last_comment_line_flag):
                fout.write(line)
    else:
        for line in data:
            fout.write(line)

os.rename(config_path + ".tmp", config_path)
