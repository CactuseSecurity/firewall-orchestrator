
-- ALTER table "rule_from" ADD column IF NOT EXISTS rule_from_id BIGSERIAL primary key;
-- ALTER TABLE "rule_from" DROP constraint if exists rule_from_pkey;
-- ALTER TABLE "rule_from" ADD constraint rule_from_pkey primary key ("rule_from_id");

ALTER TABLE "rule_to" DROP CONSTRAINT if exists rule_to_pkey;

ALTER table "rule_to" ADD column IF NOT EXISTS user_id BIGINT;
ALTER table "rule_to" ADD column IF NOT EXISTS rule_to_id BIGSERIAL primary key;
