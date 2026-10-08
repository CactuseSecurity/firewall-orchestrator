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

## Login name

Users log in with their account name: `sAMAccountName` for an Active Directory connection, `uid` for an OpenLDAP
connection, and for a connection of the default type the `sAMAccountName` of an entry that has one, otherwise its
`uid`. The middleware looks up exactly one entry with
an exact match on this attribute and checks the password only for that entry. Other names of the same account, such
as the `cn`, the `userPrincipalName`, the mail address or a further value of a multi-valued `uid`, are not accepted
(since 9.7.0). Login names containing LDAP filter characters (`*`, `\`, `(`, `)` or NUL) are rejected without any
directory access.

Logins with the `userPrincipalName` or the mail address are not supported on purpose. Unlike the account name, these
attributes are not guaranteed to be unique within a directory and across the connected directories, and a login
that matched several entries could select another account than the one the user intended. Users who logged in with
one of these names before 9.7.0 have to use their account name. The LDAP type of a connection (settings, LDAP
connections) determines which attribute is the account name.

## Login limits

Every login with credentials makes the middleware search and bind in the active LDAP connections. To keep
unauthenticated requests from turning into a flood of directory operations, the login endpoints
(`AuthenticationToken/GetTokenPair`, `Get`, `GetTokenPairForUser`, `GetForUser`) are limited as follows:

| Limit | Default | Answer when exceeded |
|---|---|---|
| Credentialed login attempts per client address and minute | 30 | `429` with `A0006` and `Retry-After` |
| Failed logins per user name and client address and minute (running attempts count as possible failures) | 10 | `429` with `A0006` and `Retry-After` |
| Active LDAP connections one login may be tried against | 16 | `503` with `A0007` (logged as error) |
| Logins waiting for LDAP at the same time | 32 | `429` with `A0006` |
| LDAP operations running at the same time (user searches, password checks, group and role lookups of all logins) | 4 | queued until a slot is free |
| Time for all directory work of one login | 10 s | `503` with `A0007` |

Anonymous token requests (no user name or password) cause no LDAP work and are never limited. When a login runs out
of time or its HTTP request is cancelled, the middleware closes the login's LDAP connections, and every directory
operation of a login waits at most 10 s for an answer (the LDAP library does not stop a waiting operation otherwise).
So a directory that does not answer cannot keep the LDAP slots occupied for longer than that. A directory that needs
more than 10 s for a login cannot be used for logins.
Token refreshes are not rate limited, as they require a valid refresh token, but share the deadline; a refresh that
runs out of time answers `503` without consuming the token.

The client address is the one the local Apache reverse proxy appends to `X-Forwarded-For`. Addresses of the
middleware host itself and the UI hosts are exempt from the per-client limit, because every login through the UI
arrives from the UI server's address. The per-user failure limit still applies to them, so repeated wrong
passwords for one user through the UI block further attempts for that user from the UI for up to a minute.
This includes service accounts such as `importer` when they log in from the same host as the UI.

As logins through the UI are not limited per client, the limits that apply to all logins together (logins waiting
for LDAP, LDAP operations at the same time) can be reached by anyone who sends many logins with arbitrary user names
through the UI login form. Other users then get `429` or `503` until the flood stops; the directories themselves are
not overloaded.

The limits can be changed in `/etc/fworch/fworch.json`; the middleware reads them at startup:

```json
{
  "login_max_directories": 16,
  "login_client_attempts_per_minute": 30,
  "login_user_failures_per_minute": 10,
  "login_trusted_client_hosts": ["ui-srv", "192.168.121.2"]
}
```

`login_trusted_client_hosts` is written by the installer from the `frontends` inventory group (inventory names
and `ansible_host` values). It is refreshed on every upgrade, so set the inventory variable
`login_trusted_client_hosts` instead of editing the file if you need other hosts. Host names that cannot be
resolved at startup are logged as a warning and not exempt. The inventory variable may be a list, a comma separated
string (`-e login_trusted_client_hosts=ui1,ui2`) or a JSON list given as a string
(`-e 'login_trusted_client_hosts=["ui1","ui2"]'`).

The middleware also reads hand-edited values: the numbers may be given as strings (`"16"`) and
`login_trusted_client_hosts` as a comma separated string. A value of another type is ignored with a warning in the
log, and the default applies.
