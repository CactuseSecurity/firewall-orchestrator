insert into config (config_key, config_value, config_user) VALUES ('maxImportDuration', '4', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('maxImportInterval', '12', 0) ON CONFLICT DO NOTHING;

ALTER TABLE "management" DROP COLUMN IF exists "last_import_md5_rules";
ALTER TABLE "management" DROP COLUMN IF exists "last_import_md5_objects";
ALTER TABLE "management" DROP COLUMN IF exists "last_import_md5_users";

ALTER TABLE "management" ADD COLUMN IF NOT EXISTS "last_import_attempt" Timestamp;
ALTER TABLE "management" ADD COLUMN IF NOT EXISTS "last_import_attempt_successful" Boolean NOT NULL Default false,
