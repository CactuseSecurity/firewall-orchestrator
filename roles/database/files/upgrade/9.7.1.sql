-- TLS certificate checking is configured per connection type: firewall connections (importCheckCertificates,
-- which keeps its value and now also covers autodiscovery and Check Point change requests), email servers and
-- external ticket systems. The new switches keep the unchecked behaviour where such a connection is configured.
-- (The aggregates without GROUP BY always return exactly one row.)
INSERT INTO config (config_key, config_value, config_user)
SELECT 'emailCheckCertificates', CASE WHEN count(*) > 0 THEN 'False' ELSE 'True' END, 0
FROM config server
JOIN config tls ON tls.config_key = 'emailTls' AND tls.config_user = 0
WHERE server.config_key = 'emailServerAddress' AND server.config_user = 0
    AND COALESCE(server.config_value, '') <> '' AND COALESCE(tls.config_value, 'None') <> 'None'
ON CONFLICT (config_key, config_user) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
SELECT 'extTicketSystemsCheckCertificates', CASE WHEN count(*) > 0 THEN 'False' ELSE 'True' END, 0
FROM config
WHERE config_key = 'extTicketSystems' AND config_user = 0
    AND COALESCE(config_value, '') ~ '"Url"\s*:\s*"[^"]'
ON CONFLICT (config_key, config_user) DO NOTHING;

-- At most one active (not yet finished) external request per ticket, so concurrent calls cannot start the request
-- chain of a ticket twice (GHSA-qqm3-cf7w-65wr). The middleware sets finish_date when closing a request, before it
-- creates the next one. Until now, requests closed manually by an admin kept an empty finish_date, so set it for
-- them and for every request followed by a newer one of the same ticket (the chain only continues after the
-- previous request is closed). Afterwards each ticket has at most one request without finish_date.
-- The state names are the values the middleware writes into ext_request_state (enum ExtStates), not the
-- admin-editable names of request.ext_state.
UPDATE ext_request request
SET finish_date = now()
FROM (SELECT ticket_id, max(id) AS newest_id FROM ext_request GROUP BY ticket_id) newest
WHERE request.ticket_id = newest.ticket_id
    AND request.finish_date IS NULL
    AND (request.id < newest.newest_id
        OR request.ext_request_state IN ('ExtReqAckRejected', 'ExtReqAcknowledged', 'ExtReqDiscarded'));

CREATE UNIQUE INDEX IF NOT EXISTS uidx_ext_request_one_active_per_ticket ON ext_request (ticket_id) WHERE finish_date IS NULL;

-- The tenant visibility functions (rule_fully_visible_to_tenant) look up the gateways of a rule for every rule,
-- rule_from and rule_to row a tenant reads (GHSA-v8hx-cx2q-j75v); without an index each lookup scans the table.
CREATE INDEX IF NOT EXISTS idx_rule_enforced_on_gateway_rule_id ON rule_enforced_on_gateway (rule_id);

-- Rule versions removed before the upgrade to v9 keep the gateways they were enforced on. The 9.0 migration
-- (addRulebaseLinkEntries, addRuleEnforcedOnGatewayEntries) dated its rulebase links and gateway entries with the last
-- import before the upgrade, which is after the removal of these versions. The tenant visibility functions only count
-- links valid during the lifetime of a rule version (link_valid_for_rule_version), so the tenants of the gateways no
-- longer saw them. Only v8 data is changed: the v9 importer does not write rule.dev_id.
-- The migration also attached Policy Targets rules to gateways that did not enforce those versions. Only the
-- gateway recorded on the v8 rule is evidence of historical enforcement; other entries must not be dated back.
UPDATE rule_enforced_on_gateway reg
SET created = r.rule_create, removed = r.removed
FROM rule r
WHERE r.rule_id = reg.rule_id AND reg.dev_id = r.dev_id AND r.removed IS NOT NULL AND reg.created >= r.removed;

-- Only the first links of a management can come from the migration. Restore each gateway/rulebase link as far
-- back as a removed v8 rule on that same gateway and rulebase proves it existed, never to the management's first
-- import. Gateways added before the migration but after a rule version's removal must not gain its history.
-- Links added after the migration, and links without historical enforcement evidence, remain unchanged.
WITH first_link AS (
    SELECT gw.mgm_id, min(rl.created) AS created
    FROM rulebase_link rl JOIN device gw ON (gw.dev_id = rl.gw_id)
    GROUP BY gw.mgm_id
), migrated_link AS (
    SELECT rl.id, min(r.rule_create) AS created
    FROM rulebase_link rl
        JOIN device gw ON (gw.dev_id = rl.gw_id)
        JOIN first_link fl ON (fl.mgm_id = gw.mgm_id AND fl.created = rl.created)
        JOIN rule r ON (r.dev_id = rl.gw_id AND r.rulebase_id = rl.to_rulebase_id AND r.mgm_id = gw.mgm_id)
    WHERE r.removed <= rl.created
    GROUP BY rl.id
)
UPDATE rulebase_link rl
SET created = ml.created
FROM migrated_link ml
WHERE rl.id = ml.id AND ml.created < rl.created;
