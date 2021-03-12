cd 
rm -rf firewall-orchestrator
ssh-agent bash -c 'ssh-add .ssh/id_github_deploy && git clone git@github.com:CactuseSecurity/firewall-orchestrator.git'
cd firewall-orchestrator
ansible-playbook site.yml -e "installation_mode=uninstall"
ansible-playbook site.yml -e "installation_mode=new"
