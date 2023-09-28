#!/usr/bin/python3
# changes a random rule from a native fortigate config

import random
import string
import os
import sys
import json
import logging


def randomOctet():
    return str(random.randrange(0, 256))


def randomIp():
    return randomOctet() + '.' + randomOctet() + '.' + randomOctet() + '.' + randomOctet()


def randomUid():
    s = ''.join(random.choices(string.ascii_lowercase + string.digits, k=32))
    return s[:8] + '-' + s[8:12] + '-' + s[12:16] + '-' + s[16:20] + '-' + s[20:]


# constants:
maxElements = 5
commentChangeId = "FWORCH: "

# fortiOS specific:
anyObj =  {
      "name": "all",
      "q_origin_key": "all"
   }

srcString = 'srcaddr'
dstString = 'dstaddr'
nwObjString = "nw_obj_firewall/address"
deleteElement = False
changeSource = False

if len(sys.argv) == 2:
    config_path = sys.argv[1]
else:
    logging.error('did not specify config file as parameter')
    print ("syntax: changeRule.py configFileName")
    exit(1)

tempConfigFile = config_path + ".tmp"

with open(config_path) as f:
    config = json.load(f)

# fortiOs settings:
rules = config['rules']['rules']


numberOfRules = len(rules)
numberOfRules = round(numberOfRules/2)    # only change the first half of the rules and keep the rest as is

pickedRuleNumber = random.randrange(0, numberOfRules)
rule = rules[pickedRuleNumber]

if random.randrange(0, 2)==0:
    changeSource = True
    ruleSide = rule[srcString]
else:
    ruleSide = rule[dstString]

if len(ruleSide)>=maxElements:
    deleteElement=True
    del ruleSide[len(ruleSide)-1]

actionChoices = ['changeSrcOrDst', 'enDisable', 'reverseAction', 'reverseLogging']

actionChosen = actionChoices[random.randrange(0, len(actionChoices))]

if actionChosen == 'changeSrcOrDst':
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
        config[nwObjString].append(nwObj)

        nwObjRef = {
                "name": newIp,
                "q_origin_key": newIp
            }
        ruleSide.append(nwObjRef)

elif actionChosen == 'enDisable':
    if rule['status'] == "enable":
        rule['status'] = "disable"
    else:
        rule['status'] = "enable"
elif actionChosen == 'reverseAction':
    if rule['action'] == "accept":
        rule['action'] = "deny"
    else:
        rule['action'] = "accept"
elif actionChosen == 'reverseLogging':
    if rule['logtraffic'] == "all" or rule['logtraffic'] == "utm":
        rule['logtraffic'] = "disable"
    else:
        rule['logtraffic'] = "all"
else:
    print ("unknown action chosen: " + actionChosen )
with open(tempConfigFile, 'w', encoding='utf-8') as f:
    json.dump(config, f, ensure_ascii=False, indent=4)

os.rename(tempConfigFile, config_path)
if changeSource:
    sideString = 'source'
else:
    sideString = 'destination'

logText = 'changeRule simulator: changed rule no. ' + str(pickedRuleNumber)+ ', changeType=' + actionChosen
if actionChosen=='changeSrcOrDst':
    logText += ', changed ' + sideString 
#print (logText)
logging.info(logText)
