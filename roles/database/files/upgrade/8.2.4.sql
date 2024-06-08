CREATE OR REPLACE FUNCTION get_rules_for_owner(device_row device, ownerid integer)
RETURNS SETOF rule AS $$
    BEGIN
        RETURN QUERY
        SELECT r.* FROM rule r
            LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
            LEFT JOIN objgrp_flat rf_of ON (rf.obj_id=rf_of.objgrp_flat_id)
            LEFT JOIN object rf_o ON (rf_of.objgrp_flat_member_id=rf_o.obj_id)
            LEFT JOIN owner_network ON
            (ip_ranges_overlap(rf_o.obj_ip, rf_o.obj_ip_end, ip, ip_end, rf.negated != r.rule_src_neg))
        WHERE r.dev_id = device_row.dev_id AND owner_id = ownerid AND rule_head_text IS NULL
        UNION
        SELECT r.* FROM rule r
            LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
            LEFT JOIN objgrp_flat rt_of ON (rt.obj_id=rt_of.objgrp_flat_id)
            LEFT JOIN object rt_o ON (rt_of.objgrp_flat_member_id=rt_o.obj_id)
            LEFT JOIN owner_network ON
            (ip_ranges_overlap(rt_o.obj_ip, rt_o.obj_ip_end, ip, ip_end, rt.negated != r.rule_dst_neg))
        WHERE r.dev_id = device_row.dev_id AND owner_id = ownerid AND rule_head_text IS NULL
        ORDER BY rule_name;
    END;
$$ LANGUAGE 'plpgsql' STABLE;
