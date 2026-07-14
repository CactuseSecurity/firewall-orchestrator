CREATE TABLE IF NOT EXISTS modelling.permitted_owners
(
    connection_id int,
    app_id int,
    primary key (connection_id, app_id)
);

ALTER TABLE modelling.permitted_owners DROP CONSTRAINT IF EXISTS modelling_permitted_owners_connection_foreign_key;
ALTER TABLE modelling.permitted_owners ADD CONSTRAINT modelling_permitted_owners_connection_foreign_key
    FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE modelling.permitted_owners DROP CONSTRAINT IF EXISTS modelling_permitted_owners_owner_foreign_key;
ALTER TABLE modelling.permitted_owners ADD CONSTRAINT modelling_permitted_owners_owner_foreign_key
    FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS interface_permission Varchar;
