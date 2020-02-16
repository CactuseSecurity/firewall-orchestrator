-- Datei web/include/db-change.php austauschen
-- Datei web/htdocs/inctext/reporting_tables_druck.inc.php austauschen (nur Umlaut Ae-Aenderung)

-- Ergaenzen in iso-view.sql:
-- einheitliche View auf source und destination aller regeln
CREATE OR REPLACE VIEW view_rule_source_or_destination AS
         SELECT rule.rule_id, rule.rule_dst_neg AS rule_neg, objgrp_flat.objgrp_flat_member_id AS obj_id
           FROM rule
      LEFT JOIN rule_to USING (rule_id)
   LEFT JOIN objgrp_flat ON rule_to.obj_id = objgrp_flat.objgrp_flat_id
   LEFT JOIN object ON objgrp_flat.objgrp_flat_member_id = object.obj_id
UNION
         SELECT rule.rule_id, rule.rule_src_neg AS rule_neg, objgrp_flat.objgrp_flat_member_id AS obj_id
           FROM rule
      LEFT JOIN rule_from USING (rule_id)
   LEFT JOIN objgrp_flat ON rule_from.obj_id = objgrp_flat.objgrp_flat_id
   LEFT JOIN object ON objgrp_flat.objgrp_flat_member_id = object.obj_id

GRANT SELECT ON TABLE view_rule_source_or_destination TO GROUP secuadmins, reporters;