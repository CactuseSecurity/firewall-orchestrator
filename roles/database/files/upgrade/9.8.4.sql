-- maximum and default number of objects a REST list endpoint of the middleware returns per page
INSERT INTO config (config_key, config_value, config_user)
VALUES ('maxRestResponseObjectLimit', '10000', 0)
ON CONFLICT DO NOTHING;
