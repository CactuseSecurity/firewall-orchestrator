# Upgrade instructions

It is really simple.

Always (re-)create the installer Ansible environment before upgrading. This
installs the pinned Ansible version and refreshes the required collections, so
security fixes to pinned collections actually reach the server. The pinned
collections require `ansible-core >=2.16`; the distro Ansible package may be
older, so do not rely on it for upgrades.

If you already have a local git repository from the original installation:

```console
  cd firewall-orchestrator
  git pull                                                          # to upgrade the repo from the original repo@github
  source scripts/install-ansible-from-venv.sh                       # refresh pinned Ansible + collections
  ./scripts/run-playbook-with-sudo.sh site.yml -e "installation_mode=upgrade"
```

If you do not have a local repo:

```console
  git clone https://github.com/cactusesecurity/firewall-orchestrator
  cd firewall-orchestrator
  source scripts/install-ansible-from-venv.sh                       # refresh pinned Ansible + collections
  ./scripts/run-playbook-with-sudo.sh site.yml -e "installation_mode=upgrade"
```
