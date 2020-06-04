# functional requirements
## UI
- easy to adapt (eg. add new reports)
- nfr (non-functional): modern look
- cross-plattform support runs on windows, macos, linux (or browser when moving logic into webserver)
- authentication, rbac, ...
- allow for tenants (rbac per firewall device or management plus IP-ranges - at least that is the status quo)
- admin gui parts
  - add/edit/delete management
  - add/edit/delete device
  - add/edit/delete client/tenant
  - add/edit/delete user
  - add/edit/delete external LDAP
- multi-language support (start with German + English)
## rest
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
  - support cp r7x (phase out), r8x (api), fortigate (add api support), junos (keep as is), iptables, palo alto (start with api), baracuda (drop support), netscreen (drop support),
...
# non-functional requirements
- database:
  - keep database size down (e.g. clean-up jobs removing all data older than 5 years)
  - this could be an issue with documentation data
  - or split database into archive and live
- gui: 
  - modern look & feel
  - fast response (immediate, max 5 secs)
