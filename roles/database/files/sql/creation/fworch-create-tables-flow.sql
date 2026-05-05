
-- flow -----------------------------------------------------------

-- create schema
create schema flow;

-- create tables
create table flow.nwobject
(
    nwobj_id BIGSERIAL PRIMARY KEY,
    name varchar,
    ip_start inet, -- null for e.g. FQDN-based objects
    ip_end inet,
    nwobj_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    check ((ip_start IS NULL) = (ip_end IS NULL)),
    check (ip_start <= ip_end),
    check (state IN ('requested', 'denied', 'implemented', 'removed'))
);

create table flow.nwgroup
(
    nwgroup_id BIGSERIAL PRIMARY KEY,
    name varchar NOT NULL,
    nwgrp_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    check (state IN ('requested', 'denied', 'implemented', 'removed'))
);

create table flow.svcobject
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
    check ((port_start IS NULL) = (port_end IS NULL)),
    check (port_start <= port_end),
    check (port_start between 0 and 65535),
    check (port_end between 0 and 65535),
    check (state IN ('requested', 'denied', 'implemented', 'removed'))
);

create table flow.svcgroup
(
    svcgroup_id BIGSERIAL PRIMARY KEY,
    name varchar NOT NULL,
    svcgrp_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    check (state IN ('requested', 'denied', 'implemented', 'removed'))
);

create table flow.timeobject
(
    timeobj_id BIGSERIAL PRIMARY KEY,
    name varchar NOT NULL,
    start_time Timestamp with time zone,
    end_time Timestamp with time zone,
    timeobj_hash varchar(64) NOT NULL UNIQUE,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    show_in_request_module boolean NOT NULL DEFAULT FALSE,
    check (start_time <= end_time),
    check (state IN ('requested', 'denied', 'implemented', 'removed'))
);

create table flow.access
(
    access_id BIGSERIAL PRIMARY KEY,
    access_hash varchar(64) NOT NULL UNIQUE,
    requester_id integer,
    owner_id integer,
    state varchar(32) NOT NULL DEFAULT 'requested',
    removed_date Timestamp with time zone,
    check (state in ('requested', 'denied', 'implemented', 'removed'))
);

create table flow.access_source
(
    access_id bigint NOT NULL,
    nwobj_id bigint NOT NULL,
    primary key (access_id, nwobj_id)
);

create table flow.access_source_grp
(
    access_id bigint NOT NULL,
    nwgroup_id bigint NOT NULL,
    primary key (access_id, nwgroup_id)
);

create table flow.access_destination
(
    access_id bigint NOT NULL,
    nwobj_id bigint NOT NULL,
    primary key (access_id, nwobj_id)
);

create table flow.access_destination_grp
(
    access_id bigint NOT NULL,
    nwgroup_id bigint NOT NULL,
    primary key (access_id, nwgroup_id)
);

create table flow.access_service
(
    access_id bigint NOT NULL,
    svcobj_id bigint NOT NULL,
    primary key (access_id, svcobj_id)
);

create table flow.access_service_grp
(
    access_id bigint NOT NULL,
    svcgroup_id bigint NOT NULL,
    primary key (access_id, svcgroup_id)
);

create table flow.access_timeobject
(
    access_id bigint NOT NULL,
    timeobj_id bigint NOT NULL,
    primary key (access_id, timeobj_id)
);

create table flow.nwgroup_member
(
    nwgroup_id bigint NOT NULL,
    nwobj_id bigint NOT NULL,
    primary key (nwgroup_id, nwobj_id)
);

create table flow.svcgroup_member
(
    svcgroup_id bigint NOT NULL,
    svcobj_id bigint NOT NULL,
    primary key (svcgroup_id, svcobj_id)
);

create table flow.rule_flow_mapping
(
    rule_id bigint PRIMARY KEY,
    access_id bigint NOT NULL
);

create table flow.nwobject_mapping
(
    flow_nwobj_id bigint NOT NULL,
    obj_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

create table flow.svcobject_mapping
(
    flow_svcobj_id bigint NOT NULL,
    svc_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

create table flow.timeobject_mapping
(
    flow_timeobj_id bigint NOT NULL,
    time_obj_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

create table flow.nwgroup_mapping
(
    flow_nwgroup_id bigint NOT NULL,
    objgrp_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);

create table flow.svcgroup_mapping
(
    flow_svcgroup_id bigint NOT NULL,
    svcgrp_id bigint NOT NULL PRIMARY KEY,
    mgm_id integer NOT NULL,
    active_on_mgm boolean NOT NULL DEFAULT FALSE
);
