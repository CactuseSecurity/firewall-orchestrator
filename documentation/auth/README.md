# Authentication and Authorization in Firewall Orchestrator

Users can either be stored in the local LDAP server that comes with the Firewall Orchestrator or you may use one or more external LDAP servers (recommended).


## Implementation
In Firewall Orchestrator all permissions are tied to roles which are enforced solely in the API layer. 
Therefore we can ensure the same access security via API access as whithin the product itself.

Also have a look at
- the [role based access control model](rbac.md)
- how to [define roles & permissions in hasura](https://hasura.io/docs/1.0/graphql/manual/auth/authorization/index.html)
  - For an example see <https://dev.to/lineup-ninja/modelling-teams-and-user-security-with-hasura-204i>
  - see [hasura doc on auth](https://hasura.io/docs/1.0/graphql/manual/auth/authorization/roles-variables.html)

![Auth process overview](fworch-auth-process.png)

## Roles

The latest and most detailed role based acccess model can always be found in the help pages of Firewall Orchestrator itself.

There is also multi-tenancy support which you can use to implement per-tenant permissions for defined firewall gateways.

See <rbac.md>
