# Created by alf
# Enlarges rule 60 by a random srcadress in fortigate.cfg
# First step: create functions that build a random ip address and uuid

import random
import string
import fnmatch


def random_octet():
    return str(random.randrange(0, 255))


def random_ip():
    return random_octet() + '.' + random_octet() + '.' + random_octet() + '.' + random_octet()


def random_uuid():
    s = ''.join(random.choices(string.ascii_lowercase + string.digits, k=32))
    return s[:8] + '-' + s[8:12] + '-' + s[12:16] + '-' + s[16:20] + '-' + s[20:]


# Second step: build new network object in "config firewall address"

ip_address = random_ip()
uuid = random_uuid()
count = 1
line_to_insert_at = -1
fin = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "r")
data = fin.readlines()
fin.close()
for line in data:
    if fnmatch.filter([line], 'config firewall address[!6]*'):
        line_to_insert_at = count
    else:
        count = count + 1
if line_to_insert_at > -1:
    data.insert(line_to_insert_at, '    edit "{}"\n'.format(ip_address))
    data.insert(line_to_insert_at + 1, '        set uuid {}\n'.format(uuid))
    data.insert(line_to_insert_at + 2, '        set associated-interface "kids-wifi"\n')
    data.insert(line_to_insert_at + 3, '        set subnet {} 255.255.255.255\n'.format(ip_address))
    data.insert(line_to_insert_at + 4, '        set comment "Automatically built for test purposes"\n')
    data.insert(line_to_insert_at + 5, '    next\n')
    data.insert(line_to_insert_at + 6, '# recognition comment for auto-delete function')

fout = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "w")
data = "".join(data)
fout.write(data)
fout.close()

# Third step: add new objects to rule 60

with open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "r") as fin:
    lines = fin.readlines()
with open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "w") as fout:
    uid_flag = False
    delete_unused_networkobjects = False
    for line in lines:
        if fnmatch.filter([line], '*edit 60*'):
            uid_flag = True
        if fnmatch.filter([line], '*next*'):
            uid_flag = False
        if fnmatch.filter([line], '*set srcaddr*') and uid_flag:
            if fnmatch.filter([line], '*"all"*') or len(line) > 200:
                line = '        set srcaddr "{}"\n'.format(ip_address)
                delete_unused_networkobjects = True
            else:
                line = line.rstrip() + ' "{}"\n'.format(ip_address)
        fout.write(line)

# Utils

# This routine deletes all automatically created network objects except the most recent
if delete_unused_networkobjects:
    delete_flag = False
    with open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "r") as fin:
        lines = fin.readlines()
    with open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "w") as fout:
        for line in lines:
            if line.strip("\n") == "# recognition comment for auto-delete function":
                delete_flag = True
            if line.strip("\n") == 'edit "SSLVPN_TUNNEL_ADDR1"':
                delete_flag = False
            if not delete_flag:
                fout.write(line)
