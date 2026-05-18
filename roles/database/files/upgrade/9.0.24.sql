insert into config (config_key, config_value, config_user) VALUES ('modIntegrationMode', 'FullyIntegrated', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modIntegrationStates', '[]', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modIntegrationStateMarker', 'ImplementationState', 0) ON CONFLICT DO NOTHING;

-- The following changes are related to the addition of rule_src/dst_zone (text, containing joined zone names) to the rule table.
-- Keeping the old columns rule_from/to_zone (int, currently unused) for now to avoid having to change existing views, some of which are not even used anymore

ALTER TABLE IF EXISTS public.rule
    ADD COLUMN IF NOT EXISTS rule_src_zone TEXT;

ALTER TABLE IF EXISTS public.rule
    ADD COLUMN IF NOT EXISTS rule_dst_zone TEXT;

DO $$
BEGIN
    IF to_regclass('public.rule') IS NULL
       OR to_regclass('public.rule_from_zone') IS NULL
       OR to_regclass('public.rule_to_zone') IS NULL
       OR to_regclass('public.zone') IS NULL THEN
        RETURN;
    END IF;

    WITH source_zones AS (
        SELECT
            rfz.rule_id,
            string_agg(DISTINCT z.zone_name, '|' ORDER BY z.zone_name) AS joined_zone_names
        FROM public.rule_from_zone rfz
        JOIN public.zone z
            ON z.zone_id = rfz.zone_id
        WHERE z.zone_name IS NOT NULL
        GROUP BY rfz.rule_id
    )
    UPDATE public.rule r
    SET rule_src_zone = source_zones.joined_zone_names
    FROM source_zones
    WHERE r.rule_id = source_zones.rule_id
      AND COALESCE(r.rule_src_zone, '') <> source_zones.joined_zone_names;

    WITH destination_zones AS (
        SELECT
            rtz.rule_id,
            string_agg(DISTINCT z.zone_name, '|' ORDER BY z.zone_name) AS joined_zone_names
        FROM public.rule_to_zone rtz
        JOIN public.zone z
            ON z.zone_id = rtz.zone_id
        WHERE z.zone_name IS NOT NULL
        GROUP BY rtz.rule_id
    )
    UPDATE public.rule r
    SET rule_dst_zone = destination_zones.joined_zone_names
    FROM destination_zones
    WHERE r.rule_id = destination_zones.rule_id
      AND COALESCE(r.rule_dst_zone, '') <> destination_zones.joined_zone_names;
END
$$;
