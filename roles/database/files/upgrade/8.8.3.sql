ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS requested_on_fw boolean default false;
ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS removed boolean default false;
ALTER TABLE modelling.connection ADD COLUMN IF NOT EXISTS removal_date timestamp;

UPDATE modelling.connection SET requested_on_fw=true WHERE requested_on_fw=false;
