UPDATE config
SET config_key = 'CustomFieldOwnerKey'
WHERE config_key = 'OwnerSourceCustomFieldKey';

-- Fix inconsistent mgm_id in changelog tables
UPDATE changelog_rule AS c
SET mgm_id = r.mgm_id
FROM rule AS r
WHERE c.new_rule_id = r.rule_id
  AND c.mgm_id <> r.mgm_id;

UPDATE changelog_object AS c
SET mgm_id = o.mgm_id
FROM object AS o
WHERE c.new_obj_id = o.obj_id
  AND c.mgm_id <> o.mgm_id;

UPDATE changelog_service AS c
SET mgm_id = s.mgm_id
FROM service AS s
WHERE c.new_svc_id = s.svc_id
  AND c.mgm_id <> s.mgm_id;

UPDATE changelog_user AS c
SET mgm_id = u.mgm_id
FROM usr AS u
WHERE c.new_user_id = u.user_id
  AND c.mgm_id <> u.mgm_id;
