# WP-05 — Resolve the Check Point functional target

## Agent mission

Resolve the only policy package, access layer, or inline layer in which customer Section placement may occur. This package enforces functional precedence before Section matching.

## Inputs

- target management and gateway from path analysis/implementation task;
- effective Check Point provisioning configuration from WP-02;
- imported topology such as `Device.Package`, `Device.LocalRulebase`, `Management.Rulebases`, and `RulebaseLink` relations;
- explicit identifiers approved in WP-00.

## Scope

- Return one typed Check Point functional target or a typed failure.
- Resolve the uniquely addressed policy package and access layer.
- Resolve an inline layer only when configuration/topology explicitly requires one.
- Prefer UIDs for stable imported-topology matching and keep package-qualified names for display.
- Detect zero candidates as `FunctionalTargetNotFound` and multiple candidates as `FunctionalTargetAmbiguous`.
- Provide the membership predicate later used to prove that a matched Section remains inside the target.
- Reject non-Check Point device types as `UnsupportedFirewallType` before the policy-snapshot query.

## Constraints

The specification supplies no safe heuristic for deriving an inline layer from a zone pair. Do not guess from display names or select the first link. If metadata is absent, name the missing identifier in the failure detail.

Customer placement may narrow a position inside this target. It may not replace or broaden it.

## Tests

Cover a classic package/layer, an explicit inline layer, duplicate layer names in different packages, a missing package, an ambiguous layer, a broken rulebase link, and a non-Check Point gateway.

## Acceptance criteria

- The result uniquely identifies the selected imported package/layer and provides stable target identifiers in the returned placement plan.
- Duplicate names cannot produce a plan for the wrong package.
- All unresolved or ambiguous cases stop without fallback.
- A Section from another layer fails membership validation as `SectionOutsideFunctionalTarget`.

## Out of scope

Do not read live rules, match a Section, consume the resulting plan, or implement another vendor's target model.
