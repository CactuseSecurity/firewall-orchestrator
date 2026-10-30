# Upgrade instructions

It is really simple.

## Prerequisite: this host must run 9.0.16 or newer

Upgrades starting from a version older than 9.0.16 are not supported any more. 9.0.16 moved
the API (Hasura) from Docker to Podman, and the installer no longer carries out that move.
The installer checks the installed version before it changes anything and stops such an
upgrade, because running it would leave the old Docker container on the API port.

An installation from 8.0 to 9.0.15 gets here in two upgrades. `v9.8.4` still moves the API
to Podman, so check that release out first:

```console
  cd firewall-orchestrator
  git fetch --tags
  git checkout v9.8.4
```

Then run the interim upgrade **the way that release documents it**, in its own
`documentation/installer/server-upgrade.md`. Once the host runs 9.8.4, return to the current
version (`git checkout main`) and upgrade again as described below.

An installation older than 8.0 needs one more step first: the database migration files
below 8.0 have been removed as well, and `v8.9.6` is the last release that still carries
them. Upgrade with `v8.9.6` the way that release documents it (at the time of writing
`ansible-playbook site.yml -K -e "installation_mode=upgrade"`), then with `v9.8.4`, then
with the current version. The `v9.8.4` installer tells you so as well when you skip the
first step.

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

## After an operating system upgrade

An in-place upgrade of the operating system can move PostgreSQL and python to new major or
minor versions (e.g. Debian 12 -> 13: PostgreSQL 15 -> 17, python 3.11 -> 3.13). Upgrade the
operating system first, then upgrade Firewall Orchestrator as described above.

### PostgreSQL major version

The operating system upgrade installs the new PostgreSQL server but leaves the database in
the old format. The installer detects this and stops before it changes PostgreSQL, naming
the version it found. Migrate the database, then run the upgrade again.

#### Debian and Ubuntu

```console
  pg_lsclusters                         # e.g. "15 main ... online" and no 17 main
  sudo systemctl stop fworch-importer-api fworch-ui fworch-middleware fworch-hasura-api
  sudo pg_upgradecluster 15 main        # creates 17/main from 15/main, moves 15 to another port
  pg_lsclusters                         # 17 main must be online on port 5432
  ./scripts/run-playbook-with-sudo.sh site.yml -e "installation_mode=upgrade"
```

Keep the default dump method of `pg_upgradecluster` (do not use `-m upgrade` or `-m link`).
It rebuilds all indexes, which is required after a C library upgrade: the old cluster then
warns about a "collation version mismatch", and text indexes built with the old sort order
can return wrong results.

`pg_upgradecluster` refuses to create the new cluster when the locale of your session is
not installed on the server (e.g. `LC_TIME=de_DE.UTF-8` passed on by ssh). Generate the
missing locale with `locale-gen`, or run the command with `LC_ALL=C.UTF-8`.

Once Firewall Orchestrator works on the new cluster, remove the old one:

```console
  sudo pg_dropcluster 15 main
  sudo apt purge postgresql-15 postgresql-client-15
```

#### Red Hat and Rocky

```console
  sudo systemctl stop fworch-importer-api fworch-ui fworch-middleware fworch-hasura-api postgresql
  sudo dnf install postgresql-upgrade
  sudo postgresql-setup --upgrade       # moves the old data to /var/lib/pgsql/data-old
  sudo systemctl start postgresql
  sudo -u postgres reindexdb --all
  ./scripts/run-playbook-with-sudo.sh site.yml -e "installation_mode=upgrade"
```

`postgresql-setup --upgrade` keeps the old index files, so `reindexdb --all` rebuilds them
with the sort order of the new C library. From PostgreSQL 15 on, then also clear the
"collation version mismatch" warning in every database
(`ALTER DATABASE <name> REFRESH COLLATION VERSION`) - never without the reindex.

`postgresql-upgrade` only provides the PostgreSQL version that the distribution shipped
before the current one. For a larger step, dump the database with `pg_dumpall` before the
operating system upgrade, and restore it into a new data directory
(`postgresql-setup --initdb`) afterwards.

Once Firewall Orchestrator works on the new version, remove `/var/lib/pgsql/data-old`.

### Python version

The importer and the customizing scripts run in python venvs. A venv only works with the
python minor version it was built for, so after the operating system upgrade the importer
fails with errors like `ModuleNotFoundError: No module named 'urllib3'`. The Firewall
Orchestrator upgrade rebuilds every venv that was built for another python version.
