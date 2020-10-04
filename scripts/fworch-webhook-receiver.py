#!/usr/bin/python3
# quelle: https://github.com/Esri/webhooks-samples/tree/master/python/receiver/flask

# starten: python3 /home/fworchtest/fworch-webhook-receiver.py
# in webhook definition von github:
#   https://cactus.de:60344/fwo
#   content-type: application/json
#   trigger: just the push-event
#   Disable SSL verification

# todo: deal with timeout of long build process

from flask import Flask, request
from OpenSSL import SSL
import os
import sys
import json
import subprocess
from datetime import datetime

root_dir = '/home/fworchtest/'
tmp_git_dir = root_dir + 'tmp_git'
github_hostname = "github.com"
project_path = '/CactuseSecurity/firewall-orchestrator.git'
ssh_priv_key_file = root_dir + '.ssh/id_github_deploy'
webhook_log_file_basename = 'webhookPayloads.txt'

# prepare a path for git cloning
os.system('rm -rf ' + tmp_git_dir)
os.system('mkdir -p ' + tmp_git_dir)

webhook_logfile = root_dir + '/' + webhook_log_file_basename
if os.path.exists(webhook_logfile):
    append_write = 'a' # append if already exists
else:
    append_write = 'w' # make a new file if not

#Step Two: Set parameters for server:
app = Flask(__name__)

@app.route('/fwo', methods=['GET'])
def get_handler():
   webpage = '<h1>fworch test server webhook listener</h1>\n'
   f = open(webhook_logfile,'r')
   str = f.read()
   f.close()
   webpage += '<pre>'+str+'</pre>'
   return webpage

@app.route('/fwo', methods=['POST'])
def post_handler():
   f = open(webhook_logfile,append_write)
   req_data = request.get_json()
   req_data_str = json.dumps(req_data)
   now = datetime.now() # current date and time
   # debugging # log = '--- '+ now.strftime("%Y-%m-%d %H:%M:%S") +' ---\nraw post request: '+req_data_str+'\n'

   if 'head_commit' in req_data:
      now = datetime.now() # current date and time
      log = '--- build starting   '+ now.strftime("%Y-%m-%d %H:%M:%S") +' ---\n'
      modifications = req_data['head_commit']['modified']
      log += 'found modified files: ' + json.dumps(modifications) + '\n'
      target_path  =  tmp_git_dir
      clone_cmd = "cd " + tmp_git_dir + " && ssh-agent bash -c 'ssh-add " + ssh_priv_key_file + " && git clone ssh://git@" + github_hostname + project_path + "'"
      log += 'executing ' + clone_cmd + '\n'
      os.system(clone_cmd) # Cloning
      build_cmd = "cd " + tmp_git_dir + "/firewall-orchestrator && ssh-agent bash -c 'ssh-add " + ssh_priv_key_file + " && " + \
          "ansible-playbook -i inventory site.yml -e \"testkeys=yes\" --skip-tags \"test\"" + "'"
          #"ansible-playbook -i inventory site.yml -e \"testkeys=yes\" --skip-tags \"test,frontend\"" + "'"
      log += 'executing build command: ' + build_cmd + '\n'
      os.system(build_cmd) # building fworch backend
      now = datetime.now() # current date and time
      log += '--- build completed '+ now.strftime("%Y-%m-%d %H:%M:%S") +' ---\n'
   f.write(log)
   f.close()
   return '{"success":"true"}'

if __name__ == "__main__":
   context = ('ssl.cert', 'ssl.key') # certificate and key file. Cannot be self signed certs
   app.run(host='0.0.0.0', port=60344, ssl_context='adhoc', threaded=True, debug=True)
