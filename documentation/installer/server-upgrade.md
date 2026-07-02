# Upgrade instructions

It is really simple.

If you already have a local git repository from the original installation:

```console
  cd firewall-orchestrator
  git pull                                                          # to upgrade the repo from the original repo@github
  ./scripts/run-playbook-with-sudo.sh site.yml -e "installation_mode=upgrade"
```

If you do not have a local repo:

```console
  git clone https://github.com/cactusesecurity/firewall-orchestrator
  cd firewall-orchestrator
  ./scripts/run-playbook-with-sudo.sh site.yml -e "installation_mode=upgrade"
```
