insert into stm_link_type (id, name) VALUES (6, 'nat') ON CONFLICT (id) DO NOTHING;
insert into stm_link_type (id, name) VALUES (7, 'policy') ON CONFLICT (id) DO NOTHING;
