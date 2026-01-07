Installer orchestration is implemented with Ansible playbooks, inventory, and role definitions. The files below cover the top-level entry points, inventory structure, and each role's handlers, tasks, templates, and variables. Summaries are grouped by role to help agents locate provisioning and upgrade steps quickly.

## Entry points

### `site.yml`
Top-level Ansible playbook that orchestrates installation and upgrades across all host groups. It applies roles in the correct order to deploy the full stack.

### `ansible.cfg`
Ansible configuration for the installer runs. Sets defaults such as inventory location, roles path, and execution behavior.

## Inventory

### `inventory/group_vars/all.yml`
Group variables for the all inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/group_vars/apiserver.yml`
Group variables for the apiserver inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/group_vars/cloud.yml`
Group variables for the cloud inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/group_vars/databaseserver.yml`
Group variables for the databaseserver inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/group_vars/frontends.yml`
Group variables for the frontends inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/group_vars/importers.yml`
Group variables for the importers inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/group_vars/middlewareserver.yml`
Group variables for the middlewareserver inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/group_vars/sampleserver.yml`
Group variables for the sampleserver inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/group_vars/testservers.yml`
Group variables for the testservers inventory group. Defines role settings, ports, and feature toggles applied to that group.

### `inventory/hosts.yml`
Inventory file defining hosts and their group membership. It maps nodes to groups such as frontends, apiserver, and databaseserver.

## Roles

### api

#### handlers

##### `roles/api/handlers/main.yml`
Main handlers for the api role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/api/tasks/api-apache-install-and-setup.yml`
Task file for the api role handling api apache install and setup. Included by the role when that step is needed in the installer workflow.

##### `roles/api/tasks/api-create-docu.yml`
Task file for the api role handling api create docu. Included by the role when that step is needed in the installer workflow.

##### `roles/api/tasks/hasura-install.yml`
Task file for the api role handling hasura install. Included by the role when that step is needed in the installer workflow.

##### `roles/api/tasks/main.yml`
Main task list for the api role. It orchestrates the role flow and includes additional task files.

##### `roles/api/tasks/run-upgrades.yml`
Task file for the api role handling run upgrades. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/api/templates/fworch-hasura-docker-api.service.j2`
Jinja2 template for fworch hasura docker api.service.j2 used by the api role. Renders configuration or service files during installation.

##### `roles/api/templates/httpd.conf.j2`
Jinja2 template for httpd.conf.j2 used by the api role. Renders configuration or service files during installation.

### common

#### tasks

##### `roles/common/tasks/conf_file_creator.yml`
Task file for the common role handling conf file creator. Included by the role when that step is needed in the installer workflow.

##### `roles/common/tasks/global-apache2-config.yml`
Task file for the common role handling global apache2 config. Included by the role when that step is needed in the installer workflow.

##### `roles/common/tasks/install-api-calls.yml`
Task file for the common role handling install api calls. Included by the role when that step is needed in the installer workflow.

##### `roles/common/tasks/install_syslog.yml`
Task file for the common role handling install syslog. Included by the role when that step is needed in the installer workflow.

##### `roles/common/tasks/last_commit_id_file_creator.yml`
Task file for the common role handling last commit id file creator. Included by the role when that step is needed in the installer workflow.

##### `roles/common/tasks/main.yml`
Main task list for the common role. It orchestrates the role flow and includes additional task files.

##### `roles/common/tasks/maintenance-site.yml`
Task file for the common role handling maintenance site. Included by the role when that step is needed in the installer workflow.

##### `roles/common/tasks/redhat_preps.yml`
Task file for the common role handling redhat preps. Included by the role when that step is needed in the installer workflow.

##### `roles/common/tasks/run-upgrades.yml`
Task file for the common role handling run upgrades. Included by the role when that step is needed in the installer workflow.

##### `roles/common/tasks/uninstall.yml`
Task file for the common role handling uninstall. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/common/templates/httpd-maintenance.conf`
Jinja2 template for httpd maintenance.conf used by the common role. Renders configuration or service files during installation.

##### `roles/common/templates/iso.conf.j2`
Jinja2 template for iso.conf.j2 used by the common role. Renders configuration or service files during installation.

### database

#### tasks

##### `roles/database/tasks/create-ro-user.yml`
Task file for the database role handling create ro user. Included by the role when that step is needed in the installer workflow.

##### `roles/database/tasks/create-users.yml`
Task file for the database role handling create users. Included by the role when that step is needed in the installer workflow.

##### `roles/database/tasks/install-database.yml`
Task file for the database role handling install database. Included by the role when that step is needed in the installer workflow.

##### `roles/database/tasks/main.yml`
Main task list for the database role. It orchestrates the role flow and includes additional task files.

##### `roles/database/tasks/redhat_preps.yml`
Task file for the database role handling redhat preps. Included by the role when that step is needed in the installer workflow.

##### `roles/database/tasks/run-unit-tests.yml`
Task file for the database role handling run unit tests. Included by the role when that step is needed in the installer workflow.

##### `roles/database/tasks/unused-add-tablespace.yml`
Task file for the database role handling unused add tablespace. Included by the role when that step is needed in the installer workflow.

##### `roles/database/tasks/upgrade-database.yml`
Task file for the database role handling upgrade database. Included by the role when that step is needed in the installer workflow.

### docker

#### handlers

##### `roles/docker/handlers/main.yml`
Main handlers for the docker role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/docker/tasks/main.yml`
Main task list for the docker role. It orchestrates the role flow and includes additional task files.

##### `roles/docker/tasks/run-upgrades.yml`
Task file for the docker role handling run upgrades. Included by the role when that step is needed in the installer workflow.

##### `roles/docker/tasks/set-docker-daemon-proxy.yml`
Task file for the docker role handling set docker daemon proxy. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/docker/templates/unused_docker-config.json.j2`
Jinja2 template for unused docker config.json.j2 used by the docker role. Renders configuration or service files during installation.

##### `roles/docker/templates/unused_docker_config.j2`
Jinja2 template for unused docker config.j2 used by the docker role. Renders configuration or service files during installation.

### finalize

#### tasks

##### `roles/finalize/tasks/main.yml`
Main task list for the finalize role. It orchestrates the role flow and includes additional task files.

##### `roles/finalize/tasks/run-upgrades.yml`
Task file for the finalize role handling run upgrades. Included by the role when that step is needed in the installer workflow.

### importer

#### handlers

##### `roles/importer/handlers/main.yml`
Main handlers for the importer role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/importer/tasks/fetch-importer-pwd.yml`
Task file for the importer role handling fetch importer pwd. Included by the role when that step is needed in the installer workflow.

##### `roles/importer/tasks/main.yml`
Main task list for the importer role. It orchestrates the role flow and includes additional task files.

##### `roles/importer/tasks/run-upgrades.yml`
Task file for the importer role handling run upgrades. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/importer/templates/fworch-importer-api.service.j2`
Jinja2 template for fworch importer api.service.j2 used by the importer role. Renders configuration or service files during installation.

##### `roles/importer/templates/fworch-importer-legacy.service.j2`
Jinja2 template for fworch importer legacy.service.j2 used by the importer role. Renders configuration or service files during installation.

### lib

#### handlers

##### `roles/lib/handlers/main.yml`
Main handlers for the lib role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/lib/tasks/install_dot_net.yml`
Task file for the lib role handling install dot net. Included by the role when that step is needed in the installer workflow.

##### `roles/lib/tasks/install_puppeteer.yml`
Task file for the lib role handling install puppeteer. Included by the role when that step is needed in the installer workflow.

##### `roles/lib/tasks/main.yml`
Main task list for the lib role. It orchestrates the role flow and includes additional task files.

### middleware

#### handlers

##### `roles/middleware/handlers/main.yml`
Main handlers for the middleware role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/middleware/tasks/create_auth_secrets.yml`
Task file for the middleware role handling create auth secrets. Included by the role when that step is needed in the installer workflow.

##### `roles/middleware/tasks/install_and_run_mw_service.yml`
Task file for the middleware role handling install and run mw service. Included by the role when that step is needed in the installer workflow.

##### `roles/middleware/tasks/main.yml`
Main task list for the middleware role. It orchestrates the role flow and includes additional task files.

##### `roles/middleware/tasks/mw_apache_install_and_setup.yml`
Task file for the middleware role handling mw apache install and setup. Included by the role when that step is needed in the installer workflow.

##### `roles/middleware/tasks/run-upgrades.yml`
Task file for the middleware role handling run upgrades. Included by the role when that step is needed in the installer workflow.

##### `roles/middleware/tasks/set_initial_ldap_tree.yml`
Task file for the middleware role handling set initial ldap tree. Included by the role when that step is needed in the installer workflow.

##### `roles/middleware/tasks/upgrade_ldap_tree.yml`
Task file for the middleware role handling upgrade ldap tree. Included by the role when that step is needed in the installer workflow.

##### `roles/middleware/tasks/upgrade_ldif_file.yml`
Task file for the middleware role handling upgrade ldif file. Included by the role when that step is needed in the installer workflow.

##### `roles/middleware/tasks/upgrade_modify_routine.yml`
Task file for the middleware role handling upgrade modify routine. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/middleware/templates/fworch-middleware.service.j2`
Jinja2 template for fworch middleware.service.j2 used by the middleware role. Renders configuration or service files during installation.

##### `roles/middleware/templates/httpd.conf`
Jinja2 template for httpd.conf used by the middleware role. Renders configuration or service files during installation.

##### `roles/middleware/templates/ldif_files/tree_level_0.ldif.j2`
Jinja2 template for tree level 0.ldif.j2 used by the middleware role. Renders configuration or service files during installation.

##### `roles/middleware/templates/ldif_files/tree_level_1.ldif.j2`
Jinja2 template for tree level 1.ldif.j2 used by the middleware role. Renders configuration or service files during installation.

##### `roles/middleware/templates/ldif_files/tree_level_2.ldif.j2`
Jinja2 template for tree level 2.ldif.j2 used by the middleware role. Renders configuration or service files during installation.

##### `roles/middleware/templates/ldif_files/tree_operators.ldif.j2`
Jinja2 template for tree operators.ldif.j2 used by the middleware role. Renders configuration or service files during installation.

##### `roles/middleware/templates/ldif_files/tree_roles.ldif.j2`
Jinja2 template for tree roles.ldif.j2 used by the middleware role. Renders configuration or service files during installation.

##### `roles/middleware/templates/ldif_files/tree_systemusers.ldif.j2`
Jinja2 template for tree systemusers.ldif.j2 used by the middleware role. Renders configuration or service files during installation.

##### `roles/middleware/templates/ldif_files/tree_tenant0.ldif.j2`
Jinja2 template for tree tenant0.ldif.j2 used by the middleware role. Renders configuration or service files during installation.

### openldap-server

#### defaults

##### `roles/openldap-server/defaults/main.yml`
Default variables for the openldap-server role. Provides baseline settings consumed by tasks and templates.

#### vars

##### `roles/openldap-server/vars/Debian.yml`
Role variables for openldap-server (Debian.yml). Defines fixed or OS-specific values used by tasks.

##### `roles/openldap-server/vars/RedHat.yml`
Role variables for openldap-server (RedHat.yml). Defines fixed or OS-specific values used by tasks.

##### `roles/openldap-server/vars/main.yml`
Role variables for openldap-server. Defines fixed values or OS-specific defaults used by tasks.

#### handlers

##### `roles/openldap-server/handlers/main.yml`
Main handlers for the openldap-server role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/openldap-server/tasks/main.yml`
Main task list for the openldap-server role. It orchestrates the role flow and includes additional task files.

##### `roles/openldap-server/tasks/run-upgrades.yml`
Task file for the openldap-server role handling run upgrades. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/openldap-server/templates/config.ldif.j2`
Jinja2 template for config.ldif.j2 used by the openldap-server role. Renders configuration or service files during installation.

##### `roles/openldap-server/templates/ldap.conf.j2`
Jinja2 template for ldap.conf.j2 used by the openldap-server role. Renders configuration or service files during installation.

##### `roles/openldap-server/templates/override.conf.j2`
Jinja2 template for override.conf.j2 used by the openldap-server role. Renders configuration or service files during installation.

#### meta

##### `roles/openldap-server/meta/main.yml`
Role metadata for openldap-server. Declares dependencies and supported platforms for Ansible Galaxy.

### openssl-cert

#### tasks

##### `roles/openssl-cert/tasks/main.yml`
Main task list for the openssl-cert role. It orchestrates the role flow and includes additional task files.

### sample-auth-data

#### defaults

##### `roles/sample-auth-data/defaults/main.yml`
Default variables for the sample-auth-data role. Provides baseline settings consumed by tasks and templates.

#### tasks

##### `roles/sample-auth-data/tasks/auth_sample_data.yml`
Task file for the sample-auth-data role handling auth sample data. Included by the role when that step is needed in the installer workflow.

##### `roles/sample-auth-data/tasks/main.yml`
Main task list for the sample-auth-data role. It orchestrates the role flow and includes additional task files.

##### `roles/sample-auth-data/tasks/modify_ldap_tree.yml`
Task file for the sample-auth-data role handling modify ldap tree. Included by the role when that step is needed in the installer workflow.

##### `roles/sample-auth-data/tasks/sample_owner_data.yml`
Task file for the sample-auth-data role handling sample owner data. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/sample-auth-data/templates/tree_groups_for_sample_operators.ldif.j2`
Jinja2 template for tree groups for sample operators.ldif.j2 used by the sample-auth-data role. Renders configuration or service files during installation.

##### `roles/sample-auth-data/templates/tree_roles_for_sample_operators.ldif.j2`
Jinja2 template for tree roles for sample operators.ldif.j2 used by the sample-auth-data role. Renders configuration or service files during installation.

##### `roles/sample-auth-data/templates/tree_sample_groups.ldif.j2`
Jinja2 template for tree sample groups.ldif.j2 used by the sample-auth-data role. Renders configuration or service files during installation.

##### `roles/sample-auth-data/templates/tree_sample_operators.ldif.j2`
Jinja2 template for tree sample operators.ldif.j2 used by the sample-auth-data role. Renders configuration or service files during installation.

##### `roles/sample-auth-data/templates/tree_sample_tenants.ldif.j2`
Jinja2 template for tree sample tenants.ldif.j2 used by the sample-auth-data role. Renders configuration or service files during installation.

### sample-data

#### tasks

##### `roles/sample-data/tasks/add_second_ldap_db.yml`
Task file for the sample-data role handling add second ldap db. Included by the role when that step is needed in the installer workflow.

##### `roles/sample-data/tasks/create-demo-credentials.yml`
Task file for the sample-data role handling create demo credentials. Included by the role when that step is needed in the installer workflow.

##### `roles/sample-data/tasks/create-devices.yml`
Task file for the sample-data role handling create devices. Included by the role when that step is needed in the installer workflow.

##### `roles/sample-data/tasks/create-test-credentials.yml`
Task file for the sample-data role handling create test credentials. Included by the role when that step is needed in the installer workflow.

##### `roles/sample-data/tasks/main.yml`
Main task list for the sample-data role. It orchestrates the role flow and includes additional task files.

##### `roles/sample-data/tasks/setup-sample-import.yml`
Task file for the sample-data role handling setup sample import. Included by the role when that step is needed in the installer workflow.

##### `roles/sample-data/tasks/unused_setup-config-changes.yml`
Task file for the sample-data role handling unused setup config changes. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/sample-data/templates/ext_add_user.ldif.j2`
Jinja2 template for ext add user.ldif.j2 used by the sample-data role. Renders configuration or service files during installation.

##### `roles/sample-data/templates/ext_role.ldif.j2`
Jinja2 template for ext role.ldif.j2 used by the sample-data role. Renders configuration or service files during installation.

##### `roles/sample-data/templates/ext_roles.ldif.j2`
Jinja2 template for ext roles.ldif.j2 used by the sample-data role. Renders configuration or service files during installation.

##### `roles/sample-data/templates/ext_user.ldif.j2`
Jinja2 template for ext user.ldif.j2 used by the sample-data role. Renders configuration or service files during installation.

##### `roles/sample-data/templates/second_db.ldif.j2`
Jinja2 template for second db.ldif.j2 used by the sample-data role. Renders configuration or service files during installation.

### tests-integration

#### handlers

##### `roles/tests-integration/handlers/main.yml`
Main handlers for the tests-integration role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/tests-integration/tasks/b64pad.yml`
Task file for the tests-integration role handling b64pad. Included by the role when that step is needed in the installer workflow.

##### `roles/tests-integration/tasks/main.yml`
Main task list for the tests-integration role. It orchestrates the role flow and includes additional task files.

##### `roles/tests-integration/tasks/test-api.yml`
Task file for the tests-integration role handling test api. Included by the role when that step is needed in the installer workflow.

##### `roles/tests-integration/tasks/test-auth.yml`
Task file for the tests-integration role handling test auth. Included by the role when that step is needed in the installer workflow.

##### `roles/tests-integration/tasks/test-database.yml`
Task file for the tests-integration role handling test database. Included by the role when that step is needed in the installer workflow.

##### `roles/tests-integration/tasks/test-importer.yml`
Task file for the tests-integration role handling test importer. Included by the role when that step is needed in the installer workflow.

##### `roles/tests-integration/tasks/test-web.yml`
Task file for the tests-integration role handling test web. Included by the role when that step is needed in the installer workflow.

##### `roles/tests-integration/tasks/write-config-test-user-creds.yml`
Task file for the tests-integration role handling write config test user creds. Included by the role when that step is needed in the installer workflow.

### tests-unit

#### tasks

##### `roles/tests-unit/tasks/main.yml`
Main task list for the tests-unit role. It orchestrates the role flow and includes additional task files.

### ui

#### handlers

##### `roles/ui/handlers/main.yml`
Main handlers for the ui role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/ui/tasks/install_and_run_ui_service.yml`
Task file for the ui role handling install and run ui service. Included by the role when that step is needed in the installer workflow.

##### `roles/ui/tasks/main.yml`
Main task list for the ui role. It orchestrates the role flow and includes additional task files.

##### `roles/ui/tasks/run-upgrades.yml`
Task file for the ui role handling run upgrades. Included by the role when that step is needed in the installer workflow.

##### `roles/ui/tasks/ui_apache_install_and_setup.yml`
Task file for the ui role handling ui apache install and setup. Included by the role when that step is needed in the installer workflow.

#### templates

##### `roles/ui/templates/fworch-blazor-ui.service.j2`
Jinja2 template for fworch blazor ui.service.j2 used by the ui role. Renders configuration or service files during installation.

##### `roles/ui/templates/httpd.conf`
Jinja2 template for httpd.conf used by the ui role. Renders configuration or service files during installation.

### webhook

#### defaults

##### `roles/webhook/defaults/main.yml`
Default variables for the webhook role. Provides baseline settings consumed by tasks and templates.

#### handlers

##### `roles/webhook/handlers/main.yml`
Main handlers for the webhook role. Defines restart or reload actions triggered by tasks.

#### tasks

##### `roles/webhook/tasks/main.yml`
Main task list for the webhook role. It orchestrates the role flow and includes additional task files.

#### templates

##### `roles/webhook/templates/fworch-webhook-receiver.py.j2`
Jinja2 template for fworch webhook receiver.py.j2 used by the webhook role. Renders configuration or service files during installation.

##### `roles/webhook/templates/fworch-webhook-receiver.service.j2`
Jinja2 template for fworch webhook receiver.service.j2 used by the webhook role. Renders configuration or service files during installation.
