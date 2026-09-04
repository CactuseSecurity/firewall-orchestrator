
--- Compliance ---
create schema compliance;

create table compliance.network_zone_communication
(
	criterion_id INT,
    from_network_zone_id bigint NOT NULL,
	to_network_zone_id bigint NOT NULL,
    removed timestamp with time zone,
	created timestamp with time zone default now()
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

