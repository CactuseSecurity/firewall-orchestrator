-- dropping trigger for materialized view view_rule_with_owner
drop trigger IF exists refresh_view_rule_with_owner_delete_trigger ON recertification CASCADE;

insert into config (config_key, config_value, config_user) VALUES ('ownerLdapId', '1', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('ownerLdapGroupNames', 'cn=ModellerGroup_@@ExternalAppId@@,ou=groupOfUniqueNames,dc=fworch,dc=internal', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('manageOwnerLdapGroups', 'true', 0) ON CONFLICT DO NOTHING;
