#!/bin/bash

curl -k -H 'Accept: application/json; indent=4' https://127.0.0.1:443/api/managements/
curl -k -H 'Accept: application/json; indent=4' https://127.0.0.1:443/api/devices/

curl -k 'https://localhost/api/graphql/' -H "Content-Type:application/json" -d '{ "query": "query { devices { devName devId } }" }'
{"data":{"devices":[{"devName":"fortigate-test","devId":"1"},{"devName":"checkpoint-demo","devId":"2"}]}}


# TODO: Zugriff auf API nur mit Password erlauben
# TODO: Test Schreiben von Daten (Management, Devices, ...)
# TODO: CSRF beheben, indem Verweis auf 
# TODO: Überlegen, welche API-Zugriffe erlaubt werden sollen (Reports)


# GraphQl:
# https://graphql.org/
# download graphiql app for linux: https://github.com/skevy/graphiql-app/releases/download/v0.7.2/graphiql-app-0.7.2-x86_64.AppImage
# https://docs.graphene-python.org/projects/django/en/latest/
# https://medium.com/@alhajee2009/graphql-with-django-a-tutorial-that-works-2812be163a26

# Hasura-graphql-engine - Framework to provide graphql for Postgresql databases
# Hasura: https://github.com/hasura/graphql-engine/blob/master/architecture/live-queries.md
# access gui: http://localhost:8080/console/api-explorer

query showDevice {
  devices {
    dev_id: devId
    devName
    devRulebase
    management_info: mgm {
      mgm_id:mgmId
      mgmName
      clearingImportRan
      configPath
      importerHostname
      sshHostname
      sshPort
    }
  }
}

{
  "query": "{\n  devices {\n    devId\n    devName\n    devRulebase\n    doNotImport\n  }\n  managements {\n    mgmId\n    mgmName\n    sshHostname\n  }\n}\n",
}

----------------------

Hasura:
todo:
- decide if dependency from postgres when using hasura is ok
- build usage report
- get config at a certain date not just the active config
- define some queries & mutations
- expose (track) only necessary tables - and use ansible to do so
- define role based access model:
  - full admin (able to change tables management, device, stm_...)
  - fw admin (able to document changes)
  - reporter (able to request reports)
- add authentiation & authorization
- let someone build a client
- define query that resolves all groups
- make run natively without docker
- test without any graphql, django or restframework
- test performance with fits import
- find out how to handle tenants (users able to see only certain managements & devices)
- decide whether to drop client views (filter based on ip addresses) within a rulebase

cat docker-run.sh
#! /bin/bash
docker run -d --net=host -p 8080:8080 \
       -e HASURA_GRAPHQL_DATABASE_URL=postgres://dbadmin:st8chel@localhost:5432/isodb \
       -e HASURA_GRAPHQL_ENABLE_CONSOLE=true \
       hasura/graphql-engine:v1.0.0

query listRules {
  rule(where: {active: {_eq: true}, dev_id: {_eq: 1}, rule_disabled: {_eq: false}}, order_by: {rule_num: asc}) {
    rule_num
    rule_src
    rule_dst
    rule_svc
    rule_action
    rule_track
  }
}

query listRulesAllDevices {
  device {
    dev_name
    rules(where: {active: {_eq: true}, rule_disabled: {_eq: false}}, order_by: {rule_num: asc}) {
      rule_num
      rule_id
      rule_uid
      rule_src
      rule_dst
      rule_svc
      rule_action
      rule_track
    }
  }
}

query listRulesOfAllDevicesResolved {
  device {
    dev_id
    dev_name
    stm_dev_typ {
      dev_typ_name
      dev_typ_version
    }
    management {
      mgm_id
      mgm_name
    }
    rules(where: {active: {_eq: true}, rule_disabled: {_eq: false}}, order_by: {rule_num: asc}) {
      rule_num
      rule_id
      rule_uid
      rule_froms {
        object {
          obj_name
        }
      }
      rule_tos {
        object {
          obj_name
        }
      }
      rule_services {
        service {
          svc_name
          svc_id
        }
      }
      rule_action
      rule_track
    }
  }
}

mutation updateRuleRuleComment($rule_id: Int!, $new_comment: String!) {
  __typename
  update_rule(where: {rule_id: {_eq: $rule_id}}, _set: {rule_comment: $new_comment}) {
     affected_rows
    returning {
      rule_id
      rule_comment_post: rule_comment
    }
  }
}


Query variables:
{
  "rule_id":156,
  "new_comment": "ganz neuer Kommentar"
}

# from db-import-ids.php:
	function setImportIdMgmList() {
		$mgm_filter = $this->filter->getMgmFilter();
		$report_timestamp = $this->filter->getReportTime();
		$sqlcmd = "SELECT max(control_id) AS import_id, import_control.mgm_id AS mgm_id FROM import_control" .
				" INNER JOIN management USING (mgm_id)" .
				" INNER JOIN device ON (management.mgm_id=device.mgm_id)" .
				" WHERE start_time<='$report_timestamp' AND NOT stop_time IS NULL AND $mgm_filter AND successful_import" .
				" AND NOT device.hide_in_gui AND NOT management.hide_in_gui " . 
				" GROUP BY import_control.mgm_id";
		$import_id_table = $this->db_connection->iso_db_query($sqlcmd);
		if (!$this->error->isError($import_id_table)) {
			$this->import_id_mgm_id_table = $import_id_table;
		} else {
			$err = $import_id_table;
			$this->error->raiseError("E-NWL1: Could not query DB: $sqlcmd" . $err->getMessage());
		}
		return;
	}


# from db-rule.php:
	function getRules($report_timestamp, $report_id) {
		if (empty ($this->rule_list)) {
			if ($this->error->isError($this->rule_ids)) {
				$this->error->raiseError("E-RL1: Rule Ids not loaded properly. ".$this->rule_ids->getMessage());
			}
			$import_id_mgm_id_str = $this->import_ids->getImportIdMgmStringList();
			$sqlcmd = "SELECT rule_order.dev_id, rule_order.rule_number, rule.*, " . 
					" from_zone.zone_name AS rule_from_zone_name, to_zone.zone_name AS rule_to_zone_name" .
					" FROM rule_order" .
					" INNER JOIN device ON (rule_order.dev_id=device.dev_id) " .
					" INNER JOIN management ON (device.mgm_id=management.mgm_id) " .
					" INNER JOIN rule USING (rule_id)" .
					" LEFT JOIN zone as from_zone ON rule.rule_from_zone=from_zone.zone_id" .  // does not work with INNER JOIN
					" LEFT JOIN zone as to_zone ON rule.rule_to_zone=to_zone.zone_id" . // does not work with INNER JOIN
					" INNER JOIN import_control ON (rule_order.control_id=import_control.control_id)" .
					" INNER JOIN stm_track ON (stm_track.track_id=rule.track_id)" .
					" INNER JOIN stm_action ON (stm_action.action_id=rule.action_id)" .
					" INNER JOIN temp_filtered_rule_ids ON (rule.rule_id=temp_filtered_rule_ids.rule_id)" .
					" WHERE temp_filtered_rule_ids.report_id=$report_id AND successful_import AND (rule_order.control_id, management.mgm_id) IN $import_id_mgm_id_str" .
					" ORDER BY rule_order.dev_id,rule_from_zone_name,rule_to_zone_name,rule_order.rule_number";


