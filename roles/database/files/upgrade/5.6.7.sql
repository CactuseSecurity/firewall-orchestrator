
ALTER TABLE "ldap_connection" ADD column IF NOT EXISTS "active" Boolean NOT NULL Default TRUE;
