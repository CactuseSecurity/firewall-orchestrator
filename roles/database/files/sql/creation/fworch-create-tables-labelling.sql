-- labelling schema: generic key/value labels (e.g. Guardicore labels, app roles, stages) and their assignment
-- to rules, connections, owner networks and access flows (see issue #4949)
-- every assignment change is recorded as a label_change_event, as changes to these cross reference tables
-- do not create a changelog_rule entry

create schema labelling;

-- label definition --------------------------------------------------------

-- label keys, e.g. owner, tenant, AppRole, Stage, OS
create table labelling.label_key
(
    key_id SERIAL PRIMARY KEY,
    key_name Varchar NOT NULL,
    key_source_internal Boolean NOT NULL Default FALSE, -- true when the key is maintained within FWO, false when imported
    key_is_owner_scoped Boolean NOT NULL Default FALSE,
    allow_multiple_values Boolean NOT NULL Default FALSE,
    constraint labelling_label_key_key_name_unique unique (key_name)
);

-- label values of a key, e.g. Stage: Prod, Test, Dev
create table labelling.label_value
(
    label_id BIGSERIAL PRIMARY KEY,
    key_id Integer NOT NULL,
    value Varchar NOT NULL,
    is_imported Boolean NOT NULL Default TRUE,
    constraint labelling_label_value_key_id_value_unique unique (key_id, value)
);

-- label history -----------------------------------------------------------

create table labelling.label_change_event
(
    id BIGSERIAL PRIMARY KEY,
    import_control_id BIGINT,
    created_at Timestamp with time zone NOT NULL Default now(),
    actor Varchar, -- distinguished name of the changing user
    request_id BIGINT,
    origin_is_ui Boolean NOT NULL Default TRUE, -- false when the change stems from an import
    connection_id Integer
);

-- applying labels ---------------------------------------------------------
-- a label assignment is active while removed_event_id is null; re-adding a removed label creates a new row,
-- the partial unique indices in fworch-create-indices.sql allow only one active assignment per object and label

-- to firewall.rule (e.g. for Guardicore), source labels: AppRole, Stage, ...
create table labelling.rule_source_label
(
    rule_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (rule_id, label_id, created_event_id)
);

-- to firewall.rule, destination labels: AppRole, Stage
create table labelling.rule_destination_label
(
    rule_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (rule_id, label_id, created_event_id)
);

-- to firewall.rule as a whole: App
create table labelling.rule_label
(
    rule_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (rule_id, label_id, created_event_id)
);

-- to modelling.connection, source labels: AppRole
create table labelling.connection_source_label
(
    connection_id Integer NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (connection_id, label_id, created_event_id)
);

-- to modelling.connection, destination labels: AppRole
create table labelling.connection_destination_label
(
    connection_id Integer NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (connection_id, label_id, created_event_id)
);

-- to modelling.connection as a whole: App
create table labelling.connection_label
(
    connection_id Integer NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (connection_id, label_id, created_event_id)
);

-- to flow.access: App
create table labelling.access_flow_label
(
    flow_access_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    primary key (flow_access_id, label_id)
);

-- to owner_network: AppRole, App, AppZone, NetworkArea (could replace the different groupings)
create table labelling.owner_network_label
(
    owner_network_id BIGINT NOT NULL,
    label_id BIGINT NOT NULL,
    created_event_id BIGINT NOT NULL,
    removed_event_id BIGINT,
    primary key (owner_network_id, label_id, created_event_id)
);
