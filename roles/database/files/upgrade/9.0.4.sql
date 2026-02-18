-- 9.0.4
-- migrate modelling mail recipients to multi-select JSON representation
INSERT INTO config (config_key, config_value, config_user) VALUES ('modReqEmailOtherAddresses', '', 0) ON CONFLICT DO NOTHING;
INSERT INTO config (config_key, config_value, config_user) VALUES ('modDecommEmailOtherAddresses', '', 0) ON CONFLICT DO NOTHING;

UPDATE config
SET config_value = '{"none":false,"other_addresses":false,"owner_responsible_type_ids":[2]}'
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'OwnerGroupOnly';

UPDATE config
SET config_value = '{"none":false,"other_addresses":false,"owner_responsible_type_ids":[1]}'
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'OwnerMainResponsible';

UPDATE config
SET config_value = '{"none":false,"other_addresses":false,"ensure_at_least_one_notification":true,"owner_responsible_type_ids":[2,1]}'
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'FallbackToMainResponsibleIfOwnerGroupEmpty';

UPDATE config
SET config_value = '{"none":false,"other_addresses":true,"owner_responsible_type_ids":[]}'
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'OtherAddresses';

UPDATE config
SET config_value = (
    SELECT json_build_object(
        'none', false,
        'other_addresses', false,
        'owner_responsible_type_ids', COALESCE(json_agg(id ORDER BY sort_order DESC), '[]'::json)
    )::text
    FROM owner_responsible_type
    WHERE active = true
)
WHERE config_key IN ('modReqEmailReceiver', 'modDecommEmailReceiver')
  AND config_value = 'AllOwnerResponsibles';
