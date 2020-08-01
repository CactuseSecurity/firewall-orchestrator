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

## initial login web gui

- use your local browser to connect to [https://localhost/](https://localhost)
- login with username admin, password fworch.1
- you are requested to change this password during first login
- choose a secure password with >=9 chars, alphanumeric chars and at least a special character

## login api admin

- use your local browser to connect to <https://localhost/api/>
- login with admin password st8cheltier

## non local login

- to enable login from another system: ....

## view reports

- click reporting

## your systems comes with test data pre-installed

- this data needs to be removed for production systems (delete cascade from management where mgm_id=1 or mgm_id=2) --> integrate in GUI

## how to integrate your own firewall
