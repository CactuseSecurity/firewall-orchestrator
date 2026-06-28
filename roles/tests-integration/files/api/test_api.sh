#!/bin/bash

curl -k -H 'Accept: application/json; indent=4' https://localhost:443/api/managements/
curl -k -H 'Accept: application/json; indent=4' https://localhost:443/api/devices/

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
       -e HASURA_GRAPHQL_DATABASE_URL=postgres://dbadmin:st8chel@localhost:5432/fworchdb \
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
		$import_id_table = $this->db_connection->fworch_db_query($sqlcmd);
		if (!$this->error->isError($import_id_table)) {
			$this->import_id_mgm_id_table = $import_id_table;
		} else {
			$err = $import_id_table;
			$this->error->raiseError("E-NWL1: Could not query DB: $sqlcmd" . $err->getMessage());
		}
		return;
	}
