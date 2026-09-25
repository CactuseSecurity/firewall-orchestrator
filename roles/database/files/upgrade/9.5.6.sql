-- Wait time of the variance analysis for a pending rule_owner mapping run.
-- Only relevant for the NameField mapping source; 0 keeps the previous behaviour of
-- falling back to the marker query right away.
insert into config (config_key, config_value, config_user) VALUES ('varianceNameFieldWaitTime', '0', 0) ON CONFLICT DO NOTHING;
