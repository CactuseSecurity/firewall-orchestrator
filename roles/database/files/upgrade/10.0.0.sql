-- Guardicore integration (issues #4945, #4949): labelling schema, label columns of firewall rules
-- and the Guardicore device types for the Guardicore importer

-- device types
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (33,'Guardicore Management','REST','Akamai','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (34,'Guardicore Gateway','REST','Akamai','',false,false,false) ON CONFLICT DO NOTHING;

-- labels of an imported rule side as ORed AND-groups [{"label-key": "label-value", ...}, ...], null without labels
ALTER TABLE firewall.rule ADD COLUMN IF NOT EXISTS rule_src_labels jsonb;
ALTER TABLE firewall.rule ADD COLUMN IF NOT EXISTS rule_dst_labels jsonb;

-- labelling schema: generic key/value labels (e.g. Guardicore labels, app roles, stages) and their assignment
-- to rules, connections, owner networks and access flows (see issue #4949)
-- every assignment change is recorded as a label_change_event, as changes to these cross reference tables
-- do not create a changelog_rule entry

create schema if not exists labelling;

-- label definition --------------------------------------------------------

-- label keys, e.g. owner, tenant, AppRole, Stage, OS
create table if not exists labelling.label_key
(
    key_id SERIAL PRIMARY KEY,
    key_name Varchar NOT NULL,
    key_source_internal Boolean NOT NULL Default FALSE, -- true when the key is maintained within FWO, false when imported
    key_is_owner_scoped Boolean NOT NULL Default FALSE,
    allow_multiple_values Boolean NOT NULL Default FALSE,
    constraint labelling_label_key_key_name_unique unique (key_name)
);

-- label values of a key, e.g. Stage: Prod, Test, Dev
create table if not exists labelling.label_value
(
    label_id BIGSERIAL PRIMARY KEY,
    key_id Integer NOT NULL,
    value Varchar NOT NULL,
    is_imported Boolean NOT NULL Default TRUE,
    constraint labelling_label_value_key_id_value_unique unique (key_id, value)
);

-- label history -----------------------------------------------------------

create table if not exists labelling.label_change_event
(
    id BIGSERIAL PRIMARY KEY,
    import_control_id BIGINT,
    created_at Timestamp with time zone NOT NULL Default now(),
    actor Varchar, -- distinguished name of the changing user as sent by the client, informational only
    actor_id Integer, -- uiuser of the changing user, preset by the API from the session for UI roles
    request_id BIGINT,
    origin_is_ui Boolean NOT NULL Default TRUE, -- false when the change stems from an import
    connection_id Integer
);

-- applying labels ---------------------------------------------------------
-- a label assignment is active while removed_event_id is null; re-adding a removed label creates a new row,
-- the partial unique indices in fworch-create-indices.sql allow only one active assignment per object and label
-- (per object, label and AND-group for the source and destination sides of rules and connections)

-- to firewall.rule (e.g. for Guardicore), source labels: AppRole, Stage, ...
create table if not exists labelling.rule_source_label
(
    rule_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    and_group Integer NOT NULL Default 0, -- number of the AND-group of the side, the groups of a side are ORed
    primary key (rule_id, label_id, created_event_id, and_group)
);

-- to firewall.rule, destination labels: AppRole, Stage
create table if not exists labelling.rule_destination_label
(
    rule_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    and_group Integer NOT NULL Default 0, -- number of the AND-group of the side, the groups of a side are ORed
    primary key (rule_id, label_id, created_event_id, and_group)
);

-- to firewall.rule as a whole: App
-- label_source names the writer of the assignment (app_sync: RuleAppLabelSync, import: the importer); each writer closes
-- only its own assignments, so the same label may be active once per writer
create table if not exists labelling.rule_label
(
    rule_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    label_source Varchar NOT NULL,
    primary key (rule_id, label_id, created_event_id)
);

-- to modelling.connection, source labels: AppRole
create table if not exists labelling.connection_source_label
(
    connection_id Integer NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    and_group Integer NOT NULL Default 0, -- number of the AND-group of the side, the groups of a side are ORed
    primary key (connection_id, label_id, created_event_id, and_group)
);

-- to modelling.connection, destination labels: AppRole
create table if not exists labelling.connection_destination_label
(
    connection_id Integer NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    and_group Integer NOT NULL Default 0, -- number of the AND-group of the side, the groups of a side are ORed
    primary key (connection_id, label_id, created_event_id, and_group)
);

-- to modelling.connection as a whole: App
create table if not exists labelling.connection_label
(
    connection_id Integer NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (connection_id, label_id, created_event_id)
);

-- to flow.access: App
create table if not exists labelling.access_flow_label
(
    flow_access_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    primary key (flow_access_id, label_id)
);

-- to owner_network: AppRole, App, AppZone, NetworkArea (could replace the different groupings)
create table if not exists labelling.owner_network_label
(
    owner_network_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (owner_network_id, label_id, created_event_id)
);

-- labelling foreign keys
ALTER TABLE labelling.label_value DROP CONSTRAINT IF EXISTS labelling_label_value_label_key_fkey;
ALTER TABLE labelling.label_value ADD CONSTRAINT labelling_label_value_label_key_fkey FOREIGN KEY (key_id) REFERENCES labelling.label_key(key_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.label_change_event DROP CONSTRAINT IF EXISTS labelling_label_change_event_import_control_fkey;
ALTER TABLE labelling.label_change_event ADD CONSTRAINT labelling_label_change_event_import_control_fkey FOREIGN KEY (import_control_id) REFERENCES import_control(control_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.label_change_event DROP CONSTRAINT IF EXISTS labelling_label_change_event_request_ticket_fkey;
ALTER TABLE labelling.label_change_event ADD CONSTRAINT labelling_label_change_event_request_ticket_fkey FOREIGN KEY (request_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.label_change_event DROP CONSTRAINT IF EXISTS labelling_label_change_event_connection_fkey;
ALTER TABLE labelling.label_change_event ADD CONSTRAINT labelling_label_change_event_connection_fkey FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.label_change_event DROP CONSTRAINT IF EXISTS labelling_label_change_event_actor_fkey;
ALTER TABLE labelling.label_change_event ADD CONSTRAINT labelling_label_change_event_actor_fkey FOREIGN KEY (actor_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.rule_source_label DROP CONSTRAINT IF EXISTS labelling_rule_source_label_rule_fkey;
ALTER TABLE labelling.rule_source_label ADD CONSTRAINT labelling_rule_source_label_rule_fkey FOREIGN KEY (rule_id) REFERENCES firewall.rule(rule_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_source_label DROP CONSTRAINT IF EXISTS labelling_rule_source_label_label_value_fkey;
ALTER TABLE labelling.rule_source_label ADD CONSTRAINT labelling_rule_source_label_label_value_fkey FOREIGN KEY (label_id) REFERENCES labelling.label_value(label_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_source_label DROP CONSTRAINT IF EXISTS labelling_rule_source_label_created_event_fkey;
ALTER TABLE labelling.rule_source_label ADD CONSTRAINT labelling_rule_source_label_created_event_fkey FOREIGN KEY (created_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_source_label DROP CONSTRAINT IF EXISTS labelling_rule_source_label_removed_event_fkey;
ALTER TABLE labelling.rule_source_label ADD CONSTRAINT labelling_rule_source_label_removed_event_fkey FOREIGN KEY (removed_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.rule_destination_label DROP CONSTRAINT IF EXISTS labelling_rule_destination_label_rule_fkey;
ALTER TABLE labelling.rule_destination_label ADD CONSTRAINT labelling_rule_destination_label_rule_fkey FOREIGN KEY (rule_id) REFERENCES firewall.rule(rule_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_destination_label DROP CONSTRAINT IF EXISTS labelling_rule_destination_label_label_value_fkey;
ALTER TABLE labelling.rule_destination_label ADD CONSTRAINT labelling_rule_destination_label_label_value_fkey FOREIGN KEY (label_id) REFERENCES labelling.label_value(label_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_destination_label DROP CONSTRAINT IF EXISTS labelling_rule_destination_label_created_event_fkey;
ALTER TABLE labelling.rule_destination_label ADD CONSTRAINT labelling_rule_destination_label_created_event_fkey FOREIGN KEY (created_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_destination_label DROP CONSTRAINT IF EXISTS labelling_rule_destination_label_removed_event_fkey;
ALTER TABLE labelling.rule_destination_label ADD CONSTRAINT labelling_rule_destination_label_removed_event_fkey FOREIGN KEY (removed_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.rule_label DROP CONSTRAINT IF EXISTS labelling_rule_label_rule_fkey;
ALTER TABLE labelling.rule_label ADD CONSTRAINT labelling_rule_label_rule_fkey FOREIGN KEY (rule_id) REFERENCES firewall.rule(rule_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_label DROP CONSTRAINT IF EXISTS labelling_rule_label_label_value_fkey;
ALTER TABLE labelling.rule_label ADD CONSTRAINT labelling_rule_label_label_value_fkey FOREIGN KEY (label_id) REFERENCES labelling.label_value(label_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_label DROP CONSTRAINT IF EXISTS labelling_rule_label_created_event_fkey;
ALTER TABLE labelling.rule_label ADD CONSTRAINT labelling_rule_label_created_event_fkey FOREIGN KEY (created_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.rule_label DROP CONSTRAINT IF EXISTS labelling_rule_label_removed_event_fkey;
ALTER TABLE labelling.rule_label ADD CONSTRAINT labelling_rule_label_removed_event_fkey FOREIGN KEY (removed_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.connection_source_label DROP CONSTRAINT IF EXISTS labelling_connection_source_label_connection_fkey;
ALTER TABLE labelling.connection_source_label ADD CONSTRAINT labelling_connection_source_label_connection_fkey FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_source_label DROP CONSTRAINT IF EXISTS labelling_connection_source_label_label_value_fkey;
ALTER TABLE labelling.connection_source_label ADD CONSTRAINT labelling_connection_source_label_label_value_fkey FOREIGN KEY (label_id) REFERENCES labelling.label_value(label_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_source_label DROP CONSTRAINT IF EXISTS labelling_connection_source_label_created_event_fkey;
ALTER TABLE labelling.connection_source_label ADD CONSTRAINT labelling_connection_source_label_created_event_fkey FOREIGN KEY (created_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_source_label DROP CONSTRAINT IF EXISTS labelling_connection_source_label_removed_event_fkey;
ALTER TABLE labelling.connection_source_label ADD CONSTRAINT labelling_connection_source_label_removed_event_fkey FOREIGN KEY (removed_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.connection_destination_label DROP CONSTRAINT IF EXISTS labelling_connection_destination_label_connection_fkey;
ALTER TABLE labelling.connection_destination_label ADD CONSTRAINT labelling_connection_destination_label_connection_fkey FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_destination_label DROP CONSTRAINT IF EXISTS labelling_connection_destination_label_label_value_fkey;
ALTER TABLE labelling.connection_destination_label ADD CONSTRAINT labelling_connection_destination_label_label_value_fkey FOREIGN KEY (label_id) REFERENCES labelling.label_value(label_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_destination_label DROP CONSTRAINT IF EXISTS labelling_connection_destination_label_created_event_fkey;
ALTER TABLE labelling.connection_destination_label ADD CONSTRAINT labelling_connection_destination_label_created_event_fkey FOREIGN KEY (created_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_destination_label DROP CONSTRAINT IF EXISTS labelling_connection_destination_label_removed_event_fkey;
ALTER TABLE labelling.connection_destination_label ADD CONSTRAINT labelling_connection_destination_label_removed_event_fkey FOREIGN KEY (removed_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.connection_label DROP CONSTRAINT IF EXISTS labelling_connection_label_connection_fkey;
ALTER TABLE labelling.connection_label ADD CONSTRAINT labelling_connection_label_connection_fkey FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_label DROP CONSTRAINT IF EXISTS labelling_connection_label_label_value_fkey;
ALTER TABLE labelling.connection_label ADD CONSTRAINT labelling_connection_label_label_value_fkey FOREIGN KEY (label_id) REFERENCES labelling.label_value(label_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_label DROP CONSTRAINT IF EXISTS labelling_connection_label_created_event_fkey;
ALTER TABLE labelling.connection_label ADD CONSTRAINT labelling_connection_label_created_event_fkey FOREIGN KEY (created_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.connection_label DROP CONSTRAINT IF EXISTS labelling_connection_label_removed_event_fkey;
ALTER TABLE labelling.connection_label ADD CONSTRAINT labelling_connection_label_removed_event_fkey FOREIGN KEY (removed_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE labelling.access_flow_label DROP CONSTRAINT IF EXISTS labelling_access_flow_label_flow_access_fkey;
ALTER TABLE labelling.access_flow_label ADD CONSTRAINT labelling_access_flow_label_flow_access_fkey FOREIGN KEY (flow_access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.access_flow_label DROP CONSTRAINT IF EXISTS labelling_access_flow_label_label_value_fkey;
ALTER TABLE labelling.access_flow_label ADD CONSTRAINT labelling_access_flow_label_label_value_fkey FOREIGN KEY (label_id) REFERENCES labelling.label_value(label_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.owner_network_label DROP CONSTRAINT IF EXISTS labelling_owner_network_label_owner_network_fkey;
ALTER TABLE labelling.owner_network_label ADD CONSTRAINT labelling_owner_network_label_owner_network_fkey FOREIGN KEY (owner_network_id) REFERENCES owner_network(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.owner_network_label DROP CONSTRAINT IF EXISTS labelling_owner_network_label_label_value_fkey;
ALTER TABLE labelling.owner_network_label ADD CONSTRAINT labelling_owner_network_label_label_value_fkey FOREIGN KEY (label_id) REFERENCES labelling.label_value(label_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.owner_network_label DROP CONSTRAINT IF EXISTS labelling_owner_network_label_created_event_fkey;
ALTER TABLE labelling.owner_network_label ADD CONSTRAINT labelling_owner_network_label_created_event_fkey FOREIGN KEY (created_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE labelling.owner_network_label DROP CONSTRAINT IF EXISTS labelling_owner_network_label_removed_event_fkey;
ALTER TABLE labelling.owner_network_label ADD CONSTRAINT labelling_owner_network_label_removed_event_fkey FOREIGN KEY (removed_event_id) REFERENCES labelling.label_change_event(id) ON UPDATE RESTRICT ON DELETE SET NULL;

-- labelling indices
CREATE INDEX IF NOT EXISTS idx_labelling_label_change_event_import_control ON labelling.label_change_event (import_control_id) WHERE import_control_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_label_change_event_request ON labelling.label_change_event (request_id) WHERE request_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_label_change_event_connection ON labelling.label_change_event (connection_id) WHERE connection_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_label_change_event_actor ON labelling.label_change_event (actor_id) WHERE actor_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_labelling_rule_source_label_active_unique ON labelling.rule_source_label (rule_id, label_id, and_group) WHERE removed_event_id IS NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_rule_source_label_label_id ON labelling.rule_source_label (label_id);
CREATE INDEX IF NOT EXISTS idx_labelling_rule_source_label_created_event ON labelling.rule_source_label (created_event_id);
CREATE INDEX IF NOT EXISTS idx_labelling_rule_source_label_removed_event ON labelling.rule_source_label (removed_event_id) WHERE removed_event_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_labelling_rule_destination_label_active_unique ON labelling.rule_destination_label (rule_id, label_id, and_group) WHERE removed_event_id IS NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_rule_destination_label_label_id ON labelling.rule_destination_label (label_id);
CREATE INDEX IF NOT EXISTS idx_labelling_rule_destination_label_created_event ON labelling.rule_destination_label (created_event_id);
CREATE INDEX IF NOT EXISTS idx_labelling_rule_destination_label_removed_event ON labelling.rule_destination_label (removed_event_id) WHERE removed_event_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_labelling_rule_label_active_unique ON labelling.rule_label (rule_id, label_id, label_source) WHERE removed_event_id IS NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_rule_label_label_id ON labelling.rule_label (label_id);
CREATE INDEX IF NOT EXISTS idx_labelling_rule_label_created_event ON labelling.rule_label (created_event_id);
CREATE INDEX IF NOT EXISTS idx_labelling_rule_label_removed_event ON labelling.rule_label (removed_event_id) WHERE removed_event_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_labelling_connection_source_label_active_unique ON labelling.connection_source_label (connection_id, label_id, and_group) WHERE removed_event_id IS NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_connection_source_label_label_id ON labelling.connection_source_label (label_id);
CREATE INDEX IF NOT EXISTS idx_labelling_connection_source_label_created_event ON labelling.connection_source_label (created_event_id);
CREATE INDEX IF NOT EXISTS idx_labelling_connection_source_label_removed_event ON labelling.connection_source_label (removed_event_id) WHERE removed_event_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_labelling_connection_destination_label_active_unique ON labelling.connection_destination_label (connection_id, label_id, and_group) WHERE removed_event_id IS NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_connection_destination_label_label_id ON labelling.connection_destination_label (label_id);
CREATE INDEX IF NOT EXISTS idx_labelling_connection_destination_label_created_event ON labelling.connection_destination_label (created_event_id);
CREATE INDEX IF NOT EXISTS idx_labelling_connection_destination_label_removed_event ON labelling.connection_destination_label (removed_event_id) WHERE removed_event_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_labelling_connection_label_active_unique ON labelling.connection_label (connection_id, label_id) WHERE removed_event_id IS NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_connection_label_label_id ON labelling.connection_label (label_id);
CREATE INDEX IF NOT EXISTS idx_labelling_connection_label_created_event ON labelling.connection_label (created_event_id);
CREATE INDEX IF NOT EXISTS idx_labelling_connection_label_removed_event ON labelling.connection_label (removed_event_id) WHERE removed_event_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_access_flow_label_label_id ON labelling.access_flow_label (label_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_labelling_owner_network_label_active_unique ON labelling.owner_network_label (owner_network_id, label_id) WHERE removed_event_id IS NULL;
CREATE INDEX IF NOT EXISTS idx_labelling_owner_network_label_label_id ON labelling.owner_network_label (label_id);
CREATE INDEX IF NOT EXISTS idx_labelling_owner_network_label_created_event ON labelling.owner_network_label (created_event_id);
CREATE INDEX IF NOT EXISTS idx_labelling_owner_network_label_removed_event ON labelling.owner_network_label (removed_event_id) WHERE removed_event_id IS NOT NULL;

-- key of the App label; Hasura restricts modellers to App labels of their own apps by this entry
INSERT INTO config (config_key, config_value, config_user) VALUES ('appLabelKey', 'App', 0) ON CONFLICT DO NOTHING;

-- read-only database user: a new installation grants it every schema in db_schemata, an upgrade has to add labelling
GRANT USAGE ON SCHEMA labelling TO fwo_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA labelling TO fwo_ro;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA labelling TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA labelling GRANT SELECT ON TABLES TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA labelling GRANT USAGE, SELECT ON SEQUENCES TO fwo_ro;

-- resolving service groups is a device specific setting (issue #5787): the former global modelling setting
-- modRolloutResolveServiceGroups becomes the global value of resolveServiceGroups where it differs from the default (true);
-- a repeated upgrade finds no config entry any more and keeps a value an administrator set in the meantime
INSERT INTO device_specific_config_node (node_type, object_key, display_name, sort_order)
SELECT 'global', 'global', 'Global', 0
WHERE EXISTS (SELECT 1 FROM config WHERE config_key = 'modRolloutResolveServiceGroups' AND config_user = 0
    AND lower(trim(config_value)) = 'false')
ON CONFLICT (node_type, object_key) DO NOTHING;
INSERT INTO device_specific_config_value (node_id, config_key, config_value)
SELECT n.id, 'resolveServiceGroups', 'false'::jsonb
FROM device_specific_config_node n
JOIN config c ON c.config_key = 'modRolloutResolveServiceGroups' AND c.config_user = 0 AND lower(trim(c.config_value)) = 'false'
WHERE n.node_type = 'global' AND n.object_key = 'global'
ON CONFLICT (node_id, config_key) DO NOTHING;
DELETE FROM config WHERE config_key = 'modRolloutResolveServiceGroups';
