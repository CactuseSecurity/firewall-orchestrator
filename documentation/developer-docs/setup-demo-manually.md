# How to setup a demo machine (e.g. demo.itsecorg.de)

1. Allow direct access to internet in firewall (installation via proxy does not reliably work yet)

2. Clone repo

        git clone https://github.com/CactuseSecurity/firewall-orchestrator.git

3. Drop old database

        sudo -u postgres psql -c "drop database fworchdb"

4. Install fworch

        ansible-playbook/ -e "clean_install=1 ui_web_port=6443 site.yml -K

5. allow login without pwd change for users

        sudo -u postgres psql -d fworchdb -c "update uiuser set uiuser_password_must_be_changed=false"

6. Lock firewall
