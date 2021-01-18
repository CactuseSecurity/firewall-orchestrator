To manually rollback a hanging import of management with ID 1:

`sudo -u postgres psql -d fworchdb -c "select * from rollback_import_of_mgm(1)"`
