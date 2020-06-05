# Enables crontab routines to automatically change sample data
# Created by alf
#fin = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "rt")
#import os


import datetime
import os

with open('dateInfo.txt','a') as outFile:
    outFile.write('\n' + str(datetime.datetime.now()))


from crontab import CronTab

user = open(os.system("bash -c \"whoami\""), "rt")
my_cron = CronTab(user='alf')
job = my_cron.new(command='python3 /home/alf/writeDate.py')
job.minute.every(1)

my_cron.write()