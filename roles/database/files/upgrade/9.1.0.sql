--------------------------------------------------------------------------
-- Flow Schema

CREATE SCHEMA IF NOT EXISTS flow;

CREATE TABLE IF NOT EXISTS flow.nwobject
(
    nwobj_id BIGSERIAL PRIMARY KEY,
    name varchar,
    ip_start inet, -- null for e.g. FQDN-based objects
    ip_end inet,
    nwobj_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    CHECK (ip_start <= ip_end),
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.nwgroup
(
    nwgroup_id BIGSERIAL PRIMARY KEY,
    name varchar NOT NULL,
    nwgrp_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.svcobject
(
    svcobj_id BIGSERIAL PRIMARY KEY,
    name varchar NOT NULL,
    port_start integer, -- null for e.g. icmp-based objects
    port_end integer,
    ip_proto_id integer NOT NULL,
    svcobj_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    CHECK (port_start <= port_end),
    CHECK (port_start BETWEEN 0 AND 65535),
    CHECK (port_end BETWEEN 0 AND 65535),
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.svcgroup
(
    svcgroup_id BIGSERIAL PRIMARY KEY,
    name varchar NOT NULL,
    svcgrp_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.timeobject
(
    timeobj_id BIGSERIAL PRIMARY KEY,
    name varchar NOT NULL,
    start_time Timestamp with time zone,
    end_time Timestamp with time zone,
    timeobj_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    CHECK (start_time <= end_time),
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.access
(
    access_id BIGSERIAL PRIMARY KEY,
    access_hash varchar(64) NOT NULL UNIQUE,
    requester_id integer,
    owner_id integer,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.access_source
(
    access_id bigint NOT NULL,
    nwobj_id bigint NOT NULL,
    PRIMARY KEY (access_id, nwobj_id)
);

CREATE TABLE IF NOT EXISTS flow.access_source_grp
(
    access_id bigint NOT NULL,
    nwgroup_id bigint NOT NULL,
    PRIMARY KEY (access_id, nwgroup_id)
);

CREATE TABLE IF NOT EXISTS flow.access_destination
(
    access_id bigint NOT NULL,
    nwobj_id bigint NOT NULL,
    PRIMARY KEY (access_id, nwobj_id)
);

CREATE TABLE IF NOT EXISTS flow.access_destination_grp
(
    access_id bigint NOT NULL,
    nwgroup_id bigint NOT NULL,
    PRIMARY KEY (access_id, nwgroup_id)
);

CREATE TABLE IF NOT EXISTS flow.access_service
(
    access_id bigint NOT NULL,
    svcobj_id bigint NOT NULL,
    PRIMARY KEY (access_id, svcobj_id)
);

CREATE TABLE IF NOT EXISTS flow.access_service_grp
(
    access_id bigint NOT NULL,
    svcgroup_id bigint NOT NULL,
    PRIMARY KEY (access_id, svcgroup_id)
);

CREATE TABLE IF NOT EXISTS flow.access_timeobject
(
    access_id bigint NOT NULL,
    timeobj_id bigint NOT NULL,
    PRIMARY KEY (access_id, timeobj_id)
);

CREATE TABLE IF NOT EXISTS flow.nwgroup_member
(
    nwgroup_id bigint NOT NULL,
    nwobj_id bigint NOT NULL,
    PRIMARY KEY (nwgroup_id, nwobj_id)
);

CREATE TABLE IF NOT EXISTS flow.svcgroup_member
(
    svcgroup_id bigint NOT NULL,
    svcobj_id bigint NOT NULL,
    PRIMARY KEY (svcgroup_id, svcobj_id)
);

CREATE TABLE IF NOT EXISTS flow.rule_flow_mapping
(
    rule_id bigint PRIMARY KEY,
    access_id bigint NOT NULL
);

CREATE TABLE IF NOT EXISTS flow.nwobject_mapping
(
    flow_nwobj_id bigint NOT NULL,
    obj_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS flow.svcobject_mapping
(
    flow_svcobj_id bigint NOT NULL,
    svc_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS flow.timeobject_mapping
(
    flow_timeobj_id bigint NOT NULL,
    time_obj_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS flow.nwgroup_mapping
(
    flow_nwgroup_id bigint NOT NULL,
    objgrp_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS flow.svcgroup_mapping
(
    flow_svcgroup_id bigint NOT NULL,
    svcgrp_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

ALTER TABLE flow.svcobject DROP CONSTRAINT IF EXISTS flow_svcobject_proto_foreign_key;
ALTER TABLE flow.svcobject ADD CONSTRAINT flow_svcobject_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access DROP CONSTRAINT IF EXISTS flow_access_requester_foreign_key;
ALTER TABLE flow.access ADD CONSTRAINT flow_access_requester_foreign_key FOREIGN KEY (requester_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access DROP CONSTRAINT IF EXISTS flow_access_owner_foreign_key;
ALTER TABLE flow.access ADD CONSTRAINT flow_access_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_source DROP CONSTRAINT IF EXISTS flow_access_source_access_foreign_key;
ALTER TABLE flow.access_source ADD CONSTRAINT flow_access_source_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_source DROP CONSTRAINT IF EXISTS flow_access_source_nwobject_foreign_key;
ALTER TABLE flow.access_source ADD CONSTRAINT flow_access_source_nwobject_foreign_key FOREIGN KEY (nwobj_id) REFERENCES flow.nwobject(nwobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_source_grp DROP CONSTRAINT IF EXISTS flow_access_source_grp_access_foreign_key;
ALTER TABLE flow.access_source_grp ADD CONSTRAINT flow_access_source_grp_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_source_grp DROP CONSTRAINT IF EXISTS flow_access_source_grp_nwgroup_foreign_key;
ALTER TABLE flow.access_source_grp ADD CONSTRAINT flow_access_source_grp_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES flow.nwgroup(nwgroup_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_destination DROP CONSTRAINT IF EXISTS flow_access_destination_access_foreign_key;
ALTER TABLE flow.access_destination ADD CONSTRAINT flow_access_destination_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_destination DROP CONSTRAINT IF EXISTS flow_access_destination_nwobject_foreign_key;
ALTER TABLE flow.access_destination ADD CONSTRAINT flow_access_destination_nwobject_foreign_key FOREIGN KEY (nwobj_id) REFERENCES flow.nwobject(nwobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_destination_grp DROP CONSTRAINT IF EXISTS flow_access_destination_grp_access_foreign_key;
ALTER TABLE flow.access_destination_grp ADD CONSTRAINT flow_access_destination_grp_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_destination_grp DROP CONSTRAINT IF EXISTS flow_access_destination_grp_nwgroup_foreign_key;
ALTER TABLE flow.access_destination_grp ADD CONSTRAINT flow_access_destination_grp_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES flow.nwgroup(nwgroup_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_service DROP CONSTRAINT IF EXISTS flow_access_service_access_foreign_key;
ALTER TABLE flow.access_service ADD CONSTRAINT flow_access_service_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_service DROP CONSTRAINT IF EXISTS flow_access_service_svcobject_foreign_key;
ALTER TABLE flow.access_service ADD CONSTRAINT flow_access_service_svcobject_foreign_key FOREIGN KEY (svcobj_id) REFERENCES flow.svcobject(svcobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_service_grp DROP CONSTRAINT IF EXISTS flow_access_service_grp_access_foreign_key;
ALTER TABLE flow.access_service_grp ADD CONSTRAINT flow_access_service_grp_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_service_grp DROP CONSTRAINT IF EXISTS flow_access_service_grp_svcgroup_foreign_key;
ALTER TABLE flow.access_service_grp ADD CONSTRAINT flow_access_service_grp_svcgroup_foreign_key FOREIGN KEY (svcgroup_id) REFERENCES flow.svcgroup(svcgroup_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_timeobject DROP CONSTRAINT IF EXISTS flow_access_timeobject_access_foreign_key;
ALTER TABLE flow.access_timeobject ADD CONSTRAINT flow_access_timeobject_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_timeobject DROP CONSTRAINT IF EXISTS flow_access_timeobject_timeobject_foreign_key;
ALTER TABLE flow.access_timeobject ADD CONSTRAINT flow_access_timeobject_timeobject_foreign_key FOREIGN KEY (timeobj_id) REFERENCES flow.timeobject(timeobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.rule_flow_mapping DROP CONSTRAINT IF EXISTS flow_rule_flow_rule_foreign_key;
ALTER TABLE flow.rule_flow_mapping ADD CONSTRAINT flow_rule_flow_rule_foreign_key FOREIGN KEY (rule_id) REFERENCES rule(rule_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.rule_flow_mapping DROP CONSTRAINT IF EXISTS flow_rule_flow_access_foreign_key;
ALTER TABLE flow.rule_flow_mapping ADD CONSTRAINT flow_rule_flow_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.nwgroup_member DROP CONSTRAINT IF EXISTS flow_nwgroup_member_nwgroup_foreign_key;
ALTER TABLE flow.nwgroup_member ADD CONSTRAINT flow_nwgroup_member_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES flow.nwgroup(nwgroup_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.nwgroup_member DROP CONSTRAINT IF EXISTS flow_nwgroup_member_nwobject_foreign_key;
ALTER TABLE flow.nwgroup_member ADD CONSTRAINT flow_nwgroup_member_nwobject_foreign_key FOREIGN KEY (nwobj_id) REFERENCES flow.nwobject(nwobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.svcgroup_member DROP CONSTRAINT IF EXISTS flow_svcgroup_member_svcgroup_foreign_key;
ALTER TABLE flow.svcgroup_member ADD CONSTRAINT flow_svcgroup_member_svcgroup_foreign_key FOREIGN KEY (svcgroup_id) REFERENCES flow.svcgroup(svcgroup_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.svcgroup_member DROP CONSTRAINT IF EXISTS flow_svcgroup_member_svcobject_foreign_key;
ALTER TABLE flow.svcgroup_member ADD CONSTRAINT flow_svcgroup_member_svcobject_foreign_key FOREIGN KEY (svcobj_id) REFERENCES flow.svcobject(svcobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.nwobject_mapping DROP CONSTRAINT IF EXISTS flow_nwobject_mapping_flow_nwobject_foreign_key;
ALTER TABLE flow.nwobject_mapping ADD CONSTRAINT flow_nwobject_mapping_flow_nwobject_foreign_key FOREIGN KEY (flow_nwobj_id) REFERENCES flow.nwobject(nwobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.nwobject_mapping DROP CONSTRAINT IF EXISTS flow_nwobject_mapping_object_foreign_key;
ALTER TABLE flow.nwobject_mapping ADD CONSTRAINT flow_nwobject_mapping_object_foreign_key FOREIGN KEY (obj_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.nwobject_mapping DROP CONSTRAINT IF EXISTS flow_nwobject_mapping_management_foreign_key;
ALTER TABLE flow.nwobject_mapping ADD CONSTRAINT flow_nwobject_mapping_management_foreign_key FOREIGN KEY (mgm_id) REFERENCES management(mgm_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.svcobject_mapping DROP CONSTRAINT IF EXISTS flow_svcobject_mapping_flow_svcobject_foreign_key;
ALTER TABLE flow.svcobject_mapping ADD CONSTRAINT flow_svcobject_mapping_flow_svcobject_foreign_key FOREIGN KEY (flow_svcobj_id) REFERENCES flow.svcobject(svcobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.svcobject_mapping DROP CONSTRAINT IF EXISTS flow_svcobject_mapping_service_foreign_key;
ALTER TABLE flow.svcobject_mapping ADD CONSTRAINT flow_svcobject_mapping_service_foreign_key FOREIGN KEY (svc_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.svcobject_mapping DROP CONSTRAINT IF EXISTS flow_svcobject_mapping_management_foreign_key;
ALTER TABLE flow.svcobject_mapping ADD CONSTRAINT flow_svcobject_mapping_management_foreign_key FOREIGN KEY (mgm_id) REFERENCES management(mgm_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.timeobject_mapping DROP CONSTRAINT IF EXISTS flow_timeobject_mapping_flow_timeobject_foreign_key;
ALTER TABLE flow.timeobject_mapping ADD CONSTRAINT flow_timeobject_mapping_flow_timeobject_foreign_key FOREIGN KEY (flow_timeobj_id) REFERENCES flow.timeobject(timeobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.timeobject_mapping DROP CONSTRAINT IF EXISTS flow_timeobject_mapping_time_object_foreign_key;
ALTER TABLE flow.timeobject_mapping ADD CONSTRAINT flow_timeobject_mapping_time_object_foreign_key FOREIGN KEY (time_obj_id) REFERENCES time_object(time_obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.timeobject_mapping DROP CONSTRAINT IF EXISTS flow_timeobject_mapping_management_foreign_key;
ALTER TABLE flow.timeobject_mapping ADD CONSTRAINT flow_timeobject_mapping_management_foreign_key FOREIGN KEY (mgm_id) REFERENCES management(mgm_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.nwgroup_mapping DROP CONSTRAINT IF EXISTS flow_nwgroup_mapping_flow_nwgroup_foreign_key;
ALTER TABLE flow.nwgroup_mapping ADD CONSTRAINT flow_nwgroup_mapping_flow_nwgroup_foreign_key FOREIGN KEY (flow_nwgroup_id) REFERENCES flow.nwgroup(nwgroup_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.nwgroup_mapping DROP CONSTRAINT IF EXISTS flow_nwgroup_mapping_objgrp_foreign_key;
ALTER TABLE flow.nwgroup_mapping ADD CONSTRAINT flow_nwgroup_mapping_objgrp_foreign_key FOREIGN KEY (objgrp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.nwgroup_mapping DROP CONSTRAINT IF EXISTS flow_nwgroup_mapping_management_foreign_key;
ALTER TABLE flow.nwgroup_mapping ADD CONSTRAINT flow_nwgroup_mapping_management_foreign_key FOREIGN KEY (mgm_id) REFERENCES management(mgm_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.svcgroup_mapping DROP CONSTRAINT IF EXISTS flow_svcgroup_mapping_flow_svcgroup_foreign_key;
ALTER TABLE flow.svcgroup_mapping ADD CONSTRAINT flow_svcgroup_mapping_flow_svcgroup_foreign_key FOREIGN KEY (flow_svcgroup_id) REFERENCES flow.svcgroup(svcgroup_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.svcgroup_mapping DROP CONSTRAINT IF EXISTS flow_svcgroup_mapping_svcgrp_foreign_key;
ALTER TABLE flow.svcgroup_mapping ADD CONSTRAINT flow_svcgroup_mapping_svcgrp_foreign_key FOREIGN KEY (svcgrp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.svcgroup_mapping DROP CONSTRAINT IF EXISTS flow_svcgroup_mapping_management_foreign_key;
ALTER TABLE flow.svcgroup_mapping ADD CONSTRAINT flow_svcgroup_mapping_management_foreign_key FOREIGN KEY (mgm_id) REFERENCES management(mgm_id) ON UPDATE RESTRICT ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS idx_flow_access_hash ON flow.access (access_hash);
CREATE INDEX IF NOT EXISTS idx_flow_access_active ON flow.access (access_hash) WHERE state IN ('requested', 'implemented');
CREATE INDEX IF NOT EXISTS idx_flow_access_source_nwobj ON flow.access_source (nwobj_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_source_grp_nwgrp ON flow.access_source_grp (nwgroup_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_destination_nwobj ON flow.access_destination (nwobj_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_destination_grp_nwgrp ON flow.access_destination_grp (nwgroup_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_service_svcobj ON flow.access_service (svcobj_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_service_grp_svcgrp ON flow.access_service_grp (svcgroup_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_timeobject ON flow.access_timeobject (timeobj_id);
CREATE INDEX IF NOT EXISTS idx_flow_rule_flow_access ON flow.rule_flow_mapping (access_id);

CREATE INDEX IF NOT EXISTS idx_flow_nwgroup_member_nwobj ON flow.nwgroup_member (nwobj_id);

CREATE INDEX IF NOT EXISTS idx_flow_svcgroup_member_svcobj ON flow.svcgroup_member (svcobj_id);

CREATE INDEX IF NOT EXISTS idx_flow_nwobject_mapping_obj ON flow.nwobject_mapping (obj_id);
CREATE INDEX IF NOT EXISTS idx_flow_nwobject_mapping_mgm ON flow.nwobject_mapping (mgm_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_flow_nwobject_mapping_active_unique ON flow.nwobject_mapping (flow_nwobj_id, mgm_id) WHERE active_on_mgm = TRUE;

CREATE INDEX IF NOT EXISTS idx_flow_svcobject_mapping_svc ON flow.svcobject_mapping (svc_id);
CREATE INDEX IF NOT EXISTS idx_flow_svcobject_mapping_mgm ON flow.svcobject_mapping (mgm_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_flow_svcobject_mapping_active_unique ON flow.svcobject_mapping (flow_svcobj_id, mgm_id) WHERE active_on_mgm = TRUE;

CREATE INDEX IF NOT EXISTS idx_flow_timeobject_mapping_time_obj ON flow.timeobject_mapping (time_obj_id);
CREATE INDEX IF NOT EXISTS idx_flow_timeobject_mapping_mgm ON flow.timeobject_mapping (mgm_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_flow_timeobject_mapping_active_unique ON flow.timeobject_mapping (flow_timeobj_id, mgm_id) WHERE active_on_mgm = TRUE;

CREATE INDEX IF NOT EXISTS idx_flow_nwgroup_mapping_objgrp ON flow.nwgroup_mapping (objgrp_id);
CREATE INDEX IF NOT EXISTS idx_flow_nwgroup_mapping_mgm ON flow.nwgroup_mapping (mgm_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_flow_nwgroup_mapping_active_unique ON flow.nwgroup_mapping (flow_nwgroup_id, mgm_id) WHERE active_on_mgm = TRUE;

CREATE INDEX IF NOT EXISTS idx_flow_svcgroup_mapping_svcgrp ON flow.svcgroup_mapping (svcgrp_id);
CREATE INDEX IF NOT EXISTS idx_flow_svcgroup_mapping_mgm ON flow.svcgroup_mapping (mgm_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_flow_svcgroup_mapping_active_unique ON flow.svcgroup_mapping (flow_svcgroup_id, mgm_id) WHERE active_on_mgm = TRUE;

------------------------------------------------------------------------------
