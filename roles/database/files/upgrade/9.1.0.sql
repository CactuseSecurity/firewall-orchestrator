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
    CHECK ((ip_start IS NULL) = (ip_end IS NULL)),
    CHECK (ip_start <= ip_end),
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.nwgroup
(
    nwgrp_id BIGSERIAL PRIMARY KEY,
    name varchar,
    nwgrp_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.svcobject
(
    svcobj_id BIGSERIAL PRIMARY KEY,
    name varchar,
    port_start integer, -- null for e.g. icmp-based objects
    port_end integer,
    ip_proto_id integer NOT NULL,
    svcobj_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    CHECK (port_start <= port_end),
    CHECK ((port_start IS NULL) = (port_end IS NULL)),
    CHECK (port_start BETWEEN 0 AND 65535),
    CHECK (port_end BETWEEN 0 AND 65535),
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.svcgroup
(
    svcgrp_id BIGSERIAL PRIMARY KEY,
    name varchar,
    svcgrp_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    CHECK (state IN ('requested', 'denied', 'implemented', 'removed'))
);

CREATE TABLE IF NOT EXISTS flow.timeobject
(
    timeobj_id BIGSERIAL PRIMARY KEY,
    name varchar,
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
    nwgrp_id bigint NOT NULL,
    PRIMARY KEY (access_id, nwgrp_id)
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
    nwgrp_id bigint NOT NULL,
    PRIMARY KEY (access_id, nwgrp_id)
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
    svcgrp_id bigint NOT NULL,
    PRIMARY KEY (access_id, svcgrp_id)
);

CREATE TABLE IF NOT EXISTS flow.access_timeobject
(
    access_id bigint NOT NULL,
    timeobj_id bigint NOT NULL,
    PRIMARY KEY (access_id, timeobj_id)
);

CREATE TABLE IF NOT EXISTS flow.nwgroup_member
(
    nwgrp_id bigint NOT NULL,
    nwobj_id bigint NOT NULL,
    PRIMARY KEY (nwgrp_id, nwobj_id)
);

CREATE TABLE IF NOT EXISTS flow.svcgroup_member
(
    svcgrp_id bigint NOT NULL,
    svcobj_id bigint NOT NULL,
    PRIMARY KEY (svcgrp_id, svcobj_id)
);

ALTER TABLE object ADD COLUMN IF NOT EXISTS flow_nwobj_id bigint;
ALTER TABLE object DROP CONSTRAINT IF EXISTS flow_nwobj_id_foreign_key;
ALTER TABLE object ADD CONSTRAINT flow_nwobj_id_foreign_key FOREIGN KEY (flow_nwobj_id) REFERENCES flow.nwobject(nwobj_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE object ADD COLUMN IF NOT EXISTS flow_nwgrp_id bigint;
ALTER TABLE object DROP CONSTRAINT IF EXISTS flow_nwgrp_id_foreign_key;
ALTER TABLE object ADD CONSTRAINT flow_nwgrp_id_foreign_key FOREIGN KEY (flow_nwgrp_id) REFERENCES flow.nwgroup(nwgrp_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE object ADD COLUMN IF NOT EXISTS flow_active boolean NOT NULL DEFAULT FALSE;
-- only one entry per (mgm_id, flow_nwobj_id) or (mgm_id, flow_nwgrp_id) should be active (selected) at a time
CREATE UNIQUE INDEX IF NOT EXISTS object_flow_nwobj_id_active_only_one_per_mgm
    ON object (mgm_id, flow_nwobj_id)
    WHERE flow_active = true;
CREATE UNIQUE INDEX IF NOT EXISTS object_flow_nwgrp_id_active_only_one_per_mgm
    ON object (mgm_id, flow_nwgrp_id)
    WHERE flow_active = true;

ALTER TABLE service ADD COLUMN IF NOT EXISTS flow_svcobj_id bigint;
ALTER TABLE service DROP CONSTRAINT IF EXISTS flow_svcobj_id_foreign_key;
ALTER TABLE service ADD CONSTRAINT flow_svcobj_id_foreign_key FOREIGN KEY (flow_svcobj_id) REFERENCES flow.svcobject(svcobj_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE service ADD COLUMN IF NOT EXISTS flow_svcgrp_id bigint;
ALTER TABLE service DROP CONSTRAINT IF EXISTS flow_svcgrp_id_foreign_key;
ALTER TABLE service ADD CONSTRAINT flow_svcgrp_id_foreign_key FOREIGN KEY (flow_svcgrp_id) REFERENCES flow.svcgroup(svcgrp_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE service ADD COLUMN IF NOT EXISTS flow_active boolean NOT NULL DEFAULT FALSE;
CREATE UNIQUE INDEX IF NOT EXISTS service_flow_svcobj_id_active_only_one_per_mgm
    ON service (mgm_id, flow_svcobj_id)
    WHERE flow_active = true;
CREATE UNIQUE INDEX IF NOT EXISTS service_flow_svcgrp_id_active_only_one_per_mgm
    ON service (mgm_id, flow_svcgrp_id)
    WHERE flow_active = true;

ALTER TABLE time_object ADD COLUMN IF NOT EXISTS flow_timeobj_id bigint;
ALTER TABLE time_object DROP CONSTRAINT IF EXISTS flow_timeobj_id_foreign_key;
ALTER TABLE time_object ADD CONSTRAINT flow_timeobj_id_foreign_key FOREIGN KEY (flow_timeobj_id) REFERENCES flow.timeobject(timeobj_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE time_object ADD COLUMN IF NOT EXISTS flow_active boolean NOT NULL DEFAULT FALSE;
CREATE UNIQUE INDEX IF NOT EXISTS time_object_flow_timeobj_id_active_only_one_per_mgm
    ON time_object (mgm_id, flow_timeobj_id)
    WHERE flow_active = true;

ALTER TABLE rule ADD COLUMN IF NOT EXISTS flow_access_id bigint;
ALTER TABLE rule DROP CONSTRAINT IF EXISTS flow_access_id_foreign_key;
ALTER TABLE rule ADD CONSTRAINT flow_access_id_foreign_key FOREIGN KEY (flow_access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE SET NULL;
ALTER TABLE rule ADD COLUMN IF NOT EXISTS flow_active boolean NOT NULL DEFAULT FALSE;
CREATE UNIQUE INDEX IF NOT EXISTS rule_flow_access_id_active_only_one_per_mgm
    ON rule (mgm_id, flow_access_id)
    WHERE flow_active = true;

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
ALTER TABLE flow.access_source_grp ADD CONSTRAINT flow_access_source_grp_nwgroup_foreign_key FOREIGN KEY (nwgrp_id) REFERENCES flow.nwgroup(nwgrp_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_destination DROP CONSTRAINT IF EXISTS flow_access_destination_access_foreign_key;
ALTER TABLE flow.access_destination ADD CONSTRAINT flow_access_destination_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_destination DROP CONSTRAINT IF EXISTS flow_access_destination_nwobject_foreign_key;
ALTER TABLE flow.access_destination ADD CONSTRAINT flow_access_destination_nwobject_foreign_key FOREIGN KEY (nwobj_id) REFERENCES flow.nwobject(nwobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_destination_grp DROP CONSTRAINT IF EXISTS flow_access_destination_grp_access_foreign_key;
ALTER TABLE flow.access_destination_grp ADD CONSTRAINT flow_access_destination_grp_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_destination_grp DROP CONSTRAINT IF EXISTS flow_access_destination_grp_nwgroup_foreign_key;
ALTER TABLE flow.access_destination_grp ADD CONSTRAINT flow_access_destination_grp_nwgroup_foreign_key FOREIGN KEY (nwgrp_id) REFERENCES flow.nwgroup(nwgrp_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_service DROP CONSTRAINT IF EXISTS flow_access_service_access_foreign_key;
ALTER TABLE flow.access_service ADD CONSTRAINT flow_access_service_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_service DROP CONSTRAINT IF EXISTS flow_access_service_svcobject_foreign_key;
ALTER TABLE flow.access_service ADD CONSTRAINT flow_access_service_svcobject_foreign_key FOREIGN KEY (svcobj_id) REFERENCES flow.svcobject(svcobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_service_grp DROP CONSTRAINT IF EXISTS flow_access_service_grp_access_foreign_key;
ALTER TABLE flow.access_service_grp ADD CONSTRAINT flow_access_service_grp_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_service_grp DROP CONSTRAINT IF EXISTS flow_access_service_grp_svcgroup_foreign_key;
ALTER TABLE flow.access_service_grp ADD CONSTRAINT flow_access_service_grp_svcgroup_foreign_key FOREIGN KEY (svcgrp_id) REFERENCES flow.svcgroup(svcgrp_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.access_timeobject DROP CONSTRAINT IF EXISTS flow_access_timeobject_access_foreign_key;
ALTER TABLE flow.access_timeobject ADD CONSTRAINT flow_access_timeobject_access_foreign_key FOREIGN KEY (access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.access_timeobject DROP CONSTRAINT IF EXISTS flow_access_timeobject_timeobject_foreign_key;
ALTER TABLE flow.access_timeobject ADD CONSTRAINT flow_access_timeobject_timeobject_foreign_key FOREIGN KEY (timeobj_id) REFERENCES flow.timeobject(timeobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.nwgroup_member DROP CONSTRAINT IF EXISTS flow_nwgroup_member_nwgroup_foreign_key;
ALTER TABLE flow.nwgroup_member ADD CONSTRAINT flow_nwgroup_member_nwgroup_foreign_key FOREIGN KEY (nwgrp_id) REFERENCES flow.nwgroup(nwgrp_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.nwgroup_member DROP CONSTRAINT IF EXISTS flow_nwgroup_member_nwobject_foreign_key;
ALTER TABLE flow.nwgroup_member ADD CONSTRAINT flow_nwgroup_member_nwobject_foreign_key FOREIGN KEY (nwobj_id) REFERENCES flow.nwobject(nwobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE flow.svcgroup_member DROP CONSTRAINT IF EXISTS flow_svcgroup_member_svcgroup_foreign_key;
ALTER TABLE flow.svcgroup_member ADD CONSTRAINT flow_svcgroup_member_svcgroup_foreign_key FOREIGN KEY (svcgrp_id) REFERENCES flow.svcgroup(svcgrp_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE flow.svcgroup_member DROP CONSTRAINT IF EXISTS flow_svcgroup_member_svcobject_foreign_key;
ALTER TABLE flow.svcgroup_member ADD CONSTRAINT flow_svcgroup_member_svcobject_foreign_key FOREIGN KEY (svcobj_id) REFERENCES flow.svcobject(svcobj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS idx_flow_access_hash ON flow.access (access_hash);
CREATE INDEX IF NOT EXISTS idx_flow_access_active ON flow.access (access_hash) WHERE state IN ('requested', 'implemented');
CREATE INDEX IF NOT EXISTS idx_flow_access_source_nwobj ON flow.access_source (nwobj_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_source_grp_nwgrp ON flow.access_source_grp (nwgrp_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_destination_nwobj ON flow.access_destination (nwobj_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_destination_grp_nwgrp ON flow.access_destination_grp (nwgrp_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_service_svcobj ON flow.access_service (svcobj_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_service_grp_svcgrp ON flow.access_service_grp (svcgrp_id);
CREATE INDEX IF NOT EXISTS idx_flow_access_timeobject ON flow.access_timeobject (timeobj_id);

CREATE INDEX IF NOT EXISTS idx_flow_nwgroup_member_nwobj ON flow.nwgroup_member (nwobj_id);

CREATE INDEX IF NOT EXISTS idx_flow_svcgroup_member_svcobj ON flow.svcgroup_member (svcobj_id);

------------------------------------------------------------------------------

ALTER TABLE import_control ADD COLUMN IF NOT EXISTS flow_sync_done Boolean NOT NULL Default FALSE;