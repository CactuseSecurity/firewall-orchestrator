# Getting started with Firewall Orchestrator

Check if you meet the [system requirements}(system-requiremnts.md).

Follow the [installation instructions](installer/README.MD).

## Initial login web ui

- use a local browser to connect to [https://localhost/](https://localhost)
- alternatively you can also use a browser on a remote system for all your access to FWORCH, simply specifying the firewall orchestrator system's IP address or name
- accept the certificate warning
- login with username admin, the password can be found in /etc/fworch/secrets/ui_admin_pwd, it will also displayed at the end of the installation routine
- create additional users and assign roles (see help pages) 

## API login graphiql

- use your local browser to connect to <https://localhost:9443/api>
- login with hasura admin password found in file /etc/fworch/secrets/hasura_admin_pwd

## non local login

- you may also access the web UI and API remotely by replacing localhost with your systems IP address or resolvable hostname

## Using the Web Interface
Per default the system systems comes with sample data pre-installed to allow for quick testing of functionality. The sample data may be removed at any time using the "Remove sample data" buttons in the respective settings section.

## How to integrate your own firewall systems

Open the Web UI, head to Settings - Managements, then have a look at the help pages. 
