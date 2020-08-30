# Advanced installation options


## Install parameters
### Installation behind a proxy (no direct Internet connection)

e.g. with IP 1.2.3.4, listening on port 3128<br>
note: this does not yet work 100%

```console
cd firewall-orchestrator; ansible-playbook -i inventory -e "http_proxy=http://1.2.3.4:3128 https_proxy=http://1.2.3.4:3128" site.yml -K
```

### Debugging

Set debug parameter to "true" for extended debugging info during installation.

```console
cd firewall-orchestrator; ansible-playbook -i inventory/ site.yml -e "debug_level='2'" -K
```

### Parameter "ui_php" to additionally install old php UI

With the following option the old php based user interface will be installed in addition to the new one at ui_php_web_port (defaults to 8443):

```console
cd firewall-orchestrator; ansible-playbook -i inventory -e "ui_php=1 ui_php_web_port=44310" site.yml -K
```

### Parameter "clean_install" to start with fresh database

if you want to drop the database and re-install from scratch, simply add the variable clean_install as follows:

```console
cd firewall-orchestrator; ansible-playbook -i inventory -e "clean_install=1" site.yml -K
```

### Parameter "api_no_metadata" to prevent meta data import

e.g. if your hasura metadata file needs to be re-created from scratch, then use the following switch::

```console
cd firewall-orchestrator; ansible-playbook -i inventory -e "api_no_metadata=1" site.yml -K
```

### Parameter "without_sample_data" to not create sample data (i.e. in production)

The following command prevents the creation of sample data in the database:

```console
cd firewall-orchestrator; ansible-playbook -i inventory -e "without_sample_data=1" site.yml -K
```

### Parameter "connect_sting" to add Cactus test firewall CP R8x

The following command adds the sting test firewall to your fw orch system (needs VPN tunnel to Cactus)

```console
cd firewall-orchestrator; ansible-playbook -i inventory -e "connect_sting=1" site.yml -K
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
host    all         dbadmin             10.5.10.10/32            md5
```
