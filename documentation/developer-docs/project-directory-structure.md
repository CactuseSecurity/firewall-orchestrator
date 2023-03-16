# project directory structure, install routines

- This project is provisioned by Ansible
- The playbook (main) is site.yml
- The hosts and its associated roles are described in the following sections
- Corresponding variables are defined in the inventory directory
- All servers and their ip addresses are defined in inventory/hosts

## hosts: all
- This host group defines roles and variables for all hosts
- A list of important variables from inventory/all

```console
fworch_user: fworch
fworch_home: "{{ fworch_parent_dir }}/{{ fworch_user }}"
sample_config_user: fworchsample
sample_config_user_home: "/home/{{ sample_config_user }}"
```

- The only role defined in all is iso-common
- The tasks of iso-common include

  - creating {{ fworch_parent_dir }}/fworch
  - creating user fworch
  - adding file iso.conf to {{ fworch_parent_dir }}/fworch/etc
  - creating logs
  
## hosts: backends

- By default, this is localhost for demo purposes
- This should be changed to fit the customers infrastructure
- Important variables from inventory/backends include
  
```console
database_dir: /var/lib/pgsql/data
```

- The roles to be installed on the backend are
  - docker
  - database
  - api
  - auth
  - openldap-server
  
- The role docker executes the tasks
  - downloads and installs Docker and related packages
  - creates local config directory {{ fworch_home }}/.docker and adds config.json

- The role database executes the tasks
  - installs postgresql DBMS
  - copies install directory (roles/backend/files/install) to {{ fworch_home }}. It contains the database
  - removes all containers to make sure the database can be dropped (otherwise the hasura process blocks dropping the database)
  - copies and executes database install scripts
  - sets passwords for database users
  
- The role api executes the tasks
  1. create the directories
```yaml
- name: hasura basic file setup 
  import_tasks: hasura-files.yml

- name: hasura install 
  import_tasks: hasura-install.yml

- name: hasura basic config
  import_tasks: hasura-basic-config.yml

- name: create query_collection and allow-list 
  import_tasks: api-query-collection.yml

- name: api create documentation
  import_tasks: hasura-create-docu.yml
  when: "api_docu is defined and api_docu == 'yes'"
```
  2. sets up hasura in {{ api_home }}
