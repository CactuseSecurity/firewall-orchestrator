insert into config (config_key, config_value, config_user) VALUES ('reducedProtocolSetProtocols', '["tcp","udp","icmp","esp"]', 0) ON CONFLICT DO NOTHING;
