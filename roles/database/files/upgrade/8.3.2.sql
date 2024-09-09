insert into config (config_key, config_value, config_user) VALUES ('welcomeMessage', '', 0) ON CONFLICT DO NOTHING;
-- INSERT INTO txt VALUES ('H9054', 'German',  'Nachricht die auf der Anmeldeseite angezeigt werden soll.');
-- INSERT INTO txt VALUES ('H9054', 'English', 'Message that is displayed on Login Page');