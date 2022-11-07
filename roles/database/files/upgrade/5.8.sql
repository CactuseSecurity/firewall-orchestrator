ALTER TABLE management ADD COLUMN IF NOT EXISTS "domain_uid" varchar;

UPDATE stm_dev_typ SET dev_typ_is_mgmt = TRUE WHERE dev_typ_id=9; -- fix wrong value: Check Point','R8x

DROP index IF EXISTS "svcgrp_flat_svcgrp_flat_id";
Create index "svcgrp_flat_svcgrp_flat_id" on "svcgrp_flat" ("svcgrp_flat_id");

DROP index IF EXISTS IX_Relationship105; -- Create index "IX_Relationship105" on "objgrp_flat" ("objgrp_flat_id");
Create index IF NOT EXISTS idx_objgrp_flat01 on objgrp_flat (objgrp_flat_id);

DROP index IF EXISTS "IX_Relationship119"; -- on "svcgrp_flat" ("svcgrp_flat_member_id");
DROP index IF EXISTS "IX_Relationship150"; -- on "usergrp_flat" ("usergrp_flat_member_id");
DROP index IF EXISTS "IX_Relationship106"; -- on "objgrp_flat" ("objgrp_flat_member_id");
Create index IF NOT EXISTS "idx_svcgrp_flat02" on "svcgrp_flat" ("svcgrp_flat_member_id");
Create index IF NOT EXISTS "idx_usergrp_flat02" on "usergrp_flat" ("usergrp_flat_member_id");
Create index IF NOT EXISTS "idx_objgrp_flat02" on "objgrp_flat" ("objgrp_flat_member_id");

