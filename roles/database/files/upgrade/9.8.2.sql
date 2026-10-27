-- IP addresses added in the owner dialog and the demo owner data were stored in owner_network without a name. As app
-- servers they crashed the owner recertification report (#5572). Name them the way the UI names new app servers: the
-- naming convention prefix (app servers only) followed by the host ip, the network in cidr notation, or the ip range.
-- Only rows without a name are changed, so running this again changes nothing.
DO $$
DECLARE
    naming_convention jsonb := '{}'::jsonb;
    nameless RECORD;
    network_bits int;
    prefix_key text;
    ip_part text;
BEGIN
    BEGIN
        SELECT config_value::jsonb INTO naming_convention FROM config
            WHERE config_key = 'modNamingConvention' AND config_user = 0;
    EXCEPTION WHEN invalid_text_representation THEN
        naming_convention := '{}'::jsonb;
    END;
    naming_convention := COALESCE(naming_convention, '{}'::jsonb);

    FOR nameless IN
        SELECT id, ip, ip_end, nw_type FROM owner_network WHERE name IS NULL OR name = ''
    LOOP
        IF nameless.ip = nameless.ip_end THEN
            prefix_key := 'appServerPrefix';
            ip_part := host(nameless.ip);
        ELSE
            SELECT min(bits) INTO network_bits
                FROM generate_series(0, CASE WHEN family(nameless.ip) = 4 THEN 32 ELSE 128 END) AS bits
                WHERE host(network(set_masklen(nameless.ip::inet, bits))) = host(nameless.ip)
                    AND host(broadcast(set_masklen(nameless.ip::inet, bits))) = host(nameless.ip_end);
            IF network_bits IS NOT NULL THEN
                prefix_key := 'networkPrefix';
                ip_part := host(nameless.ip) || '/' || network_bits;
            ELSE
                prefix_key := 'ipRangePrefix';
                ip_part := host(nameless.ip) || '-' || host(nameless.ip_end);
            END IF;
        END IF;

        UPDATE owner_network
            SET name = CASE WHEN nameless.nw_type = 10 THEN COALESCE(naming_convention ->> prefix_key, '') ELSE '' END || ip_part
            WHERE id = nameless.id;
    END LOOP;
END $$;
