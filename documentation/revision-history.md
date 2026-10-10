# Firewall Orchestrator Revision History

## 9.7.1 - 09.10.2026
- security (GHSA-f5f6-w5vg-mxp9): make TLS certificate checking of outbound connections to external systems
  configurable with one switch per connection type, validated against the host trust store:
  - firewall connections (importCheckCertificates): now also covers autodiscovery and Check Point change
    requests, which accepted any certificate so far; the importer uses the OS CA bundle instead of certifi
  - email servers (new emailCheckCertificates) and external ticket systems (new
    extTicketSystemsCheckCertificates), which accepted any certificate so far; certificate revocation is not
    checked online (no CRL / OCSP requests), as for all other connections
  - fresh installations check all connection types by default. Upgrades keep the value of importCheckCertificates
    and switch the new email and ticket system checks off where such a connection is configured. Two connections
    can behave differently after an upgrade:
    - autodiscovery and Check Point change requests follow importCheckCertificates: with checking switched on,
      they are now checked as well, against the trust store of the middleware and UI hosts
    - the importer names the OS CA bundle explicitly for the Check Point, FortiManager and FortiOS API calls, so
      REQUESTS_CA_BUNDLE no longer applies to them (OPNsense imports and config downloads still use it if set);
      add a private CA to the trust store of the importer host instead
  - unchecked connections are reported once per endpoint in the log
  - the Tufin RLM app data customizing script reads checkCertificates from its config file
  - remove obsolete certificate options: the log-only ssl_verification/suppress_cert_warnings arguments of
    the importer's import_management, and the undeployed, non-functional API helper scripts in
    roles/api/files/scripts (with their "--ssl" option, which switched checking off by default) together
    with the fwo_api.py scripting copy they relied on
- security (GHSA-cg7h-hr7j-pr7w): bound the importer's direct import of config files from a URL or local file
  - redirects are no longer followed; a redirect is rejected with its target in the import error
  - downloads are streamed with a size limit of 256 MiB (decompressed, also checked against Content-Length),
    5 minutes per read and 30 minutes in total, checked while the data arrives
  - local files must be regular files (no devices or pipes) and are read up to the size limit only
  - autodiscovery skips domains and ADOMs whose name is in URI form, so a remote manager cannot make the
    importer read from a URL or local file
- security (GHSA-3v53-q5h9-pvfh): no demo data by default
  - add_demo_data now defaults to the value of testkeys: a standard installation creates no sample users,
    owners or devices with publicly known passwords; installations with testkeys=yes get them automatically
  - demo data can still be requested explicitly with -e add_demo_data=yes, which logs a security warning
  - upgrades leave existing demo data in place, including the users user1_demo and user2_demo with their
    publicly known passwords. Remove it with the "Remove Sample Data" buttons in the settings (users, groups,
    tenants, managements, credentials, owners). The users page and the daily check only know users that logged
    in at least once: click "Synchronize to LDAP" on the users page first and reload the page, so that the demo
    users of the internal LDAP are listed and removed as well; the daily check raises an alert as long as known
    sample data exists
  - testkeys is defined for all hosts in inventory/group_vars/all.yml (it was only set for the middleware), so
    all hosts of a distributed installation agree on creating the demo data
  - the GitHub test installations request demo data explicitly, as the JWT integration test logs in with a user
    of the sample LDAP data
  - remove the unused fixed importer_password from the middleware inventory
- security (GHSA-3cwm-h5cm-r3f8, rated low, accepted risk): harden the SonarCloud workflows, which keep building
  pull requests of trusted fork owners with the Sonar token in the pull_request_target context
  - remove all caching from both SonarCloud workflows, so that a cache entry written by pull request code
    cannot run in later runs
  - pin all actions of both SonarCloud workflows to commit SHAs
  - pull_request_target runs use the workflow file of the default branch (main), so the hardening takes effect
    for pull requests once it has reached main
  - document the accepted residual risk and its conditions (project-scoped token with Browse and Execute
    Analysis only, no other secrets, no caches) in documentation/developer-docs/github/sonarcloud-workflow.md
  - a policy test enforces the conditions that can be checked from the repository; it reads the workflows with a
    YAML parser, so comments, quoting, anchors, flow collections, dynamic secret indexes or a checkout with
    gh pr checkout cannot hide a violation
- security (GHSA-qqm3-cf7w-65wr): the middleware endpoint starting the external requests of a ticket no longer
  accepts foreign ticket ids
  - modellers may only start the external requests of tickets whose request tasks all belong to owners they
    may edit; other or unknown tickets are answered with 404, admins keep access to all tickets
  - completed tickets and tickets whose external requests are still being processed are refused (409)
  - a new unique index allows at most one active external request (one without finish date) per ticket, so
    concurrent calls cannot start the request chain twice; requests closed manually by an admin now get a
    finish date as well, and upgrades set the missing finish dates of already closed requests
- security (GHSA-fhcc-7hg5-jj89): the compliance report api (POST /api/Compliance/Report) is limited to the
  caller's scope
  - only admin and auditor may call it, as in the UI; reporter, reporter-viewall and recertifier now get 403
  - the report runs with the caller's own permissions and covers the requested managementIds; a management
    that is not visible to the caller or not relevant for the compliance check is answered with 400
  - the report shows the stored results of the last compliance check instead of running a new check
  - failures are answered with an error status (401, 429 or 503 with Retry-After, 500) instead of an empty
    report, which means no violations
- security: LDAP login
  - a login looks up exactly one directory entry with an exact, escaped filter on the login attribute
    (sAMAccountName for Active Directory, uid for OpenLDAP; for the default type the sAMAccountName of an entry
    that has one, otherwise its uid) and checks the
    password only for that entry. Breaking change: users who logged in with their cn, userPrincipalName or mail
    address, or with another than the first value of a multi-valued uid, have to use their account name now.
    Logins with the userPrincipalName or the mail address are not supported on purpose: these names are not
    guaranteed to be unique, and a login matching several entries could select another account than intended
  - login names containing LDAP filter characters (*, \\, (, ) or NUL) are rejected without directory access
  - logins are limited per client address (30 attempts per minute) and per user name and client (10 failures per
    minute); the UI and middleware hosts are exempt from the per-client limit. At most 16 directories per login,
    32 waiting logins and 4 parallel user searches and password checks are allowed, and all directory work of a
    login has to finish within 10 seconds. Refused logins are answered with 429 (A0006) or 503 (A0007) and
    Retry-After; the limits can be set in fworch.json (login_* keys, see documentation/auth/README.md). A
    login_* value of the wrong type is ignored with a warning instead of stopping the middleware, and the
    installer also accepts login_trusted_client_hosts as a JSON list given as a string
  - when a login runs out of time or is cancelled, its LDAP connections are closed, and every directory operation
    of a login waits at most 10 seconds for an answer, so a directory that does not answer cannot keep the LDAP
    slots occupied for longer than 10 seconds after cancellation
  - login attempts of a user that are still running count as possible failures, so concurrent guesses cannot
    exceed the failure limit; the group and role lookups of logins share the 4 parallel LDAP operations
- security (GHSA-v8hx-cx2q-j75v): the tenant visibility of rule sources and destinations (rule_from /
  rule_to) no longer depends on unrelated devices
  - the full rulebase check used the device of an arbitrary rule instead of the device of the checked rule,
    because the row parameter had the same name as its table; a tenant could therefore see the ip filtered
    sources and destinations of a rule as soon as any other device or management was fully visible to it,
    and lose sight of those of a fully visible device
  - the row parameters are renamed (p_rule_from / p_rule_to) and all columns are qualified
  - the full visibility of a rule no longer uses the obsolete rule.dev_id, which the importer does not write
    (so far, unshared device and management mappings of a tenant had no effect on rules). A rule is fully
    visible if the tenant has an unshared mapping to its management or to one of the gateways it is enforced
    on (rule_enforced_on_gateway); for a rule that applies to all gateways, or a nat rule, to a gateway linking
    its rulebase. An access rule installed on targets that are no gateways of the management (e.g. a gateway
    group) is not fully visible. Only gateway links valid for the rule version count: a rule or rulebase moved
    to another gateway is no longer fully visible through the old one, while historic rule versions stay
    visible through the gateways they were enforced on. Gateway links only count within the rule's management
    or a child of its global management; foreign gateway entries left by the v9 migration cannot grant visibility.
    A gateway entry also only counts while the gateway links the rule's rulebase: the v9 migration gave rules
    installed on "Policy Targets" entries for the gateways of all rulebases, also of the same management.
    This applies to rules, rule sources and destinations
    and to the tenant simulation, which now lists the rules of the rulebases linked to a gateway
  - upgrades date the rulebase links and gateway entries created by the v9 migration back to the lifetime of the
    migrated rule versions, so rule versions removed before the upgrade to v9 stay visible to the tenants of
    the gateway recorded on each v8 rule; each gateway/rulebase link is restored only as far back as those rules
    prove it existed. Other gateways, including those added before the migration but after an old rule version
    was removed, do not gain that version's history; links added later and data imported by v9 are not changed
  - a new index on rule_enforced_on_gateway (rule_id) keeps these checks fast
  - plain network objects (no group) used directly in a rule are matched against the tenant networks by their
    own address; so far only group members were matched, as the importer writes objgrp_flat rows for groups
    only, so such rules were not visible to the tenant. This also applies to the tenant simulation of objects
    and rule changes; the simulation of objects determines the visible objects set based (about 15 times
    faster than with a check per rule source and destination)
  - a database integration test checks the visibility in a rolled back transaction, including unrelated
    visible devices and managements (with rule.dev_id set as in older data), negated sources and destinations,
    rules enforced on one or several gateways, rules for all gateways, nat rules, rules installed on unknown
    targets, moved rules and rulebases, historic rule versions, full management visibility and the admin tenant;
    a second one runs the 9.7.1 upgrade twice and checks the certificate switches and the migrated gateway links
- security (GHSA-p8qh-59qx-rjj4): the Cisco ASA importer no longer imports access-list entries it does not fully
  understand with a broader meaning
  - so far an address it did not understand (e.g. "interface inside", a source port, any6) became "any", unknown
    port operators (lt, gt, neq) and icmp types became any port or any icmp, unknown trailing tokens were ignored,
    and entries that failed to parse were skipped with a warning (dropping e.g. a deny entry)
  - every token of an extended access-list entry must now be understood; otherwise the import fails with the
    config line number, token position and the unsupported construct, and the last imported config stays
    unchanged. This includes port numbers and names (0 to 65535, known names, ranges in ascending order)
  - newly supported: any6, IPv6 hosts and prefixes, name aliases in subnet addresses, lt / gt ports, icmp types,
    log options, "line N" and time-range (imported as rule time with a time object named like the time-range;
    its absolute and periodic times are not imported)
  - service object groups keep all members: every second of several consecutive group-object or "service-object
    object" lines was dropped so far
  - the services of the imported rules keep the restrictions of the entry: a protocol group with a port gets that
    port for each of its protocols (so far any port), a port group of a tcp or udp entry only contributes its
    services of that protocol (as a group named "<group> (tcp)" if it has others), icmp6 types and sctp ports
    are kept, and numeric protocols get their protocol number (so far 0). A port or service that cannot be
    represented fails the import instead of being imported as any
  - still not supported (import fails): interface, user and security-group addresses, source ports, neq and
    icmp codes
  - rule uids of entries that were already parsed correctly do not change. Entries whose meaning changed (icmp
    with a type, lt / gt ports, any6 and IPv6 addresses, subnets given by a name alias, entries with a
    time-range) get new uids, so they show up as removed and added on the first import after the upgrade
- security (GHSA-4qpx-88jv-297x): the per-ticket locks of the middleware workflow action endpoint no longer pile up
  - a lock exists only while a request holds or waits for its ticket and is removed afterwards, so requests with
    many different ticket ids no longer grow the middleware memory; waiting stops when the client aborts
  - at most 1000 tickets, 32 requests per ticket and 8 tickets per user are locked or waited for at the same
    time; further requests get a retry message and are not processed. Ticket ids of 0 or less are rejected
- security (GHSA-m87v-j229-2g65): bound the work single middleware REST requests can cause
  - every user may send 600 requests per minute (with bursts of up to one minute's budget); getFlowComplianceState,
    getIpDataForOwners and the Flow catalog lists additionally run at most 2 requests per user at the same time
    and accept request bodies up to 1 MiB. Rejected requests get 429 with Retry-After. Service accounts,
    anonymous tokens and unauthenticated requests are not limited here; the limits can be set in fworch.json
    (rate_limit_* keys, see documentation/auth/README.md)
  - breaking change for API clients that do not page: getIpDataForOwners returns at most 10000 applications per
    request, getAddressObjects, getServiceObjects and getTimeObjects at most 1000 objects and getAddressGroups
    and getServiceGroups at most 250 groups (new root keys limit and offset for the Flow catalog lists). The
    response header X-Has-More tells whether further entries follow
  - getFlowComplianceState loads all requested policies with one query and accepts at most 100 entries each in
    source, destination, service and policies
- security (GHSA-frxw-mwhq-xgjq): failed initial workflow actions of a new ticket are no longer reported as success
  - the ticket stays saved, but the failure is recorded in its change history and raised as an alert
  - the createTicket api returns the new field actionsStatus (completed or failed)
- security (GHSA-2xpm-58hq-qwvm): getServiceObjectId no longer picks one of several matching portless services
  - if more than one service object matches (e.g. several icmp services), the lookup answers with 409 and the
    candidates instead of an id; services with ports and the canonical ANY service are unique
- security (GHSA-8hf3-3hp5-gj32): the installer only runs verified downloads
  - the Hasura container image is pinned by digest, the Hasura cli and dotnet-install.sh (pinned to a commit of
    dotnet/install-scripts) are checked against a checksum before they are executed
  - remote servers are connected with StrictHostKeyChecking=accept-new instead of no: a changed host key aborts
    the installation (see documentation/installer/install-advanced.md)
  - all actions of the GitHub workflows are pinned to commit SHAs; policy tests reject unpinned actions,
    container images without digest and executable downloads without checksum
  - Chrome for Testing keeps following the current stable release (accepted risk, documented)
- api: upgrade hasura graphql API to 2.50.3
- importer: long-running imports no longer fail when the access token expires - the importer now refreshes it via refresh token (proactively and on demand) and resumes, including mid-way through a chunked API call

## 9.7.0 - 07.10.2026
- resolve reverse DNS asynchronously with shutdown cancellation; reuse stored names and empty
  results across imports and allow disabling new lookups in Settings - Logging. A lookup without
  definitive answer (DNS server unreachable, server failure, refused) is repeated by the next import;
  once no DNS server can be reached, an import skips its remaining lookups instead of waiting for
  each of them to time out.
- store aggregation period and import time on each log row. The table heading uses only the
  displayed rows; mixed or unknown periods are shown per row. Existing rows retain unknown
  timing until reimported. Counts and timing are updated together, including additive imports.

- enrich imported log data with the external application IDs and network areas containing each
  source and destination address, plus reverse-DNS names. The connection log table displays the
  six new values and leaves unavailable metadata empty. The values are calculated once per import
  batch from the owner networks and area address ranges and stored per address in
  logging.ip_metadata, so an address logged by several owners is enriched once and carries the
  applications of every owner it belongs to. An address the log entries no longer refer to loses
  its metadata with the next import run, so the enrichment does not outlive the configured log
  data retention.
- rename the area IP data conversion script convertNwObjDataFromGit.py to
  convert_area_ip_data_from_git.py. The upgrade removes the old file and moves a configured subnet
  data import source pointing at the delivered script to the new name.
- add generate_area_ip_data.py, which generates sample area IP data covering the app servers of an
  app-data file; generate_app_and_log_data.py now also writes this area IP data file.
- the log table heading names the period the log counts were aggregated over and the import time,
  localized, e.g. "Logs (aggregated over 1 Week(s) until 10/5/2026)", the import ending the period.
  Settings - Logging can enable displaying the time of the import in the heading (default: off). The period is the new
  setting Settings - Logging - default log time range (default: 7 days, used for CSV imports), which a
  log data import file can override in the new optional top-level field log_time_range_in_seconds.
  The middleware warns about entries logged outside the supplied period. New setting to hide the log time column (default: on) (issue #5391).
  
## 9.6.2 - 02.10.2026

- installer: Red Hat Enterprise Linux 8 is no longer supported. Fresh installations and
  upgrades now stop before changing the host if its OS release is unsupported. Supported
  platforms are Debian 12+, Ubuntu 22.04+ (LTS only), Red Hat Enterprise Linux 9+ and Rocky 9+;
  Debian testing/unstable are also accepted. Move existing RHEL 8 installations to a supported
  platform before upgrading. `allow_unsupported_os=true` bypasses the guard with a warning
  for development and testing only; it does not make an installation supported
- SBOM: stable releases include CycloneDX source SBOMs. The installer can optionally generate
  combined source and installed-host SBOMs with `generate_sbom=true`; generation is disabled
  by default
- add workflow task types object_create and object_modify for a single network object (host, network,
  address range) or service that stands alone without a group. Only the request side is covered: the
  tasks can be created, edited, approved and passed through the workflow; implementation tasks show the
  object read-only. External ticket systems reject both task types, the Check Point integration follows in a
  later version
- the workflow action "create flow" stores the object of an object_create task as flow object in state
  requested (or binds it to the flow object of the same values) and links the request element to it, as it
  does for group members; object_modify is not mapped to the flow database yet
- object_modify references an existing imported object, which is selected through a server side search
  limited to the visible managements. The old values are stored in the task as unchanged element, the
  new values as modify element, as rule_modify does for the rule content
- the requester role may read active network objects and services of its visible managements (only the
  columns needed for the search, at most 50 rows per query)
- the upgrade copies the group_create state matrices of every workflow configuration for both new task
  types; both task types stay unavailable until an admin adds them to the available task types

## 9.6.1 - 05.10.2026
- add a management rulebases view to the rules report: select start rulebases per management instead of
  gateways (including rulebases without a gateway link); each selected rulebase is reported with the
  rulebases linked from it for the gateways using it
- make the default rules report view (gateway rules or management rulebases) configurable globally
  and personally; personal settings take precedence and saved report templates retain their view
- seed the global default as gateway-based for fresh installations and upgrades, preserving an
  existing configured value during upgrades
- add index on rulebase_link.to_rulebase_id to speed up the management rulebases view
- dependencies: update NuGet packages (AngleSharp to 1.8.3, coverlet.collector to 10.1.0, MailKit to 4.18.1, Moq to 4.21.0, PuppeteerSharp to 25.12.0, Quartz to 4.3.0, Scalar.AspNetCore to 2.17.13)
- request workflow UI: split request-task metadata and element editing into dedicated components while keeping the task type synchronized across the editors
- request workflow UI: fix task-type initialization when creating a task after viewing an existing task, and keep the selected gateway option stable when "All" is selected
- request workflow UI: correct owner-field layout and improve request-task, implementation-task, ticket and access-element test coverage through dedicated test fixtures
- clarify the localized Object Catalog and Service Catalog labels
- autodiscovery (FortiManager): an ADOM whose UID changed on the FortiManager is now recognized as existing
  (matched by ADOM name within the same super manager) instead of being proposed for deletion and
  re-creation; VDOMs missing in FWO are offered for addition. The name fallback is skipped if another
  ADOM already matches the management by UID

## 9.6.0 - 30.09.2026
- middleware: upgrade of the job scheduler Quartz.NET from 3.21 to 4.1 (Quartz.Extensions.Hosting and
  Quartz.Serialization.Json are no longer separate packages), together with updated NuGet packages for
  MailKit/MimeKit, PuppeteerSharp, Scalar, IdentityModel and the test tooling; SBOM regenerated
- middleware: stopping the middleware now signals cancellation to running scheduled jobs instead of waiting
  for them to finish. Each job stops at its next checkpoint and leaves no half-done result behind: a
  cancelled report is neither archived nor sent, an interrupted app data import closes its import control as
  unsuccessful and does not deactivate the apps it has not reached, and an interrupted device auto discovery
  does not report the managements it has not reached as deleted
- middleware: jobs get up to 2 minutes to unwind on shutdown; the systemd unit fworch-middleware now allows
  180 seconds (TimeoutStopSec) before killing the process

## 9.5.10 - 29.09.2026
- add database storage for hierarchical provisioning configuration nodes and sparse per-node setting overrides
- add DTOs for hierarchical provisioning configuration
- add UI page Settings - Provisioning settings (settings/fwconfigprovisioning) to view (auditor) and edit (admin)
  the provisioning settings per level Global > Device type > Management > Gateway, including a help page
- provisioning settings are inherited along the current device hierarchy; a management or gateway moved to another
  device type or management inherits from its new parent levels, and its stored node is moved there on the next save;
  the settings manager resolves values only along a ProvisioningScopePath built from the current management and
  gateway objects, never along the stored parent links (guarded by a unit test)
- restrict Hasura permissions on provisioning_config_node: node identity (id, node_type, object_key) is no longer
  writable and the global node can no longer be deleted; the database enforces node types and a single global root
- the provisioning settings page loads all stored nodes with one query and resolves a level with one query for all
  its parent levels, instead of one query per node or level
- the placeholder value Undefined of the provisioning setting enums is rejected when storing and reading an override
- auditors see the provisioning settings with all editors disabled
- the provisioning settings editor is locked while a save is running, and retained Fortinet-only values are shown as
  dormant overrides with an action to clear them after a management or gateway is moved to another device type
- Interface-request notifications are suppressed for inactive requested owners, incomplete legacy
  requests and unresolved requesting owners; suppression records include the reason and resolved
  subject placeholders
- Daily reminder checks evaluate whether a notification is due before recording a suppression, so
  non-due reminders do not create audit noise or advance the notification definition's last-sent
  state
- add upgrade seed for request task sort configuration
- new network zone tree path analysis: for every combination of a source and a destination ip range
  it determines the firewalls between them from the paths to the root network and to the internet
  that the zone matrix stores per subnet, cutting both paths at their lowest common ancestor.
  Traffic from or to the internet zone is answered with the internet path of the other side, and a
  range that falls into the auto calculated catch-all zone yields no path. The algorithm lives in
  the new library FWO.NetworkTopology and works on data alone, without database access. It can be
  selected as "Network Zone Tree" under the path analysis algorithm setting, but no function calls
  it yet - the REST endpoint that exposes it follows in a later version
- the configuration key `complianceDesignatedZoneMatrix` is renamed to `designatedZoneMatrix`,
  because the designated zone matrix is used beyond compliance. The upgrade renames the existing
  entry, so an installation keeps the matrix it had configured
- the path analysis based on routing tables moved into the class `RoutingBasedPathAnalyzer` without
  any change in behaviour, so that both procedures sit behind one interface
- network_zone.device_ip_range_root and network_zone.device_ip_range_internet can now be read per
  matrix through the api, including the name of the device on the path
- matrix import now rejects paths to root that do not describe one tree. A gateway may name only
  one successor towards the root across all subnets of a matrix, and the successors must not form
  a cycle. The error names the gateway and both successors, so the gateway can be located in the import file. Paths to the internet stay unchecked, as several routes there are intended. A matrix
  whose root paths contradicted each other was imported before and made the path analysis report
  routes that do not exist

## 9.5.9 - 28.09.2026
- security fix (SEC-11): a local user was identified by its dn alone, although a dn is unique only
  inside the directory that holds it. The same dn in two connected LDAPs therefore resolved to one
  local user: the login of the second directory's user took over the row of the first one,
  overwrote its tenant and directory, and received a token for that local subject. A token refresh,
  a scheduled report and the report and normalized config endpoints could likewise rebuild a user in
  the wrong directory. A local user is now identified by its LDAP connection and dn (new unique key
  uiuser_ldap_connection_id_uuid_key replacing uiuser_uuid_key); a user that is already known
  locally is only authenticated in its own directory and must resolve to the same local user again;
  the UI loads the session user and the self-service permissions of uiuser match the local user id
  of the token instead of the dn; and the password change flag is set by local user id.
  Admin-delegated token requests can select the target's LDAP connection with
  options.targetLdapId; without it, a target found in multiple directories is rejected instead of
  silently choosing the first account.
  The upgrade binds local users that belong to no LDAP connection to their directory where it is
  unambiguous and lists the remaining ones as a warning: such a user gets a new local user at the
  next login unless uiuser.ldap_connection_id is set by hand before
- security fix (SEC-15): the fw-admin role could change tenants, managements and credentials of all
  tenants: its Hasura permissions had no tenant restriction, and the tenant update endpoint of the
  middleware (which writes with the middleware's own role) accepted it as well. The role was not
  assigned by default and is removed from every layer: LDAP, JWT default role, middleware
  endpoints, Hasura permissions, UI and help texts. The upgrade deletes cn=fw-admin from the
  internal LDAP and lists its former members in the installer output; they need another role or
  group where they still need access
- security fix (SEC-15): deleting credentials a management still used deleted that management with
  all of its imported data (import credential) or silently unbound them (export credential). Both
  foreign keys refuse such a deletion now, and the credential settings name the managements that
  have to get other credentials first, also when removing sample data. A management can still be
  pointed to another host while keeping its credentials; this is left to the admin role, see
  documentation/auth/rbac.md
- security fix (SEC-19): every role reachable from a UI session could list all local users of all
  tenants together with their dn, tenant, LDAP connection, last login, password flags and password
  history. Roles other than admin and auditor now see their own user, the users of their tenant, or
  all users when they belong to tenant0, and of these only id, user name, first and last name, email,
  dn and language; they see only their own LDAP connection. Their own login time, password change
  time and password change flag are read through the new self-only computed fields own_last_login,
  own_last_password_change and own_password_must_be_changed (new query getOwnUser used by UI login,
  user settings and report generation). The auditor no longer reads the password history. The JWT
  always carries x-hasura-tenant-id (0 when no tenant could be resolved). In installations with
  several tenants, a user of one tenant no longer sees the name of a workflow handler, requester or
  comment author, or the owner of a report, who belongs to another tenant

## 9.5.8 - 28.09.2026
- REST workflow request creation accepts a `preWorkflowTicketReference` and stores it on the created
  ticket so integrations can retain the reference to the preceding workflow ticket
- Harden the versioning workflow: a product version is now sealed by its `vX.Y.Z-dev` or `vX.Y.Z` tag, and the new "Version gate" GitHub action blocks pull requests that would merge onto a sealed version or open a new version before the previous one was sealed
- The new "Version tag guard" GitHub action reports version tags created on a commit carrying a different `product_version` and merges that landed on an already sealed version
- The "Version gate" also checks the database upgrade scripts a pull request touches: a script named above `product_version` is never selected by the upgrade play, and one named for a version the base branch has already passed is skipped by every installation that has taken that version, so both are refused. Upgrade scripts must carry a plain `major.minor.patch` name without zero-padded components, scripts of older versions must not be modified, and no script at or below the current version may be deleted

## 9.5.7 - 28.09.2026
- security fix (SEC-01): the auditor role could update the columns of its own uiuser row that define
  who the account is - uuid, uiuser_username, tenant_id, ldap_connection_id and the password flags.
  A login and a token refresh derive the roles of a user by resolving uiuser.uuid against LDAP, so
  rewriting that column let an auditor have its authorization rebuilt as a different, more
  privileged subject. Self-service updates of uiuser are now limited to uiuser_language for every
  role except the middleware, and each of those permissions constrains the row after the update as
  well as before it, so an update cannot move a row to another subject
- security fix (SEC-04): the LDAP connection test is a POST instead of a GET carrying a body, so
  the credentials entered for a test are no longer part of a request that proxies and http clients
  handle inconsistently and may cache or log
- security fix (SEC-06): the workflow action endpoint could be asked to execute the side effects of
  a state change more than once. The state of a ticket or task is persisted before its actions are
  requested, so the endpoint could only check that the object already stands in the requested state,
  which stays true after the transition happened and therefore let the same request be replayed to
  send mails, raise external requests or create flows again. The new table
  request.state_change_execution records which state the actions of an object were last executed
  for; the middleware claims it in a single statement, and a repeated request now executes nothing
  and shows the warning E8018 instead. Known limitation: workflow monitoring in the modes that
  suppress state actions sets a state without recording it, so the next legitimate return to that
  state is refused the same way and shows E8018 - mail, external request and flow creation for it
  then have to be triggered again by hand
- security fix (SEC-09): the flow catalog tables were readable without restriction by every workflow
  role, and a request element could be pointed at any flow entry by id. A requester could therefore
  enumerate flow objects an administrator had hidden or retired, attach them to a task, and reference
  the canonical any-IP-protocol service, which is an internal representation the platform writes for
  itself. The same eligibility predicate is now enforced at all three layers: the Hasura select
  permissions of the workflow roles on flow.nwobject, flow.svcobject and flow.timeobject return only
  entries that are offered in the request module, not retired and in a live state, and exclude
  negative protocol ids; the Hasura insert and update permissions on request.reqelement refuse a flow
  object, flow group or protocol id that does not meet it; and the flow creation refuses an element
  whose stored flow id names an entry that has since been hidden or retired instead of following it.
  The request module reports a refused element instead of failing the save with a permission error.
  Negative protocol ids stay reserved for the middleware, which continues to attach the canonical any
  service when it turns a protocol-agnostic request into a flow
- security fix (SEC-10): report and notification html is assembled from stored values - object, service,
  device, management and owner names and section headers - and several of those were written into the
  generated document without being encoded for the place they land in. The headless browser that renders
  an export to pdf loaded subresources, so markup smuggled into such a value made the server itself issue
  outbound requests. The renderer now runs with scripting off and aborts every request except the document
  it starts from, and exported documents carry a content security
  policy that denies everything but their own inline styling. The link a report builds around an object
  encodes each part for its own context and refuses a target that does not stay on the document, the table
  of contents no longer turns encoded markup from the body back into live markup, and the headings and
  the object, service and user tables of a rules report encode every imported field they show - name, uid,
  comment and group members. Report output changes in two visible ways: object anchor names are now quoted,
  and exported documents carry the extra policy element

## 9.5.6 - 24.09.2026
- variance analysis: the rule_owner prefilter is no longer blocked by every pending import. A rule import
  without policy changes cannot have changed a marker and is ignored, and where somebody is waiting for the
  result the analysis waits briefly for the mapping run instead of falling back to the much slower marker
  query. The wait is capped by the new setting varianceNameFieldWaitTime (0 disables it) and never happens
  in the background job
- variance analysis: a running full reinitialize of the rule_owner mapping is now detected, so the analysis
  no longer reads a half-rebuilt mapping and silently reports implemented connections as not implemented
- variance analysis: an empty prefilter result is accepted once the mapping exists at all, instead of
  running the full marker query for every owner that has nothing on a management
- variance analysis: every fall back to the marker query is written to the log with its reason and shown
  to the user once per analysis, so the remaining cases can be found without debug logging

## 9.5.5 - 23.09.2026
- workflow phase visibility can be configured per phase as `AnyTask` (the backward-compatible default) or `TicketState` (based on the ticket state).
- REST endpoint workflow/getAuditProofCriticalChanges now also returns a taskDiff for a ticket with audit proof critical changes: for each request task whose content differs from what was originally requested, the original and the current snapshot (a deleted task has no current snapshot; start, stop, additional info and the order of owners and elements do not count as a difference), and the manual audit proof critical implementation task changes with change time, user, change text and the snapshots before and after. The request filter applies to the implementation task changes; the request task comparison describes the ticket as a whole and is not filtered.
- new REST endpoint workflow/getTicket returns a workflow ticket with all its details in JSON: ticket header data, the request tasks with their elements, approvals, implementation tasks, owners and comments, and the ticket comments. It is available to admins and auditors. Besides the workflow state ids and names, the ticket carries the same status that flow/getRequestStatus reports. The optional options.filter restricts the returned request tasks by any of their scalar fields; the ticket itself is always returned when it exists, so a filter that excludes every task yields an empty task list, while a ticketId that names no ticket is answered with 404. All validation errors of a request are reported together.

## 9.5.4 - 23.09.2026
- rework modelling notifications and move interface-request notifications to centralized notification entries
- interface-request notifications now support separate request and reminder bodies plus optional CC to the requester
- UI: the log data shown with a modelling connection now fills the browser window. The table takes as many rows per page as the window allows instead of a fixed 25 and follows a window resize, so a maximised window no longer shows a quarter-filled table with a pager below it. A new page size reaches the table only while the first page is shown, so it never moves the user to a different part of the log.
- UI: auditors can now open the modelling forms of every application - connections, provided interfaces and common services - and read the log data shown in them. Saving, deleting and requesting firewall changes remain with the responsible owners holding the modeller role.

## 9.5.3 - 17.09.2026
- rule owner mapping: an owner import no longer collides with the unique index on rule_owner and no longer
  blocks the incremental processing; a failed run is no longer reported as a successful one
- rule owner mapping: a failing import no longer holds up the imports behind it and raises an alert
- rule owner mapping: an import that fails twice in a row is repaired by a full reinitialize. Where the run
  history the repair relies on cannot be decoded or cannot be written, the repeat is never established and
  the blocked repair is reported as its own alert, naming the config entry and saying whether it has to be
  reset or whether the middleware is missing write access. A run history that could only not be fetched is
  left as the transient case it usually is: the repair is one run late rather than gone
- rule owner mapping: a full reinitialize that matches no rule removes the obsolete mappings and alerts,
  instead of leaving the previous state in place. Without any rule base the stored mappings are kept
- rule owner mapping: a mapping setting saved while its rebuild failed is remembered for up to a week, so
  the next rebuild no longer reports its intended effect as a deviation
- rule owner mapping: a run history entry that cannot be read is no longer written over, and the monitoring
  page says so instead of showing it as an empty history. An entry that cannot be written raises an alert,
  because it stays perfectly readable while the recording has stopped - the page would keep showing the last
  run that was written as if it were the current state
- rule owner mapping: a problem that is still present is reported again and replaces its own earlier alert,
  so the open alert carries the time of the latest occurrence rather than the first one
- rule owner mapping: new page monitoring/rule_owner_mapping shows the last full reinitialize runs, the
  affected rules and what caused a difference
- rule owner mapping: new setting for the log level of mapping issues
- rule owner mapping: new AlertCode RuleOwnerMapping (52) for every alert of this area

## 9.5.2 - 21.09.2026
- centralize modelling and workflow change history in the public schema. The modelling change history table moves from modelling.change_history to public.change_history, so the GraphQL root field is renamed from modelling_change_history to change_history. Scripts and external integrations querying the old field name have to be adapted. Existing entries are migrated, the modelling history views are unaffected and continue to show modelling changes only.
- changes to workflow tickets are recorded in the same table after ticket creation, with the workflow phase and the previous and new values. Content changes made in the user interface by a user other than the requester are marked as audit proof critical. The recording is available to auditors via the API and is not shown in the user interface.
- change_history entries carry a module column naming the subsystem that wrote them, currently modelling or workflow. It selects which enum the object_type column uses and limits the modelling roles to modelling entries. Existing entries are migrated as modelling.
- change history entries written through the REST workflow endpoints name the authenticated caller instead of the middleware server, and carry that caller's user id in changer_id. changer_id therefore stays empty only for changes made by automation - background jobs and unauthenticated internal callers - so an audit can tell an automated change from a human one and resolve the human one to a user record even after a directory rename.
- new REST endpoint workflow/getAuditProofCriticalChanges returns the audit proof critical changes of a workflow ticket: the change history entries of that ticket which are marked as audit proof critical, meaning a content change made in a user session by someone other than the requester. It is available to admins and auditors and reports change time, change user name, change user id and the recorded change text, newest first. The change user id is the trustworthy attribution, as the change user name is free text supplied by the writer of the change; it stays empty for changes made by automation. Change times carry the wall clock of the installation and no offset, because the underlying column is timezone naive. A ticketId that names no workflow ticket is answered with 404 and the error message "Workflow ticket with 'ticketId' <id> does not exist." instead of an empty changes list, so a mistyped or already deleted ticket id cannot be read as a ticket without audit proof critical changes; an existing ticket without such changes, or one whose changes the supplied filter excludes, still answers 200 with an empty list.
- matrix import now stores the network zone tree paths it already validated: for every subnet of a zone, the firewalls on its path to root and to the internet are written to network_zone.device_ip_range_root and network_zone.device_ip_range_internet, in the order the import file lists them. An import replaces all paths of the matrix it imports. A subnet listed twice within one zone is now rejected instead of imported.

## 9.5.1 - 12.09.2026
- installer: the one-shot `internalca_reset_certificates` upgrade switch rotates the internal
  CA and every FWO-managed client and server identity, including the self-signed identities
  from versions before 9.5.0, while preserving customer-managed certificate/key pairs. The
  subject of every retired CA is recorded, so a host that misses the rotation still has its
  FWO-issued identity recognised as such on its next upgrade instead of being taken for a
  customer certificate, and the installer refuses the switch when it is left in
  /etc/fworch/fwo-install-settings.yml rather than passed for the single run that rotates.
  The reset stops before replacing the old CA key when neither the CA nor its client leaf
  can provide the retired issuer name, and `internalca_issue_ldap_certificate=false` keeps
  FWO from issuing or replacing an externally managed OpenLDAP identity, while the trust,
  address and chain checks FWO clients depend on still apply to it. Client identities
  exported to a browser or issued to a person from the old CA are copies and are not
  rotated by the run - re-export or re-issue them, see documentation/certificates.md
- installer: `internalca_issue_apache_certificate=false` now also keeps the FWO Apache vhosts
  on the certificate and key the installed vhost already names, instead of pointing them at
  an identity the installer has just been told not to create
- UI fix: the start page shows its quick start section again and the English "What's new"
  panel ends in English - a merge had dropped both getting_started texts and appended the
  German menu list to the English whats_new_facts entry
- move compliance.ip_range to new schema network_zone.ip_range and compliance.network_zone to network_zone.zone
- add central setting for path analysis algorithm
- move many compliance settings regarding matrix and internet to their own setting page in new section network topology
- prepare network zone tree algorithm in database
- general flow settings define via name patterns which flow network groups are zones; REST endpoint flow/getAddressGroups returns zone groups as a separate list when called with option.separateZoneGroups=true
- extend the flow compliance REST endpoints to accept IPv4 and IPv6 ranges as well as CIDR networks
- report rules containing objects that cannot be assigned to a compliance network zone as `NOT ASSESSABLE` instead of compliant; real violations of the same rule remain decisive and visible
- new import matrix format with path_to_root and path_to_internet, while old format is still supported
- validation checks for matrix import

## 9.5.0 - 09.09.2026
- introducing
  - an internal CA and certificate checks for all internal communication
  - client certificates for graphql API access to prevent unauthorized access
  - validated Apache intermediate certificate-chain references for administrator-managed certificates
  - the Settings Defaults page displays the public internal CA certificate and allows copying or downloading it
  - a per-installation internal CA name, so that a browser or trust store can hold the anchors of
    several FWO installations at once; existing installations keep the name their CA was created with,
    and a browser still holding an anchor of the same name from another installation has to have it
    deleted before the new one is imported
- application roles may now only be changed by an owner holding the modeller role
- the ldap connection passwords are no longer readable via the API, not even for auditors
- **the middleware now verifies LDAP server certificates instead of accepting any of them.**
  This applies to every LDAP connection using TLS, internal and external alike. A connection
  whose certificate is issued by FWO's internal CA, or by a CA the middleware host already
  trusts, keeps working untouched. A connection whose certificate is self-signed, issued by a
  private CA that is not in the host trust store, or does not carry the address the connection
  is configured with, was silently accepted before and is now rejected - which means those
  users can no longer log in. Add the issuing CA to the middleware host's trust store, or have
  the certificate reissued for the address FWO connects to. The middleware names the server and
  the reason in its log (category LdapTls). The installer refuses the upgrade up front if a
  retained administrator-managed OpenLDAP certificate does not cover the configured address,
  if its issuing root CA is not configured as `internalca_peer_ca_certificate`, or if the
  certificate cannot be built to one of the configured anchors at all - a leaf whose issuing
  intermediate the certificate file does not carry, for instance
- installer: all endpoint names (api, middleware, ui) are derived from inventory/hosts.yml,
  so an installation that must be addressed under a specific DNS name - a name an
  administrator-managed certificate was issued for, above all - is configured in one place
- installer: new host-wide settings file /etc/fworch/fwo-install-settings.yml,
  read by every installer run from any clone on the host, whichever way ansible was started,
  and outside the git repository, so
  local settings survive git pull and every administrator upgrades with the same endpoints;
  fwo_endpoint_hostname there names all endpoints of a single-host installation at once,
  and a commented fwo-install-settings.template.yml is installed beside it for reference
- the middleware no longer waits for an unreachable API forever before starting its web server;
  it now names the endpoint and what to check in the log and exits, instead of reporting itself
  as running while its reverse proxy answers 503
- installer: an internal CA certificate is now reissued as soon as it stops covering a name
  the installation addresses its endpoint under, not only when it approaches expiry, so
  renaming an endpoint or setting fwo_endpoint_hostname on an existing installation no longer
  leaves every FWO client failing on a TLS host name mismatch
- versioning: **breaking change** upgrades from versions older than 8.0 are not supported any more.
  Every upgrade step below 8.0 has been removed - the database migrations, the version numbered
  upgrade tasks of the other roles and the LDAP tree ldif templates alike - and the installer now
  stops an upgrade from an older version before it changes anything, naming the two-step path
  (upgrade with a v8.9.6 checkout first, then with this one) instead of skipping the missing
  schema changes silently

## 9.4.7 - 07.09.2026
- enforce host-address masks for flow network-object range endpoints
- require both endpoints of a flow network-object range to be of the same address family
- store the range endpoints of flow network objects created from a request in CIDR notation
- widen a requested network endpoint to the first and the last host address of that network
- name the refused addresses in the message of a failed flow creation
- normalize existing flow network-object endpoints carrying a network mask during the upgrade
- warn during the upgrade about flow network objects sharing a range, they have to be merged manually
- stop the upgrade and name the affected flow network objects when their endpoints mix address families

## 9.4.5 - 01.09.2026
- FortiManager: keep service groups (and RPC and split multi-protocol services) normalized with ip_proto_id=null instead of protocol 0/HOPOPT; upgrade corrects previously imported data to match
- add an optional compliance-diff filter for rules with existing violations
- Insert missing src/dst/svc references to flow.access entries created by workflow module
- Flow sync now recalculates the hashes stored in the flow database when they no longer match the current hash logic, instead of skipping the affected management. Entries whose hash was generated randomly keep their hash, groups and accesses are recalculated from their members, and only changed hashes are written. Creating a flow from a request while such a recalculation runs can fail or reuse a wrong entry, because flow entries are identified by their hash; repeat the action in that case. Hashes are only recalculated when the hash logic itself changes.
- Flow time objects created by the request module before 9.4.5 stored their start and end time shifted by the UTC offset of the middleware server. The hash recalculation takes the stored times as they are, so these time objects keep the shifted period and get a new hash, which also changes the hash of every flow access using them. They are not repaired automatically: check time restrictions of flows created before 9.4.5 and request them again if the period is wrong.

## 9.4.4 - 27.08.2026
- Import protocol-agnostic `ANY` services with new ip_proto_id=-1 across supported importers and automatically create and map the corresponding flow service objects.
- Keep the canonical ANY flow service object implemented and protect it from removal.
- Return `null` service port bounds for protocol-only flow catalog objects instead of 0
- Rule and connection reports filtered by destination port or protocol now also match the canonical ANY service, so these filters can return more rows than before the upgrade.
- Cisco ASA: keep the legacy `ANY` UID for the imported "any ip protocol" service object while showing it as `any-ip` in reports; existing compliance criteria or queries pinned to the old `any-ip` UID need to be updated to use `ANY` instead.
- OPNsense: a `tcp/udp` rule with no destination port now creates and references two separate service objects (`Any/tcp` and `Any/udp`) instead of one combined object.

## 9.4.3 - 26.08.2026
- normalize and import fqdn and dynamic ip network objects with ip=null instead of 0.0.0.0/0
- IP-based tenant filtering excludes rules whose source or destination contains only addressless network objects
- add 'add auto calculated internet zone' button to compliance matrix ui

## 9.4.2 - 25.08.2026
- add setting and guard for full rollback: the deletion of all import data of a management is now gated behind the new "allowFullRollback" setting, which defaults to disabled so existing installations keep the safe behaviour after upgrade.

## 9.4.1 - 17.08.2026
- add nat import for checkpoint and forti firewall managements
- fix checkpoint import policy install detection
- fix nat rules report

## 9.4.0 - 13.08.2026
- add logging schema for imported traffic log entries with their owner and count
- make replacement of existing log entries for applications contained in an import file configurable
- increase of unit-tests
- Fixed variance-analysis comparison for imported identity network objects such as Check Point updatable objects, access roles and domain objects. When IP based rule recognition is active, configured placeholder areas for special configurations are reconciled with these imported objects by object type and name instead of placeholder IP fields.

## 9.3.4 - 11.08.2026
- fix a bug in reporting ui where users with role modeller and reporter could not fetch rsb data for app rule reports

## 9.3.3 - 10.08.2026
- fix a bug in provisioning where network objects were always created with mask 255.255.255.255

## 9.3.2 - 06.08.2026
- fix upgrade failure when migrating workflow state matrices with non-JSON configuration values

## 9.3.1 - 03.08.2026
- make rule change-ID custom field names configurable in compliance settings
- ! breaking change: old hard-coded default value "Datum-Regelpruefung" is dropped and replaced, so existing installations using this feature be impacted!
- changed workflow email CSV/JSON output and firewall request popup to separate current, added and removed group members
- various memory leakage fixes in UI

## 9.3 - 31.07.2026
- new import module for OPNsense firewalls

## 9.2.5 - 29.07.2026
- update flow time object hashing to be timezone invariant
- ! resets the flow timeobject and access tables

## 9.2.4 - 28.07.2026
- fix startup behaviour of fworch services on redhat/rocky
- close some test gaps in middleware server
- fix legacy sonar findings
- fix config change in modelling
- feat new REST API endpoint: ResolveZonesForObjects
- feat new REST API endpoint: time object ID lookup

## 9.2.3 - 17.07.2026
- speed up standard rules reports by scoping flat rule paging to selected rulebases
- add database index for standard rules report paging
- increase of unit-tests
- split + enhance Wf Action Settings
- fix: recognize multiple updatable objects by @tpurschke in #4980
- fix missing enforcement of rule
- feat new REST API endpoint: create request/ticket
- feat new REST API endpoint: getIpDataForOwners - get application ip addresses
- feat new REST API endpoint: flow return nwobj type
- Fix/installer pip config redhat

## 9.2.2 - 15.07.2026
- add generic firewall import
- enable azure2022ff for normalized config import

## 9.2.1 - 12.07.2026
- migrate firewall tables to firewall schema

## 9.2.0 - 10.07.2026 MAIN
This release makes FWO compatible with the following operating systems:
- Ubuntu 22.04 and 24.04
- Debian 11 & 12
- Red Hat 9 (new - tested with v9.8)
- Rocky 9 (new - tested with v9.8)
- Ubuntu 26.04 (new)
- Debian 13 (new)

Not supported any longer are:
- Ubuntu <  22.04
- Debian <  11

## 9.1.17 - 08.07.2026
- credentials field in management for writing on Firewalls
- migrate old Tufin template to list entry
- Support for writing firewall configurations via templates
- A template provider (Tufin or custom templates) must be configured before use
- FW config change external workflow is only active after explicit assignment per management and change category in the settings

## 9.1.16 - 06.07.2026
- move workflow state matrices to own database tables

## 9.1.15 - 06.07.2026
- make offered protocols configurable

## 9.1.14 - 01.07.2026
- remove deprecated, unused rule.rule_num column (rule ordering is handled by rule_num_numeric)
- remove deprecated, unused direct rule zone columns (rule_from_zone, rule_to_zone); rule zones remain available through the rule_from_zone and rule_to_zone link tables

## 9.1.13 - 29.06.2026 MAIN
- fix: handle gateway without rule when generating reports

## 9.1.12 - 26.06.2026 MAIN
- fix missing recertifier permission for owner recertification in hasura metadata

## 9.1.11 - 24.06.2026 MAIN
- fix: rule_owner_mapping - standardize constraint name

## 9.1.10 - 22.06.2026
- change internal logic to handle src/dst zones as security-relevant
- backfill existing rule source and destination zone text fields from rule zone links

## 9.1.9 - 18.06.2026
- request workflow: add locked tickets and request tasks for automatically created change requests
- further integration flow into workflow

## 9.1.8 - 16.06.2026
- flow db: access flows now include time objects and allow/deny flag in their functional definition
- flow sync: add hash consistency check
- this update resets the flow db!

## 9.1.7 - 08.06.2026
security patch

This PR hardens FWO installation and security-sensitive workflows. It restricts app data import file/script paths, reduces installer secret exposure, tightens Hasura config permissions, improves LDAP/install test idempotency, removes the obsolete webhook role, and fixes related installer/test reliability issues. It also includes targeted documentation and version updates.

- Import file/script handling now restricts app data sources to allowed .json and .py files under the customizing directory, rejecting unsafe paths and logging file hashes.
- Installer secret handling now uses no_log, avoids passing Hasura secrets on command lines, and prints secret file locations instead of secret values.
- LDAP installation now uses password files, tolerates existing entries, seeds missing test LDAP parents, and avoids premature middleware restarts.
- Hasura permissions now prevent scoped users from modifying global config rows while giving middleware-server dedicated global config access.
- Installer and integration tests now wait for required services, tolerate missing optional customizing scripts, and improve cleanup/reliability behavior.
- The obsolete webhook role and its docs, service files, templates, syslog, logrotate, and playbook wiring were removed.
- The app data import UI now selects allowed import stems instead of accepting arbitrary free-form paths.
- Product documentation and revision history were updated for this security-hardening release.
- The password-change REST endpoint now has an explicit [Authorize] requirement so anonymous callers are rejected before password-change logic runs.
- Importer and customizing-script HTTP calls now use connect/read timeouts so a stalled firewall or API endpoint can no longer hang an importer worker indefinitely.
- FortiOS (REST) VIP/destination-NAT objects are now normalized to their external IP, so policies referencing VIP objects resolve instead of failing the import or losing coverage.
- Tenant settings role handling now mirrors the backend per operation: the page stays viewable for admin/auditor/fw-admin, adding/deleting tenants and saving device visibility are admin-only (matching the REST and Hasura permissions), and editing existing tenants is allowed for admin/fw-admin.
- Remaining hardcoded strings on the scheduler monitoring page were moved into the localization texts.
- The shared confirm dialogs now raise DisplayChanged(false) after a successful action, so the parent's bound visibility state no longer remains stale.

## 9.1.6 - 08.06.2026
- remove obsolete database last_seen fields

## 9.1.5 - 05.06.2026
- remove old jwt token lifetime config values
- asynchronous initial JWT bootstrap in the UI
- subscription-aware reconnect logic after JWT refresh
- a separate GraphQL subscription client path
- improved cancellation and JWT-expiry handling
- a small cleanup of exception logging for subscription errors

## 9.1.4 - 03.06.2026
- remove legacy ownerLdapGroupNames owner mapping fallback

## 9.1.3 - 27.05.2026
- add optional workflow flow merging for Flow DB creation

## 9.1.2 - 26.05.2026 MAIN
- database: fix flow foreign key duplication on fresh install plus upgrade path

## 9.1.1 - 21.05.2026
- Workflow: add configurable execution order for actions assigned to states.

## 9.1.0 - 20.05.2026
- JWT refresh token
- introduce flow schema

## 9.0.24 - 27.04.2026
- introduce new modelling integration mode WorkflowNotifications

## 9.0.23 - 27.04.2026
- enhance notifications by bcc
- add display-only workflow label report column option
- add default template for workflow tickets approved last week
Removed deprecated configuration keys:
- updateRuleOwnerMappingActive
- updateRuleOwnerMappingStartAt
These settings are no longer used due to the full automation of UpdateRuleOwner.

## 9.0.22 - 26.04.2026 MAIN
- fixes missing source or destination in rule expiry notification report
- fixes time zone issues with checkpoint time objects
- fixes python tests failing on python 3.10
- fixes owner import from custom file

## 9.0.21 - 21.04.2026 MAIN
- fix ldap users with special chars not being processed correctly in role handling
- fix empty mail being sent for orphaned rule report
- update dependencies (notably closing mailkit and pytest vuln)
- fix time zone issues in importer

## 9.0.20 - 11.04.2026
- extend notification handling

## 9.0.19 - 09.04.2026
- add owner additional_info jsonb field including owner edit UI support

## 9.0.18 - 03.04.2026
- add new column automatic_only to workflow states

## 9.0.16 - 31.03.2026
- remove not needed stm_owner_mapping_source
- add Full re-initialize of RuleOwner mapping for IP-based rules
- add matched_objects field in rule_owner table for track matched objects - IpBased

## 9.0.16 - 26.03.2026 MAIN
- bug fixing
- moving from docker to podman

## 9.0.15 - 19.03.2026
- rename OwnerSourceCustomFieldKey to CustomFieldOwnerKey in config

## 9.0.14 - 17.03.2026
- prepare owner decommission notification

## 9.0.13 - 12.03.2026
- mark lifecycle states as active

## 9.0.12 - 12.03.2026
- new config values for rule expiry notification

## 9.0.11 - 04.03.2026
- new config value for requesting only own objects

## 9.0.10 - 28.02.2026
- new config value for User synchronization in owner data import

## 9.0.9 - 25.02.2026
- remove stale v8 code

## 9.0.8 - 25.02.2026
- new config value for removed App Server handling

## 9.0.7 - 25.02.2026
- add import of time objects
- create changelog_owner table

## 9.0.6 - 20.02.2026
- add import of time objects

## 9.0.5 - 18.02.2026
- update rule_owner table for REST api
- update import_control to allow flexible tracking of different import types
- create rule_owner mapping for custom_field via button and service/job
- update import_control to allow flexible tracking of different import types

## 9.0.4 - 13.02.2026 MAIN
- maintenance release with explicit 9.0.4 upgrade step

## 9.0.3 - 12.02.2026
- introduce interface permissions

## 9.0.2 - 10.02.2026
- importer: call api chunked where needed

**Breaking changes**
- Due to introduction of venv for all imports, the following steps have to be taken to manually import a config:

```shell
  sudo -u fworch -i
  cd importer
  source importer-venv/bin/activate
  python3 ./import_mgm.py -m xy -fs -d1
```
  As we now need support for pip, in installations behind url filter, make sure that all sub-domains of "pythonhosted.org" are also allowed.

- Limiting database listener to localhost for security reasons

## 9.0.1 - 07.02.2026 MAIN
- generalized owner responsibles with configurable responsible types
- add allow_write_access to responsible types to control modelling and recertification

## 9.0 - 24.01.2026 MAIN
A complete 80K lines rework of FWO, including
- database changes to deduplicate rules (rule to gateway mapping now 1:n by introducing rulebase and rulebase_link tables)
- migrating import module from mixed python/pgsql to pure python

## 8.9.6 - 05.01.2026 MAIN
- new parameters for notifications

## 8.9.5 - 10.12.2025 MAIN
- bugfix release: modelling - change planning showed duplicate NA elements for rule delete requests

## 8.9.4 - 09.12.2025 MAIN
- bugfix release: common service connection not editable
- new custom scripts for iiq and cmdb import

## 8.9.3 - 05.11.2025 MAIN
- hotfix missing permissions for app data import in certain constellations

## 8.9.2 - 17.10.2025 MAIN
- add ownerLifeCycleState
- add manageable ownerLifeCycleState menu
- fix two modelling ui glitches

## 8.9.1 - 02.10.2025 MAIN
- owner-recertification

## 8.8.10 - 07.09.2025
- new report type owner-recertification

## 8.8.9 - 27.08.2025
- prepare tables + settings for owner recert + first throw recert popup
- notification service
- decommissioning of interfaces
- iconification of modelling and related modules
- fix overwrite of objects with interface

## 8.8.8 - 23.08.2025 MAIN
- add read-only db user fwo_ro
- hadening changes
  - apache config (information leakage)
  - listeners (hasura, postgres)
  - log santisation

## 8.8.8 - 21.08.2025
- add read-only db user fwo_ro
- also reducing db listener to localhost and other hardening changes

## 8.8.6 - 22.07.2025 MAIN
hotfix release
- CP importer new
  - stm_track: "extended log" and "detailed log"
  - fixing services-other ip proto import
- improved quality control with stricter automated checks
- various fixes in modelling module

## 8.8.6 - 08.07.2025
- hotfix CP importer new stm_track: "extended log" and "detailed log"

## 8.8.5 - 17.06.2025
- new enum values for Request Element Field Types
- hotfix change recognition: separate rule changes and "all changes" to make object version handling work properly

## 8.8.4 - 02.06.2025
- hotfix for Check Point importer suppor for DLP actions (ask, inform)

## 8.8.3 - 15.05.2025
- deactivation of connections

## 8.8.2 - 07.05.2025
- displayed state via variance analysis

## 8.8 - 17.04.2025 MAIN
* fix stm_action by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2844
* add missing rulebase_link constraints by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2845
* fix rule_metadata creation by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2865
* remove dev_id fk constraint from rule_metadata by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2909
* fix missing rule_metadata.rulebase_id by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2911
* fix warnings and rule normalize bug by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2912
* fix missing upgrade scripts from pre 9 by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2938
* Cactus develop fix importer main level bug by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3009
* Endpoint for getting rules by @abarz722 in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3027
* ExtRequest - increase logging by @abarz722 in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3029
* Nuget Updates by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3038
* Nuget Updates by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3042
* fix(ui): ip filtering in app report by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3040
* Preventing use of NA objects in connections by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3043
* fix(ui rsb): ui crash likely caused by duplicates in query result by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3046
* LDAP Nuget Update changes by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3056
* Defer AZ creation until second button click by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2856
* Removing minor py-re deprecation warnings  by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3053
* feat(ui): rsb enhancements by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3073
* User UI glitch by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3089
* Modelling new AR drop down strange initial value by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3091
* Verify modelled services for empty groups by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3087
* adding app servers fails without name by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3088
* Modelling - no NA should be usable for selected interfaces by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3086
* new customized app data import script by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3101
* adding csv appdata import stats by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3103
* reformatting app server ip struct by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3105
* css cache changes by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3108
* show more clearly if everything is (horizontally) displayed by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3096
* Fixed connection object duplication by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3118
* Modelling csv import improvements by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3113
* IP check improvements by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3133
* Nuget Updates by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3136
* Some report generation improvements by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3117
* Config change subscribe add "autoReplaceAppServer"  #3138 by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3148
* Nuget Updates by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3143
* External ticket timout fix by @NilsPur in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3151
* feat(ui): ip filter line observes negation in rules by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3164
* allow for flexible ldap group name templating, fix #3114 by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3165
* Variance Report First Throw by @abarz722 in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3080
* feat(ui rsb): show ip/port of flat members by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3172
* fix(ui report): ip filter on negated rule to/from by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3173

## 8.7.2 - 20.03.2025
- new config values
- external request: attempt counter

## 8.7.1 - 07.03.2025 MAIN
- fix modelling select existing interfac
- fix modelling settings ldap selection
- fix workflow ticket close spinner

## 8.7.1 - 05.03.2025
- ldap writepath for groups

## 8.7 - 03.03.2025 MAIN
- General UI
  - pop-up unification and clean-up
  - removing unnecessary scroll-bars
- PDF generation: replacing engine wkhtml with puppeteer
- Modelling
  - Edit application role (AR): make objects sortable by IP or name
  - adding change requests to history
  - adding option to name all application servers by reverse DNS and fall-back to prefix + ip
- API: upgrade Hasura to 2.45.2
- Workflow: some performance improvements

## 8.6.3 - 20.02.2025
- dns lookup for app server names

## 8.6.2 - 03.01.2025 MAIN
Hotfix for network modelling:
- fix: when visiting the library for the second time, app servers were missing due to uninitialized area data.

## 8.6.1 - 17.12.2024 MAIN
Fixes network modelling
- lock external requests to avoid multiple external tickets
- fix missing comments
- wait cycles for access request after group changes
- save publish flag at interface creation
- disregard dummyAppRole for status determination
- inherit extra configs from interface
- sanitize extra configs
- sort tasks for connection Id and show already adapted name of new members
- small monitoring adaptations
- some cleanup + removal of compiler warnings
- fix ldap group creation regression
- restrict owner_network uniqness constraint to same import source
- UI interface search pop-up transformed into filterable table

Upgrade Hasura API to v2.45.1

## 8.6.1 - 12.12.2024
- external request: introduce locks

## 8.6 - 11.12.2024 MAIN
Features
- Modelling
  - Create Application Zones
  - Add monitoring for external requests for admins
  - Add re-initialization for external requests
  - consolidation modelling external requests
  - adding optional access requst on behalf of UI user
  - adding live update of external task/ticket status
  - app server name handling rework (NONAME --> <prefix>_<IP address>)
  - owner groups can now also be external LDAP groups
- Reporting
  - refining connection report (adding Common service, app role, network area details)
Fixes
- Importer
  - adding missing colors in Check Point importer
  - new VOIP service object and Internet object
- UI
  - SECURITY: updating System.Text.Encodings.Web v4.5.0 --> v8.0.0

## 8.5.4 - 04.12.2024
- external request: introduce wait cycles

## 8.5.3 - 27.11.2024
- owner import - make ldap selectable (internal/external)
- small fixes regarding missing config data for two schedulers (daily, app data import)

## 8.5.2 - 27.11.2024
- some check point importer fixes
  - 4 new colors
  - added Internet object
  - added voip one more object

## 8.5.1 - 18.11.2024
- reporting - fixing PDF generation on various platforms
- modelling - fixing AR editing: strict prevention of all area mixing

## 8.5 - 13.11.24 MAIN
Network Modelling feature update
- modelling can be requested as firewall change via external ticketing tool
- includes all approle handling
- simple form of rule change request (always request all connections as rules)
- api hasura upgrade to 2.44.0
Fixes
- various small UI fixes
- importer (CP: handle None objects)

## 8.4.3 - 05.11.2024
- extra parameters in modelling connection

## 8.4.1 - 30.10.24 MAIN
Network Modelling feature update
- import of app server IP addresses via CSV upload
- import of multiple sources for area IP data
- new option email notification: fall-back to main owner if group is empty
Fixes
- corrections in displaying UI messages
- converting owner network ip data to standard format "range"
- importer
  - check point - fix import of all VSX instances
  - fortinet - add hit counts and install on information

## 8.4.2 - 17.10.2024
- external request

## 8.4.1 - 15.10.24
- Add missing FK connection.proposed_app_id #2591

## 8.4 - 30.09.24 MAIN
Stability release
- various small bug fixes
  - installer (redundant code deleting test user)
  - importer (switching from full details to standard, re-adding VSX gateway support, voip domain handling in cp parser)
  - reporting (app-rule report containing multiple objects)
  - middleware (config subscriptions)
  - reporting (temporarily highlight linked to object in rsb)
  - modelling (sync connections - not always part of overview table after creation)
  - RBA (role picking when user has multiple roles)
  - UI various: adding missing pager control
  - UI various: spinner clean-up
- features/upgrades
  - Added login page welcome message and settings
  - Added last hit information in app-rule report
  - API - upgrading to 2.43.0
  - various security upgrades dotnet (restsharp, jwt, ...)

## 8.3.2 - 09.09.2024
- Added welcome message and settings

## 8.3.1 - 14.08.24 MAIN
Hotfix:
- in CheckPoint importer: fix missing group members

## 8.3.1 - 08.07.2024
- workflow: external state handling
- fix config value
- remove uniqueness of owner names

## 8.3 - 25.06.2024 MAIN
Maintenance release
- fix misleading login error message when authorisation is missing
- fix email credential decryption
- start of Tufin SecureChange integration
- remove cascading delete for used interfaces
- owner-filtering for new report type
- new setting for email recipients
- owner-import custom script improvements#

## 8.2.4 - 19.06.2024
- owner-filtering for new report type
- new setting for email recipients

## 8.2.3 - 26.05.2024
- remove cascading delete for used interfaces
- new properties field in connections

## 8.2.2 - 14.05.2024
- fix email credential decryption
- start of Tufin SecureChange integration

## 8.2.1 - 03.05.2024
- fix misleading login error message when authorisation is missing

## 8.2 - 30.04.2024 MAIN
- new workflow for modelling: interface request
  - adding all imported modelling users to local db (uiuser) - to enable email notification
- new features for modelling
  - display NAs in Report LSB and Export
  - count and display members of areas in selection list
- upgrade to dotnet 8.0 (middleware and UI server)
- encrypt emailPassword in config
- fixes:
  - demo managements (change import from deactivated to activated - does not affect test managements)

## 8.1.2 - 22.04.2024
- encrypt emailPassword in config
- fix demo managements (change import from deactivated to activated - does not affect test managements)
- upgrade to dotnet 8.0
- adding all imported modelling users to uiuser

## 8.1.1 - 15.04.2024
- interface request workflow first version

## 8.1 - 10.04.2024 MAIN
- UI: iconifying modelling UI buttons (can now use icons instead of text buttons - configurable per user)
- Importer: first version of VMware NSX import module
- API: adding customizing script for bulk configs via API
- Database security: all credentials in the database are now encrypted - breaking change (for developer debugging only): add the following local file when using -e testkeys=true:
  /etc/fworch/secrets/main_key with content "not4production..not4production.."
- Importer fix: remove log locking from importer due to stalling importer stops

## 8.0.3 - 08.04.2024
- add maintenance page during upgrade
- sample customizing py script with sample data, closes  Installer customizable config (settings) #2275
- remove log locking from importer due to stalling importer stops
- credentials encryption, closes encrypt passwords and keys #1508
  - breaking change for developer debugging: add the following local file when using -e testkeys=true:
    /etc/fworch/secrets/main_key with content "not4production..not4production.."
- add custom (user-defined) fields to import
  - cp only so far, other fw types missing
  - user-defined fields are not part of reports yet

## 8.0.2 - 11.03.2024
- first version of NSX import module

## 8.0.1 - 20.02.2024
- iconify modelling
- add missing config values

## 8.0 - 19.02.2024 MAIN
- Introducing new Network Modelling module
  - allows your organisation to define the target state of all network connection on a per-application basis (or other distributed ownerships)
- Backend
  - Introducing Scheduled import change notification including inline or attached change report (replacing simple import notification from import module)
  - upgrade hasura graphql API to 2.37.0
- UI
  - New look and feel: Moving to vanilla bootstrap css v5.3.2 (allowing for future up to date css usage)
  - ip based tenant filtering: introducing unfiltered_managements and devices and adding extended tenant to device mapping settings
- Installer (breaking change!)
  - introducing venv for newer ansible versions and thereby removing annoying ansible version handling in installer (see https://github.com/CactuseSecurity/firewall-orchestrator/blob/main/documentation/installer/basic-installation.md for details)
- bugfixes for
  - import log locking
  - integration tests with credentials when installing without demo data
  - pdf creation on debian testing plattform (trixie)
