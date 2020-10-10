#!/usr/bin/python3
# source: https://github.com/Esri/webhooks-samples/tree/master/python/receiver/flask

# starting with: python3 /home/fworchtest/fworch-webhook-receiver.py
# in github settings webhook:
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
import re

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
   if 'head_commit' in req_data:
      now = datetime.now() # current date and time
      f.write('--- webhook call received '+ now.strftime("%Y-%m-%d %H:%M:%S") +' ---\n')
      modified_files = req_data['head_commit']['modified']
      f.write('found modified files: ' + json.dumps(modified_files) + '\n')
      relevant_change = False
      pattern = '^roles\/|^inventory\/|^site.yml$'
      for modified_file in modified_files:
         if (re.match(pattern, modified_file)):
            relevant_change = True
      pattern = '^scripts\/fworch-webhook-receiver.py$'
      for modified_file in modified_files:
         if (re.match(pattern, modified_file)):
            webhook_script_changed = True
      if (relevant_change):
         f.write('Relevant change: start building\n')
         target_path  =  tmp_git_dir
         clone_cmd = "cd " + tmp_git_dir + " && ssh-agent bash -c 'ssh-add " + ssh_priv_key_file + " && git clone ssh://git@" + github_hostname + project_path + "'"
         f.write('executing ' + clone_cmd + '\n')
         os.system(clone_cmd) # Cloning
         if (webhook_script_changed):
            os.system("cp " + tmp_git_dir + "/scripts/fworch-webhook-receiver.py " + root_dir)
            # might not work due to user rights:
            # os.system("systemctl restart fworch-webhook-receiver.service")
         build_cmd = "cd " + tmp_git_dir + "/firewall-orchestrator && ssh-agent bash -c 'ssh-add " + ssh_priv_key_file + " && " + \
            "ansible-playbook -i inventory site.yml -e \"testkeys=yes installation_mode=upgrade\" --skip-tags \"test\"" + "'"
         f.write('executing build command: ' + build_cmd + '\n')
         os.system(build_cmd) # building fworch backend
         now = datetime.now() # current date and time
         f.write('--- build completed '+ now.strftime("%Y-%m-%d %H:%M:%S") +' ---\n')
      else:
         f.write('No relevant change found, not re-building.\n')
   f.close()
   return '{"success":"true"}'

if __name__ == "__main__":
   context = ('ssl.cert', 'ssl.key') # certificate and key file. Cannot be self signed certs
   app.run(host='0.0.0.0', port=60344, ssl_context='adhoc', threaded=True, debug=True)
