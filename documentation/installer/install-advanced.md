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

Remember if your server resides behind a proxy that you will have to set the proxy for pip as follows before installing ansible:

         pip config set global.proxy http://proxy:3128


In case of timeout issues (you might be behind a security proxy that does intensive scanning), try to install ansible using the command:

          pip --default-timeout=3600 install ansible
          
##### issues with existing pip config

In case of errors with existing pip config, do not use the script to create the venv but proceed as follows:

remove any local pip config and install manually from the cloned repository directory:
    
    rm -f $HOME/.config/pip/pip.conf
    cd firewall-orchestrator
    sudo apt-get update
    sudo apt-get install --yes python3-venv
    python3 -m venv --clear $HOME/.fwo/installer-venv
    source $HOME/.fwo/installer-venv/bin/activate
    pip install -r requirements.txt
    if [ -f scripts/requirements.txt ]; then pip install -r scripts/requirements.txt; fi
    pip install ansible
    ansible-galaxy collection install -r collections/requirements.yml -p collections --force

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

Set this parameter only if the installer is allowed to install EPEL and enable CodeReady Builder/CRB on RedHat-like systems:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "allowRepoChangesForRedhat=true"
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
