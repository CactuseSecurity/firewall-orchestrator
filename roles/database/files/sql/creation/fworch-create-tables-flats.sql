-- these tables contain redundant data that is imported from the main tables and flattened for easier querying in the UI. 
-- They are updated during the import process and are not used for any other purpose. 
-- They are not normalized and may contain duplicate data, but this is acceptable for their intended use case.

Create table "usergrp_flat"
(
	"active" Boolean NOT NULL Default TRUE,
	"usergrp_flat_id" BIGINT NOT NULL,
	"usergrp_flat_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("usergrp_flat_id","usergrp_flat_member_id")
);

Create table "objgrp_flat"
(
	"objgrp_flat_id" BIGINT NOT NULL,
	"objgrp_flat_member_id" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT
);

Create table "svcgrp_flat"
(
	"svcgrp_flat_id" BIGINT NOT NULL,
	"svcgrp_flat_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT
);
