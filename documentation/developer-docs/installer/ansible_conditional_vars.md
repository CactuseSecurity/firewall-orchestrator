# Ansible conditional variables

## Example

Say you register a variable like this

    - name: check if there already is an ldap connection in DB
      community.postgresql.postgresql_query:
        login_db: fworchdb
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
    
If you want to reference the "count" do it like this

    when: ldap_conn_present.query_result.0.count == 0
    
Notice you can't put "" around 0 or you'll get an error
