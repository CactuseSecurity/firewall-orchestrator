ALTER TABLE ext_request ADD COLUMN IF NOT EXISTS locked boolean default false;
