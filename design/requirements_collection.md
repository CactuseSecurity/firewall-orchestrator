# Requirements

## functional requirements

### UI

#### UI general

- resizing of areas (e.g. navigation menue)
- multi-language support (start with German + English)
- cross-plattform support runs on windows, macos, linux (or browser when moving logic into webserver)
- authentication, rbac, ...
- allow for tenants (rbac per firewall device or management plus IP-ranges - at least that is the status quo)
- use symbols in menues in addition to text (e.g. oi cog)

##### multi-purpose filter

- implement as text box and graphical element in parallel
- text filter is derived and automatically displayed when clicking filters
- user is able to choose filter modules
- UI must prevent filters from being displayed that do not belong to the current data (dependencies)
- low prio: display further info per element (ip, client, manufacturer), might be implemented by mouse-over functionality

#### reporting

- create templates that define report
- provide filter line with simple language containing logical operators
- easy to adapt (eg. add new reports)
- nfr (non-functional): modern look
- report visibility should be definable (group, users)
- nice to have: positional bar at upper edge (see nzz.ch)
- part of UI functionality must be "schedulable" (report generation)

#### admin gui parts

- add/edit/delete management
- add/edit/delete device
- add/edit/delete client/tenant
- add/edit/delete user
- add/edit/delete external LDAP

#### workflow creation

#### worklow deployment

### rest

- fulfill regulatory requirements of financial institutions

  - change reports
  - config reports
  - ticket link (documentation)
  - re-certification workflow
  - access request workflow
  - todo: research regulatory requirements in detail

- reports

  - config report
  - change report

- re-certification workflow
- change request workflow

  - rule request (add/delete rules)
  - object request (add/delete objects)

- integrated help/manual
- documentation of undocumented config changes

  - add new feature: define pattern of ticket id to parse ticket number from comment fields and add to new meta data field

- gui - see separate task
- import modules

  - can run on separate servers
  - planning to access db via api (phase 2)
  - support cp r7x (phase out), r8x (api), fortigate (add api support), junos (keep as is), iptables, palo alto (start with api), baracuda (drop support), netscreen #(drop support), ...

## non-functional requirements

- database:

  - keep database size down (e.g. clean-up jobs removing all data older than 5 years)
  - this could be an issue with documentation data
  - or split database into archive and live

- gui:

  - modern look & feel
  - fast response (immediate, max 5 secs)
