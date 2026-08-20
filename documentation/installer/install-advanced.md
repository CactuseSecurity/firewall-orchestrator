# Advanced installation options

always change into the firewwall-orchestrator directory before starting the installation.

On Ubuntu 26.04 or other systems with sudo 1.9.16+, use `./scripts/run-playbook-with-sudo.sh` instead of `ansible-playbook ... -K`. For example:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e installation_mode=upgrade
```

Full sudoers rights are still required. If sudo already works without a password, the wrapper runs the playbook directly; otherwise it creates a temporary passwordless sudoers entry for the current user, runs the playbook without `-K`, and removes the entry again when the playbook exits.

## Install parameters

### Installation mode parameter

installation_mode options:
- new (default) - assumes that no fworch is installed on the target devices - fails if it finds an installation
- uninstall     - uninstalls the product including any data (database, ldap, files)!
- upgrade       - installs on top of an existing system preserving any existing data in ldap, database, api; removes all files from target and copies latest sources instead

#### Upgrading ####

If you have an ansible version less than 2.13 on your machine, before doing an upgrade, switch into the virtual pyhton environment you created during installation before running the upgrade:

```console
cd ~/firewall-orchestrator
source ansible-venv/bin/activate
```

Then for upgrading firewall orchestrator, use the following switch:

```console
cd ~/firewall-orchestrator
./scripts/run-playbook-with-sudo.sh site.yml -e installation_mode=upgrade
```

#### Uninstall ####
If you want to drop the database and re-install from scratch, do the following:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e installation_mode=uninstall
./scripts/run-playbook-with-sudo.sh site.yml
```

### Installation behind a proxy (no direct Internet connection)

By default, during installation or upgrade the proxy settings are read from the OS environment of the installer host.
The installer reads both lowercase and uppercase proxy variables. If both forms are set and non-empty, the lowercase value wins.
For example you may either use /etc/environment or add a global system-wide config file /etc/profile.d/proxy.sh and add the following content:

```console
export http_proxy=http://proxy.int:3128
export https_proxy=http://proxy.int:3128
export no_proxy=127.0.0.1,localhost
```

Also make sure that your proxy is configured in your .gitconfig to be able to do the initial repo cloning.
See https://gist.github.com/evantoli/f8c23a37eb3558ab8765.

If instead you need to individually set a proxy before installation/upgrade, use the following commands in your terminal:
```console
export http_proxy=http://proxy.int:3128
export https_proxy=http://proxy.int:3128
export no_proxy=127.0.0.1,localhost
./scripts/run-playbook-with-sudo.sh site.yml
```

Use the following syntax for authenticated proxy access:

    export http_proxy=http://USERNAME:PASSWORD@proxy.int:8080/

If you use Debian you need to additionally specify the proxy for apt in:

    sudo vim.tiny /etc/apt/apt.conf.d/proxy.conf

Add the following lines with your proxy and port:
```console
Acquire::http::Proxy "http://proxy_server:port/";
Acquire::https::Proxy "http://proxy_server:port/";
```

If you use authentication:

    Acquire::http::Proxy "http://user:password@proxy_server:port/";

Note that the following domains (and their sub-domains) must be reachable through the proxy:

    cactus.de (and sub-Domains, only for downloading test data, not needed if run with "--skip-tags test")
    ubuntu.com
    canonical.com
    github.com, api.github.com
    githubusercontent.com
    docker.io (and subdomains)
    hasura.io, releases.hasura.io
    postgresql.org
    microsoft.com
    nuget.org, api.nuget.org
    googlechromelabs.github.io
    storage.googleapis.com
    pypi.org
    pythonhosted.org (and sub-domains)
    snapcraft.io, api.snapcraft.io
    snapcraftcontent.com (and sub-domains)

#### For vscode-debugging only - most are needed for downloading extensions
    visualstudio.com (and subdomains)
    vsassets.io (and subdomains)
    digicert.com (and subdomains)
    dot.net (and subdomains) 
    windows.net (and subdomains)
    applicationinsights.azure.com (and subdomains)
    exp-tas.com (and subdomains)

#### Pyhton proxy config

When using `scripts/install-ansible-from-venv.sh`, export the proxy variables described above. Pip automatically honors them; the script does not write a pip configuration file.


In case of timeout issues (you might be behind a security proxy that does intensive scanning), try to install ansible using the command:

          pip --default-timeout=3600 install ansible
          
##### Existing pip configuration

The venv helper preserves any existing pip configuration, including an organization-provided package index. It sets only a command-local download timeout and does not create or change `$HOME/.config/pip/pip.conf`.

If that configuration cannot provide the required packages, have the package-index administrator correct the repository or use an approved index. Do not remove an existing user or OS-managed pip configuration as an installer workaround.

### Parameter "api_no_metadata" to prevent meta data import

e.g. if your hasura metadata file needs to be re-created from scratch, then use the following switch:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "api_no_metadata=yes"
```
### Parameter "force_install" to force installation even though operating system packages are not up2date

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "force_install=yes"
```

### Parameter "allowRepoChangesForRedhat" to allow RedHat repository changes

By default, the installer does not add or enable RedHat repositories. If required packages are not available from the already enabled repositories, prepare the OS repositories outside the installer.

Internally mirrored repositories may use arbitrary repository IDs and do not need the `epel-release` package. The installer checks whether the required packages are already available before inspecting or changing EPEL and CodeReady Builder/CRB configuration. If the configured repositories provide every required package, their names and origin do not matter and their metadata cache is not forcibly refreshed.

Set this parameter only if the installer is allowed to install EPEL and enable CodeReady Builder/CRB on RedHat-like systems:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "allowRepoChangesForRedhat=true"
```

Before installing anything, the installer checks that the packages the current host needs are available from the enabled repositories and aborts with the list of missing packages if they are not. On a middleware server these are `openldap-servers`, `python3-pyOpenSSL`, `python3-cryptography` and the build dependencies of `python-ldap` (`python3-devel`, `python3-ldap`, `openldap-devel`, `cyrus-sasl-devel`, `openssl-devel`); on an importer they are the Python interpreter packages listed below. Several of them are not part of BaseOS/AppStream: `openldap-devel` and `cyrus-sasl-devel` come from CodeReady Builder/CRB and `python3-ldap` from EPEL.

### Python version for the importer on RedHat

The importer runs in its own virtual environment and needs Python 3.9 or newer. RHEL 8 (Python 3.6) and RHEL 9 (Python 3.9) therefore use the parallel installable `python3.11`, while RHEL 10 uses its platform `python3` (Python 3.12), which has no `python3.11` package. The same rule applies to the installer virtual environment created by `scripts/install-ansible-from-venv.sh`.

### Dotnet SDK installation on RedHat

The installer needs the dotnet SDK for the UI and the middleware server. On RedHat-like systems it tries four sources in this order and stops at the first one that works:

1. the already configured repositories (normally RHEL AppStream)
2. the same repositories again after the cached rpms were dropped, pinned to the repositories that offer the package and with their metadata refreshed, retried up to three times for transient errors
3. the Microsoft package repository (`packages.microsoft.com`), which is added and given priority over the configured repositories for `dotnet-*`, `aspnetcore-*` and `netstandard-*` packages - **opt-in only**, see below
4. the Microsoft `dotnet-install.sh` script, which installs into `dotnet_script_install_dir` (`/usr/local/fworch/dotnet`) and links `dotnet_binary_link` (`/usr/bin/dotnet`) - **opt-in only**, see below

Steps 1 and 2 are the only ones that run on a default installation. They install the SDK from the package sources the host already has, which is what a hardened setup wants: the OS team provides `dotnet-sdk-<dotnet_version>` in the local repo before the FWORCH installation and the installer neither adds a repository nor downloads anything.

Step 3 writes `/etc/yum.repos.d/microsoft-prod.repo` and imports the Microsoft RPM GPG key, so it changes the package sources of the host. It is covered by the same parameter as the CRB/EPEL repositories above and is not used unless it is requested explicitly:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "allowRepoChangesForRedhat=true"
```

Step 4 downloads a script from the internet and executes it as root, and it installs the SDK outside package management. It is therefore not used unless it is requested explicitly either:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "allowDotnetScriptInstallForRedhat=true"
```

Without either parameter the installer stops after step 2 and asks for the dotnet SDK matching `dotnet_version` from `inventory/group_vars/all.yml` to be installed manually. An already installed SDK of the required version is detected and left untouched, whether it came from a package or from an earlier script installation.

Two further parameters harden the script fallback wherever it is used, on RedHat and on Debian testing/Debian 13 and newer, where it is the only available source:

- `dotnet_install_script_url` points at the script. Set it to an own reviewed copy to avoid downloading it from `dot.net` at install time.
- `dotnet_install_script_checksum` verifies the downloaded script, for example `sha256:0123abc...`. Upstream publishes no checksum, so the value has to be taken from a reviewed revision. Empty by default, which skips the verification.

An SDK installed by the script does not go into the `/usr/share/dotnet` used by the distribution and Microsoft packages, but into a directory of its own below the installation directory. The uninstall therefore removes it together with the other FWORCH directories and never touches an SDK it does not own. `/usr/bin/dotnet` is only removed if it still points into that directory.

### Package download failures on hardened RedHat hosts

Both the dotnet SDK and the Chromium libraries needed for PDF generation can fail with a download error although `dnf` clearly found the package:

```console
Failed to download packages: libXfixes-5.0.3-16.el9.x86_64: Cannot download, all mirrors were already tried without success
```

The package was resolved from the repository metadata but its rpm could not be fetched, which is a repository access problem on the host rather than a missing package. The installer drops the cached rpms and retries once, because a truncated rpm and a metadata cache left over from an earlier point release both make every mirror fail again. The retry is pinned to the repositories that offer the package, and only their metadata is refreshed: `dnf clean all` would take the metadata of every repository with it and leave the host unable to run `dnf` at all until each of them is reachable again, including repositories that have nothing to do with the package being installed. A permanent refusal - "all mirrors were already tried", a 403 or a 404 - is not retried, because it answers every attempt the same way.

If the retry fails too, the remaining causes are outside the installer:

- the repository is a Satellite/Capsule synced on demand (lazy) that cannot reach its upstream content
- a proxy that allows the repodata but blocks the package download path or large downloads
- a detached or expired subscription entitlement, which answers the content urls with 403

Both error messages quote every attempt with the error it failed with, and the dotnet one adds the content urls `dnf` resolved for the packages, which is what identifies the blocked path or the 403. The same urls can be listed manually, together with the status of the failing one:

```console
dnf download --url --resolve libXfixes
dnf -v install libXfixes
```

Restore the repository access according to your OS repository policy, then rerun the installer.

### Packages missing from the enabled RedHat repositories

A download error is not the only reason a package task fails. If `dnf` does not find the package in the metadata at all it reports it differently:

```console
No match for argument: dotnet-sdk-10.0
```

Here the enabled repositories genuinely do not offer the package, usually because the host is pinned to an older point release, because the repository providing it (normally AppStream) is not enabled, or because the Satellite/Capsule content view was published before the package entered the upstream repository. The versions the host can actually see are listed by:

```console
dnf list --showduplicates dotnet-sdk-10.0
dnf repolist --enabled
```

The error messages of the dotnet SDK and of the Chromium dependency installation quote the error `dnf` reported and show the hints matching it, so the cases can be told apart without rerunning the installer. The hints are selected by the **first** error of the attempt chain, because that is the one describing the problem with the packages being installed: a later attempt can fail on a repository that has nothing to do with them.

### Repository metadata that cannot be refreshed on RedHat

The third case is a repository whose metadata `dnf` cannot read at all:

```console
Failed to download metadata for repo 'epel': Cannot download repomd.xml: All mirrors were tried
```

This fails every `dnf` transaction on the host, whether or not the repository offers the packages being installed, so it is reported with the name of the repository and hints of its own. Either restore access to the reported repository or disable it according to your OS repository policy:

```console
dnf repolist --enabled
dnf makecache --repo=<repo>
```

### Parameter "docker_network" after the Podman migration

This legacy parameter is ignored by the current installer because Hasura now runs with Podman host networking instead of a Docker bridge.

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "docker_network=172.26.0.1/16"
```

### Parameter "install_syslog" allows disabling of separate syslog installation

Default value is install_syslog=yes but if you already have a syslog service running then you can skip syslog installation and configure your existing service manually.

run installation without syslog installation:
```console
./scripts/run-playbook-with-sudo.sh site.yml -e "install_syslog=no"
```

Here is a sample config you can use for configuring your already running syslog:

variables (already set in inventory):
```console
product_name: fworch
middleware_server_syslog_id: "{{ product_name }}.middleware-server"
ui_syslog_id: "{{ product_name }}-ui"
ldap_syslog_id: slapd
```
rsyslog config
```console

  - name: edit rsyslog
    blockinfile:
      path: "/etc/rsyslog.d/30-{{ product_name }}.conf"
      create: yes
      block: |
        # syslog for {{ product_name }}
        # Log {{ product_name }} log messages to file
        local6.warning                 /var/log/{{ product_name }}/error.log
        local6.=info                   /var/log/{{ product_name }}/login_info.log
        local6.debug                   /var/log/{{ product_name }}/debug.log

        if $programname == '{{ product_name }}-database' then /var/log/{{ product_name }}/database.log
        if $programname == '{{ middleware_server_syslog_id }}' then /var/log/{{ product_name }}/middleware.log
        if $programname == '{{ ui_syslog_id }}' then /var/log/{{ product_name }}/ui.log
        if $programname == '{{ ldap_syslog_id }}' then /var/log/{{ product_name }}/ldap.log
        if $programname == '{{ product_name }}-api' then /var/log/{{ product_name }}/api.log
        if $programname startswith '{{ product_name }}-import' then /var/log/{{ product_name }}/importer.log
        if $programname startswith '{{ product_name }}-' and $msg contains "Audit" then /var/log/{{ product_name }}/audit.log
  - name: edit logrotate
    blockinfile:
      path: "/etc/logrotate.d/{{ product_name }}"
      create: yes
      block: |
        /var/log/{{ product_name }}/*.log {
            compress
            maxage 7
            rotate 99
            maxsize 4096k
            missingok
            copytruncate
        }
```

### Parameter "api_docu" to install API documentation

Generating a full hasura (all tables, etc. tracked) API documentation  currently requires
- at least 10 GB total free hdd for test install
- a minimum of 8 GB RAM

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "api_docu=yes"
```

api docu can then be accessed at <https://server/api_schema/index.html>

## User interface 

### Communication modes

The following options exist for communication to the UI:
- standard: with http-->https rewrite and websockets (this is the default value)
- no_ws: do not use websocket connection (in case you have a filtering proxy in your line of communication that does not like ws)
- allow_http: do not rewrite http to https - helpful if you do the TLS termination on a reverse proxy in front of the UI
- no_ws_and_allow_http: combination of the two above

Example:
```console
./scripts/run-playbook-with-sudo.sh site.yml -e "ui_comm_mode=no_ws"
```

### Specifying server name and aliases

To make sure that firewall orchestrator UI webserver responds to the correct DNS name, you may add the following parameters:

Example to set fwodemo.cactus.de as webserver name:
```console
./scripts/run-playbook-with-sudo.sh site.yml -e "ui_server_name='fwodemo.cactus.de'"
```
Example to set fwodemo.cactus.de and two additional aliases as websrver names:
```console
./scripts/run-playbook-with-sudo.sh site.yml -e "ui_server_name=fwodemo.cactus.de ui_server_alias=' fwo1.cactus.de fwo2.cactus.de'"
```

### Server Alias string

To be able to configure your webserver name, you may add the following parameter:

Example to set fwodemo.cactus.de as websrver name:
```console
./scripts/run-playbook-with-sudo.sh site.yml -e "ui_server_alias='fwodemo.cactus.de'"
```
Example to set fwodemo.cactus.de and fwo2.cactus.de as websrver names:
```console
./scripts/run-playbook-with-sudo.sh site.yml -e "ui_server_alias='fwodemo.cactus.de fwo2.cactus.de'"
```

## Distributed setup with multiple servers

You have to edit inventory/hosts.yml according to your needs

install-srv is the local machine the installation is started from. By default FWO is installed on this server

If you want to use distributed machines add them like ui-srv and test-srv in the following example

```console
all:
  hosts:
    install-srv:
      ansible_connection: local
      ansible_host: localhost
    ui-srv:
      ansible_connection: ssh
      ansible_host: 192.168.121.2
    test-srv:
      ansible_connection: ssh
      ansible_host: test.example.com
```

The names you define (like ui-srv and test-srv) are abitrary and only relevant in the hosts.yml file.

After you defined additional distributed servers you have to add them to the host groups in hosts.yml

```console
  children:
    frontends:
      hosts:
        ui-srv:
    databaseserver:
      hosts:
        install-srv:
    apiserver:
      hosts:
        install-srv:
    importers:
      hosts:
        install-srv:
    middlewareserver:
      hosts:
        install-srv:
    sampleserver:
      hosts:
        test-srv:
    testservers:
      hosts:
        test-srv:
    logserver:
      hosts:
        install-srv:
```

## old

if you want to distribute functionality to different hosts:

modify firewall-orchestrator/inventory/hosts to your needs

change ip addresses) of hosts to install to, e.g.

```console
isofront ansible_host=10.5.5.5
isoback ansible_host=10.5.10.10
```

put the hosts into the correct section (`[frontends]`, `[backends]`, `[importers]`)

make sure all target hosts meet the requirements for ansible (user with pub key auth & full sudo rights)

enter the address of the database backend server, e.g.

```console
fworch database hostname              10.5.10.10
```

modify /etc/postgresql/x.y/main/pg_hba.conf to allow dbadmin access from web frontend(s), e.g.

```console

host    all         dbadmin             10.5.10.10/32          md5
```
