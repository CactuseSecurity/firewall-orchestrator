# Getting started with Firewall Orchestrator

## system requirements
- currently supported operating systems are Ubuntu >=18.04 and Debian >=10
- Postgresql >=9 must be available
- may be run on virtual systems

### for minimal test installation
- 20 GB HDD
- 8 GB RAM
- 1 CPU

### for production
- 80 GB HDD (large, long-running installations might need 500 GB or more)
- 16 GB RAM
- 4 CPUs

## initial login web ui

- use your local browser to connect to [https://localhost/](https://localhost)
- login with username admin, the password can be found in /etc/fworch/secets/ui_admin_pwd, it will also displayed at the end of the installation routine
- go to settings - personal password and change your password, choosing a secure password (e.g. >=9 chars, alphanumeric chars and at least a special character)

## api login graphiql

- use your local browser to connect to <https://localhost:9443/api>
- login with hasura admin password found in file /etc/fworch/secets/hasura_admin_pwd

## non local login

- to enable login from another system: ....

## view reports

- click reporting

## your systems comes with test data pre-installed

- this data needs to be removed for production systems
- use e.g. settings - gateways - 

## how to integrate your own firewall
