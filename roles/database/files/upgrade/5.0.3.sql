Alter table "config" add  column "config_user" Integer;
Alter table "config" drop constraint "config_pkey";
Alter table "config" add primary key ("config_key","config_user");
Alter table "config" add foreign key ("config_user") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
