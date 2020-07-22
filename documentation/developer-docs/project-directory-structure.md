# project directory structure, install routines

- This project is provisioned by Ansible
- The playbook (main) is site.yml
- The hosts and its associated roles are described in the following sections
- Corresponding variables are defined in the inventory directory
- All servers and their ip addresses are defined in inventory/hosts


## hosts: all

- This host group defines roles and variables for all hosts
- A list of important variables from inventory/all

  ```
  - iso_user: itsecorg
  - iso_home: "/usr/share/{{ iso_user }}"
  - sample_config_user: isosample
  - sample_config_user_home: "/home/{{ sample_config_user }}"
  
  ```
 
- The only role defined in all is iso-common
- The tasks of iso-common include

  - creating /usr/share/itsecorg
  - creating user itsecorg
  - adding file iso.conf to /usr/share/itsecorg/etc
  - creating logs
  
## hosts: backends

  - By default, this is isosrv (localhost) for demo purposes
  - This should be changed to fit the customers infrastructure

- Important variables from inventory/backends include
  
    ```
    - database_dir: /var/lib/pgsql/data
    
    ```

- The roles to be installed on backends are

  - docker
  - backend
  - api
  - auth
  - openldap-server
  
- The role docker executes the tasks

  - downloads and installs Docker and related packages
  - creates local config directory {{ iso_home }}/.docker and adds config.json

- The role database executes the tasks

  - installs postgresql DBMS
  - copies install directory (roles/backend/files/install) to {{ iso_home }}. It contains the database
  - removes all containers to make sure the database can be dropped (otherwise the hasura process blocks dropping the database)
  - copies and executes database install scripts
  - sets passwords for database users
  
- The role api executes the tasks
  - defines the directories
      ```
      - api_home="{{ iso_home }}/api"
      - hasura_bin="/usr/local/bin/hasura"
      ```
  - sets up hasura in {{ api_home }}
  
