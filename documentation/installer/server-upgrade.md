# Upgrade instructions

It is really simple.

If you already have a local git repository from the original installation:

```console
  cd firewall-orchestrator
  git pull                                                          # to upgrade the repo from the original repo@github
  ansible-playbook site.yml -K -e "installation_mode=upgrade"
```

If you do not have a local repo:

```console
  git clone https://github.com/cactusesecurity/firewall-orchestrator
  cd firewall-orchestrator
  ansible-playbook site.yml -K -e "installation_mode=upgrade"
```
