# Advanced installation options

always change into the firewwall-orchestrator directory before starting the installation!

## Install parameters

### Installation mode parameter

The following switch can be used to set the type of installation to perform

```console
ansible-playbook -i inventory -e "installation_mode=upgrade" site.yml -K
```

installation_mode options:
- new (default) - assumes that no fworch is installed on the target devices - fails if it finds an installation
- uninstall     - uninstalls the product including any data (database, ldap, files)!
- upgrade       - installs on top of an existing system preserving any existing data in ldap, database, api; removes all files from target and copies latest sources instead
                

### Installation behind a proxy (no direct Internet connection)

e.g. with IP 1.2.3.4, listening on port 3128<br>
note: this does not yet work 100%

```console
ansible-playbook -i inventory -e "http_proxy=http://1.2.3.4:3128 https_proxy=http://1.2.3.4:3128" site.yml -K
```

### Test - with fixed jwt keys - not for production use

Use the test switch to always use the same fixed jwt generation keys

```console
ansible-playbook -i inventory/ site.yml -e "testkeys=yes" -K
```

This helps with debugging c# code in visual studio (code) - you can use a static backend (ldap & api) with these keys.

You need to
- add the config file and keys once on your local development machine
- set up an ssh tunnel to the back end machine

        sudo ssh -i /home/tim/.ssh/id_rsa -p 10222 tim@localhost -L 9443:localhost:9443 -L 636:localhost:636

    or to the central test server

        sudo ssh -i /home/tim/.ssh/id_rsa -p 60333 developer@cactus.de -L 9443:localhost:9443 -L 636:localhost:636

### Debugging

Set debug level for extended debugging info during installation.

```console
ansible-playbook -i inventory/ site.yml -e "debug_level='2'" -K
```
### Testing

To only run tests (for an existing installation) use tags as follows:

```console
ansible-playbook -i inventory/ site.yml --tags test -K
```

### Parameter "ui_php" to additionally install old php UI

With the following option the old php based user interface will be installed in addition to the new one at ui_php_web_port (defaults to 8443):

```console
ansible-playbook -i inventory -e "ui_php=1 ui_php_web_port=44310" site.yml -K
```

### Parameter "clean_install" to start with fresh database

if you want to drop the database and re-install from scratch, simply add the variable clean_install as follows:
NB: this switch has been removed in favor of the "cleaner" method:

```console
ansible-playbook -i inventory -e "installation_mode=uninstall" site.yml -K
ansible-playbook -i inventory -e "installation_mode=new" site.yml -K
```


```console
ansible-playbook -i inventory -e "clean_install=1" site.yml -K
```

### Parameter "api_no_metadata" to prevent meta data import

e.g. if your hasura metadata file needs to be re-created from scratch, then use the following switch::

```console
ansible-playbook -i inventory -e "api_no_metadata=1" site.yml -K
```

### Parameter "without_sample_data" to not create sample data (i.e. in production)

The following command prevents the creation of sample data in the database:

```console
ansible-playbook -i inventory -e "without_sample_data=1" site.yml -K
```

### Parameter "connect_sting" to add Cactus test firewall CP R8x

The following command adds the sting test firewall to your fw orch system (needs VPN tunnel to Cactus)

```console
ansible-playbook -i inventory -e "connect_sting=1" site.yml -K
```

### Parameter "api_docu" to install API documentation

Generating a full hasura (all tables, etc. tracked) API documentation  currently requires
- 2.3 GB additional hdd space (at least 10 GB total for test install)
- a minimum of 8 GB RAM
- 4 minutes to generate

```console
cd firewall-orchestrator; ansible-playbook -i inventory -e "create api_docu=yes" site.yml -K
```

api docu can then be accessed at <https://server/api_schema/index.html>

### Parameter "second_ldap_db" to install second ldap database

if you want to install a second ldap database "dc=example,dc=com"

```console
cd firewall-orchestrator; ansible-playbook -i inventory -e "second_ldap_db=yes" site.yml -K
```


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
