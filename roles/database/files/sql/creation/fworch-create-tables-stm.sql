
Create table "stm_link_type"
(
	"id" Integer primary key,
	"name" Varchar NOT NULL
);

Create table "stm_action"
(
	"action_id" Integer,
	"action_name" Varchar NOT NULL,
	"allowed" BOOLEAN NOT NULL DEFAULT TRUE,
 primary key ("action_id")
);

Create table "stm_color"
(
	"color_id" SERIAL,
	"color_name" Varchar NOT NULL,
	"color_rgb" Char(7) NOT NULL,
	"color_comment" Text,
 primary key ("color_id")
);

Create table "stm_dev_typ"
(
	"dev_typ_id" Integer,
	"dev_typ_manufacturer" Varchar,
	"dev_typ_name" Varchar NOT NULL,
	"dev_typ_version" Varchar NOT NULL,
	"dev_typ_comment" Text,
	"dev_typ_predef_svc" Text,
	"dev_typ_predef_obj" Text,
	"dev_typ_is_mgmt" Boolean,
	"dev_typ_config_file_rules" Varchar,
	"dev_typ_config_file_basic_objects" Varchar,
	"dev_typ_config_file_users" Varchar,
	"dev_typ_is_multi_mgmt" Boolean Default FALSE,
	"is_pure_routing_device" Boolean Default FALSE,
 primary key ("dev_typ_id")
);

Create table "stm_obj_typ"
(
	"obj_typ_id" Integer,
	"obj_typ_name" Varchar NOT NULL,
	"obj_typ_comment" Text,
 primary key ("obj_typ_id")
);

Create table "stm_track"
(
	"track_id" Integer,
	"track_name" Varchar NOT NULL,
 primary key ("track_id")
);

CREATE TABLE "stm_import"
(
    "import_type_id" Integer PRIMARY KEY,
    "import_type_name" Varchar NOT NULL
);

Create table "stm_ip_proto"
(
	"ip_proto_id" Integer NOT NULL,
	"ip_proto_name" Varchar,
	"ip_proto_comment" Text,
 primary key ("ip_proto_id")
);

Create table "stm_svc_typ"
(
	"svc_typ_id" Integer NOT NULL UNIQUE,
	"svc_typ_name" Varchar,
	"svc_typ_comment" Text,
 primary key ("svc_typ_id")
);

Create table "stm_usr_typ"
(
	"usr_typ_id" Integer NOT NULL UNIQUE,
	"usr_typ_name" Varchar,
 primary key ("usr_typ_id")
);
