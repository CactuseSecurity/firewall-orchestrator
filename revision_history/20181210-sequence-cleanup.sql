-- reset sequence numbers of import tables
-- NOTE: DB MODEL NEEDS TO BE CHANGED AS WELL!!!

Create sequence "public"."import_service_svc_id_seq"
Increment 1
Minvalue 1
Maxvalue 9223372036854775807
Cache 1;

Create sequence "public"."import_zone_zone_id_seq"
Increment 1
Minvalue 1
Maxvalue 9223372036854775807
Cache 1;

DELETE FROM import_object;
ALTER SEQUENCE import_object_obj_id_seq RESTART;

DELETE FROM import_rule;
ALTER SEQUENCE import_rule_rule_id_seq RESTART;

DELETE FROM import_user;
ALTER SEQUENCE import_user_user_id_seq RESTART;

ALTER TABLE import_service DROP COLUMN "zone_id";
ALTER TABLE import_service DROP COLUMN "svc_id";
ALTER TABLE import_service ADD COLUMN "svc_id" Integer NOT NULL Default nextval('public.import_service_svc_id_seq'::text) UNIQUE;
DELETE FROM import_service;
ALTER SEQUENCE import_service_svc_id_seq RESTART;

DELETE FROM import_zone;
ALTER SEQUENCE import_zone_zone_id_seq RESTART;
ALTER TABLE import_zone ADD COLUMN "zone_id" Integer NOT NULL Default nextval('public.import_zone_zone_id_seq'::text) UNIQUE;

GRANT SELECT, UPDATE ON SEQUENCE public.import_service_svc_id_seq TO PUBLIC;
GRANT ALL ON SEQUENCE public.import_service_svc_id_seq TO dbadmin;

GRANT SELECT, UPDATE ON SEQUENCE public.import_zone_zone_id_seq TO PUBLIC;
GRANT ALL ON SEQUENCE public.import_zone_zone_id_seq TO dbadmin;

