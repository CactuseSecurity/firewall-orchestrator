
Create table if not exists "customtxt"
(
	"id" Varchar NOT NULL,
    "language" Varchar NOT NULL,
	"txt" Varchar NOT NULL,
    primary key ("id", "language")
);

DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'customtxt_language_fkey')
  THEN
        Alter table "customtxt" add foreign key ("language") references "language" ("name") on update restrict on delete cascade;
  END IF;
END $$;
