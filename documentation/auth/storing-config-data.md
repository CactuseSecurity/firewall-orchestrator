# Where to store configuration data

Most config data will be stored in the database and only be accessible via API call. 

## All data necessary for making an API call needs to be held in a config file
The following needs to be stored in a configuration file fworch.yaml on each module that needs the config data:
- API URI (needed by UI, Auth, Importer modules; public)
- JWT secret (needed by Auth and API modules; conficential), UI should be able to use certificate??

## Config data that can be retrieved via API
- LDAP Connections (needed by Auth module; confidential)
- JWT Hash algorithm (needed by API, Auth, UI; public)
- LDAP manager for local instance (needed for adding local ldap users; Auth module; confidential)

## Config data only needed during installation phase
Data that is only needed during ansible installation phase will be dealt with either in variables or stored in config files if needed
