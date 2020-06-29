/*
	Comment:
		added automatic reference fixing at the end of each import
		now broken references will cause failure of import
		
	created the following funtions (in iso-qa.sql):
		fix_broken_refs () - fixes all refs (active rules for all systems, delimiter = '|')
		check_broken_refs (VARCHAR, BOOLEAN) - checks (2nd param=false) or fixes (2nd param=true) broken refs; 1st param=<delimiter of ruleobjects>
		get_active_rules_with_broken_refs_per_mgm (VARCHAR, BOOLEAN, INTEGER) - same as check_broken_refs limited to a single management (mgm id = 3rd param)
		get_active_rules_with_broken_refs_per_dev (VARCHAR, BOOLEAN, INTEGER) - same as check_broken_refs limited to a single device (dev id = 3rd param) 	
						
	migration:
		replace and execute: 
			/usr/share/itsecorg/install/database/stored-procedures/iso-qa.sql 
			/usr/share/itsecorg/install/database/stored-procedures/iso-import-main.sql
		e.g.
			psql -d isodb -c "\i /usr/share/itsecorg/install/database/stored-procedures/iso-qa.sql"
			psql -d isodb -c "\i /usr/share/itsecorg/install/database/stored-procedures/iso-import-main.sql"
		clean-up old broken references once by running
			psql -c "select * from fix_broken_refs ()"

**/

