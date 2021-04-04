# installation options for testing purposes

always change into the firewwall-orchestrator directory before starting the installation!

## Install parameters

### Test - with fixed jwt keys - not for production use

Use the test switch to always use the same fixed jwt generation keys

```console
ansible-playbook/ site.yml -e "testkeys=yes" -K
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
ansible-playbook/ site.yml -e "debug_level='2'" -K
```
### Testing

To only run tests (for an existing installation) use tags as follows:

```console
ansible-playbook/ site.yml --tags test -K
```

### Parameter "api_no_metadata" to prevent meta data import

e.g. if your hasura metadata file needs to be re-created from scratch, then use the following switch::

```console
ansible-playbook -e "api_no_metadata=yes" site.yml -K
```

### Parameter "without_sample_data" to not create sample data (i.e. in production)

The following command prevents the creation of sample data in the database:

```console
ansible-playbook -e "without_sample_data=yes" site.yml -K
```

### Parameter "connect_sting" to add Cactus test firewall CP R8x

The following command adds the sting test firewall to your fw orch system (needs VPN tunnel to Cactus)

```console
ansible-playbook -e "connect_sting=yes" site.yml -K
```

### Parameter "second_ldap_db" to install second ldap database

if you want to install a second (local) ldap database "dc=example,dc=com"

```console
cd firewall-orchestrator; ansible-playbook -e "second_ldap_db=yes" site.yml -K
```
