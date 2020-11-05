-- test upgrade

-- introducing new more easily extensible language schema


-- contains exactly one entry for each language available
Create table "language"
(
	"id" Integer NOT NULL,
	"name" Varchar NOT NULL,
    primary key ("id")
);

-- contains all texts in all languages
Create table "plain_text"
(
	"id" Serial,
	"key" Varchar NOT NULL,
	"language_id" Integer,
    "text" Varchar,
    primary key ("id", "language_id")
);
