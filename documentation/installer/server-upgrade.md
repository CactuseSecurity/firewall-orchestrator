# Upgrade instructions

it is really simple:

```console
  cd firewall-orchestrator
  git pull
  ansible-playbook site.yml -K -e "installation_mode=upgrade"
```
