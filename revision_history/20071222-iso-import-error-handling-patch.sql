-- $Id: 20071222-iso-import-error-handling-patch.sql,v 1.1.2.2 2007-12-21 22:46:47 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20071222-iso-import-error-handling-patch.sql,v $
-- als itsecorg-user:

DROP FUNCTION import_all_main (INTEGER);
DROP FUNCTION import_global_refhandler_main (INTEGER);

-- importer-Verzeichnis komplett austauschen
-- \i install/database/iso-import-main.sql
-- \i install/database/iso-rule-refs.sql
-- \i install/database/iso-basic-procs.sql