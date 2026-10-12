# REST API CIDR Support Testing Plan

## Purpose

Verify that the supported REST endpoints accept exactly one of `ipHost`, `ipNetwork`, or `ipRange`,
normalize it to inclusive address bounds, and reject malformed or ambiguous input.

The affected endpoints are:

- `POST /api/flow/getAddressObjectId`
- `POST /api/flow/getFlowComplianceState`
- `POST /api/Compliance/resolveZonesForObjects`
- `POST /api/workflow/createTicket`

The same contract applies to address lookup, every flow source/destination, every zone-resolution
leaf, and every create-ticket address object.

## Automated tests

Run the focused feature tests:

```powershell
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --no-restore `
  --filter "FullyQualifiedName~IPOperationsTest|FullyQualifiedName~AddressInputValidationTest|FullyQualifiedName~FlowComplianceValidationTest|FullyQualifiedName~ComplianceZoneValidationTest|FullyQualifiedName~FlowControllerValidationTest|FullyQualifiedName~CreateTicketEndpointTest|FullyQualifiedName~OpenApiRequiredSchemaTransformerTest|FullyQualifiedName~OpenApiEndpointDocumentationOperationTransformerTest"
```

Then run the complete regression suite:

```powershell
dotnet test roles/FWO.sln --no-restore
```

All tests must pass without compilation errors.

The automated coverage verifies:

- IPv4 and IPv6 network bounds, including `/0`, `/32`, and `/128`.
- Host normalization to identical start and end values.
- CIDR normalization to inclusive bounds, including CIDRs with host bits.
- Shared-parser normalization of supported unusual IPv4 and IPv6 forms.
- Rejection of malformed values and IPv4 shorthand or integer forms excluded by strict IPv4 parsing.
- Rejection of wrong-length or empty ranges, mixed families, reversed ranges, masks in hosts or
  ranges, and multiple input forms.
- Rejection of signed or whitespace-padded CIDR prefixes.
- Safe repeated validation of the same request object.
- Forwarding of normalized bounds to downstream services and database queries.
- Request-to-workflow bound mapping for ticket creation.
- Exclusive address representations in OpenAPI examples and schemas.

## Test environment

Use a non-production installation containing:

- A known IPv4 address object or range.
- A known IPv6 object or range, if IPv6 is used by the installation.
- Zone data covering the selected addresses.
- A policy suitable for flow-compliance testing.

Open `/api-docs`. Obtain a token from `POST /api/AuthenticationToken/GetTokenPair` and enter only
the returned `accessToken` in the top-level Authentication field.

Before testing, record the selected addresses and their expected object IDs, zones, policies, and
compliance results. Use addresses from the test installation instead of the documentation ranges
when a database match is required.

## Valid input cases

| Input type | Representation | Value | Expected normalized range |
| --- | --- | --- | --- |
| IPv4 host | `ipHost` | `192.0.2.10` | `192.0.2.10` - `192.0.2.10` |
| Padded IPv4 host | `ipHost` | `192.000.002.010` | `192.0.2.8` - `192.0.2.8` |
| IPv4 network | `ipNetwork` | `192.0.2.0/30` | `192.0.2.0` - `192.0.2.3` |
| IPv4 network with host bits | `ipNetwork` | `192.0.2.10/24` | `192.0.2.0` - `192.0.2.255` |
| IPv4 host network | `ipNetwork` | `192.0.2.10/32` | `192.0.2.10` - `192.0.2.10` |
| IPv6 host | `ipHost` | `2001:db8::10` | `2001:db8::10` - `2001:db8::10` |
| IPv6 network | `ipNetwork` | `2001:db8::/126` | `2001:db8::` - `2001:db8::3` |
| IPv6 host network | `ipNetwork` | `2001:db8::10/128` | `2001:db8::10` - `2001:db8::10` |
| Scoped IPv6 host | `ipHost` | `fe80::1%3` | `fe80::1%3` - `fe80::1%3` |
| IPv4-mapped IPv6 host | `ipHost` | `::ffff:192.0.2.10` | `::ffff:192.0.2.10` - `::ffff:192.0.2.10` |
| Equal range | `ipRange` | `["192.0.2.10", "192.0.2.10"]` | `192.0.2.10` - `192.0.2.10` |
| Multi-address range | `ipRange` | `["192.0.2.10", "192.0.2.20"]` | `192.0.2.10` - `192.0.2.20` |

## Endpoint checks

### Address object lookup

For `POST /api/flow/getAddressObjectId`:

- Submit a known host through `ipHost`, a known CIDR through `ipNetwork`, and the equivalent
  two-bound `ipRange`.
- Confirm all equivalent representations return the same object `id` and `name`.
- Confirm the GraphQL lookup receives the normalized inclusive bounds.

### Zone resolution

For `POST /api/Compliance/resolveZonesForObjects`:

- Test all three representations on top-level and nested leaves.
- Compare the resolved zones for equivalent inputs.
- Test both `network` and `host` leaf types.
- Confirm a `host` leaf accepts `ipHost`, an equal `ipRange`, or a host-prefix CIDR.
- Confirm a `host` leaf rejects a CIDR containing multiple addresses.

### Flow compliance

For `POST /api/flow/getFlowComplianceState`:

- Mix `ipHost`, `ipNetwork`, and `ipRange` entries across source and destination lists.
- Confirm the returned policies, compliance state, and violations are identical.

### Ticket creation

For `POST /api/workflow/createTicket`:

- Create address objects using each representation.
- Confirm workflow elements retain the established `IpString`/`IpEnd` contract and receive the
  normalized inclusive bounds.
- Confirm validation errors identify the affected `addressObjects[index]` path.

## Invalid input cases

Test these cases against every applicable endpoint:

| Case | Example |
| --- | --- |
| Missing representation | No non-empty address field |
| Multiple representations | `ipHost` together with `ipNetwork` or `ipRange` |
| Wrong range length | `ipRange` with zero, one, or three entries |
| Empty range endpoint | `["192.0.2.10", ""]` |
| Mask in host or range | `ipHost: "192.0.2.10/32"` |
| Signed prefix | `192.0.2.0/+24` |
| Whitespace prefix | `192.0.2.0/ 24` or `192.0.2.0/24 ` |
| IPv4 prefix too large | `192.0.2.0/33` |
| IPv6 prefix too large | `2001:db8::/129` |
| Reversed range | `192.0.2.20` - `192.0.2.10` |
| Mixed families | IPv4 start with IPv6 end |
| IPv4 shorthand | `127.1` |
| Integer IPv4 | `2130706433` |
| Out-of-range octet | `192.0.2.256` |
| Malformed address | `192..2.10` |
| Surrounding whitespace | `192.0.2.10 ` |

Every invalid request must:

- Return HTTP status `400` rather than `500`.
- Identify the relevant `ipHost`, `ipNetwork`, or `ipRange` field.
- Exclude internal stack traces from the response.
- Cause no database change or unexpected server-log exception.

## Contract migration

`ipStart` and `ipEnd` are intentionally removed from request DTOs and OpenAPI schemas. This is a
breaking request-contract migration: verify that clients use one of the three new forms. The
original three endpoints reject the names through generic unknown-key validation. Create-ticket
keeps its unknown-key deserialization behavior, but an address object containing only the removed
fields fails the exactly-one-representation check. Response and persistence contracts remain
unchanged.

## OpenAPI and documentation

Inspect `/api-docs` and `/api-docs/v1.json` and confirm:

- All four consumers expose only `ipHost`, `ipNetwork`, and `ipRange` for address input.
- Address schemas contain three `oneOf` branches.
- `ipRange` has `minItems: 2` and `maxItems: 2`.
- Shared FWO parser behavior and CIDR host-bit normalization are documented.
- Examples demonstrate all three representations without empty alternatives.
- No individual address entry contains multiple representations.
- The zone-resolution example includes `ipNetwork`.
- The create-ticket example includes one new address representation.
- The documentation index links to the address-input guide and this testing plan; both cover the
  implemented ticket-creation behavior.

## Acceptance checklist

- [ ] Focused tests pass.
- [ ] Complete regression suite passes.
- [ ] IPv4 and IPv6 hosts work.
- [ ] IPv4 and IPv6 CIDRs work, including host-bit normalization.
- [ ] CIDRs produce the same results as equivalent ranges.
- [ ] Supported unusual IPv4 and IPv6 forms follow shared-parser normalization.
- [ ] Invalid and ambiguous representations return HTTP `400`.
- [ ] Zone `host` restrictions still work.
- [ ] Equal and multi-address ranges work.
- [ ] OpenAPI schemas, descriptions, and examples match runtime behavior.
- [ ] Server logs contain no unexpected errors.

Detailed parsing combinations belong in automated tests. Manual testing only needs representative
endpoint cases and comparisons between equivalent representations. Performance testing, testing every
possible prefix manually, and large-scale fuzz testing are not required for this change.
