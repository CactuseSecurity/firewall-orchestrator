insert into config (config_key, config_value, config_user) VALUES ('reqUseFlowDb', 'False', 0) ON CONFLICT DO NOTHING;

ALTER TABLE request.reqtask ADD COLUMN IF NOT EXISTS flow_access_id bigint;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS flow_nwobj_id bigint;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS flow_nwgrp_id bigint;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS flow_svcobj_id bigint;
ALTER TABLE request.reqelement ADD COLUMN IF NOT EXISTS flow_svcgrp_id bigint;

ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_flow_access_foreign_key;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_flow_access_foreign_key FOREIGN KEY (flow_access_id) REFERENCES flow.access(access_id) ON UPDATE RESTRICT ON DELETE SET NULL;

ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_flow_nwobject_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_flow_nwobject_foreign_key FOREIGN KEY (flow_nwobj_id) REFERENCES flow.nwobject(nwobj_id) ON UPDATE RESTRICT ON DELETE SET NULL;

ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_flow_nwgroup_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_flow_nwgroup_foreign_key FOREIGN KEY (flow_nwgrp_id) REFERENCES flow.nwgroup(nwgrp_id) ON UPDATE RESTRICT ON DELETE SET NULL;

ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_flow_svcobject_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_flow_svcobject_foreign_key FOREIGN KEY (flow_svcobj_id) REFERENCES flow.svcobject(svcobj_id) ON UPDATE RESTRICT ON DELETE SET NULL;

ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_flow_svcgroup_foreign_key;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_flow_svcgroup_foreign_key FOREIGN KEY (flow_svcgrp_id) REFERENCES flow.svcgroup(svcgrp_id) ON UPDATE RESTRICT ON DELETE SET NULL;
