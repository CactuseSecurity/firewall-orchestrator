# Large Ruleset Report Performance Testing

Use `scripts/performance/create_large_ruleset.py` to create a report-visible ruleset through the GraphQL API on a disposable or dedicated performance FWO instance.

The script creates:

- an optional generated management and gateway
- one successful rule import
- one linked rulebase
- generated network objects and TCP services
- access rules with source, destination, service, resolved-object, resolved-service, and gateway-enforcement references

Example:

```bash
python3 scripts/performance/create_large_ruleset.py \
  --api-url https://fwo.example/api/v1/graphql \
  --middleware-url https://fwo.example/ \
  --user admin \
  --password-file /usr/local/fworch/etc/secrets/ui_admin_pwd \
  --rules 100000 \
  --objects 10000 \
  --services 1000 \
  --batch-size 1000
```

For a local disposable system with a Hasura admin secret:

```bash
python3 scripts/performance/create_large_ruleset.py \
  --api-url https://localhost:9443/api/v1/graphql \
  --admin-secret-file /path/to/hasura-admin-secret \
  --insecure \
  --rules 100000
```

Then start the UI/API in Debug mode and generate a Rules report for the generated management. Debug logs from `ReportRules` show:

- initial structure fetch time
- rule page fetch and attach time per page
- rule paging summary per management
- filling report data time
- slowest rule-tree builds by management/device
- total rules report generation time

Keep this outside unit tests. The intent is to measure report behavior against real API/database writes and the normal report-generation path.
