-- add table rule_time

create table time_object
(
	time_obj_id BIGSERIAL PRIMARY KEY,
	time_obj_type INT DEFAULT 0, -- 0 = undefined, 1 = time span, ...
	time_obj_name Varchar,
	time_obj_uid Varchar,
	start_time TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
	end_time TIMESTAMP WITH TIME ZONE,
	created BIGINT,
	removed BIGINT
);

create table rule_time
(
	rule_time_id BIGSERIAL PRIMARY KEY,
	rule_id BIGINT,
  	time_obj_id BIGINT,
	created BIGINT,
  	removed BIGINT
);
