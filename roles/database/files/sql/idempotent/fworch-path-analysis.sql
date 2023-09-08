DROP FUNCTION IF EXISTS devices_in_path (cidr, cidr);

CREATE OR REPLACE FUNCTION public.routing_interface (c_network_object cidr, i_dev_id integer, i_ip_version integer)
    RETURNS integer
    LANGUAGE 'plpgsql' STABLE
    AS $BODY$
BEGIN
    RETURN (
        SELECT
            interface_id
        FROM
            gw_route
        WHERE (ip_version = i_ip_version
            AND (destination <<= c_network_object
                OR destination >>= c_network_object)
            AND routing_device = i_dev_id)
    ORDER BY
        masklen(destination) DESC,
        metric ASC
    LIMIT 1);
END;
$BODY$;

CREATE OR REPLACE FUNCTION public.devices_in_path (c_source cidr, c_destination cidr)
    RETURNS SETOF device
    LANGUAGE 'plpgsql' STABLE ROWS 20
    AS $BODY$
DECLARE
    dev device;
    i_dev_id integer;
BEGIN
    IF family(c_source::inet) = family(c_destination::inet) THEN
        FOR dev IN
        SELECT
            *
        FROM
            device LOOP
                IF routing_interface (c_source, dev.dev_id, family(c_source::inet)) != routing_interface (c_destination, dev.dev_id, family(c_destination::inet)) THEN
                    RETURN NEXT dev;
                ELSIF c_source IN
                    (SELECT
                        ip
                    FROM
                        gw_interface
                    WHERE
                        routing_device = dev.dev_id) THEN
                        RETURN NEXT dev;
                ELSIF c_destination IN
                    (SELECT
                        ip
                    FROM
                        gw_interface
                    WHERE
                        routing_device = dev.dev_id) THEN
                        RETURN NEXT dev;
                END IF;
            END LOOP;
    END IF;
    RETURN;
END;
$BODY$;

