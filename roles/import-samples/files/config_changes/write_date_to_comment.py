# Changes the word 'wizard' to 'lizard' in fortigate.cfg
# Created by alf

import fnmatch
import datetime
#import re

fin = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "rt")
fout = open("/home/isosample/sample-configs/fortinet_demo/deleteme.txt", "wt")
data = fin
uid = 52
uid_flag = False
for line in fin:
    if fnmatch.filter([line], '*edit {}*'.format(uid)):
        uid_flag = True
    if fnmatch.filter([line], '*next*'):
        uid_flag = False
    if fnmatch.filter([line], '*set comments*') and uid_flag:
        # fout.write(line.replace('"*"', '"{}"'.format(datetime.datetime.now())))
        #line = re.sub('"*"', '"{}"'.format(datetime.datetime.now()), line)
        #fout.write(line.re.sub('set comments "VPN: Cactus-DA (Created by VPN wizard)"', 'test'))
        line = '        set comments "{}"\n'.format(datetime.datetime.now())
    #fout.write(line)
    data.write(line)
fin.close()
fout.close()

from shutil import copyfile
copyfile("/home/isosample/sample-configs/fortinet_demo/deleteme.txt", "/home/isosample/sample-configs/fortinet_demo/fortigate.cfg")
