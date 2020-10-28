# The variable db_exists

db_exists is used to veriefy if the database fworch_db exists before installing it

## output

the output for non-existing fworch_db is

    ok: [localhost] => {
    "db_exists": {
            "changed": false,
            "failed": false,
            "query": "SELECT count(*) FROM pg_database WHERE datname='fworch_db'",
            "query_result": [
                {
                    "count": 0
                }
            ],
            "rowcount": 1,
            "statusmessage": "SELECT 1",
            "warnings": [
            "Database name has not been passed, used default database to connect to."
            ]
        }
    }
    
## reference important data

the correct way to reference the count is

    db_exists.query_result.0.count
    
 
