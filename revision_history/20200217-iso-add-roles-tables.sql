-- also dropping all "with OIDs" statements !!!
-- Alter table %all% delete "with oids";

Drop sequence "public"."role_role_id_seq" Cascade;

Create sequence "public"."role_role_id_seq"
Increment 1
Minvalue 1
Maxvalue 9223372036854775807
Cache 1;

Create table "role"
(
	"role_id" Integer NOT NULL Default nextval('public.role_role_id_seq'::text),
	"role_name" Varchar NOT NULL,
	"role_can_view_all_devices" Boolean NOT NULL Default false,
	"role_is_superadmin" Boolean NOT NULL default false,
 primary key ("role_id")
);

Create table "role_to_user"
(
	"role_id" Integer NOT NULL,
	"user_id" Integer NOT NULL,
 primary key ("role_id", "user_id")
);

Create table "role_to_device"
(
	"role_id" Integer NOT NULL,
	"device_id" Integer NOT NULL,
 primary key ("role_id", "device_id")
);



Alter table "role_to_user" add  foreign key ("role_id") references "role" ("role_id") on update restrict on delete cascade;
Alter table "role_to_user" add  foreign key ("user_id") references "isoadmin" ("isoadmin_id") on update restrict on delete cascade;
Alter table "role_to_device" add  foreign key ("role_id") references "role" ("role_id") on update restrict on delete cascade;
Alter table "role_to_device" add  foreign key ("device_id") references "device" ("dev_id") on update restrict on delete cascade;

Grant select on "role" to group "secuadmins";
Grant select on "role" to group "isoadmins";
Grant select on "role" to group "reporters";
Grant update on "role" to group "isoadmins";
Grant insert on "role" to group "isoadmins";
Grant select on "role_to_user" to group "secuadmins";
Grant select on "role_to_user" to group "isoadmins";
Grant select on "role_to_user" to group "reporters";
Grant update on "role_to_user" to group "isoadmins";
Grant insert on "role_to_user" to group "isoadmins";
Grant select on "role_to_device" to group "secuadmins";
Grant select on "role_to_device" to group "isoadmins";
Grant select on "role_to_device" to group "reporters";
Grant update on "role_to_device" to group "isoadmins";
Grant insert on "role_to_device" to group "isoadmins";

insert into role (role_name, role_can_view_all_devices, role_is_superadmin) values ('Superadmin', true, true);
insert into role (role_name, role_can_view_all_devices, role_is_superadmin) values ('Reporters', true, true);
insert into role_to_user (role_id, user_id) values (1, 3);
