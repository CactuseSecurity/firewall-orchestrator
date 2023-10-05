
Create table if not exists "customtxt"
(
	"id" Varchar NOT NULL,
    "language" Varchar NOT NULL,
	"txt" Varchar NOT NULL,
    primary key ("id", "language")
);

Alter table "customtxt" add foreign key ("language") references "language" ("name") on update restrict on delete cascade;
