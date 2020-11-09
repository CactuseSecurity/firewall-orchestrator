# Ansible conditional vatiables

## Example

Say you register a variable like this

    - name: check if there already is an ldap connection in DB
      postgresql_query:
        db: fworchdb
        query: SELECT COUNT(*) FROM ldap_connection
      become: yes
      become_user: postgres
      register: ldap_conn_present
 
The content of ldap_conn_present is

        "ldap_conn_present": {
            "changed": false,
            "failed": false,
            "query": "SELECT COUNT(*) FROM ldap_connection",
            "query_result": [
                {
                    "count": 0
                }
            ],
            "rowcount": 1,
            "statusmessage": "SELECT 1"
        }
    }
    
