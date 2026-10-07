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
