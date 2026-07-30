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

## Parsing tolerance

`config.xml` omits empty elements (a group without members has no `<member>`, a DHCP gateway stores
`dynamic` instead of an address). Supplementary sections - users, groups, interfaces, interface
groups, aliases and gateways - are therefore parsed leniently: an entry that cannot be validated is
logged and skipped instead of aborting the import. Access rules are the exception and stay strict,
so that no rule is ever silently dropped.

## Services and protocols

A rule's protocol is part of its service in the FWO model, while OPNsense keeps it on the rule and
its port aliases are protocol-agnostic. Rules restricted to TCP or UDP therefore reference a
protocol-specific service named `<port>/<protocol>` (e.g. `53/tcp`, `Any/tcp`, `WebPorts/tcp`),
carrying the matching `ip_proto`; a port alias used by both a TCP and a UDP rule is instantiated
once per protocol. `any` and `tcp/udp` rules cannot be expressed as a single `ip_proto` and keep the
plain, protocol-agnostic service. Non-port protocols (ICMP, ESP, GRE, ...) become a service named
after the protocol.

## Rule uids

Rules are keyed by their `@uuid`. Legacy rules written by older OPNsense/pfSense generations may
have none; those get a deterministic uid derived from the rule content (prefix `opnsense-rule-`), so
it stays stable when the rule is moved. Identical uid-less rules are disambiguated by a counter.

## Object naming

Addresses and ports written directly into an alias are normalized under their plain literal
(`192.0.2.10`, `80`), which is the same name a rule referencing that literal produces - alias members
and rule literals therefore end up as one object instead of two.

## Interface selectors

OPNsense rules can reference an interface instead of an address object:

- `$interface` (e.g. `lan`) selects the subnet configured on the interface and is normalized to a
  network object.
- `$interface` + `ip` (e.g. `lanip`) selects the interface address itself and is normalized to a
  host object.
- Dual-stack interfaces become a group holding the IPv4 (`_v4`) and IPv6 (`_v6`) member object.
- Interfaces without a static address (DHCP, SLAAC, unconfigured) stay an empty group.
- Interface groups are normalized to groups holding their member interface objects.
