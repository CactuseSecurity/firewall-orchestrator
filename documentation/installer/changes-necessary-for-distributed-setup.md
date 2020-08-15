# all the changes needed for distributed setup of modules

## inventory/hosts

add all hosts involved, e.g.

    isofront ansible_host=10.1.1.81
    isoback ansible_host=10.1.1.83
    isoside ansible_host=10.1.1.82

    [frontends]
    isofront

    [backendserver]
    isoback

    [apiserver]
    isoback

    [importers]
    isoside

    [authserver]
    isoback
    # does not work with other hosts at the moment

    [sampleserver]
    isoside


## inventory/all

set specific IP or hostname for database host, e.g.

    fworch_db_host: 127.0.0.1


## roles/auth/tasks/main.yml

this does not work remotely (auth host <> db host), as there is no postgres user on a non-db machine:

    - name: copy authentication sql file
    copy:
        src: pre_auth_functions.sql
        dest: "{{ fworch_home }}/auth/"
        owner: "{{ fworch_user }}"
        group: "{{ fworch_user }}"
    become: yes

    - name: create functions needed during authentication
    command: 'psql -d {{ fworch_db_name }} -c "\i {{ fworch_home }}/auth/pre_auth_functions.sql"'
    become: yes
    become_user: postgres

either do this on the db machine directly or run it via postgresql_query (with ansible 2.8ff)

## roles/frontend

read config to know where the auth server is listening!