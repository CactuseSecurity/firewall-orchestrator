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

e.g. with IP 1.2.3.4, listening on port 3128<br>

```console
ansible-playbook -e "http_proxy=http://1.2.3.4:3128 https_proxy=http://1.2.3.4:3128" site.yml -K
```
### Parameter "ui_php" to additionally install old php UI

With the following option the old php based user interface will be installed in addition to the new one at ui_php_web_port (defaults to 8443):

```console
ansible-playbook -e "ui_php=1 ui_php_web_port=44310" site.yml -K
```

### Parameter "api_no_metadata" to prevent meta data import

e.g. if your hasura metadata file needs to be re-created from scratch, then use the following switch::

```console
ansible-playbook -e "api_no_metadata=yes" site.yml -K
```

### Parameter "api_docu" to install API documentation

Generating a full hasura (all tables, etc. tracked) API documentation  currently requires
- 2.3 GB additional hdd space (at least 10 GB total for test install)
- a minimum of 8 GB RAM
- 4 minutes to generate

```console
cd firewall-orchestrator; ansible-playbook -e "api_docu=yes" site.yml -K
```

api docu can then be accessed at <https://server/api_schema/index.html>

### Parameter "audit_user" to add an audit user to ldap db

if you want to have an extra read-only audit-user called e.g. auditor1, use the following switch:

```console
cd firewall-orchestrator; ansible-playbook -e "audit_user=auditor1" site.yml -K
```

The initial password will be "fworch.2"

## Distributed setup with multiple servers

if you want to distribute functionality to different hosts:

modify firewall-orchestrator/inventory/hosts to your needs

change ip addresses) of hosts to install to, e.g.

```console
isofront ansible_host=10.5.5.5
isoback ansible_host=10.5.10.10
```

put the hosts into the correct section (`[frontends]`, `[backends]`, `[importers]`)

make sure all target hosts meet the requirements for ansible (user with pub key auth & full sudo rights)

modify isohome/etc/iso.conf on frontend(s):

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
