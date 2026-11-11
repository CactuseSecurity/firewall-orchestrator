# all the changes needed for distributed setup of modules

## inventory/hosts

add all hosts involved, e.g.

    fworch-front ansible_host=10.1.1.81
    fworch-back ansible_host=10.1.1.83
    fworch-side ansible_host=10.1.1.82

    [frontends]
    fworch-front

    [backendserver]
    fworch-back

    [apiserver]
    fworch-side

    [importers]
    fworch-side

    [authserver]
    fworch-back
    # does not work with other hosts at the moment

    [sampleserver]
    fworch-side


## inventory/group_vars/all.yml

Enable the existing network listener behavior for services split across hosts:

    distributed_install: true

Nothing else has to be set here. Every endpoint name - the database host, the API, the
middleware and the UI - is derived from the host names in `inventory/hosts.yml` above,
and `distributed_install: true` is what makes the internal clients address those names
instead of loopback. Name the hosts there by the DNS names their certificates are
issued for; see `documentation/certificates.md`.

## roles/database/tasks/main.yml

- change pg_hba.conf entries to allow acces via network
- `distributed_install: true` makes PostgreSQL listen on the configured API
  network address; no manual postgresql.conf listener change is needed

## roles/auth/tasks/main.yml - needs some work

this does not work remotely (auth host <> db host), as there is no postgres user on a non-db machine:

    - name: copy authentication sql file
    copy:
        src: pre_auth_functions.sql
        dest: "{{ fworch_home }}/auth/"
        owner: "{{ fworch_user }}"
        group: "{{ fworch_group }}"
    become: yes

    - name: create functions needed during authentication
    command: 'psql -d {{ fworch_db_name }} -c "\i {{ fworch_home }}/auth/pre_auth_functions.sql"'
    become: yes
    become_user: postgres

either do this on the db machine directly or run it via postgresql_query (with ansible 2.8ff)

### more

read variables from config to know what to listen on?

## roles/frontend - needs some work

read config to know where the auth server is listening!

## roles/api - needs some work

- "read jwt_secret_key from file" has to work both on ui, auth, api hosts
- need to generate it once and than copy to "all" hosts in etc_secrets dir
