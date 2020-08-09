
INSERT INTO  hdb_catalog.hdb_query_collection (collection_name, collection_defn, comment, is_system_defined) 
VALUES ('filterQueries', '{"queries": [
    {
        "name": "filterDevices",
        "query": "query filter_dyn($manufacturer_id: [Int!], $management_id: [Int!], $device_id: [Int!]) {
            stm_dev_typ(where: {dev_typ_id: {_in: $manufacturer_id}}) {
                dev_typ_name
                dev_typ_version
                dev_typ_id
                management(where: {mgm_id: {_in: $management_id}}) {
                    mgm_id
                    mgm_name
                    devices(where: {dev_id: {_in: $device_id}}) {
                        dev_id
                        dev_name
                    }
                }
            }
        }"
    },
    {
        "name": "filterDeviceTypes",
        "query": "query filterDeviceTypes($manufacturer_id: [Int!], $management_id: [Int!], $device_id: [Int!]) {
            stm_dev_typ(where: {_and: {dev_typ_id: {_in: $manufacturer_id}, devices: {dev_id: {_in: $device_id}}, management: {mgm_id:{_in: $management_id}}}}) {
                dev_typ_id
                dev_typ_name
            }
        }"
    }
]}', 'filter queries', FALSE);
