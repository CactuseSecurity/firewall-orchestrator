-- create changelog_owner
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'changelog_owner'
    ) THEN
        CREATE TABLE "changelog_owner"
        (
            "log_owner_id" BIGSERIAL,
            "control_id" BIGINT NOT NULL,
            "new_owner_id" BIGINT
                CONSTRAINT "changelog_owner_new_rule_id_constraint"
                CHECK ((change_action='D' AND new_owner_id IS NULL) OR NOT new_owner_id IS NULL),
            "old_owner_id" BIGINT
                CONSTRAINT "changelog_owner_old_rule_id_constraint"
                CHECK ((change_action='I' AND old_owner_id IS NULL) OR NOT old_owner_id IS NULL),
            "abs_change_id" BIGINT NOT NULL
                DEFAULT nextval('public.abs_change_id_seq'::text)
                UNIQUE,
            "change_action" CHAR(1) NOT NULL,
            "source_id" VARCHAR,
            "security_relevant" BOOLEAN NOT NULL DEFAULT TRUE,
            PRIMARY KEY ("log_owner_id")
        );
    END IF;
END $$;

-- control_id
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'changelog_owner_control_id_import_control_foreign_key'
    ) THEN
        ALTER TABLE "changelog_owner"
        ADD CONSTRAINT changelog_owner_control_id_import_control_foreign_key
        FOREIGN KEY ("control_id")
        REFERENCES "import_control" ("control_id")
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
    END IF;
END $$;

-- new_owner_id
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'changelog_owner_new_owner_id_owner_id_foreign_key'
    ) THEN
        ALTER TABLE "changelog_owner"
        ADD CONSTRAINT changelog_owner_new_owner_id_owner_id_foreign_key
        FOREIGN KEY ("new_owner_id")
        REFERENCES "owner" ("id")
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
    END IF;
END $$;

-- old_owner_id
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'changelog_owner_old_owner_id_owner_id_foreign_key'
    ) THEN
        ALTER TABLE "changelog_owner"
        ADD CONSTRAINT changelog_owner_old_owner_id_owner_id_foreign_key
        FOREIGN KEY ("old_owner_id")
        REFERENCES "owner" ("id")
        ON UPDATE RESTRICT
        ON DELETE CASCADE;
    END IF;
END $$;