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

-- Credentials are no longer used for imports only: external ticket systems reference them as well (GHSA-jhj7-f9vj-r9v8).
DO $$
BEGIN
    IF to_regclass('public.import_credential') IS NOT NULL AND to_regclass('public.credential') IS NULL THEN
        ALTER TABLE import_credential RENAME TO credential;
    END IF;
    IF to_regclass('public.import_credential_id_seq') IS NOT NULL AND to_regclass('public.credential_id_seq') IS NULL THEN
        ALTER SEQUENCE import_credential_id_seq RENAME TO credential_id_seq;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'import_credential_pkey' AND conrelid = to_regclass('public.credential'))
        AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'credential_pkey') THEN
        ALTER TABLE credential RENAME CONSTRAINT import_credential_pkey TO credential_pkey;
    END IF;
END $$;

-- External ticket systems kept their Authorization header string ("Basic " + base64 of "username:password") in
-- plain text in the config, from where it was copied into every external request (GHSA-jhj7-f9vj-r9v8). Move it into
-- a credential with the encrypted password, which the ticket system references by CredentialId. The middleware
-- builds the header from the credential when it creates a request. A header that is not a basic authentication of
-- this form cannot be converted: it is removed and a credential has to be selected for the ticket system.
DO $$
DECLARE
    r_config RECORD;
    j_systems jsonb;
    j_system jsonb;
    j_migrated jsonb;
    t_authorization text;
    t_user_password text;
    t_main_key text;
    i_separator integer;
    i_credential_id integer;
BEGIN
    FOR r_config IN
        SELECT config_user, config_value FROM config
        WHERE config_key = 'extTicketSystems' AND config_value LIKE '%"Authorization"%'
    LOOP
        BEGIN
            j_systems := r_config.config_value::jsonb;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING 'upgrade 9.7.1: external ticket systems of config user % are no valid JSON and keep their authorization', r_config.config_user;
            CONTINUE;
        END;
        IF jsonb_typeof(j_systems) <> 'array' THEN
            CONTINUE;
        END IF;

        j_migrated := '[]'::jsonb;
        FOR j_system IN SELECT value FROM jsonb_array_elements(j_systems)
        LOOP
            t_authorization := btrim(COALESCE(j_system ->> 'Authorization', ''));
            j_system := j_system - 'Authorization';
            t_user_password := NULL;
            IF t_authorization ~* '^Basic\s+\S+$' AND t_authorization <> 'Basic xyz' THEN
                BEGIN
                    t_user_password := convert_from(decode(regexp_replace(t_authorization, '^Basic\s+', '', 'i'), 'base64'), 'UTF8');
                EXCEPTION WHEN OTHERS THEN
                    t_user_password := NULL;
                END;
            END IF;
            i_separator := COALESCE(position(':' IN t_user_password), 0);

            IF COALESCE((j_system ->> 'CredentialId')::integer, 0) > 0 THEN
                NULL; -- already references a credential
            ELSIF i_separator > 0 THEN
                IF t_main_key IS NULL THEN
                    t_main_key := getMainKey();
                END IF;
                INSERT INTO credential (credential_name, username, secret)
                    VALUES (concat(j_system ->> 'Name', ' (external ticket system ', j_system ->> 'Id', ')'),
                        substr(t_user_password, 1, i_separator - 1),
                        encryptText(substr(t_user_password, i_separator + 1), t_main_key))
                    RETURNING id INTO i_credential_id;
                j_system := j_system || jsonb_build_object('CredentialId', i_credential_id);
            ELSIF t_authorization NOT IN ('', 'Basic xyz') THEN
                RAISE WARNING 'upgrade 9.7.1: the authorization of external ticket system "%" is no basic authentication and was removed, select a credential for it', j_system ->> 'Name';
            END IF;
            j_migrated := j_migrated || jsonb_build_array(j_system);
        END LOOP;

        UPDATE config SET config_value = j_migrated::text
        WHERE config_key = 'extTicketSystems' AND config_user = r_config.config_user;
    END LOOP;
END $$;

-- External requests no longer keep the Authorization header string: the copy of the ticket system stored with a
-- request references the credential its ticket system was given above, and the middleware builds the header from it
-- right before each call, so that a changed password applies to open requests as well. The calls go to the URL stored
-- with the request, so the credential is only assigned while the configured ticket system with the same id still has
-- this URL and type (not after a move to another server or a reuse of the id; the middleware applies the same rule).
-- Otherwise, and for a ticket system without credential (e.g. a token), the request keeps its own header, encrypted
-- with the main key.
DO $$
DECLARE
    r_request RECORD;
    j_ticket_system jsonb;
    j_configured_systems jsonb := '{}'::jsonb;
    j_configured jsonb;
    i_credential_id integer;
    t_encrypted text;
    t_main_key text;
BEGIN
    BEGIN
        -- missing TypeId: the default the middleware assumes when reading the ticket system (generic, 1)
        SELECT COALESCE(jsonb_object_agg(s ->> 'Id', jsonb_build_object(
                'CredentialId', (s ->> 'CredentialId')::integer,
                'Url', btrim(COALESCE(s ->> 'Url', '')),
                'TypeId', COALESCE(s ->> 'TypeId', '1'))), '{}'::jsonb) INTO j_configured_systems
        FROM config c
        CROSS JOIN LATERAL jsonb_array_elements(
            CASE WHEN jsonb_typeof(c.config_value::jsonb) = 'array' THEN c.config_value::jsonb ELSE '[]'::jsonb END) s
        WHERE c.config_key = 'extTicketSystems' AND c.config_user = 0
            AND jsonb_typeof(s) = 'object' AND s ->> 'Id' IS NOT NULL AND s ->> 'CredentialId' ~ '^[1-9][0-9]*$';
    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'upgrade 9.7.1: external ticket systems are no valid JSON, external requests keep their authorization';
        j_configured_systems := '{}'::jsonb;
    END;

    FOR r_request IN
        SELECT id, ext_ticket_system FROM ext_request WHERE ext_ticket_system LIKE '%"Authorization"%'
    LOOP
        BEGIN
            j_ticket_system := r_request.ext_ticket_system::jsonb;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING 'upgrade 9.7.1: ticket system of external request % is no valid JSON and keeps its authorization', r_request.id;
            CONTINUE;
        END;
        IF jsonb_typeof(j_ticket_system) <> 'object' OR COALESCE(j_ticket_system ->> 'Authorization', '') = '' THEN
            CONTINUE;
        END IF;

        j_configured := j_configured_systems -> (j_ticket_system ->> 'Id');
        i_credential_id := CASE
            WHEN j_ticket_system ->> 'CredentialId' ~ '^[1-9][0-9]*$' THEN (j_ticket_system ->> 'CredentialId')::integer
            WHEN j_configured IS NOT NULL
                AND j_configured ->> 'Url' = btrim(COALESCE(j_ticket_system ->> 'Url', ''))
                AND j_configured ->> 'TypeId' = COALESCE(j_ticket_system ->> 'TypeId', '1')
                THEN (j_configured ->> 'CredentialId')::integer
            END;
        IF i_credential_id IS NOT NULL THEN
            UPDATE ext_request
            SET ext_ticket_system = ((j_ticket_system - 'Authorization') || jsonb_build_object('CredentialId', i_credential_id))::text
            WHERE id = r_request.id;
            CONTINUE;
        END IF;

        IF t_main_key IS NULL THEN
            t_main_key := getMainKey();
        END IF;
        t_encrypted := encryptText(j_ticket_system ->> 'Authorization', t_main_key);
        IF t_encrypted IS DISTINCT FROM j_ticket_system ->> 'Authorization' THEN
            UPDATE ext_request SET ext_ticket_system = jsonb_set(j_ticket_system, '{Authorization}', to_jsonb(t_encrypted))::text
            WHERE id = r_request.id;
        END IF;
    END LOOP;
END $$;
