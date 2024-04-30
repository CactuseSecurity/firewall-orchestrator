alter table import_control add column if not exists security_relevant_changes_counter INTEGER NOT NULL Default 0;

-- add missing tenant to management mappings for demo data
DO $do$ BEGIN
    IF  EXISTS (SELECT * FROM tenant WHERE tenant_name='tenant1_demo') AND 
        EXISTS (select mgm_id FROM management where management.mgm_name='fortigate_demo')
    THEN 
        IF NOT EXISTS (SELECT * FROM tenant_to_management LEFT JOIN tenant USING (tenant_id) WHERE tenant_name='tenant1_demo') THEN 
        INSERT INTO tenant_to_management (tenant_id, management_id, shared)
            SELECT 
            tenant_id, 
            (select mgm_id FROM management where management.mgm_name='fortigate_demo'),
            TRUE
            FROM tenant WHERE tenant.tenant_name='tenant1_demo'; 
        END IF; 
    END IF; 

    IF  EXISTS (SELECT * FROM tenant WHERE tenant_name='tenant2_demo') AND
        EXISTS (select mgm_id FROM management where management.mgm_name='fortigate_demo') 
    THEN 
        IF NOT EXISTS (SELECT * FROM tenant_to_management LEFT JOIN tenant USING (tenant_id) WHERE tenant_name='tenant2_demo') THEN 
        INSERT INTO tenant_to_management (tenant_id, management_id, shared)
            SELECT
            tenant_id, 
            (select mgm_id FROM management where management.mgm_name='fortigate_demo'),
            FALSE
            FROM tenant WHERE tenant.tenant_name='tenant2_demo'; 
        END IF; 
    END IF; 
END $do$
