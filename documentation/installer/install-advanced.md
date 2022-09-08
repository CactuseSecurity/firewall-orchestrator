# Advanced installation options

always change into the firewwall-orchestrator directory before starting the installation!

## Install parameters

### Installation mode parameter

The following switch can be used to set the type of installation to perform

```console
ansible-playbook -e "installation_mode=upgrade" site.yml -K
```

If you want to drop the database and re-install from scratch, do the following:

```console
ansible-playbook -e "installation_mode=uninstall" site.yml -K
ansible-playbook -e "installation_mode=new" site.yml -K
```

installation_mode options:
- new (default) - assumes that no fworch is installed on the target devices - fails if it finds an installation
- uninstall     - uninstalls the product including any data (database, ldap, files)!
- upgrade       - installs on top of an existing system preserving any existing data in ldap, database, api; removes all files from target and copies latest sources instead
                

### Installation behind a proxy (no direct Internet connection)

By default, during installation or upgrade the proxy settings are read from the OS environment of the installer host.
For example you may have a global system-wide config file /etc/profile.d/proxy.sh with the following content:

```console
export http_proxy=http://proxy.int:3128
export https_proxy=http://proxy.int:3128
export no_proxy=127.0.0.1,localhost
```

Also make sure that your proxy is configured in your .gitconfig to be able to do the initial repo cloning.
See https://gist.github.com/evantoli/f8c23a37eb3558ab8765.

If instead you need to individually set a proxy before installation/upgrade, use the following comamnds in your terminal:
```console
export http_proxy=http://proxy.int:3128
export https_proxy=http://proxy.int:3128
export no_proxy=127.0.0.1,localhost
ansible-playbook site.yml -K
```

Use the following syntax for authenticated proxy access:

    export http_proxy=http://USERNAME:PASSWORD@proxy.int:8080/

Note that the following domains must be reachable through the proxy:

    cactus.de (only for downloading test data, not needed if run with "--skip-tags test")
    ubuntu.com
    canonical.com
    github.com
    githubusercontent.com
    docker.com
    docker.io
    hasura.io
    ansible.com
    postgresql.org
    microsoft.com     
    nuget.org

NB: for vscode-debugging, you also need access to

    visualstudio.com

### Parameter "api_no_metadata" to prevent meta data import

e.g. if your hasura metadata file needs to be re-created from scratch, then use the following switch:

```console
ansible-playbook -e "api_no_metadata=yes" site.yml -K
```

### Parameter "install_syslog" allows disabling of separate syslog installation

Default value is install_syslog=yes but if you already have a syslog service running then you can skip syslog installation and configure your existing service manually.

run installation without syslog installation:
```console
ansible-playbook -e "install_syslog=no" site.yml -K
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
        # only for devsrv:
        if $programname == '{{ product_name }}-webhook' then /var/log/{{ product_name }}/webhook.log

  - name: edit logrotate
    blockinfile:
      path: "/etc/logrotate.d/{{ product_name }}"
      create: yes
      block: |
        /var/log/{{ product_name }}/*.log {
            compress
            maxage 7
            rotate 99
            size=+4096k
            missingok
            copytruncate
            sharedscripts
                prerotate
                        systemctl stop {{ product_name }}-importer-legacy.service >/dev/null 2>&1
                endscript
                postrotate
                        systemctl start {{ product_name }}-importer-legacy.service >/dev/null 2>&1
                endscript
        }
```

### Parameter "api_docu" to install API documentation

Generating a full hasura (all tables, etc. tracked) API documentation  currently requires
- at least 10 GB total free hdd for test install
- a minimum of 8 GB RAM

```console
ansible-playbook -e "api_docu=yes" site.yml -K
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
ansible-playbook -e "ui_comm_mode=no_ws" site.yml -K
```

### Specifying server name and aliases

To make sure that firewall orchestrator UI webserver responds to the correct DNS name, you may add the following parameters:

Example to set fwodemo.cactus.de as webserver name:
```console
ansible-playbook -e "ui_server_name='fwodemo.cactus.de'" site.yml -K
```
Example to set fwodemo.cactus.de and two additional aliases as websrver names:
```console
ansible-playbook -e "ui_server_name=fwodemo.cactus.de ui_server_alias=' fwo1.cactus.de fwo2.cactus.de'" site.yml -K
```

### Server Alias string

To be able to configure your webserver name, you may add the following parameter:

Example to set fwodemo.cactus.de as websrver name:
```console
ansible-playbook -e "ui_server_alias='fwodemo.cactus.de'" site.yml -K
```
Example to set fwodemo.cactus.de and fwo2.cactus.de as websrver names:
```console
ansible-playbook -e "ui_server_alias='fwodemo.cactus.de fwo2.cactus.de'" site.yml -K
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
    backendserver:
      hosts:
        install-srv:
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

modify isohome/etc/iso.conf on frontend(s) - only needed for legacy (perl-based) importers:

enter the address of the database backend server, e.g.

```console
fworch database hostname              10.5.10.10
```

modify /etc/postgresql/x.y/main/pg_hba.conf to allow secuadmins access from web frontend(s), e.g.

```console
host    all         +secuadmins         127.0.0.1/32           md5
host    all         +secuadmins         10.5.5.5/32            md5
host    all         dbadmin             10.5.10.10/32          md5
```
