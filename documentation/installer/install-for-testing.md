# installation options for testing purposes

always change into the firewwall-orchestrator directory before starting the installation!

## testkeys - not for production use

Use the testkeys switch to always use the same fixed secrets.
This includes:
- jwt generation keys
- API hasura admin secret
- UI admin user
Note: the relevant secrets are displayed at the very end of the installation. They can also be found in the etc/secrets directory.

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "testkeys=yes"
```

A static jwt key helps with debugging c# code in visual studio (code) - you can use a static backend (ldap & api) with these keys.

You need to
- add the config file and keys once on your local development machine
- set up an ssh tunnel to the back end machine

        sudo ssh -i /home/user/.ssh/id_rsa -p 10222 user@localhost -L 9443:localhost:9443 -L 636:localhost:636

    or to the central test server

        sudo ssh -i /home/user/.ssh/id_rsa -p 60333 user@server.de -L 9443:localhost:9443 -L 636:localhost:636

## Debugging

Set dotnet installation mode to "debug" as follows (default = Release)
### Debugging dotnet applications
```console
./scripts/run-playbook-with-sudo.sh site.yml -e "dotnet_mode=Debug"
```

Set debug level for extended debugging info during installation.
```console
./scripts/run-playbook-with-sudo.sh site.yml -e "debug_level='2'"
```

## Running unit tests after installation/upgrade

To only run unit tests (for an existing installation only to be used in comination with installation_mode=upgrade) use tags as follows:

```console
./scripts/run-playbook-with-sudo.sh site.yml --tags unittests
```

## Running integration tests after installation/upgrade

To only run integration tests (for an existing installation only to be used in comination with installation_mode=upgrade) use tags as follows:

```console
./scripts/run-playbook-with-sudo.sh site.yml --tags integrationtests
```

## Running installation without any tests

```console
./scripts/run-playbook-with-sudo.sh site.yml --skip-tags unittests,integrationtests
```

## Parameter "api_no_metadata" to prevent meta data import

e.g. if your hasura metadata file needs to be re-created from scratch, then use the following switch::

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "api_no_metadata=yes"
```

## Parameter "add_demo_data" to avoid creation of sample data (i.e. in production)

The following command prevents the creation of sample data in the database:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "add_demo_data=no"
```

note: demo/sample data can also be removed via settings menues.

### Parameter "second_ldap_db" to install second ldap database

if you want to install a second ldap database "dc=example,dc=com"

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "second_ldap_db=yes"
```

### Parameter "sample_data_rate" to ramp up sample data

if you want to create sample-data changes every minute set sample_data_rate to high

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "sample_data_rate=high"
```
### Parameter "audit_user" to add an audit user to ldap db - useful for demo installation

if you want to have an extra read-only audit-user called e.g. auditor1, use the following command for installation:

```console
./scripts/run-playbook-with-sudo.sh site.yml -e "audit_user=auditor1 auditor_initial_pwd=<pwd>"
```
