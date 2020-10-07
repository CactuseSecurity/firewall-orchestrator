CREATE ROLE textreader LOGIN
  NOSUPERUSER NOINHERIT NOCREATEDB NOCREATEROLE;
COMMENT ON ROLE textreader IS 'wird nur benutzt, um informationen aus der tabelle text_msg zu lesen';

GRANT SELECT ON TABLE text_msg TO textreader;
