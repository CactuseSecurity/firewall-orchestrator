# Phase 3 — Customer recipes and production wiring

Phase 3 is deferred.

When explicitly authorized, it will:

1. analyze `FWO_Regel_Platzierung_Regelwerk.md`;
2. identify and implement any additional reusable blocks required by the agreed customer behavior;
3. define customer-specific in-memory recipes;
4. map existing `ProvisioningPositioningAlgorithm` values to complete recipes;
5. load one `RulebaseGraphSnapshot` from the latest successfully completed FWO import, regardless of age;
6. add and register a production `CheckPointRulePositioningStrategy`; and
7. render semantic anchors into Check Point API requests and connect the approved workflow caller.

Do not infer the enum mapping or customer behavior from Phase 2's structural examples.

The intended creation-only customer path resolves the new rule's source/destination zones from workflow elements and selects placement from that zone pair. Existing-rule source, destination, service, action, zone, and other semantic fields are not required unless customer analysis identifies a concrete comparison against existing rule contents.
