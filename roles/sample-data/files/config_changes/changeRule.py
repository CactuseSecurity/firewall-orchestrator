#!/usr/bin/python3
# changes a random rule from a native fortigate config

import random
import string
import os
import sys
import json


maxElements = 5
commentChangeId = "FWORCH: "
anyObj =  {
      "name": "all",
      "q_origin_key": "all"
   }
config_path = sys.argv[1] if len(sys.argv) >= 2 else "/tmp/demo10_fOS.json"
tempConfigFile = config_path + ".tmp"
deleteElement = False
changeSource = False


def randomOctet():
    return str(random.randrange(0, 256))


def randomIp():
    return randomOctet() + '.' + randomOctet() + '.' + randomOctet() + '.' + randomOctet()


def randomUid():
    s = ''.join(random.choices(string.ascii_lowercase + string.digits, k=32))
    return s[:8] + '-' + s[8:12] + '-' + s[12:16] + '-' + s[16:20] + '-' + s[20:]


with open(config_path) as f:
    config = json.load(f)

rules = config['rules']['rules']
numberOfRules = len(rules)
numberOfRules = round(numberOfRules/4)    # only change the first quarter of the rules and keep the rest as is

pickedRuleNumber = random.randrange(0, numberOfRules)
rule = rules[pickedRuleNumber]

if random.randrange(0, 2)==0:
    changeSource = True
    ruleSide = rule['srcaddr']
else:
    ruleSide = rule['dstaddr']

if len(ruleSide)>=maxElements:
    deleteElement=True
    del ruleSide[len(ruleSide)-1]

if not deleteElement:
    newUid = randomUid()
    newIp = randomIp()

    # cannot add to any obj, so delete it first
    if anyObj in ruleSide:
        del ruleSide[0]

    nwObj = {
            "name": newIp,
            "q_origin_key": newIp,
            "uuid": newUid,
            "subnet": [newIp, 32],
            "type": "ipmask",
            "obj-type": "ip",
            "comment": commentChangeId + "random ip added as simulated change",
            "associated-interface": "",
            "color": 0
        }
    config["nw_obj_firewall/address"].append(nwObj)

    nwObjRef = {
            "name": newIp,
            "q_origin_key": newIp
        }
    ruleSide.append(nwObjRef)

with open(tempConfigFile, 'w', encoding='utf-8') as f:
    json.dump(config, f, ensure_ascii=False, indent=4)

os.rename(tempConfigFile, config_path)
