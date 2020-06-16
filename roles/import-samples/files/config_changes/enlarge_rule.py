# Created by alf
# First step: create functions that build a random ip address and uuid

import random
import string
import fnmatch
from shutil import copyfile


def random_octet():
    return str(random.randrange(0, 255))


def random_ip():
    return random_octet() + '.' + random_octet() + '.' + random_octet() + '.' + random_octet()


def random_uuid():
    str = ''.join(random.choices(string.ascii_lowercase + string.digits, k=32))
    return str[:8] + '-' + str[8:12] + '-' + str[12:16] + '-' + str[16:20] + '-' + str[20:]


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

fout = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "w")
data = "".join(data)
fout.write(data)
fout.close()

# Third step: add new objects to rule 60

fin = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "rt")
fout = open("/home/isosample/sample-configs/fortinet_demo/deleteme.cfg", "wt")
uid_flag = False
for line in fin:
    if fnmatch.filter([line], '*edit 60*'):
        uid_flag = True
    if fnmatch.filter([line], '*next*'):
        uid_flag = False
    if fnmatch.filter([line], '*set srcaddr*') and uid_flag:
        if fnmatch.filter([line], '*"all"*'):
            line = '        set srcaddr "{}"\n'.format(ip_address)
        else:
            line = line.rstrip() + ' "{}"\n'.format(ip_address)
    fout.write(line)
fin.close()
fout.close()
copyfile("/home/isosample/sample-configs/fortinet_demo/deleteme.cfg", "/home/isosample/sample-configs/fortinet_demo/fortigate.cfg")
