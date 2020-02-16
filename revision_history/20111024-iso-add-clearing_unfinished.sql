-- $Id: 20111024-iso-add-clearing_unfinished.sql,v 1.1.2.1 2013-02-12 09:41:37 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20111024-iso-add-clearing_unfinished.sql,v $

alter table management add clearing_import_ran boolean default FALSE;
alter table device add clearing_import_ran boolean default FALSE;

-- iso-importer-loop.pl ersetzen
-- iso-import-main.sql ersetzen und bin/db-init2-functions-with-output.sh ausführen