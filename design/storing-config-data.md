# Where to store which configuration data
Differntiation between config data and non-config data:
all data whose knowledge is not potentially dependant on the user's role or tenant.

Most config data will be stored in the database and only be accessible via API call. 

## All data necessary for making an API call needs to be held in a config file
The following needs to be stored in a configuration file fworch.yaml on each module that needs the config data:
### public, can be stored in user context
- API URI (needed by UI, Auth, Importer modules)
- Auth server URI (needd by UI, Auth, Importer modules)
- JWT generation public key  (needed by Auth, API, Importer to verfiy JWTs)

### private, not to be shared with user
- JWT generation private key  (needed by Auth for each JWT generation)

## Config data that can be retrieved via API
### public
this data together with the 
- JWT Hash algorithm (needed by API, Auth, UI)
- texts in multiple languages (UI)
- default language per user (UI)
- device_type (UI, Importer)
- stm_
  - action
  - change_type
  - color
  - dev_typ
  - ip_proto
  - nattyp (needed?)
  - obj_typ
  - report_typ
  - svc_typ
  - track
  - usr_typ

### confidential
- LDAP Connections (needed by Auth module only; confidential, containing ldap user passwords)

## Config data only needed during installation phase
Data that is only needed during ansible installation phase will be dealt with either in variables or stored in config files if needed
- LDAP manager for local instance (superuser for local ldap server; Auth module; confidential)
- hasuar admin secret (needed for testing only; confidential)
