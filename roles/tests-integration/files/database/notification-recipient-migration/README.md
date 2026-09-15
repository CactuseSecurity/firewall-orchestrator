# Notification recipient migration fixtures

`fixtures.yml` contains source configuration snapshots and expected notification
recipient fields for the 9.5.3 upgrade. It covers both a fresh 9.5.1
installation that still has legacy enum values and an older installation that
already ran the 9.0.4 JSON conversion.

The expected `email_address_to` values use the same JSON shape as
`EmailRecipientSelection.ToConfigValue`. The owner responsible type IDs are
the shipped main (`1`) and supporting (`2`) IDs; `AllOwnerResponsibles` is
resolved dynamically from active database rows by the migration.
