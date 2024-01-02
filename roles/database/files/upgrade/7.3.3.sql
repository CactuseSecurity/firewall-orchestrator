-- contains all managements visible to a tenant

Create table if not exists tenant_to_management 
  (
    tenant_id Integer NOT NULL, 
    management_id Integer NOT NULL, 
    shared BOOLEAN NOT NULL DEFAULT TRUE, 
    primary key ("tenant_id", "management_id")
  );


-- Alter table tenant_to_management
-- drop column if exists shared;


-- Alter table tenant_to_device
-- drop column if exists shared;


Alter table tenant_to_management add column if not exists shared BOOLEAN NOT NULL DEFAULT TRUE;

Alter table tenant_to_device add column if not exists shared BOOLEAN NOT NULL DEFAULT TRUE;

Alter table management DROP column if exists unfiltered_tenant_id;


Alter table device
DROP column if exists unfiltered_tenant_id;

DO $$
BEGIN
  IF NOT EXISTS(select constraint_name
    from information_schema.referential_constraints
    where constraint_name = 'tenant_to_management_management_id_fkey')
  THEN
      Alter table "tenant_to_management" add foreign key ("management_id") references "management" ("mgm_id") on update restrict on delete cascade;
  END IF;

  IF NOT EXISTS(select constraint_name
    from information_schema.referential_constraints
    where constraint_name = 'tenant_to_management_tenant_id_fkey')
  THEN
      Alter table "tenant_to_management" add foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
  END IF;
END $$;

/*

    documentation of RBAC for tenant filtering

    - tenant to device mapping is stored in tenant_to_device and tenant_to_management tables
    - we need to make sure that the mapping is complete (e.g. no devices are visible if the management is not visible)
        - this also means we need a mechanism to set new gateways to fully visible if the management is fully visible!
          this is done in the settings after selecting the exact three-way visibility
        - new gateways start with "not shared" if the management's visibility is "not shared" (only when added via UI)
        - new gateways start as "invisible" if the management's visibility is "shared"
        - new managements start with no visibility for a tenant
        - invisible means not visible for a tenant user (e.g. reporter) but needs to be visible for the admin in the tenant settings!

        alternatively it would be possible to just set management as fully visible to result in all (future) gateways of the management to be fully visible as well
        but then the API filtering would become much more complex
    - use the same mechanisms for tenant simulation as reporter_view_all and admin as for restricted reporter:
      - not all filters can be applied in API (especially not for object vie in RSB) due to performance issues
      - this works as long as reports are generated and stored in the archive and the reporter has no direct accesss to the API
    - API access is restricted via tenant_filter as follows:
        - device table:
            {"_and":[{"mgm_id":{"_in":"x-hasura-visible-managements"}},{"dev_id":{"_in":"x-hasura-visible-devices"}}]}
        - management table:
            {"mgm_id":{"_in":"x-hasura-visible-managements"}}
        - rule table:
            {"_and":[{"mgm_id":{"_in":"x-hasura-visible-managements"}},{"dev_id":{"_in":"x-hasura-visible-devices"}},{"rule_relevant_for_tenant":{"_eq":"true"}}]}
        - rule_to table:
            {"_and":[{"rule":{"mgm_id":{"_in":"x-hasura-visible-managements"}}},{"rule":{"dev_id":{"_in":"x-hasura-visible-devices"}}},{"rule_to_relevant_for_tenant":{"_eq":"true"}}]}
        - rule_from table:
            {"_and":[{"rule":{"mgm_id":{"_in":"x-hasura-visible-managements"}}},{"rule":{"dev_id":{"_in":"x-hasura-visible-devices"}}},{"rule_from_relevant_for_tenant":{"_eq":"true"}}]}
        - object: (no restrictions on objgrp, ...)
            {"mgm_id":{"_in":"x-hasura-visible-managements"}}

    - rules and rule_from/to are fetched using the computed fields defined by functions
        - rule_relevant_for_tenant
        - get_rule_froms_for_tenant
        - get_rule_tos_for_tenant

    - Question: do we actually need to include the computed fields get_rule_froms_for_tenant, ... in the queries or can all of this be steered by API permissions and we just use the normal fields (rules, rule_tos, rule_froms)?
        Anser: for the simulation of tenants (by admin/reporter-viewall role) we need these functions as we do not have API restrictions
        - the function get_rules_for_tenant is needed to be able to simulate getting rules for a specific tenant

    - we are introducing a new quality of visibility (visible, shared visible, fully visible (not shared)) for gateways and managements
        - these visibilities are inherited from management to gateway: when a management is fully visible then all the gateways are also fully visible

    - we do not add more information to the JWT, just whether the device is visible or not:
        x-hasura-visible-devices: { 1,4 }     --> shared and not shared gateways
        x-hasura-visible-managements: { 3,6 } --> shared and not shared managements

        NOT implemented:
            x-hasura-fully-visible-devices: { 1 }
            x-hasura-fully-visible-devices: { 6 }

                then depending on the grade of visibility we either return a rule(base) unfiltered or filtered
                        {"_and":["_or":[{"mgm_id":{"_in":"x-hasura-visible-managements"}},{"dev_id":{"_in":"x-hasura-visible-devices"}}]}

    - C# data structure changes:
      - consolidate TenantGateway and TenantViewGateway
      - consolidate TenantManagment and TenantViewManagement

    - API permissions
      - mw role needs access to shared columns in tenant_to_xxx tables
      - mw role needs access to all columns in tenant_to_management table

    - issues:
        + Get tenant data - Tenants could not be fetched from LDAP --> fixed
        + managements and gateways in tenant settings vanish when set to invisible --> fixed
        + when deleting a tenant, the "edit tenant" pop-up does not close --> fixed
        + running (with x-hasura-tenant-id set to 1)
            query rulesReport {
            management(where: {hide_in_gui: {_eq: false}, stm_dev_typ: {dev_typ_is_multi_mgmt: {_eq: false}, is_pure_routing_device: {_eq: false}}}, order_by: {mgm_name: asc}) {
                id: mgm_id
                name: mgm_name
                devices(where: {hide_in_gui: {_eq: false}, stm_dev_typ: {is_pure_routing_device: {_eq: false}}}, order_by: {dev_name: asc}) {
                id: dev_id
                name: dev_name
                rules: get_rules_for_tenant(args: {tenant: 2}, where: {access_rule: {_eq: true}, _and: [{_or: [{device: {dev_id: {_eq: 1}}}]}]}, order_by: {rule_num_numeric: asc}) {
                    mgm_id: mgm_id
                    rule_uid
                    dev_id
                    #rule_tos: get_rule_tos_for_tenant(args: {tenant: 2}) { object { obj_ip obj_name } }
                }
                }
            }
            }
          returns error (when commenting in get_rule_tos_for_tenant)
          {
            "errors": [
              {
                "message": "Non default arguments cannot be omitted",
                "extensions": {
                  "path": "$.selectionSet.management.selectionSet.devices.selectionSet.get_rules_for_tenant.selectionSet.get_rule_tos_for_tenant.args.args",
                  "code": "not-supported"
                }
              }
            ]
          }
        + reloading UI for a tenant user clears the displayed tenant in LSB
        + when editing tenant - device mappings, tenants are duplicated

        + report for "not shared" management is still filtered

        - read tenant - device mappings from API and display in settings
        - write tenant - device mappings to API

        - when in tenant_filtering mode (only simulated) generating report for two gateways takes 10 times longer than separate reports

        - when saving tenant_networks (2.0.0.0/8): Save tenant - Unclassified error: : Foreign key violation. insert or update on table "tenant_network" violates foreign key constraint "tenant_network_tenant_id_fkey" . See log for details!
        - when editing tenant - device mappings, collapse all default value is wrong

*/ -- TODO: add management to tenant_to_management as shared if any device is in tenant_to_device
