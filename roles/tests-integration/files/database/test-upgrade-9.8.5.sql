-- Test of the SecureChange template migration of upgrade/9.8.5.sql. The test task inserts the upgrade file at the two
-- run-upgrade markers below, which also shows that the upgrade can be repeated.
-- An isolated schema and temporary tables protect production data; the transaction is rolled back.
-- This probes: the system most managements route to becomes global, templates equal to the shipped default are not
-- stored, only the first template of a task type and only the parts of the task type count, managements routing a change
-- category to another SecureChange system get its differing parts (an empty value where the global level overrides
-- the shipped default), the former single-template format, unknown managements and other target values are ignored,
-- other ticket system types are untouched, the backup leaves out authorization values, and a repeated upgrade keeps
-- an administrator change made in between.
\set ON_ERROR_STOP 1
BEGIN;
CREATE SCHEMA sc_templates_upgrade_9_8_5_test;
SET LOCAL search_path = sc_templates_upgrade_9_8_5_test, pg_temp;
CREATE TEMP TABLE config (config_key varchar, config_value varchar, config_user integer,
    PRIMARY KEY (config_key, config_user));
INSERT INTO config VALUES
    ('extTicketSystems', '[{"Id":1,"TypeId":2,"Name":"SC main","Url":"https://sc1","Templates":[{"TaskType":"AccessRequest","TicketTemplate":"T1","TasksTemplate":"{\"@xsi.type\":\"multi_access_request\",\"name\":\"GewünschterZugang\",\"read_only\":false,\"access_request\":{\"order\":\"AR1\",\"verifier_result\":{\"status\":\"notrun\"},\"use_topology\":true,\"targets\":{\"target\":{\"@type\":\"ANY\"}},\"users\":{\"user\":@@USERS@@},\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"action\":\"@@ACTION@@\",\"labels\":\"\"}},{\"@xsi.type\":\"text_area\",\"name\":\"Grund für den Antrag\",\"read_only\":false,\"text\":\"@@REASON@@\"},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Regel Log aktivieren?\",\"selection\":\"@@LOGGING@@\"},{\"@xsi.type\":\"date\",\"name\":\"Regel befristen bis:\"},{\"@xsi.type\":\"text_field\",\"name\":\"Anwendungs-ID\",\"text\":\"@@APPID@@\"},{\"@xsi.type\":\"checkbox\",\"name\":\"Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt\",\"value\":@@COM_DOCUMENTED@@},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)\",\"selection\":\"Nein\"}","IpTemplate":""},{"TaskType":"NetworkObjectModify","TicketTemplate":"NT1","ObjectTemplate":"O1","ServiceTemplate":"unused part"},{"TaskType":"AccessRequest","TicketTemplate":"ignored second template"}]},{"Id":2,"TypeId":2,"Name":"SC other","Url":"https://sc2","Authorization":"encrypted","Templates":[{"TaskType":"AccessRequest","TicketTemplate":"T2","TasksTemplate":"{\"@xsi.type\":\"multi_access_request\",\"name\":\"GewünschterZugang\",\"read_only\":false,\"access_request\":{\"order\":\"AR1\",\"verifier_result\":{\"status\":\"notrun\"},\"use_topology\":true,\"targets\":{\"target\":{\"@type\":\"ANY\"}},\"users\":{\"user\":@@USERS@@},\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"action\":\"@@ACTION@@\",\"labels\":\"\"}},{\"@xsi.type\":\"text_area\",\"name\":\"Grund für den Antrag\",\"read_only\":false,\"text\":\"@@REASON@@\"},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Regel Log aktivieren?\",\"selection\":\"@@LOGGING@@\"},{\"@xsi.type\":\"date\",\"name\":\"Regel befristen bis:\"},{\"@xsi.type\":\"text_field\",\"name\":\"Anwendungs-ID\",\"text\":\"@@APPID@@\"},{\"@xsi.type\":\"checkbox\",\"name\":\"Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt\",\"value\":@@COM_DOCUMENTED@@},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)\",\"selection\":\"Nein\"}"},{"TaskType":"NetworkObjectModify","TicketTemplate":"NT1","ObjectTemplate":"O1"}]},{"Id":3,"TypeId":9,"Name":"Check Point","Templates":[{"TaskType":"group_create","TicketTemplate":"CP"}]},{"Id":4,"TypeId":2,"Name":"SC legacy","TicketTemplate":"L1","TasksTemplate":""}]', 0),
    ('fwConfigChangeMgmSettings', '[{"Id":100,"Name":"mgm100","Enabled":true,"SelectedChanges":{"Rule Changes":"1","Object Changes":"1"}},{"Id":101,"Name":"mgm101","Enabled":false,"SelectedChanges":{"Rule Changes":"1","Object Changes":"Disabled"}},{"Id":102,"Name":"mgm102","Enabled":true,"SelectedChanges":{"Rule Changes":"2","Object Changes":"1"}},{"Id":103,"Name":"mgm103","Enabled":true,"SelectedChanges":{"Rule Changes":"InternalWork","Object Changes":"4"}},{"Id":999,"Name":"removed","Enabled":true,"SelectedChanges":{"Rule Changes":"2"}}]', 0);
CREATE TEMP TABLE original_systems AS SELECT config_value::jsonb AS systems FROM config WHERE config_key = 'extTicketSystems';
CREATE TEMP TABLE device_specific_config_backup (version varchar PRIMARY KEY, config_values jsonb NOT NULL);
CREATE TEMP TABLE device_specific_config_node (id bigint GENERATED BY DEFAULT AS IDENTITY (START WITH 1000) PRIMARY KEY,
    node_type varchar, object_key varchar, parent_id bigint, display_name varchar NOT NULL DEFAULT '', sort_order integer,
    UNIQUE (node_type, object_key));
CREATE TEMP TABLE device_specific_config_value (node_id bigint, config_key varchar, config_value jsonb,
    PRIMARY KEY (node_id, config_key));
-- the global node and the device type of mgm102 exist already
INSERT INTO device_specific_config_node VALUES (1, 'global', 'global', NULL, 'Global', 0), (2, 'device_type', '10', 1, 'Check Point R8x', NULL);
INSERT INTO device_specific_config_value VALUES (1, 'importerHostName', '"importer-a"');
CREATE TEMP TABLE stm_dev_typ (dev_typ_id integer PRIMARY KEY, dev_typ_name varchar, dev_typ_version varchar);
INSERT INTO stm_dev_typ VALUES (10, 'Check Point', 'R8x'), (11, 'FortiGate', '7');
CREATE TEMP TABLE management (mgm_id integer PRIMARY KEY, mgm_name varchar, dev_typ_id integer);
INSERT INTO management VALUES (100, 'mgm100', 10), (101, 'mgm101', 10), (102, 'mgm102', 10), (103, 'mgm103', 11);

-- @run-upgrade-9.8.5

CREATE TEMP TABLE expected_values (node_type varchar, object_key varchar, config_key varchar, config_value jsonb);
INSERT INTO expected_values VALUES
    ('global', 'global', 'importerHostName', '"importer-a"'),
    ('global', 'global', 'scTemplateAccessRequestTicket', '"T1"'),
    ('global', 'global', 'scTemplateNetworkObjectModifyTicket', '"NT1"'),
    ('global', 'global', 'scTemplateNetworkObjectModifyObject', '"O1"'),
    ('management', '102', 'scTemplateAccessRequestTicket', '"T2"'),
    ('management', '103', 'scTemplateNetworkObjectModifyTicket', '""'),
    ('management', '103', 'scTemplateNetworkObjectModifyObject', '""');
CREATE FUNCTION pg_temp.assert_migration() RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS ((SELECT n.node_type, n.object_key, v.config_key, v.config_value FROM device_specific_config_value v
            JOIN device_specific_config_node n ON n.id = v.node_id
        EXCEPT SELECT node_type, object_key, config_key, config_value FROM expected_values)
        UNION ALL (SELECT node_type, object_key, config_key, config_value FROM expected_values
        EXCEPT SELECT n.node_type, n.object_key, v.config_key, v.config_value FROM device_specific_config_value v
            JOIN device_specific_config_node n ON n.id = v.node_id)) THEN
        RAISE EXCEPTION 'Migrated SecureChange templates differ from the expected values';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM device_specific_config_node d JOIN device_specific_config_node m ON m.parent_id = d.id
        WHERE d.node_type = 'device_type' AND d.object_key = '11' AND d.parent_id = 1 AND d.display_name = 'FortiGate 7'
            AND m.node_type = 'management' AND m.object_key = '103' AND m.display_name = 'mgm103')
        OR NOT EXISTS (SELECT 1 FROM device_specific_config_node WHERE node_type = 'management' AND object_key = '102' AND parent_id = 2)
        OR (SELECT count(*) FROM device_specific_config_node) <> 5 THEN
        RAISE EXCEPTION 'Only the managements with differing templates and their device types must get a node';
    END IF;
    IF (SELECT config_value::jsonb FROM config WHERE config_key = 'extTicketSystems') <> '[{"Id":1,"TypeId":2,"Name":"SC main","Url":"https://sc1","Templates":[]},{"Id":2,"TypeId":2,"Name":"SC other","Url":"https://sc2","Authorization":"encrypted","Templates":[]},{"Id":3,"TypeId":9,"Name":"Check Point","Templates":[{"TaskType":"group_create","TicketTemplate":"CP"}]},{"Id":4,"TypeId":2,"Name":"SC legacy","Templates":[]}]'::jsonb THEN
        RAISE EXCEPTION 'SecureChange systems must lose their templates, other ticket systems must stay unchanged';
    END IF;
    IF (SELECT config_values FROM device_specific_config_backup WHERE version = '9.8.5 extTicketSystems')
        <> (SELECT jsonb_agg(s - 'Authorization' ORDER BY ord) FROM original_systems, jsonb_array_elements(systems) WITH ORDINALITY e(s, ord)) THEN
        RAISE EXCEPTION 'The original ticket systems must be backed up without authorization values';
    END IF;
END $$;
SELECT pg_temp.assert_migration();

-- after the upgrade an administrator changes the global access request ticket template; a repeated upgrade must keep it
UPDATE device_specific_config_value SET config_value = '"admin"' WHERE node_id = 1 AND config_key = 'scTemplateAccessRequestTicket';
UPDATE expected_values SET config_value = '"admin"' WHERE object_key = 'global' AND config_key = 'scTemplateAccessRequestTicket';

-- @run-upgrade-9.8.5
SELECT pg_temp.assert_migration();
ROLLBACK;
\echo '9.8.5 SecureChange template migration and idempotency checks passed'
