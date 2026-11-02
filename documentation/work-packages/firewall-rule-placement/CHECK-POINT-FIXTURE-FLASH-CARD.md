# Check Point test-fixture flash card

## Ten-second version

> We first inspected the real Check Point test import. Its topology was realistic and sufficient for the standard positioning path, but none of its Sections used the destination-to-source naming convention required by the new algorithm. We therefore made minimal changes to an existing sanitized Check Point API-shaped demo payload, ran that payload through the real importer, and used the importer's unchanged normalized output as our controlled fixture.

## Important correction

We did **not** copy the test server's normalized database and rename rows.

We used the test server to establish what real imported data looked like and why it could not support the required happy path. The actual fixture started from FWO's existing sanitized public Check Point demo payload.

## Why the real test import was not enough

The real import already contained:

- 24 rulebases;
- 9 guarded inline layers per gateway;
- 12 Sections per gateway;
- 70 active rules; and
- importer-produced group flattening.

That was enough for the standard end-of-rulebase positioning test.

However, none of the imported Sections followed the required canonical naming scheme:

```text
[DESTINATION_ZONE] < [SOURCE_ZONE]
```

A network matrix can map addresses to zones, but it cannot rename a Section in an imported Check Point policy. We also deliberately did not modify the external Check Point testing server.

## How the fixture was produced

```text
Sanitized Check Point API-shaped demo payload
    -> minimal controlled source changes
    -> real checkpointR8x importer
    -> unchanged normalized FWO output
    -> validation and replay on a disposable VM
```

The source payload was FWO's existing sanitized `demo17_cpr8x_v9.json`. It already contained realistic Check Point gateways, layers, Sections, guards, rules, objects, groups, and links.

## The three controlled source changes

Before running the importer, the fixture generator:

1. renamed one existing populated Section to:

   ```text
   [RP_DEST] < [RP_SOURCE]
   ```

2. changed five competing guard rules to reference an existing sanitized host object, making them definite nonmatches for the happy-path traffic; and
3. retained one gateway so the result remained small and deterministic.

The generator contains assertions for the expected source structure. If the public demo payload changes unexpectedly, generation fails instead of silently producing a different fixture.

## How the three scenarios were selected

### Happy path

The remaining unique guard already covered `1.2.0.0/16`. We selected:

```text
1.2.3.4/32 -> 1.2.4.5/32
```

Those addresses resolve to `RP_SOURCE` and `RP_DEST`, and the selected layer contains `[RP_DEST] < [RP_SOURCE]`.

### Missing Section

We selected another pair inside the same unique guard:

```text
1.2.5.5/32 -> 1.2.6.6/32
```

The layer is identifiable, but no corresponding Section exists. The safe result is therefore `NULL`.

### No matching layer guard

We selected addresses outside every imported guard:

```text
8.8.8.8/32 -> 8.8.4.4/32
```

No functional inline layer can be selected, so the safe result is again `NULL`.

## Why this remains a trustworthy feature test

- The modifications happened on the vendor/API side of the importer boundary.
- The real `checkpointR8x` importer created every normalized rulebase, link, rule, object, and group-flattening row.
- Normalized rows were not manually repaired or idealized afterward.
- Expected `rule_position` JSON was not embedded in the fixture.
- Workflow tickets and implementation tasks were not embedded in the fixture.
- The fixture was validated and replayed independently before using it for the presentation.
- The external Check Point system remained unchanged.

The useful phrase is:

> The scenario is synthetic, but the normalization is authentic.

## What we were testing

We were testing whether the positioning feature correctly consumes importer-produced normalized data.

We were **not** retesting whether the Check Point importer can independently discover this exact policy structure from the company's test server. Keeping those questions separate prevents an importer-data limitation from being mistaken for a positioning defect.

## Twenty-second spoken answer

> We checked our real Check Point test import first. It had realistic guarded layers and Sections, but none of the Sections followed the naming convention this positioning mode needs, and we did not want to alter the test firewall. So we took our existing sanitized Check Point demo API response, renamed one existing Section and isolated one guard match, then ran the result through the real importer. We froze the importer's exact normalized output as the fixture. In short: controlled vendor-shaped input, authentic importer output, and no preloaded expected positioning result.

## If someone challenges the fixture

**“Did you handcraft database rows until the test passed?”**

No. The real importer produced the normalized rows, which were exported unchanged.

**“Did you modify the Check Point testing server?”**

No. The external system remained read-only.

**“Why not use only the live import?”**

It could test the standard algorithm, but it had no Section using the required canonical naming convention, so the new happy path was structurally impossible.

**“Is this testing the importer?”**

No. It tests positioning against the kind of normalized structure the importer actually produces. Importer correctness is a separate concern.

**“What prevents an unrealistically perfect fixture?”**

Only API-shaped source data was adjusted. The real importer still determined the normalized structure, and the fixture contains no expected position or workflow result.
