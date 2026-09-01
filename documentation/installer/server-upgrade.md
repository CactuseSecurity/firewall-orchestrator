# Upgrade instructions

It is really simple.

Anything you configured for this host belongs in
`/etc/fworch/fwo-install-settings.yml`, not in `inventory/`. That file is
outside the git repository, so `git pull` and a fresh clone leave it alone and the
upgrade keeps your endpoint names and certificates. See *host-wide installer settings*
in `install-advanced.md`. If you did edit a file under `inventory/`, commit it locally
and upgrade with `git pull --rebase`, or the edit is lost or conflicts below.

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
