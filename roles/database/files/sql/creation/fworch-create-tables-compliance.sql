
--- Compliance ---
create schema compliance;

create table compliance.network_zone
(
    id BIGSERIAL PRIMARY KEY,
	name VARCHAR NOT NULL,
	description VARCHAR NOT NULL,
	super_network_zone_id bigint,
	owner_id bigint,
	removed timestamp with time zone,
	created timestamp with time zone default now(),
	criterion_id INT,
    id_string TEXT,
	is_auto_calculated_internet_zone BOOLEAN DEFAULT FALSE,
	is_auto_calculated_undefined_internal_zone BOOLEAN DEFAULT FALSE
);

create table compliance.network_zone_communication
(
	criterion_id INT,
    from_network_zone_id bigint NOT NULL,
	to_network_zone_id bigint NOT NULL,
    removed timestamp with time zone,
	created timestamp with time zone default now()
);

create table compliance.ip_range
(
    network_zone_id bigint NOT NULL,
	ip_range_start inet NOT NULL,
	ip_range_end inet NOT NULL,
	PRIMARY KEY(network_zone_id, ip_range_start, ip_range_end, created),
	removed timestamp with time zone,
	created timestamp with time zone default now(),
	criterion_id INT,
    name TEXT
);

create table compliance.policy
(
    id SERIAL PRIMARY KEY,
	name TEXT,
	created_date timestamp default now(),
	disabled bool
);

create table compliance.policy_criterion
(
    policy_id INT NOT NULL,
	criterion_id INT NOT NULL,
    removed timestamp with time zone,
	created timestamp with time zone default now()
);

create table compliance.criterion
(
    id SERIAL PRIMARY KEY,
	name TEXT,
	comment TEXT,
	criterion_type TEXT,
	content TEXT,
	removed timestamp with time zone,
	created timestamp with time zone default now(),
	import_source TEXT
);

create table compliance.violation
(
    id BIGSERIAL PRIMARY KEY,
	rule_id bigint NOT NULL,
	rule_uid TEXT,
	mgmt_uid TEXT,
	found_date timestamp with time zone default now(),
	removed_date timestamp with time zone,
	details TEXT,
	risk_score real,
	policy_id INT NOT NULL,
	criterion_id INT NOT NULL,
	is_initial BOOLEAN NOT NULL
);

-- create table compliance.assessability_issue
-- (
--     violation_id BIGINT NOT NULL,
-- 	type_id INT NOT NULL,
-- 	PRIMARY KEY(violation_id, type_id)
-- );

-- create table compliance.assessability_issue_type
-- (
-- 	type_id INT PRIMARY KEY,
--     type_name VARCHAR(50) NOT NULL
-- );

