# Upgrade instructions

It is really simple.

## Prerequisite: this host must run 8.0 or newer

Upgrades starting from a version older than 8.0 are not supported any more - the database
migration files below 8.0 have been removed. The installer checks the installed version
before it changes anything and stops such an upgrade, because running it would skip the
schema changes the older version is still missing.

An installation older than 8.0 gets here in two upgrades. `v8.9.6` is the last release
that still carries the removed migration files:

```console
  cd firewall-orchestrator
  git fetch --tags
  git checkout v8.9.6
  source scripts/install-ansible-from-venv.sh
  ./scripts/run-playbook-with-sudo.sh site.yml -e "installation_mode=upgrade"
```

Then continue with the current version as described below (`git checkout main`).

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
