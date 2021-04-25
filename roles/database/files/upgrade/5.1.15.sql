
Alter table "language" ADD COLUMN IF NOT EXISTS "culture_info" Varchar;

UPDATE language SET culture_info = 'de-DE' WHERE name='German';
UPDATE language SET culture_info = 'en-US' WHERE name='English';

Alter table "language" ALTER COLUMN "culture_info" SET NOT NULL;
