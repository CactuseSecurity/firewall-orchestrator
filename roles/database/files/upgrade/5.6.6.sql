ALTER table "rule_from" drop column IF EXISTS rule_from_id;
ALTER table "rule_to" ADD column IF NOT EXISTS user_id BIGINT;

ALTER TABLE "rule_to"
    DROP CONSTRAINT IF EXISTS "rule_to_usr_user_id_fkey" CASCADE;
ALTER TABLE "rule_to"
    ADD CONSTRAINT rule_to_usr_user_id_fkey FOREIGN KEY ("user_id") REFERENCES "usr" ("user_id") ON UPDATE RESTRICT ON DELETE CASCADE;
