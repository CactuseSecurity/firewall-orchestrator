
# rework 2025

## data collected from autodiscovery (sync per management)

```json
{
  "management": {
    "uid": "xxxx",  # either name or uid can be fetched from API
    "name": "xxx", 
    "type": "cpr8x, fortimanager, fortiOS, ...", # not read from sync but can be configured in UI/DB
    "do_not_import": false,   # not read from sync but can be configured in UI/DB
    "hide_in_ui": false,      # not read from sync but can be configured in UI/DB
    "gateways": [
      {
        "uid": "xxx",
        "name": "abc",
        "type": "cp-gw|fortigate|...",
        "do_not_import": false,   # not read from sync but can be configured in UI/DB
        "hide_in_ui": false       # not read from sync but can be configured in UI/DB
      }
    ],
    "managements": [] # for sub managements, content: see above
} 
```
- auto discovery does not store any information about rulebases!
- this is the only data stored (by initial sync of a management) in the database
- this means we cannot identify any rulebase changes, pro: these changes will be ignored
- if we cannot sync a firewall, e.g. palo, we need more fields like policy name, ...


## interface for file import (json data structure, normalized config)

```json
{
  "global_rulebases": [],
  "global_network_objects": [],
  "global_network_services": [],
  "global_users": [],
  "sub-managements": [
    {
      "uid": "xxx",
      "name": "abc",
      # type is not imported because we always assume normalized format, 
      "gateways": [
        {
          "uid": "xxx",
          "name": "xxx",
          "type": "",
          "initial_rulebase_uid": "uidxy",
          "routing": [],
          "interfaces": [],
        }
      ],
      "rulebases": [
        {
          "uid": "xxx",
          "name": "xxx",
          "type": "access|nat",
          "rb_order_no": 1,
          "rules": [
            ...       
            "src_ref": "uid1234"
          ]
        }
      ],
      "rulebase_link": [
        {
          "rulebase_from_uid": "xxx", # when null, this is the initial rulebase for a gateway
          "rule_from_uid": "xxx",     # when null, this is the initial rulebase for a gateway
          "rulebase_to_uid": "xxx",
          "link_type": "layer|ordered|...",
          "gateway-uid": "xxx"
        },
        ...more links
      ]
      "network_objects": []
      "network_services": []
      "users": []
    },
    #...more managements
  ]
}
```

## check point specific information

### getting routing information from checkpoint gateways

Note: this only works when the user has admin privileges (not just read-only!)

```
curl --request POST \
  --url https://192.168.100.111/web_api/run-script \
  --header 'Content-Type: application/json' \
  --header 'x-chkp-sid:   uvXWp05mPeL-YKAvdyWvzY4YeGGt9GApCyZ2ndwQlOg' \
  --cookie Session=Login \
  --data '{
	"script-name": "show-routing-table",
	"script": "ip route show",
	"targets": [
		"sting-gw"
	]
}'
```
return a task id:
```json
{
	"tasks": [
		{
			"target": "sting-gw",
			"task-id": "345d9cfc-43b3-4a9f-b5c9-97755e075622"
		}
	]
}
```

this must be used to get the routing table (once the task is completed):

```
curl --request POST \
  --url https://192.168.100.111/web_api/show-task \
  --header 'Content-Type: application/json' \
  --header 'x-chkp-sid:   Ir3sw1JejoCx24Cs0uHfebjhWKGaPdcjwyW9Cn86RlE' \
  --cookie Session=Login \
  --data '{
	"task-id": "345d9cfc-43b3-4a9f-b5c9-97755e075622",
	"details-level": "full"
}'
```

returns

```json
{
	"tasks": [
		{
			"uid": "5570f622-47c2-41f9-86e6-dc4d0ebf3584",
			"name": "sting-gw - show-routing-table",
			"type": "CdmTaskNotification",
			"task-id": "345d9cfc-43b3-4a9f-b5c9-97755e075622",
			"task-name": "sting-gw - show-routing-table",
			"status": "succeeded",
			"progress-percentage": 100,
			"task-details": [
				{
					"uid": "698d6899-b963-4d34-aeb7-c3c9fa58f8f4",
					"statusCode": "succeeded",
					"statusDescription": "default via ...",
					"taskNotification": "5570f622-47c2-41f9-86e6-dc4d0ebf3584",
					"gatewayId": "cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
					"gatewayName": "sting-gw",
					"transactionId": 559998982,
					"responseMessage": "xxxx"
        }
			]
		}
	]
}
```
Get the full routing table by base64-decoding the responseMessage.

### interface checkpoint (file) import (json data structure, native config)

 Here, the gateways contain interface information when the command is issued with details-level=full
 Routing info is missing though.
 Just need to add the interfaces sectino for each gateway to the gateways.

```json
{
  "users": {},
  "object_tables": [
    {
      "object_type": "hosts",
      "object_chunks": []
    },
    {
      "object_type": "networks",
      "object_chunks": []
    }, 
    {
      "object_type": "groups",
      "object_chunks": []
    },
    {
      "object_type": "gateways-and-servers",   
      "object_chunks": [
        {
          "objects": [
            {
              "uid": "cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
              "name": "sting-gw",
              "type": "simple-gateway",
              "domain": {
                "uid": "41e821a0-3720-11e3-aa6e-0800200c9fde",
                "name": "SMC User",
                "domain-type": "domain"
              },
              "policy": {
                "access-policy-revision": {
                  "uid": "ab620dc5-b80c-45c6-85ee-11f4faa9f5aa",
                  "name": "xxx",
                  "type": "session",
                  "state": "published",
                  "publish-time": {
                    "posix": 1736767952039,
                    "iso-8601": "2025-01-13T12:32+0100"
                  }
                },
              },
              "operating-system": "Gaia",
              "hardware": "Open server",
              "version": "R82",
              "ipv4-address": "192.168.10.90",
              "interfaces": [
                {
                  "interface-name": "eth5",
                  "ipv4-address": "91.26.19.194",
                  "ipv4-network-mask": "255.255.255.240",
                  "ipv4-mask-length": 28,
                  "dynamic-ip": false,
                  "topology": {
                    "leads-to-internet": true
                  }
                },
                {
                  "interface-name": "eth2",
                  "ipv4-address": "213.157.1.55",
                  "ipv4-network-mask": "255.255.255.240",
                  "ipv4-mask-length": 28,
                  "dynamic-ip": false,
                  "topology": {
                    "leads-to-internet": false,
                    "ip-address-behind-this-interface": "specific",
                    "leads-to-specific-network": {
                      "uid": "a4944c2e-1801-408f-8fd9-48f873123d5d",
                      "name": "sting-gw_eth2",
                      "type": "group"
                    },
                    "leads-to-dmz": false
                  }
                },
                {
                  "interface-name": "eth3",
                  "ipv4-address": "192.168.20.1",
                  "ipv4-network-mask": "255.255.255.192",
                  "ipv4-mask-length": 26,
                  "dynamic-ip": false,
                  "topology": {
                    "leads-to-internet": false,
                    "ip-address-behind-this-interface": "network defined by the interface ip and net mask",
                    "leads-to-dmz": false
                  }
                }
              ],
              "network-security-blades": {
                "firewall": true,
                "site-to-site-vpn": true,
                "application-control": true,
                "content-awareness": true,
                "identity-awareness": true,
                "monitoring": true,
                "url-filtering": true,
                "anti-bot": true,
                "anti-virus": true,
                "ips": true
              },
            },
            {
              "uid": "d7a49524-b877-4b2a-b949-a736d86e2baa",
              "name": "sting-mgmt",
              "type": "checkpoint-host",
              "domain": {
                "uid": "41e821a0-3720-11e3-aa6e-0800200c9fde",
                "name": "SMC User",
                "domain-type": "domain"
              },
              "ipv4-address": "192.168.100.111",
              "interfaces": [
              ],
              "version": "R82",
              "os": "Gaia",
              "hardware": "Open server",
              "sic-name": "CN=cp_mgmt_sting-mgmt,O=sting.cactus-es.de..jnbkhk",
              "sic-state": "communicating",
              "management-blades": {
                "network-policy-management": true,
                "user-directory": false,
                "compliance": false,
                "logging-and-status": true,
                "smart-event-server": true,
                "smart-event-correlation": true,
                "endpoint-policy": false,
                "secondary": false,
                "identity-logging": true
              },
              "firewall": false,
              "read-only": true,
              "available-actions": {
                "edit": "false",
                "delete": "false",
                "clone": "not_supported"
              }
            }
          ],
          "from": 1,
          "to": 2,
          "total": 2
        }
      ]
    },
      ...
  ],
  "gateways": [
    {
      "dev_name": "sting-gw",
      "dev_uid": "sting-gw-uid",  # see gateways and servers
      "rulebase_links":
      [
        {
          "from_rulebase_uid": "sting-big-gw-top-level-uid",
          "to_rulebase_uid": "sting-first-ordered-layer-uid",
          "from_rule_uid": "9cd8ce05-32db-483a-a44a-0c9131cca022",
          "link_type": "ordered"
        },
        {
          "from_rulebase_uid": "inline-layer-1-uid",
          "to_rulebase_uid": "inline-layer-2-uid",
          "from_rule_uid": "52f5f517-6801-4741-84b4-13fe26843433",
          "link_type": "inline"
        },
        {
          "from_rulebase_uid": "inline-layer-1-uid",
          "to_rulebase_uid": "inline-layer-2-uid",
          "from_rule_uid": "52f5f517-6801-4741-84b4-13fe26843433",
          "link_type": "inline"
        },
        {
          "from_rulebase_uid": "global1-uid",
          "to_rulebase_uid": "local-uid",
          "from_rule_uid": "7288ebb9-d951-4139-9ac2-980cf34a60af",
          "link_type": "local"
        },
        {
          "from_rulebase_uid": null,
          "to_rulebase_uid": "sting-first-ordered-layer-uid",
          "from_rule_uid": null,
          "link_type": "inital"
        }
      ],
      "routing": [],
      "interfaces": [
        {
          "interface-name": "eth5",
          "ipv4-address": "91.26.19.194",
          "ipv4-network-mask": "255.255.255.240",
          "ipv4-mask-length": 28,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": true
          }
        },
        {
          "interface-name": "eth2",
          "ipv4-address": "213.157.1.55",
          "ipv4-network-mask": "255.255.255.240",
          "ipv4-mask-length": 28,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": false,
            "ip-address-behind-this-interface": "specific",
            "leads-to-specific-network": {
              "uid": "a4944c2e-1801-408f-8fd9-48f873123d5d",
              "name": "sting-gw_eth2",
              "type": "group",
              "domain": {
                "uid": "41e821a0-3720-11e3-aa6e-0800200c9fde",
                "name": "SMC User",
                "domain-type": "domain"
              },
              "icon": "General/group",
              "color": "black"
            },
            "leads-to-dmz": false
          }
        },
        {
          "interface-name": "eth3",
          "ipv4-address": "192.168.20.1",
          "ipv4-network-mask": "255.255.255.192",
          "ipv4-mask-length": 26,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": false,
            "ip-address-behind-this-interface": "network defined by the interface ip and net mask",
            "leads-to-dmz": false
          }
        },
        {
          "interface-name": "eth0",
          "ipv4-address": "192.168.10.90",
          "ipv4-network-mask": "255.255.255.0",
          "ipv4-mask-length": 24,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": false,
            "ip-address-behind-this-interface": "network defined by the interface ip and net mask",
            "leads-to-dmz": false
          }
        },
        {
          "interface-name": "eth1",
          "ipv4-address": "5.7.21.44",
          "ipv4-network-mask": "255.255.255.248",
          "ipv4-mask-length": 29,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": true
          }
        },
        {
          "interface-name": "eth6",
          "ipv4-address": "192.168.5.3",
          "ipv4-network-mask": "255.255.255.0",
          "ipv4-mask-length": 24,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": true
          }
        },
        {
          "interface-name": "eth4",
          "ipv4-address": "192.168.15.1",
          "ipv4-network-mask": "255.255.255.0",
          "ipv4-mask-length": 24,
          "dynamic-ip": false,
          "topology": {
            "leads-to-internet": false,
            "ip-address-behind-this-interface": "network defined by the interface ip and net mask",
            "leads-to-dmz": false
          }
        }
      ]
    },
    {
      "dev_name": "small-gw",
      "dev_uid": "small-gw-uid",
      "rulebase_links":
      [
        {
          "from_rulebase_uid": null,
          "to_rulebase_uid": "inline-layer-1-uid",
          "from_rule_uid": null,
          "link_type": "inital"
        }
      ],
      "routing": [],
      "interfaces": []
    }
  ],
  "rulebases": [
    {
      "name": "sting-first-ordered-layer",
      "uid": "sting-first-ordered-layer-uid",
      "chunks": [
        {
          "uid": "sting-first-ordered-layer-uid",
          "name": "sting-first-ordered-layer",
          "rulebase": [
            {
              "uid": "dd12a686-256e-418c-8d8f-a7eb37fc7e5f",
              "name": "Block attacks",
              "type": "access-section",
              "from": 1,
              "to": 2,
              "rulebase": [...],
            }
        }
      ]
    },
    {
      "name": "inline-layer-2",
      "uid": "inline-layer-2-uid",
      "chunks": [
      ],
    },

  ],
  "nat_rulebases": [
    {
      "nat_rule_chunks": [...]
    }
  ]
}
```
## Graphql query for new rulebase_link structure

Assuming
- that we allow for duplication of rule in API call results (we do not fetch every rule exactly once but multiple times)

```graphql
fragment ruleDetails on rule {
  rule_uid
}

fragment rulebaseDetails on rulebase {
  id
  uid
  name
}

fragment rulebaseLinkRecursive12Layers on rulebase_link {
  link_type
  rulebase {
    ...rulebaseDetails
    rules(order_by: {rule_num_numeric: asc}) {
      ...ruleDetails
      rulebase_links(where: {gw_id: {_eq: $gwId}}) {
        rulebase {
          ...rulebaseDetails
          rules(order_by: {rule_num_numeric: asc}) {
            ...ruleDetails
            rulebase_links(where: {gw_id: {_eq: $gwId}}) {
              rulebase {
                ...rulebaseDetails
                rules(order_by: {rule_num_numeric: asc}) {
                  ...ruleDetails
                  rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                    rulebase {
                      ...rulebaseDetails
                      rules(order_by: {rule_num_numeric: asc}) {
                        ...ruleDetails
                        rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                          rulebase {
                            ...rulebaseDetails
                            rules(order_by: {rule_num_numeric: asc}) {
                              ...ruleDetails
                              rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                rulebase {
                                  ...rulebaseDetails
                                  rules(order_by: {rule_num_numeric: asc}) {
                                    ...ruleDetails
                                    rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                      rulebase {
                                        ...rulebaseDetails
                                        rules(order_by: {rule_num_numeric: asc}) {
                                          ...ruleDetails
                                          rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                            rulebase {
                                              ...rulebaseDetails
                                              rules(order_by: {rule_num_numeric: asc}) {
                                                ...ruleDetails
                                                rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                                  rulebase {
                                                    ...rulebaseDetails
                                                    rules(order_by: {rule_num_numeric: asc}) {
                                                      ...ruleDetails
                                                      rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                                        rulebase {
                                                          ...rulebaseDetails
                                                          rules(order_by: {rule_num_numeric: asc}) {
                                                            ...ruleDetails
                                                            rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                                              rulebase {
                                                                ...rulebaseDetails
                                                                rules(order_by: {rule_num_numeric: asc}) {
                                                                  ...ruleDetails
                                                                  rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                                                    rulebase {
                                                                      ...rulebaseDetails
                                                                      rules(order_by: {rule_num_numeric: asc}) {
                                                                        ...ruleDetails
                                                                        rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                                                          rulebase {
                                                                            ...rulebaseDetails
                                                                            rules(order_by: {rule_num_numeric: asc}) {
                                                                              ...ruleDetails
                                                                              rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                                                                                rulebase {
                                                                                  ...rulebaseDetails
                                                                                  rules(order_by: {rule_num_numeric: asc}) {
                                                                                    ...ruleDetails
                                                                                  }
                                                                                }
                                                                              }
                                                                            }
                                                                          }
                                                                        }
                                                                      }
                                                                    }
                                                                  }
                                                                  access_rule
                                                                }
                                                              }
                                                            }
                                                          }
                                                        }
                                                      }
                                                    }
                                                  }
                                                }
                                              }
                                            }
                                          }
                                        }
                                      }
                                    }
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}

fragment linkType on stm_link_type {
  id
  name
}

fragment rulebaseLinkRecursive4Layers on rulebase_link {
  stm_link_type {
    ...linkType
  }
  rulebase {
    ...rulebaseDetails
    rules(order_by: {rule_num_numeric: asc}) {
      ...ruleDetails
      rulebase_links(where: {gw_id: {_eq: $gwId}}) {
        stm_link_type {
          ...linkType
        }
        rulebase {
          ...rulebaseDetails
          rules(order_by: {rule_num_numeric: asc}) {
            ...ruleDetails
            rulebase_links(where: {gw_id: {_eq: $gwId}}) {
              stm_link_type {
                ...linkType
              }
              rulebase {
                ...rulebaseDetails
                rules(order_by: {rule_num_numeric: asc}) {
                  ...ruleDetails
                  rulebase_links(where: {gw_id: {_eq: $gwId}}) {
                    stm_link_type {
                      ...linkType
                    }
                    rulebase {
                      ...rulebaseDetails
                      rules(order_by: {rule_num_numeric: asc}) {
                        ...ruleDetails
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}

query getGwRules12Layers($gwId: Int!) {
  rulebase_link(where: {gw_id: {_eq: $gwId}, link_type: {_eq: 0}}) {
    ...rulebaseLinkRecursive12Layers
  }
}

query rulesReport($gwId: Int, $mgmId: Int) {
  management(where: {mgm_id: {_eq: $mgmId}}) {
    name: mgm_name
    id: mgm_id
    devices {
      name: dev_name
      id: dev_id
      rulebase_links(where: {gw_id: {_eq: $gwId}, link_type: {_eq: 0}}) {
        ...rulebaseLinkRecursive4Layers
      }
    }
  }
}
```
results in the following structure
```json
{
  "data": {
    "rulebase_link": [
      {
        "rulebase": {
         "id": 347,
         "uid": "cactus_Security-uid",
         "name": "cactus_Security-name",
         "rules": [
            {
              "rule_uid": "57f19d03-ed09-4083-9b71-b5eb28e69156",
              "rulebase_links": []
            },
            {
              "rule_uid": "f505a53a-27a3-4a09-87d2-633ec91a6c53",
              "rulebase_links": []
            },
            {
              "rule_uid": "a1f25d46-984d-42b3-a986-71279c12550f",
              "rulebase_links": [
                {
                  "rulebase": {
                    "id": 64,
                    "uid": "inline-layer-1-uid",
                    "name": "inline-layer-1",
                    "rules": [
                      {
                        "rule_uid": "acc044f6-2a4f-459b-b78c-9e7afee92621",
                        "rulebase_links": []
                      },
                      {
                        "rule_uid": "acc044f6-2a4f-459b-b78c-9e7afee92621",
                        "rulebase_links": []
                      },
                      {
                        "rule_uid": "acc044f6-2a4f-459b-b78c-9e7afee92621",
                        "rulebase_links": []
                      },
                      {
                        "rule_uid": "acc044f6-2a4f-459b-b78c-9e7afee92621",
                        "rulebase_links": [
                          {
                            "rulebase": {
                              "rules": [
                                {
                                  "rule_uid": "33ba8f84-a11d-463a-9ee8-6859b6b0035e",
                                },
                                {
                                  "rule_uid": "ea9d67bf-f098-4576-baad-1eb7b68900dd",
                                },
                                {
                                  "rule_uid": "ea9d67bf-f098-4576-baad-1eb7b68900dd",
                                }
                              ]
                            }
                          }
                        ]
                      }
                    ]
                  }
                },
                {
                  "rulebase": {
                    "uid": "inline-layer-1-uid",
                    "name": "inline-layer-1",
                    "rules": [
                      {
                        "rulebase_id": 64,
                        "rule_uid": "acc044f6-2a4f-459b-b78c-9e7afee92621",
                        "rulebase_links": []
                      },
                      {
                        "rulebase_id": 64,
                        "rule_uid": "acc044f6-2a4f-459b-b78c-9e7afee92621",
                        "rulebase_links": [
                          {
                            "rulebase": {
                              "rules": [
                                {
                                  "rule_uid": "33ba8f84-a11d-463a-9ee8-6859b6b0035e",
                                },
                                {
                                  "rule_uid": "ea9d67bf-f098-4576-baad-1eb7b68900dd",
                                }
                              ]
                            }
                          }
                        ]
                      }
                    ]
                  }
                }
              ]
            },
            {
              "rule_uid": "1d48ddf8-3e84-4bb6-8bae-3caf68714637",
              "rulebase_links": []
            },
            {
              "rule_uid": "88a1934f-9971-48f0-a0d2-7c15ab455447",
              "rulebase_links": []
            }
          ]
        }
      }
    ]
  }
}
```
Note: the more layers we fetch, the longer the query takes. Query with 7 layers takes ~4s, whereas 5 layers take 2s.

Todo: 
- add indices
- get rule_metadata into the query



Your query is slow because of the recursive nature of rulebase_links, especially when you reach the third level of recursion. Here are the primary reasons why the last recursion layer significantly impacts performance:

1. Exponential Growth of Joins
Each level of recursion in rulebase_links multiplies the number of records retrieved. The deeper the recursion, the more rulebase records are fetched, leading to:

More joins on rulebase_links
More filtering and ordering operations
A large Cartesian explosion of data if multiple links exist per rulebase
By removing the last recursion layer, you're reducing the number of joins and resulting dataset size, making the query significantly faster.


How to Optimize?
✅ Approach 1: Pre-fetch Rulebase IDs, Then Fetch Rules in Batches
Instead of fetching rules at every recursion step, first get rulebase_ids and then batch query rules separately.

graphql
Copy
query rulesReport($mgmId: [Int!], $relevantImportId: bigint, $limit: Int, $offset: Int) {
  management(
    where: {
      hide_in_gui: {_eq: false},
      mgm_id: {_in: $mgmId},
      stm_dev_typ: {dev_typ_is_multi_mgmt: {_eq: false}, is_pure_routing_device: {_eq: false}}
    }, 
    order_by: {mgm_name: asc}
  ) {
    id: mgm_id
    uid: mgm_uid
    name: mgm_name
    devices(where: {hide_in_gui: {_eq: false}, _or: [{dev_id: {_eq: 9}}]}) {
      name: dev_name
      id: dev_id
      uid: dev_uid
      rulebase_links(where: {link_type: {_eq: 0}}) {
        rulebase {
          id
        }
      }
    }
  }
}
Then, run a second query using those IDs:

graphql
Copy
query rulesBatch($rulebaseIds: [Int!], $limit: Int, $offset: Int) {
  rulebase(where: {id: {_in: $rulebaseIds}}) {
    id
    rules(limit: $limit, offset: $offset, order_by: {rule_num_numeric: asc}) {
      ...ruleOverview
    }
  }
}
Why This Works:

First query retrieves rulebase IDs efficiently (small result set).
Second query retrieves rules in a batch, reducing recursion.
Uses fewer nested loops, improving performance.
