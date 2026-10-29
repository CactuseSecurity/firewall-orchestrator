-- The Tufin SecureChange ticket templates move from the ticket systems (config extTicketSystems) to the device specific
-- settings scTemplate<TaskType><Part> (Global, device type, management). The templates of the SecureChange system that most
-- managements route their changes to (Settings - Firewall config changes, fwConfigChangeMgmSettings) become the global
-- values; a management routing a change category to another SecureChange system gets the differing templates of that
-- system's task types on its own level, where an empty value stands for the shipped default. Template parts equal to
-- the shipped default are not stored, so a later correction of the default applies. A system without templates in the
-- list format falls back to its former single TicketTemplate/TasksTemplate as AccessRequest template.
-- The ticket systems are backed up once (without authorization values), and the templates are removed from the
-- SecureChange systems, so a repeated upgrade changes nothing. Other ticket system types keep their templates.
DO $$
DECLARE
    t_systems text;
    t_mgm_settings text;
    j_systems jsonb;
    j_mgm_settings jsonb;
    -- shipped default up to 9.8.4, now SCTemplateDefaults in FWO.ExternalSystems
    j_shipped jsonb := ('[{"Id":1,"TypeId":2,"Name":"Tufin SecureChange","Url":"","Templates":[{"TaskType":"AccessRequest","TicketTemplate":"{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"domain_name\":\"\",\"workflow\":{\"name\":\"@@WORKFLOW_NAME@@\"},\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[@@TASKS@@]}}}}]}}}","TasksTemplate":"{\"@xsi.type\":\"multi_access_request\",\"name\":\"GewünschterZugang\",\"read_only\":false,\"access_request\":{\"order\":\"AR1\",\"verifier_result\":{\"status\":\"notrun\"},\"use_topology\":true,\"targets\":{\"target\":{\"@type\":\"ANY\"}},\"users\":{\"user\":@@USERS@@},\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"action\":\"@@ACTION@@\",\"labels\":\"\"}},{\"@xsi.type\":\"text_area\",\"name\":\"Grund für den Antrag\",\"read_only\":false,\"text\":\"@@REASON@@\"},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Regel Log aktivieren?\",\"selection\":\"@@LOGGING@@\"},{\"@xsi.type\":\"date\",\"name\":\"Regel befristen bis:\"},{\"@xsi.type\":\"text_field\",\"name\":\"Anwendungs-ID\",\"text\":\"@@APPID@@\"},{\"@xsi.type\":\"checkbox\",\"name\":\"Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt\",\"value\":@@COM_DOCUMENTED@@},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)\",\"selection\":\"Nein\"}"}]}]'::jsonb) -> 0 -> 'Templates' -> 0;
    i_global_system integer;
    i_global_node bigint;
    i_device_type_node bigint;
    i_management_node bigint;
    r_override RECORD;
BEGIN
    SELECT config_value INTO t_systems FROM config WHERE config_key = 'extTicketSystems' AND config_user = 0;
    IF btrim(COALESCE(t_systems, '')) = '' THEN
        RETURN;
    END IF;
    BEGIN
        j_systems := t_systems::jsonb;
    EXCEPTION WHEN invalid_text_representation THEN
        RAISE NOTICE 'extTicketSystems is no valid JSON, SecureChange templates are not migrated';
        RETURN;
    END;
    IF jsonb_typeof(j_systems) <> 'array' OR NOT EXISTS (SELECT 1 FROM jsonb_array_elements(j_systems) s
        WHERE s->>'TypeId' = '2'
            AND ((jsonb_typeof(s->'Templates') = 'array' AND jsonb_array_length(s->'Templates') > 0)
                OR COALESCE(s->>'TicketTemplate', '') <> '' OR COALESCE(s->>'TasksTemplate', '') <> '')) THEN
        RETURN;
    END IF;

    SELECT config_value INTO t_mgm_settings FROM config WHERE config_key = 'fwConfigChangeMgmSettings' AND config_user = 0;
    BEGIN
        j_mgm_settings := COALESCE(NULLIF(btrim(COALESCE(t_mgm_settings, '')), ''), '[]')::jsonb;
    EXCEPTION WHEN invalid_text_representation THEN
        j_mgm_settings := '[]'::jsonb;
    END;
    IF jsonb_typeof(j_mgm_settings) <> 'array' THEN
        j_mgm_settings := '[]'::jsonb;
    END IF;

    INSERT INTO device_specific_config_backup (version, config_values)
    SELECT '9.8.5 extTicketSystems', jsonb_agg(s - 'Authorization' ORDER BY ord)
    FROM jsonb_array_elements(j_systems) WITH ORDINALITY e(s, ord)
    ON CONFLICT DO NOTHING;

    DROP TABLE IF EXISTS tmp_sc_template_part;
    DROP TABLE IF EXISTS tmp_sc_template_value;
    DROP TABLE IF EXISTS tmp_sc_route;

    -- template parts per task type, as offered by the settings
    CREATE TEMP TABLE tmp_sc_template_part (task_type text, part text, property text, category text);
    INSERT INTO tmp_sc_template_part VALUES
        ('AccessRequest', 'Ticket', 'TicketTemplate', 'Rule Changes'),
        ('AccessRequest', 'Tasks', 'TasksTemplate', 'Rule Changes'),
        ('AccessRequest', 'Ip', 'IpTemplate', 'Rule Changes'),
        ('AccessRequest', 'NwObjGroup', 'NwObjGroupTemplate', 'Rule Changes'),
        ('AccessRequest', 'Service', 'ServiceTemplate', 'Rule Changes'),
        ('AccessRequest', 'Icmp', 'IcmpTemplate', 'Rule Changes'),
        ('AccessRequest', 'IpProtocol', 'IpProtocolTemplate', 'Rule Changes'),
        ('NetworkObjectModify', 'Ticket', 'TicketTemplate', 'Object Changes'),
        ('NetworkObjectModify', 'Tasks', 'TasksTemplate', 'Object Changes'),
        ('NetworkObjectModify', 'Object', 'ObjectTemplate', 'Object Changes'),
        ('NetworkObjectModify', 'ObjectShort', 'ObjectTemplateShort', 'Object Changes'),
        ('NetworkObjectModify', 'Ip', 'IpTemplate', 'Object Changes'),
        ('NetworkObjectModify', 'NwObjGroup', 'NwObjGroupTemplate', 'Object Changes'),
        ('NetworkServiceCreate', 'Ticket', 'TicketTemplate', 'Object Changes'),
        ('NetworkServiceCreate', 'Tasks', 'TasksTemplate', 'Object Changes'),
        ('NetworkServiceCreate', 'Service', 'ServiceTemplate', 'Object Changes'),
        ('NetworkServiceCreate', 'Icmp', 'IcmpTemplate', 'Object Changes'),
        ('NetworkServiceCreate', 'IpProtocol', 'IpProtocolTemplate', 'Object Changes'),
        ('NetworkServiceUpdate', 'Ticket', 'TicketTemplate', 'Object Changes'),
        ('NetworkServiceUpdate', 'Tasks', 'TasksTemplate', 'Object Changes'),
        ('NetworkServiceUpdate', 'Service', 'ServiceTemplate', 'Object Changes'),
        ('NetworkServiceUpdate', 'Icmp', 'IcmpTemplate', 'Object Changes'),
        ('NetworkServiceUpdate', 'IpProtocol', 'IpProtocolTemplate', 'Object Changes');

    -- non-empty template parts of every SecureChange system that differ from the shipped default; the first template
    -- of a task type counts, as when tickets were created
    CREATE TEMP TABLE tmp_sc_template_value AS
    SELECT DISTINCT ON (system_id, config_key) system_id, config_key, value
    FROM (
        SELECT (s->>'Id')::integer AS system_id, 'scTemplate' || p.task_type || p.part AS config_key,
            t->>p.property AS value, t_ord
        FROM jsonb_array_elements(j_systems) s
        CROSS JOIN LATERAL jsonb_array_elements(CASE
            WHEN jsonb_typeof(s->'Templates') = 'array' AND jsonb_array_length(s->'Templates') > 0 THEN s->'Templates'
            ELSE jsonb_build_array(jsonb_build_object('TaskType', 'AccessRequest',
                'TicketTemplate', COALESCE(s->>'TicketTemplate', ''), 'TasksTemplate', COALESCE(s->>'TasksTemplate', '')))
            END) WITH ORDINALITY tpl(t, t_ord)
        JOIN tmp_sc_template_part p ON p.task_type = t->>'TaskType'
        WHERE s->>'TypeId' = '2' AND (s->>'Id') ~ '^[0-9]{1,9}$'
    ) parts
    WHERE COALESCE(value, '') <> ''
    ORDER BY system_id, config_key, t_ord;
    DELETE FROM tmp_sc_template_value
    WHERE (config_key = 'scTemplateAccessRequestTicket' AND value = j_shipped->>'TicketTemplate')
        OR (config_key = 'scTemplateAccessRequestTasks' AND value = j_shipped->>'TasksTemplate');

    -- the SecureChange system each existing management routes a change category to
    CREATE TEMP TABLE tmp_sc_route AS
    SELECT DISTINCT m.mgm_id, m.mgm_name, m.dev_typ_id, c.key AS category, c.value::integer AS system_id
    FROM jsonb_array_elements(j_mgm_settings) ms
    CROSS JOIN LATERAL jsonb_each_text(CASE WHEN jsonb_typeof(ms->'SelectedChanges') = 'object'
        THEN ms->'SelectedChanges' ELSE '{}'::jsonb END) c
    JOIN management m ON m.mgm_id = CASE WHEN (ms->>'Id') ~ '^[0-9]{1,9}$' THEN (ms->>'Id')::integer END
    WHERE c.value ~ '^[0-9]{1,9}$'
        AND EXISTS (SELECT 1 FROM jsonb_array_elements(j_systems) s WHERE s->>'TypeId' = '2' AND s->>'Id' = c.value);

    SELECT system_id INTO i_global_system FROM (
        SELECT (s->>'Id')::integer AS system_id, (SELECT count(DISTINCT r.mgm_id) FROM tmp_sc_route r
            WHERE r.system_id = (s->>'Id')::integer) AS usage
        FROM jsonb_array_elements(j_systems) s
        WHERE s->>'TypeId' = '2' AND (s->>'Id') ~ '^[0-9]{1,9}$') candidates
    ORDER BY usage DESC, system_id
    LIMIT 1;

    INSERT INTO device_specific_config_node (node_type, object_key, display_name, sort_order)
    VALUES ('global', 'global', 'Global', 0)
    ON CONFLICT (node_type, object_key) DO NOTHING;
    SELECT id INTO i_global_node FROM device_specific_config_node WHERE node_type = 'global';

    INSERT INTO device_specific_config_value (node_id, config_key, config_value)
    SELECT i_global_node, config_key, to_jsonb(value) FROM tmp_sc_template_value WHERE system_id = i_global_system
    ON CONFLICT (node_id, config_key) DO NOTHING;

    -- managements routing to another system get the template parts of its task types that differ from the global
    -- ones; an empty value restores the shipped default where the global level overrides it
    FOR r_override IN
        SELECT r.mgm_id, r.mgm_name, r.dev_typ_id, concat_ws(' ', t.dev_typ_name, t.dev_typ_version) AS device_type_name,
            'scTemplate' || p.task_type || p.part AS config_key, COALESCE(own.value, '') AS value
        FROM tmp_sc_route r
        JOIN stm_dev_typ t ON t.dev_typ_id = r.dev_typ_id
        JOIN tmp_sc_template_part p ON p.category = r.category
        LEFT JOIN tmp_sc_template_value own ON own.system_id = r.system_id AND own.config_key = 'scTemplate' || p.task_type || p.part
        LEFT JOIN tmp_sc_template_value glob ON glob.system_id = i_global_system AND glob.config_key = 'scTemplate' || p.task_type || p.part
        WHERE r.system_id <> i_global_system AND COALESCE(own.value, '') <> COALESCE(glob.value, '')
        ORDER BY r.mgm_id, config_key
    LOOP
        INSERT INTO device_specific_config_node (node_type, object_key, parent_id, display_name)
        VALUES ('device_type', r_override.dev_typ_id::text, i_global_node, r_override.device_type_name)
        ON CONFLICT (node_type, object_key) DO NOTHING;
        SELECT id INTO i_device_type_node FROM device_specific_config_node
        WHERE node_type = 'device_type' AND object_key = r_override.dev_typ_id::text;

        INSERT INTO device_specific_config_node (node_type, object_key, parent_id, display_name)
        VALUES ('management', r_override.mgm_id::text, i_device_type_node, r_override.mgm_name)
        ON CONFLICT (node_type, object_key) DO NOTHING;
        SELECT id INTO i_management_node FROM device_specific_config_node
        WHERE node_type = 'management' AND object_key = r_override.mgm_id::text;

        INSERT INTO device_specific_config_value (node_id, config_key, config_value)
        VALUES (i_management_node, r_override.config_key, to_jsonb(r_override.value))
        ON CONFLICT (node_id, config_key) DO NOTHING;
    END LOOP;

    UPDATE config SET config_value = (
        SELECT jsonb_agg(CASE WHEN s->>'TypeId' = '2'
                THEN (s - 'TicketTemplate' - 'TasksTemplate') || jsonb_build_object('Templates', '[]'::jsonb)
                ELSE s END ORDER BY ord)::text
        FROM jsonb_array_elements(j_systems) WITH ORDINALITY e(s, ord))
    WHERE config_key = 'extTicketSystems' AND config_user = 0;

    DROP TABLE tmp_sc_template_part;
    DROP TABLE tmp_sc_template_value;
    DROP TABLE tmp_sc_route;
END $$;
