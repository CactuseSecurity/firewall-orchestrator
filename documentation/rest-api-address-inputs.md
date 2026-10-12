# REST API address inputs

The middleware uses one address-input contract in these request locations:

- `POST /api/flow/getAddressObjectId`
- Each `source` and `destination` entry of `POST /api/flow/getFlowComplianceState`
- Each leaf of `POST /api/Compliance/resolveZonesForObjects`
- Each `addressObjects` entry of `POST /api/workflow/createTicket`

Each entry must supply exactly one non-empty representation:

```json
{ "ipHost": "192.0.2.10" }
```

```json
{ "ipNetwork": "192.0.2.0/25" }
```

```json
{ "ipRange": ["192.0.2.10", "192.0.2.20"] }
```

This is a breaking request-contract change: `ipStart` and `ipEnd` are no longer request fields.
Clients must migrate each former start/end pair to `ipHost`, `ipNetwork`, or the two-entry
`ipRange`. The first three endpoints reject the removed names through their generic unknown-key
validation. `createTicket.addressObjects` retains its existing JSON behavior and ignores unknown
keys, but an entry containing only the removed names fails because it has no valid address
representation.

## Validation requirements

- `ipHost` accepts one maskless IPv4 or IPv6 address.
- `ipNetwork` requires CIDR notation. `/32` and `/128` are valid one-address networks. CIDRs with
  host bits are accepted and normalized; for example, `192.0.2.10/24` becomes the inclusive range
  `192.0.2.0` through `192.0.2.255`.
- `ipRange` contains exactly two non-empty, maskless addresses of the same family, ordered from the
  lower endpoint to the upper endpoint. Equal endpoints are valid.
- Address text is interpreted by the established FWO IP parser in strict IPv4 mode. The REST
  contract intentionally inherits that parser's supported forms and normalization instead of
  defining a separate grammar. For example, four-part IPv4 input with leading zeroes follows the
  shared parser's interpretation, and scoped, IPv4-mapped, and IPv4-compatible IPv6 forms are
  accepted when the shared parser accepts them.
- A `resolveZonesForObjects` leaf whose type is `host` must normalize to one address.

Accepted values are normalized internally to inclusive start and end bounds. Responses and stored
workflow elements keep their existing contracts: hosts map to equal bounds, networks expand to their
first and last addresses, and ranges retain their normalized endpoints.
