# OPNsense API

API endpoints documented here: <https://docs.opnsense.org/development/api.html>

## User setup

Tested with "read-only all" user permissions:

- Group `admins_ro` with privileges: `All pages`, `System: Deny config write`

And created an API key `$OS_API_KEY:$OS_API_SECRET`.

## Config source

The importer only calls the core backup API (`/api/core/backup/download/this`) when no native config
was handed in. If a native config is provided (import from file or URL), it is used as-is and no
request is sent to the firewall.

## Sanitizing

Every native config - fetched or supplied - is sanitized before it is stored or written to a debug
dump. Sanitizing drops the sections that are irrelevant for policy import (dynamic DNS, VPN, IDS,
monitoring, ...) and additionally sweeps all remaining sections for credential-bearing elements
(passwords, pre-shared keys, private keys, API keys, tokens, ...).

## Interface selectors

OPNsense rules can reference an interface instead of an address object:

- `$interface` (e.g. `lan`) selects the subnet configured on the interface and is normalized to a
  network object.
- `$interface` + `ip` (e.g. `lanip`) selects the interface address itself and is normalized to a
  host object.
- Dual-stack interfaces become a group holding the IPv4 (`_v4`) and IPv6 (`_v6`) member object.
- Interfaces without a static address (DHCP, SLAAC, unconfigured) stay an empty group.
- Interface groups are normalized to groups holding their member interface objects.
