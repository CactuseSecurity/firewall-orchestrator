
CREATE TABLE IF NOT EXISTS "provisioning_config_node"
(
	"id" BIGSERIAL,
	"node_type" Varchar NOT NULL,
	"object_key" Varchar NOT NULL,
	"parent_id" BIGINT,
	"display_name" Varchar NOT NULL Default '',
	"sort_order" Integer,
	primary key ("id")
);

CREATE TABLE IF NOT EXISTS "provisioning_config_value"
(
	"node_id" BIGINT NOT NULL,
	"config_key" Varchar NOT NULL,
	"config_value" Jsonb NOT NULL,
	primary key ("node_id","config_key")
);

DO $$
BEGIN
	IF NOT EXISTS (
		SELECT 1 FROM pg_constraint
		WHERE conname = 'provisioning_config_node_parent_id_fkey'
		AND conrelid = 'provisioning_config_node'::regclass
	) THEN
		ALTER TABLE "provisioning_config_node"
		ADD CONSTRAINT provisioning_config_node_parent_id_fkey
		FOREIGN KEY ("parent_id") REFERENCES "provisioning_config_node" ("id")
		ON UPDATE RESTRICT ON DELETE CASCADE;
	END IF;
END $$;

DO $$
BEGIN
	IF NOT EXISTS (
		SELECT 1 FROM pg_constraint
		WHERE conname = 'provisioning_config_value_node_id_fkey'
		AND conrelid = 'provisioning_config_value'::regclass
	) THEN
		ALTER TABLE "provisioning_config_value"
		ADD CONSTRAINT provisioning_config_value_node_id_fkey
		FOREIGN KEY ("node_id") REFERENCES "provisioning_config_node" ("id")
		ON UPDATE RESTRICT ON DELETE CASCADE;
	END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_provisioning_config_node_parent
ON provisioning_config_node (parent_id);

CREATE UNIQUE INDEX IF NOT EXISTS idx_provisioning_config_node_type_key
ON provisioning_config_node (node_type, object_key);

CREATE INDEX IF NOT EXISTS idx_provisioning_config_value_key
ON provisioning_config_value (config_key);

INSERT INTO provisioning_config_node (node_type, object_key, display_name, sort_order)
VALUES ('global', 'global', 'Global', 0)
ON CONFLICT (node_type, object_key) DO NOTHING;
