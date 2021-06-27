
-- cleanup
DELETE FROM txt;

-- import
INSERT INTO txt VALUES ('initial_import',		'German',	'Dieses Element ist Teil der initial importierten Konfiguration.');
INSERT INTO txt VALUES ('initial_import', 		'English',	'This entity is part of the initially imported configuration.');

-- languages
INSERT INTO txt VALUES ('English', 				'German',	'Englisch');
INSERT INTO txt VALUES ('English', 				'English',	'English');
INSERT INTO txt VALUES ('German', 				'German',	'Deutsch');
INSERT INTO txt VALUES ('German', 				'English',	'German');

-- general
INSERT INTO txt VALUES ('cancel', 				'German',	'Abbrechen');
INSERT INTO txt VALUES ('cancel', 				'English',	'Cancel');
INSERT INTO txt VALUES ('save', 				'German',	'Speichern');
INSERT INTO txt VALUES ('save', 				'English',	'Save');
INSERT INTO txt VALUES ('delete', 				'German',	'L&ouml;schen');
INSERT INTO txt VALUES ('delete', 				'English',	'Delete');
INSERT INTO txt VALUES ('clone', 				'German',	'Klonen');
INSERT INTO txt VALUES ('clone', 				'English',	'Clone');
INSERT INTO txt VALUES ('edit', 				'German',	'Bearbeiten');
INSERT INTO txt VALUES ('edit', 				'English',	'Edit');
INSERT INTO txt VALUES ('set', 				    'German',	'Setzen');
INSERT INTO txt VALUES ('set', 				    'English',	'Set');
INSERT INTO txt VALUES ('add', 				    'German',	'Hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add', 				    'English',	'Add');
INSERT INTO txt VALUES ('search', 				'German',	'Suchen');
INSERT INTO txt VALUES ('search', 			    'English',	'Search');
INSERT INTO txt VALUES ('load', 				'German',	'Laden');
INSERT INTO txt VALUES ('load', 			    'English',	'Load');
INSERT INTO txt VALUES ('ok', 				    'German',	'Ok');
INSERT INTO txt VALUES ('ok', 			        'English',	'Ok');
INSERT INTO txt VALUES ('yes', 				    'German',	'ja');
INSERT INTO txt VALUES ('yes', 			        'English',	'yes');
INSERT INTO txt VALUES ('no', 				    'German',	'nein');
INSERT INTO txt VALUES ('no', 			        'English',	'no');
INSERT INTO txt VALUES ('close', 				'German',	'Schliessen');
INSERT INTO txt VALUES ('close', 			    'English',	'Close');
INSERT INTO txt VALUES ('unspecified_error',    'German', 	'Nicht spezifizierter Fehler');
INSERT INTO txt VALUES ('unspecified_error',    'English', 	'Unspecified Error');
INSERT INTO txt VALUES ('jwt_expiry',           'German', 	'JWT abgelaufen');
INSERT INTO txt VALUES ('jwt_expiry',           'English', 	'JWT expired');
INSERT INTO txt VALUES ('api_access',           'German', 	'Zugang zur API');
INSERT INTO txt VALUES ('api_access',           'English', 	'API access');

-- login
INSERT INTO txt VALUES ('login', 				'German',	'Anmelden');
INSERT INTO txt VALUES ('login', 				'English',	'Login');
INSERT INTO txt VALUES ('username', 			'German',	'Nutzername');
INSERT INTO txt VALUES ('username', 			'English',	'Username');
INSERT INTO txt VALUES ('password', 			'German',	'Passwort');
INSERT INTO txt VALUES ('password', 			'English',	'Password');
INSERT INTO txt VALUES ('change_password', 		'German',	'Passwort &auml;ndern');
INSERT INTO txt VALUES ('change_password', 		'English',	'Change Password');
INSERT INTO txt VALUES ('old_password', 		'German',	'Altes Passwort');
INSERT INTO txt VALUES ('old_password', 		'English',	'Old Password');
INSERT INTO txt VALUES ('new_password', 		'German',	'Neues Passwort');
INSERT INTO txt VALUES ('new_password', 		'English',	'New Password');

-- navigation
INSERT INTO txt VALUES ('reporting', 			'German',	'Reporting');
INSERT INTO txt VALUES ('reporting', 			'English',	'Reporting');
INSERT INTO txt VALUES ('settings', 			'German',	'Einstellungen');
INSERT INTO txt VALUES ('settings', 			'English',	'Settings');
INSERT INTO txt VALUES ('fworch_long',			'German',	'Firewall-Orchestrator');
INSERT INTO txt VALUES ('fworch_long',			'English',	'Firewall-Orchestrator');
INSERT INTO txt VALUES ('help',					'German',	'Hilfe');
INSERT INTO txt VALUES ('help', 				'English',	'Help');
INSERT INTO txt VALUES ('logout', 				'German',	'Abmelden');
INSERT INTO txt VALUES ('logout', 				'English',	'Logout');
INSERT INTO txt VALUES ('documentation', 		'German',	'Dokumentation');
INSERT INTO txt VALUES ('documentation', 		'English',	'Documentation');
INSERT INTO txt VALUES ('request', 				'German',	'Antrag');
INSERT INTO txt VALUES ('request', 				'English',	'Request');
INSERT INTO txt VALUES ('scheduling', 			'German',	'Scheduling');
INSERT INTO txt VALUES ('scheduling', 			'English',	'Scheduling');
INSERT INTO txt VALUES ('archive', 				'German',	'Archiv');
INSERT INTO txt VALUES ('archive', 				'English',	'Archive');
INSERT INTO txt VALUES ('recertification', 		'German',	'Rezertifizierung');
INSERT INTO txt VALUES ('recertification', 		'English',	'Recertification');
INSERT INTO txt VALUES ('api', 		            'German',	'API');
INSERT INTO txt VALUES ('api', 		            'English',	'API');

-- start
INSERT INTO txt VALUES ('welcome_to',           'German', 	'Willkommen zu Firewall Orchestrator');
INSERT INTO txt VALUES ('welcome_to',           'English', 	'Welcome to Firewall Orchestrator');
INSERT INTO txt VALUES ('whats_new_in_version',	'German', 	'Was ist neu in Firewall Orchestrator Version');
INSERT INTO txt VALUES ('whats_new_in_version',	'English', 	'Release notes Firewall Orchestrator version');
INSERT INTO txt VALUES ('whats_new_facts',	    'German', 	'
<ul>
    <li>Jetzt 100% Open Source - passen Sie Firewall Orchestrator an Ihre Bed&uuml;rfnisse an. Machen Sie mit.
        Der Quellcode kann auf <a href="https://github.com/CactuseSecurity/firewall-orchestrator" target="_blank">GitHub</a> angezeigt und heruntergeladen werden.</li>
    <li>GraphQL API f&uuml;r Automatisierungen</li>
    <li>Firewall-Regel Rezertifizierungsworkflow - beseitigen Sie ihre Altlasten und erf&uuml;llen Sie aktuelle regulatorische Anforderungen.</li>
</ul>
');
INSERT INTO txt VALUES ('whats_new_facts',	    'English', 	'
<ul>
    <li>Now 100% Open Source - adjust Firewall Orchestrator to your needs. Join the community and contribute.
        The code can be viewed/downloaded from <a href="https://github.com/CactuseSecurity/firewall-orchestrator" target="_blank">GitHub</a></li>
    <li>GraphQL API for automation</li>
    <li>Firewall rule recertification workflow - removed unnecessary rules and meet current regulatory requirements.</li>
</ul>
');

INSERT INTO txt VALUES ('getting_started',	    'German', 	'Einstiegshilfe');
INSERT INTO txt VALUES ('getting_started',	    'English', 	'Quick start');
INSERT INTO txt VALUES ('getting_started_facts',	    'German', 	'
Die folgenden Hauptmen&uuml;punkte stehen (je nach Rollenzugeh&ouml;rigkeit) zur Verf&uuml;gung:<ul>
    <li><a href="/report">Reporting</a>: Erlaubt das Generieren verschiedener Reports</li>
    <li><a href="/schedule">Scheduling</a>: Zeitlich terminierte (wiederkehrende) Report-Generierung</li>
    <li><a href="/archive">Archiv</a>: Zugriff auf (per Scheduling) generierte Reports</li>
    <li><a href="/certification">Rezertifizierung</a>: Workflow zur Bereinigung des Regelwerks um nicht mehr ben&ouml;tigte Regeln</li>
    <li><a href="/help" target="_blank">Hilfeseiten</a>: Benutzerhandbuch</li>
    <li><a href="/settings">Einstellungen</a>: Alle Einstellungen wie z.B. Sprache der Benutzeroberfl&auml;che oder
        das Einbinden <a href="/settings/managements">Ihrer eigenen Firewall-Systeme</a></li>
    <li><a href="/logout">Abmelden</a>: Firewall Orchestrator verlassen</li>
</ul>
');
INSERT INTO txt VALUES ('getting_started_facts',	    'English', 	'
The following top-level menu items are available (depending on role memberships):
<ul>
    <li><a href="/report">Reporting</a>: Ad-hoc generation of all available reports</li>
    <li><a href="/schedule">Scheduling</a>: Setup (recurring) report generation</li>
    <li><a href="/archive">Archive</a>: Access your (scheduled) reports</li>
    <li><a href="/certification">Recertification</a>: Workflow for removing unnecessary rules from your rulebases</li>
    <li><a href="/help" target="_blank">Help</a>: Manual pages</li>
    <li><a href="/settings">Settings</a>: All settings like e.g. language of the user interface or 
        integration of <a href="/settings/managements">your own firewalls</a>.</li>
    <li><a href="/logout">Logout</a>: Leave Firewall Orchestrator</li>
</ul>
');

INSERT INTO txt VALUES ('getting_support',	    'German', 	'Unterst&uuml;tzung ben&ouml;tigt? Ihre Kontaktm&ouml;glichkeiten');
INSERT INTO txt VALUES ('getting_support',	    'English', 	'Do you need help? Our Contact options');
INSERT INTO txt VALUES ('support_details',	    'German', 	'
M&ouml; Sie einen Supportvertrag abschlie&szlig;en, um in den Genuss folgender Vorteile zu kommen?<br>
<ul>
<li>garantierte Unterst&uuml;tzung bei Problemen mit Firewall Orchestrator</li>
<li>Customizing: haben Sie Anpassungsw&uuml;nsche, die wir f&uuml;r Sie umsetzen sollen?</li>
</ul>
Folgende Kontaktm&ouml;glichkeiten stehen Ihnen zur Verf&uuml;gung:
<ul>
    <li>Telefon: <a href="tel:+496996233675">+49 69 962336-75</a></li>
    <li>Email: <a href="mailto:support@cactus.de">support@cactus.de</a></li>
    <li>Chat: <a href="https://fworch.cactus.de/chat">Support-Chat</a></li>
    <li>Video/Audio-Call (nach Vereinbarung): <a href="https://conf.cactus.de/fworch">Conf@cactus.de</a></li>
</ul>
');
INSERT INTO txt VALUES ('support_details',	    'English', 	'
Do you wish to sign a support contract for the following benefits?
choose from the following contact options:
Ihre Vorteile: <br>
<ul>
<li>get a direct line to qualified support personnel</li>
<li>Customizing: can we help your with individual changes or extensions of functionality?</li>
</ul>
<ul>
    <li>Phone: <a href="tel:+496996233675">+49 69 962336-75</a></li>
    <li>Email: <a href="mailto:support@cactus.de">support@cactus.de</a> </li>
    <li>Chat: <a href="https://fworch.cactus.de/chat">Support chat</a></li>
    <li>Video/Audio Call (contact us to arrange a time slot): <a href="https://conf.cactus.de/fworch">Conf@cactus.de</a></li>
</ul>
');

-- reporting
INSERT INTO txt VALUES ('select_device',		'German', 	'Device(s) ausw&auml;hlen');
INSERT INTO txt VALUES ('select_device',		'English', 	'Select device(s)');
INSERT INTO txt VALUES ('select_all',		    'German', 	'Alle ausw&auml;hlen');
INSERT INTO txt VALUES ('select_all',		    'English', 	'Select all');
INSERT INTO txt VALUES ('clear_all',		    'German', 	'Auswahl leeren');
INSERT INTO txt VALUES ('clear_all',		    'English', 	'Clear all');
INSERT INTO txt VALUES ('generate_report',		'German', 	'Report erstellen');
INSERT INTO txt VALUES ('generate_report',		'English', 	'Generate report');
INSERT INTO txt VALUES ('export_report',        'German', 	'Report exportieren');
INSERT INTO txt VALUES ('export_report',        'English', 	'Export Report');
INSERT INTO txt VALUES ('export_as',            'German', 	'Exportieren als...');
INSERT INTO txt VALUES ('export_as',            'English', 	'Export as...');
INSERT INTO txt VALUES ('export',               'German', 	'Exportieren');
INSERT INTO txt VALUES ('export',               'English', 	'Export');
INSERT INTO txt VALUES ('export_report_download','German', 	'Exportierten Report herunterladen');
INSERT INTO txt VALUES ('export_report_download','English', 'Export Report Download');
INSERT INTO txt VALUES ('download_csv',		    'German', 	'als CSV herunterladen');
INSERT INTO txt VALUES ('download_csv',		    'English', 	'Download CSV');
INSERT INTO txt VALUES ('download_pdf',		    'German', 	'als PDF herunterladen');
INSERT INTO txt VALUES ('download_pdf',		    'English', 	'Download PDF');
INSERT INTO txt VALUES ('download_html',		'German', 	'als HTML herunterladen');
INSERT INTO txt VALUES ('download_html',		'English', 	'Download HTML');
INSERT INTO txt VALUES ('download_json',		'German', 	'als JSON herunterladen');
INSERT INTO txt VALUES ('download_json',		'English', 	'Download JSON');
INSERT INTO txt VALUES ('save_as_template',		'German', 	'Als Vorlage speichern');
INSERT INTO txt VALUES ('save_as_template',		'English', 	'Save as Template');
INSERT INTO txt VALUES ('no_device_selected',	'German', 	'Kein Device ausgew&auml;hlt.');
INSERT INTO txt VALUES ('no_device_selected',	'English', 	'No device(s) selected.');
INSERT INTO txt VALUES ('filter', 				'German', 	'Filter');
INSERT INTO txt VALUES ('filter', 				'English', 	'filter');
INSERT INTO txt VALUES ('number', 				'German', 	'Nr.');
INSERT INTO txt VALUES ('number', 				'English', 	'No.');
INSERT INTO txt VALUES ('name', 				'German', 	'Name');
INSERT INTO txt VALUES ('name', 				'English', 	'Name');
INSERT INTO txt VALUES ('source', 				'German', 	'Quelle');
INSERT INTO txt VALUES ('source', 				'English', 	'Source');
INSERT INTO txt VALUES ('destination', 			'German', 	'Ziel');
INSERT INTO txt VALUES ('destination', 			'English', 	'Destination');
INSERT INTO txt VALUES ('services', 			'German', 	'Dienste');
INSERT INTO txt VALUES ('services', 			'English', 	'Services');
INSERT INTO txt VALUES ('action', 				'German', 	'Aktionen');
INSERT INTO txt VALUES ('action', 				'English', 	'Actions');
INSERT INTO txt VALUES ('track', 				'German', 	'Logging');
INSERT INTO txt VALUES ('track', 				'English', 	'Logging');
INSERT INTO txt VALUES ('disabled',				'German', 	'Deaktiviert');
INSERT INTO txt VALUES ('disabled',				'English', 	'Disabled');
INSERT INTO txt VALUES ('comment',				'German', 	'Kommentar');
INSERT INTO txt VALUES ('comment',				'English', 	'Comment');
INSERT INTO txt VALUES ('templates',			'German', 	'Vorlagen');
INSERT INTO txt VALUES ('templates',			'English', 	'Templates');
INSERT INTO txt VALUES ('creation_date',		'German', 	'Erstelldatum');
INSERT INTO txt VALUES ('creation_date',		'English', 	'Creation Date');
INSERT INTO txt VALUES ('report_template',		'German', 	'Reportvorlage');
INSERT INTO txt VALUES ('report_template',		'English', 	'Report Template');
INSERT INTO txt VALUES ('glob_no_obj',		    'German', 	'Gesamtzahl der Objekte');
INSERT INTO txt VALUES ('glob_no_obj',		    'English', 	'Global number of Objects');
INSERT INTO txt VALUES ('total_no_obj_mgt',		'German', 	'Gesamtzahl der Objekte pro Management');
INSERT INTO txt VALUES ('total_no_obj_mgt',		'English', 	'Total number of Objects per Management');
INSERT INTO txt VALUES ('no_rules_gtw',		    'German', 	'Anzahl Regeln pro Gateway');
INSERT INTO txt VALUES ('no_rules_gtw',		    'English', 	'Number of Rules per Gateway');
INSERT INTO txt VALUES ('negated',		        'German', 	'negated');
INSERT INTO txt VALUES ('negated',		        'English', 	'negiert');
INSERT INTO txt VALUES ('network_objects',		'German', 	'Netzwerkobjekte');
INSERT INTO txt VALUES ('network_objects',		'English', 	'Network objects');
INSERT INTO txt VALUES ('service_objects',		'German', 	'Serviceobjekte');
INSERT INTO txt VALUES ('service_objects',		'English', 	'Service objects');
INSERT INTO txt VALUES ('user_objects',		    'German', 	'Nutzerobjekte');
INSERT INTO txt VALUES ('user_objects',		    'English', 	'User objects');
INSERT INTO txt VALUES ('rules',		        'German', 	'Regeln');
INSERT INTO txt VALUES ('rules',		        'English', 	'Rules');
INSERT INTO txt VALUES ('no_of_rules',		    'German', 	'Anzahl Regeln');
INSERT INTO txt VALUES ('no_of_rules',		    'English', 	'Number of Rules');
INSERT INTO txt VALUES ('collapse_all',		    'German', 	'Alles einklappen');
INSERT INTO txt VALUES ('collapse_all',		    'English', 	'Collapse all');
INSERT INTO txt VALUES ('all',		            'German', 	'Alle');
INSERT INTO txt VALUES ('all',		            'English', 	'All');
INSERT INTO txt VALUES ('rule',		            'German', 	'Regel');
INSERT INTO txt VALUES ('rule',		            'English', 	'Rule');
INSERT INTO txt VALUES ('objects',		        'German', 	'Objekt');
INSERT INTO txt VALUES ('objects',		        'English', 	'Objects');
INSERT INTO txt VALUES ('change_time',		    'German', 	'&Auml;nderungszeit');
INSERT INTO txt VALUES ('change_time',		    'English', 	'Change Time');
INSERT INTO txt VALUES ('change_type',		    'German', 	'&Auml;nderungstyp');
INSERT INTO txt VALUES ('change_type',		    'English', 	'Change Type');
INSERT INTO txt VALUES ('source_zone',		    'German', 	'Quellzone');
INSERT INTO txt VALUES ('source_zone',		    'English', 	'Source Zone');
INSERT INTO txt VALUES ('destination_zone',		'German', 	'Zielzone');
INSERT INTO txt VALUES ('destination_zone',		'English', 	'Destination Zone');
INSERT INTO txt VALUES ('enabled',		        'German', 	'Aktiviert');
INSERT INTO txt VALUES ('enabled',		        'English', 	'Enabled');
INSERT INTO txt VALUES ('uid',		            'German', 	'UID');
INSERT INTO txt VALUES ('uid',		            'English', 	'UID');
INSERT INTO txt VALUES ('created',		        'German', 	'Angelegt');
INSERT INTO txt VALUES ('created',		        'English', 	'Created');
INSERT INTO txt VALUES ('last_modified',		'German', 	'Zuletzt ge&auml;ndert');
INSERT INTO txt VALUES ('last_modified',		'English', 	'Last Modified');
INSERT INTO txt VALUES ('first_hit',		    'German', 	'Erster Treffer');
INSERT INTO txt VALUES ('first_hit',		    'English', 	'First Hit');
INSERT INTO txt VALUES ('last_hit',		        'German', 	'Letzter Treffer');
INSERT INTO txt VALUES ('last_hit',		        'English', 	'Last Hit');
INSERT INTO txt VALUES ('ip',		            'German', 	'IP');
INSERT INTO txt VALUES ('ip',		            'English', 	'IP');
INSERT INTO txt VALUES ('zone',		            'German', 	'Zone');
INSERT INTO txt VALUES ('zone',		            'English', 	'Zone');
INSERT INTO txt VALUES ('last_changed',		    'German', 	'Zuletzt ge&auml;ndert');
INSERT INTO txt VALUES ('last_changed',		    'English', 	'Last changed');
INSERT INTO txt VALUES ('source_port',		    'German', 	'Quellport');
INSERT INTO txt VALUES ('source_port',		    'English', 	'Source Port');
INSERT INTO txt VALUES ('destination_port',		'German', 	'Zielport');
INSERT INTO txt VALUES ('destination_port',	    'English', 	'Destination Port');
INSERT INTO txt VALUES ('protocol',		        'German', 	'Protokoll');
INSERT INTO txt VALUES ('protocol',		        'English', 	'Protocol');
INSERT INTO txt VALUES ('code',		            'German', 	'Code');
INSERT INTO txt VALUES ('code',		            'English', 	'Code');
INSERT INTO txt VALUES ('timeout',		        'German', 	'Timeout');
INSERT INTO txt VALUES ('timeout',		        'English', 	'Timeout');
INSERT INTO txt VALUES ('real_name',		    'German', 	'Realer Name');
INSERT INTO txt VALUES ('real_name',		    'English', 	'Real Name');
INSERT INTO txt VALUES ('group_members',		'German', 	'Gruppenmitglieder');
INSERT INTO txt VALUES ('group_members',		'English', 	'Group Members');
INSERT INTO txt VALUES ('group_members_flat',	'German', 	'Gruppenmitglieder (gegl&auml;ttet)');
INSERT INTO txt VALUES ('group_members_flat',	'English', 	'Group Members (flattened)');
INSERT INTO txt VALUES ('object_fetch',         'German', 	'Abholen der Objekte');
INSERT INTO txt VALUES ('object_fetch',         'English', 	'Object Fetch');
INSERT INTO txt VALUES ('template_fetch',       'German', 	'Abholen der Vorlagen');
INSERT INTO txt VALUES ('template_fetch',       'English', 	'Report Template Fetch');
INSERT INTO txt VALUES ('save_template',        'German', 	'Speichern der Vorlage');
INSERT INTO txt VALUES ('save_template',        'English', 	'Save Report Template');
INSERT INTO txt VALUES ('edit_template',        'German', 	'&Auml;ndern der Vorlage');
INSERT INTO txt VALUES ('edit_template',        'English', 	'Edit Report Template');
INSERT INTO txt VALUES ('delete_template',      'German', 	'L&ouml;schen der Vorlage');
INSERT INTO txt VALUES ('delete_template',      'English', 	'Delete Report Template');

-- schedule
INSERT INTO txt VALUES ('schedule', 			'German',	'Terminplan');
INSERT INTO txt VALUES ('schedule', 			'English',	'Schedule');
INSERT INTO txt VALUES ('start_time', 			'German',	'Startzeit');
INSERT INTO txt VALUES ('start_time', 			'English',	'Start Time');
INSERT INTO txt VALUES ('repeat_interval', 		'German',	'Wiederholungsintervall');
INSERT INTO txt VALUES ('repeat_interval', 		'English',	'Repeat Interval');
INSERT INTO txt VALUES ('template',			    'German', 	'Vorlage');
INSERT INTO txt VALUES ('template',			    'English', 	'Template');
INSERT INTO txt VALUES ('owner',				'German', 	'Eigent&uuml;mer');
INSERT INTO txt VALUES ('owner',				'English', 	'Owner');
INSERT INTO txt VALUES ('active', 			    'German',	'Aktiv');
INSERT INTO txt VALUES ('active', 			    'English',	'Active');
INSERT INTO txt VALUES ('output_format', 		'German',	'Ausgabeformat');
INSERT INTO txt VALUES ('output_format', 		'English',	'Output Format');
INSERT INTO txt VALUES ('report_schedule', 		'German',	'Reporttermin');
INSERT INTO txt VALUES ('report_schedule', 		'English',	'Report Schedule');
INSERT INTO txt VALUES ('repeat_every', 		'German',	'Wiederholung (alle)');
INSERT INTO txt VALUES ('repeat_every', 		'English',	'Repeat Every');
INSERT INTO txt VALUES ('Never', 		        'German',	'Niemals');
INSERT INTO txt VALUES ('Never', 		        'English',	'Never');
INSERT INTO txt VALUES ('Days', 		        'German',	'Tag(e)');
INSERT INTO txt VALUES ('Days', 		        'English',	'Day(s)');
INSERT INTO txt VALUES ('Weeks', 		        'German',	'Woche(n)');
INSERT INTO txt VALUES ('Weeks', 		        'English',	'Week(s)');
INSERT INTO txt VALUES ('Months', 		        'German',	'Monat(e)');
INSERT INTO txt VALUES ('Months', 		        'English',	'Month(s)');
INSERT INTO txt VALUES ('Years', 		        'German',	'Jahr(e)');
INSERT INTO txt VALUES ('Years', 		        'English',	'Year(s)');
INSERT INTO txt VALUES ('save_scheduled_report','German',	'Termin speichern');
INSERT INTO txt VALUES ('save_scheduled_report','English',	'Save scheduled report');
INSERT INTO txt VALUES ('edit_scheduled_report','German',	'Termin bearbeiten');
INSERT INTO txt VALUES ('edit_scheduled_report','English',	'Edit scheduled report');
INSERT INTO txt VALUES ('delete_scheduled_report','German',	'Termin l&ouml;schen');
INSERT INTO txt VALUES ('delete_scheduled_report','English','Delete scheduled report');

-- archive
INSERT INTO txt VALUES ('download',				'German', 	'Herunterladen');
INSERT INTO txt VALUES ('download',				'English', 	'Download');
INSERT INTO txt VALUES ('generation_date',		'German', 	'Erstelldatum');
INSERT INTO txt VALUES ('generation_date',		'English', 	'Generation Date');
INSERT INTO txt VALUES ('generated_report',		'German', 	'Erstellter Report');
INSERT INTO txt VALUES ('generated_report',		'English', 	'Generated Report');
INSERT INTO txt VALUES ('download_as_csv',		'German', 	'Herunterladen als CSV');
INSERT INTO txt VALUES ('download_as_csv',		'English', 	'Download as CSV');
INSERT INTO txt VALUES ('download_as_pdf',		'German', 	'Herunterladen als PDF');
INSERT INTO txt VALUES ('download_as_pdf',		'English', 	'Download as PDF');
INSERT INTO txt VALUES ('download_as_html',		'German', 	'Herunterladen als HTML');
INSERT INTO txt VALUES ('download_as_html',		'English', 	'Download as HTML');
INSERT INTO txt VALUES ('download_as_json',		'German', 	'Herunterladen als JSON');
INSERT INTO txt VALUES ('download_as_json',		'English', 	'Download as JSON');
INSERT INTO txt VALUES ('fetch_report',		    'German', 	'Erstellten Report holen');
INSERT INTO txt VALUES ('fetch_report',		    'English', 	'Fetch downloads of generated report');
INSERT INTO txt VALUES ('delete_report',		'German', 	'Erstellten Report l&ouml;schen');
INSERT INTO txt VALUES ('delete_report',		'English', 	'Delete generated report');

-- recertification
INSERT INTO txt VALUES ('recertify',		    'German', 	'Rezertifizieren');
INSERT INTO txt VALUES ('recertify',		    'English', 	'Recertify');
INSERT INTO txt VALUES ('decertify',		    'German', 	'Dezertifizieren');
INSERT INTO txt VALUES ('decertify',		    'English', 	'Decertify');
INSERT INTO txt VALUES ('none',		            'German', 	'Sp&auml;ter');
INSERT INTO txt VALUES ('none',		            'English', 	'None');
INSERT INTO txt VALUES ('due_within',		    'German', 	'F&auml;llig in (Tagen)');
INSERT INTO txt VALUES ('due_within',		    'English', 	'Due within (days)');
INSERT INTO txt VALUES ('load_rules',		    'German', 	'Regeln laden');
INSERT INTO txt VALUES ('load_rules',		    'English', 	'Load Rules');
INSERT INTO txt VALUES ('execute_selected',		'German', 	'Ausgew&auml;hlte Aktionen ausf&uuml;hren');
INSERT INTO txt VALUES ('execute_selected',		'English', 	'Execute Selected Actions');
INSERT INTO txt VALUES ('next_recert',		    'German', 	'Datum n&auml;chste Rezertifizierung');
INSERT INTO txt VALUES ('next_recert',		    'English', 	'Next Recertification Date');
INSERT INTO txt VALUES ('last_recertifier',		'German', 	'Letzter Rezertifizierer');
INSERT INTO txt VALUES ('last_recertifier',		'English', 	'Last Recertifier Name');
INSERT INTO txt VALUES ('unknown',		        'German', 	'(unbekannt)');
INSERT INTO txt VALUES ('unknown',		        'English', 	'(unknown)');
INSERT INTO txt VALUES ('recerts_executed',		'German', 	'Durchgef&uuml;hrte Rezertifizierungen: ');
INSERT INTO txt VALUES ('recerts_executed',		'English', 	'Executed recertifications: ');
INSERT INTO txt VALUES ('decerts_executed',		'German', 	'Durchgef&uuml;hrte Dezertifizierungen: ');
INSERT INTO txt VALUES ('decerts_executed',		'English', 	'Executed decertifications: ');
INSERT INTO txt VALUES ('add_comment',          'German',   'Kommentar hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_comment',          'English',  'Add Comment');
INSERT INTO txt VALUES ('last_certify_date',    'German',   'Datum der letzten Rezertifizierung');
INSERT INTO txt VALUES ('last_certify_date',    'English',  'Last recertification date');
INSERT INTO txt VALUES ('marked_to_be_removed', 'German',   'Als zu l&ouml;schen markiert');
INSERT INTO txt VALUES ('marked_to_be_removed', 'English',  'Marked to be removed');
INSERT INTO txt VALUES ('decert_date',          'German',   'Dezertifizierungsdatum');
INSERT INTO txt VALUES ('decert_date',          'English',  'Decertification date');
INSERT INTO txt VALUES ('recert_comment',       'German',   'Zertifzierungskommentar');
INSERT INTO txt VALUES ('recert_comment',       'English',  'Certification comment');

-- settings
INSERT INTO txt VALUES ('devices',				'German', 	'Ger&auml;te');
INSERT INTO txt VALUES ('devices',				'English', 	'Devices');
INSERT INTO txt VALUES ('managements',			'German', 	'Managements');
INSERT INTO txt VALUES ('managements',			'English', 	'Managements');
INSERT INTO txt VALUES ('gateways',		    	'German', 	'Gateways');
INSERT INTO txt VALUES ('gateways',		    	'English', 	'Gateways');
INSERT INTO txt VALUES ('import_status',       	'German', 	'Importstatus');
INSERT INTO txt VALUES ('import_status',    	'English', 	'Import Status');
INSERT INTO txt VALUES ('authorization',		'German', 	'Berechtigungen');
INSERT INTO txt VALUES ('authorization',		'English', 	'Authorization');
INSERT INTO txt VALUES ('ldap_conns',	        'German', 	'LDAP-Verbindungen');
INSERT INTO txt VALUES ('ldap_conns',	        'English', 	'LDAP Connections');
INSERT INTO txt VALUES ('tenants',		        'German', 	'Mandanten');
INSERT INTO txt VALUES ('tenants',		        'English', 	'Tenants');
INSERT INTO txt VALUES ('users',		        'German', 	'Nutzer');
INSERT INTO txt VALUES ('users',		        'English', 	'Users');
INSERT INTO txt VALUES ('groups',		        'German', 	'Gruppen');
INSERT INTO txt VALUES ('groups',		        'English', 	'Groups');
INSERT INTO txt VALUES ('roles',		        'German', 	'Rollen');
INSERT INTO txt VALUES ('roles',		        'English', 	'Roles');
INSERT INTO txt VALUES ('defaults',		        'German', 	'Voreinstellungen');
INSERT INTO txt VALUES ('defaults',		        'English', 	'Defaults');
INSERT INTO txt VALUES ('standards',		    'German', 	'Standardeinstellungen');
INSERT INTO txt VALUES ('standards',		    'English', 	'Defaults');
INSERT INTO txt VALUES ('password_policy',      'German', 	'Passworteinstellungen');
INSERT INTO txt VALUES ('password_policy',      'English', 	'Password Policy');
INSERT INTO txt VALUES ('personal',             'German', 	'Pers&ouml;nlich');
INSERT INTO txt VALUES ('personal',             'English', 	'Personal');
INSERT INTO txt VALUES ('language',             'German', 	'Sprache');
INSERT INTO txt VALUES ('language',             'English', 	'Language');
INSERT INTO txt VALUES ('add_new_management',   'German', 	'Neues Management hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_new_management',   'English', 	'Add new management');
INSERT INTO txt VALUES ('edit_management',      'German', 	'Management bearbeiten');
INSERT INTO txt VALUES ('edit_management',      'English', 	'Edit Management');
INSERT INTO txt VALUES ('host',                 'German', 	'Host');
INSERT INTO txt VALUES ('host',                 'English', 	'Host');
INSERT INTO txt VALUES ('hostname',             'German', 	'Hostname');
INSERT INTO txt VALUES ('hostname',             'English', 	'Hostname');
INSERT INTO txt VALUES ('port',                 'German', 	'Port');
INSERT INTO txt VALUES ('port',                 'English', 	'Port');
INSERT INTO txt VALUES ('config_path',          'German', 	'Konfigurationspfad');
INSERT INTO txt VALUES ('config_path',          'English', 	'Config Path');
INSERT INTO txt VALUES ('importer_host',        'German', 	'Importer Host');
INSERT INTO txt VALUES ('importer_host',        'English', 	'Importer Host');
INSERT INTO txt VALUES ('import_disabled',      'German', 	'Import Deaktiviert');
INSERT INTO txt VALUES ('import_disabled',      'English', 	'Import Disabled');
INSERT INTO txt VALUES ('import_enabled',       'German', 	'Import Aktiviert');
INSERT INTO txt VALUES ('import_enabled',       'English', 	'Import Enabled');
INSERT INTO txt VALUES ('debug_level',          'German', 	'Debug Stufe');
INSERT INTO txt VALUES ('debug_level',          'English', 	'Debug Level');
INSERT INTO txt VALUES ('device_type',          'German', 	'Ger&auml;tetyp');
INSERT INTO txt VALUES ('device_type',          'English', 	'Device Type');
INSERT INTO txt VALUES ('import_user',          'German', 	'Import Nutzer');
INSERT INTO txt VALUES ('import_user',          'English', 	'Import User');
INSERT INTO txt VALUES ('login_secret',         'German', 	'Privater Schl&uuml;ssel');
INSERT INTO txt VALUES ('login_secret',         'English', 	'Login Secret');
INSERT INTO txt VALUES ('public_key',           'German', 	'&ouml;ffentlicher Schl&uuml;ssel');
INSERT INTO txt VALUES ('public_key',           'English', 	'Public Key');
-- INSERT INTO txt VALUES ('force_initial_import', 'German', 	'Initialen Import erzwingen');
-- INSERT INTO txt VALUES ('force_initial_import', 'English', 	'Force Initial Import');
INSERT INTO txt VALUES ('hide_in_ui',           'German', 	'Nicht sichtbar');
INSERT INTO txt VALUES ('hide_in_ui',           'English', 	'Hide in UI');
INSERT INTO txt VALUES ('add_new_gateway',      'German', 	'Neues Gateway hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_new_gateway',      'English', 	'Add new gateway');
INSERT INTO txt VALUES ('edit_gateway',         'German', 	'Gateway bearbeiten');
INSERT INTO txt VALUES ('edit_gateway',         'English', 	'Edit Gateway');
INSERT INTO txt VALUES ('management',           'German', 	'Management');
INSERT INTO txt VALUES ('management',           'English', 	'Management');
INSERT INTO txt VALUES ('rulebase',             'German', 	'Rulebase');
INSERT INTO txt VALUES ('rulebase',             'English', 	'Rulebase');
INSERT INTO txt VALUES ('details',              'German', 	'Details');
INSERT INTO txt VALUES ('details',              'English', 	'Details');
INSERT INTO txt VALUES ('import_status_details','German', 	'Importstatusdetails f&uuml;r ');
INSERT INTO txt VALUES ('import_status_details','English', 	'Import Status Details for ');
INSERT INTO txt VALUES ('last_incomplete',      'German', 	'Letzter Unvollendeter');
INSERT INTO txt VALUES ('last_incomplete',      'English', 	'Last Incomplete');
INSERT INTO txt VALUES ('rollback',             'German', 	'Zur&uuml;cksetzen');
INSERT INTO txt VALUES ('rollback',             'English', 	'Rollback');
INSERT INTO txt VALUES ('last_success',         'German', 	'Letzter Erfolg');
INSERT INTO txt VALUES ('last_success',         'English', 	'Last Success');
INSERT INTO txt VALUES ('last_import',          'German', 	'Letzter Import');
INSERT INTO txt VALUES ('last_import',          'English', 	'Last Import');
INSERT INTO txt VALUES ('last_successful_import','German', 	'Letzter erfolgreicher Import');
INSERT INTO txt VALUES ('last_successful_import','English', 'Last Successful Import');
INSERT INTO txt VALUES ('first_import',         'German', 	'Erster Import');
INSERT INTO txt VALUES ('first_import',         'English', 	'First Import');
INSERT INTO txt VALUES ('success',              'German', 	'Erfolg');
INSERT INTO txt VALUES ('success',              'English', 	'Success');
INSERT INTO txt VALUES ('errors',               'German', 	'Fehler');
INSERT INTO txt VALUES ('errors',               'English', 	'Errors');
INSERT INTO txt VALUES ('start',                'German', 	'Start');
INSERT INTO txt VALUES ('start',                'English', 	'Start');
INSERT INTO txt VALUES ('stop',                 'German', 	'Stop');
INSERT INTO txt VALUES ('stop',                 'English', 	'Stop');
INSERT INTO txt VALUES ('remove_sample_data',   'German', 	'Beispieldaten l&ouml;schen');
INSERT INTO txt VALUES ('remove_sample_data',	'English', 	'Remove Sample Data');
INSERT INTO txt VALUES ('refresh', 				'German',	'Neu anzeigen');
INSERT INTO txt VALUES ('refresh', 				'English',	'Refresh');
INSERT INTO txt VALUES ('add_new_user',			'German',	'Neuen Nutzer hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_new_user',			'English',	'Add new user');
INSERT INTO txt VALUES ('edit_user',		    'German',	'Nutzer bearbeiten');
INSERT INTO txt VALUES ('edit_user',		    'English',	'Edit User');
INSERT INTO txt VALUES ('reset_password',       'German', 	'Passwort zur&uuml;cksetzen');
INSERT INTO txt VALUES ('reset_password',       'English', 	'Reset Password');
INSERT INTO txt VALUES ('add_new_group',		'German',	'Neue Gruppe hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_new_group',		'English',	'Add New Group');
INSERT INTO txt VALUES ('edit_group',		    'German',	'Gruppe bearbeiten');
INSERT INTO txt VALUES ('edit_group',		    'English',	'Edit Group');
INSERT INTO txt VALUES ('delete_group',		    'German',	'Gruppe l&ouml;schen');
INSERT INTO txt VALUES ('delete_group',		    'English',	'Delete Group');
INSERT INTO txt VALUES ('add_user_to_group',	'German',	'Nutzer zu Gruppe hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_user_to_group',	'English',	'Add user to group');
INSERT INTO txt VALUES ('delete_user_from_group','German',	'Nutzer von Gruppe l&ouml;schen');
INSERT INTO txt VALUES ('delete_user_from_group','English',	'Delete user from group');
INSERT INTO txt VALUES ('add_user_to_role',	    'German',	'Nutzer/Gruppe zu Rolle hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_user_to_role',	    'English',	'Add user/group to role');
INSERT INTO txt VALUES ('delete_user_from_role','German',	'Nutzer/Gruppe von Rolle l&ouml;schen');
INSERT INTO txt VALUES ('delete_user_from_role','English',	'Delete user/group from role');
INSERT INTO txt VALUES ('get_user_from_ldap',   'German',	'Nutzer von LDAP holen');
INSERT INTO txt VALUES ('get_user_from_ldap',   'English',	'Get user from LDAP');
INSERT INTO txt VALUES ('delete_user',          'German', 	'Nutzer l&ouml;schen');
INSERT INTO txt VALUES ('delete_user',          'English', 	'Delete user');
INSERT INTO txt VALUES ('active_user',          'German', 	'Aktiver Nutzer');
INSERT INTO txt VALUES ('active_user',          'English', 	'Active User');
INSERT INTO txt VALUES ('user',                 'German', 	'Nutzer');
INSERT INTO txt VALUES ('user',                 'English', 	'User');
INSERT INTO txt VALUES ('group',                'German', 	'Gruppe');
INSERT INTO txt VALUES ('group',                'English', 	'Group');
INSERT INTO txt VALUES ('from_ldap',            'German', 	'von LDAP');
INSERT INTO txt VALUES ('from_ldap',            'English', 	'from LDAP');
INSERT INTO txt VALUES ('search_pattern',       'German', 	'Suchmuster');
INSERT INTO txt VALUES ('search_pattern',       'English', 	'Search Pattern');
INSERT INTO txt VALUES ('new_dn',               'German', 	'Neu (Dn)');
INSERT INTO txt VALUES ('new_dn',               'English', 	'New (Dn)');
INSERT INTO txt VALUES ('user_group',           'German', 	'Nutzer/Gruppe');
INSERT INTO txt VALUES ('user_group',           'English', 	'User/Group');
INSERT INTO txt VALUES ('add_gateway',          'German', 	'Gateway hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_gateway',          'English', 	'Add Gateway');
INSERT INTO txt VALUES ('delete_gateway',       'German', 	'Gateway l&ouml;schen');
INSERT INTO txt VALUES ('delete_gateway',       'English', 	'Delete Gateway');
INSERT INTO txt VALUES ('gateway',              'German', 	'Gateway');
INSERT INTO txt VALUES ('gateway',              'English', 	'Gateway');
INSERT INTO txt VALUES ('add_new_ldap',         'German', 	'Neue LDAP-Verbindung hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_new_ldap',         'English', 	'Add new LDAP connection');
INSERT INTO txt VALUES ('edit_ldap',            'German', 	'LDAP-Verbindung bearbeiten');
INSERT INTO txt VALUES ('edit_ldap',            'English', 	'Edit LDAP connection');
INSERT INTO txt VALUES ('address',              'German', 	'Adresse');
INSERT INTO txt VALUES ('address',              'English', 	'Address');
INSERT INTO txt VALUES ('tenant_level',         'German', 	'Mandantenebene');
INSERT INTO txt VALUES ('tenant_level',         'English', 	'Tenant Level');
INSERT INTO txt VALUES ('type',                 'German', 	'Typ');
INSERT INTO txt VALUES ('type',                 'English', 	'Type');
INSERT INTO txt VALUES ('pattern_length',       'German', 	'Musterl&auml;nge');
INSERT INTO txt VALUES ('pattern_length',       'English', 	'Pattern Length');
INSERT INTO txt VALUES ('user_search_path',     'German', 	'Suchpfad Nutzer');
INSERT INTO txt VALUES ('user_search_path',     'English', 	'User Search Path');
INSERT INTO txt VALUES ('role_search_path',     'German', 	'Suchpfad Rollen');
INSERT INTO txt VALUES ('role_search_path',     'English', 	'Role Search Path');
INSERT INTO txt VALUES ('group_search_path',    'German', 	'Suchpfad Gruppen');
INSERT INTO txt VALUES ('group_search_path',    'English', 	'Group Search Path');
INSERT INTO txt VALUES ('search_user',          'German', 	'Nutzer f&uuml;r Suche');
INSERT INTO txt VALUES ('search_user',          'English', 	'Search User');
INSERT INTO txt VALUES ('search_user_pwd',      'German', 	'Passwort Nutzer f&uuml;r Suche');
INSERT INTO txt VALUES ('search_user_pwd',      'English', 	'Search User Pwd');
INSERT INTO txt VALUES ('write_user',           'German', 	'Schreibender Nutzer');
INSERT INTO txt VALUES ('write_user',           'English', 	'Write User');
INSERT INTO txt VALUES ('write_user_pwd',       'German', 	'Passwort Schreibender Nutzer');
INSERT INTO txt VALUES ('write_user_pwd',       'English', 	'Write User Pwd');
INSERT INTO txt VALUES ('tenant',               'German', 	'Mandant');
INSERT INTO txt VALUES ('tenant',               'English', 	'Tenant');
INSERT INTO txt VALUES ('pwMinLength',          'German', 	'Mindestl&auml;nge');
INSERT INTO txt VALUES ('pwMinLength',          'English', 	'Min Length');
INSERT INTO txt VALUES ('pwUpperCaseRequired',  'German', 	'Grossbuchstaben enthalten');
INSERT INTO txt VALUES ('pwUpperCaseRequired',  'English', 	'Upper Case Required');
INSERT INTO txt VALUES ('pwLowerCaseRequired',  'German', 	'Kleinbuchstaben enthalten');
INSERT INTO txt VALUES ('pwLowerCaseRequired',  'English', 	'Lower Case Required');
INSERT INTO txt VALUES ('pwNumberRequired',     'German', 	'Ziffern enthalten');
INSERT INTO txt VALUES ('pwNumberRequired',     'English', 	'Number Required');
INSERT INTO txt VALUES ('pwSpecialCharactersRequired','German','Sonderzeichen enthalten (!?(){}=~$%&#*-+.,_)');
INSERT INTO txt VALUES ('pwSpecialCharactersRequired','English','Special Characters Required (!?(){}=~$%&#*-+.,_)');
INSERT INTO txt VALUES ('default_language',     'German', 	'Standardsprache');
INSERT INTO txt VALUES ('default_language',     'English', 	'Default Language');
INSERT INTO txt VALUES ('elementsPerFetch',     'German', 	'Pro Abruf geholte Elemente');
INSERT INTO txt VALUES ('elementsPerFetch',     'English', 	'Elements per fetch');
INSERT INTO txt VALUES ('maxInitialFetchesRightSidebar','German','Max initiale Abrufe rechte Randleiste');
INSERT INTO txt VALUES ('maxInitialFetchesRightSidebar','English','Max initial fetches right sidebar');
INSERT INTO txt VALUES ('autoFillRightSidebar', 'German', 	'Komplettes F&uuml;llen rechte Randleiste');
INSERT INTO txt VALUES ('autoFillRightSidebar', 'English', 	'Completely auto-fill right sidebar');
INSERT INTO txt VALUES ('dataRetentionTime',    'German', 	'Datenaufbewahrungszeit (in Tagen)');
INSERT INTO txt VALUES ('dataRetentionTime',    'English', 	'Data retention time (in days)');
INSERT INTO txt VALUES ('importSleepTime',      'German', 	'Importintervall (in Sekunden)');
INSERT INTO txt VALUES ('importSleepTime',      'English', 	'Import sleep time (in seconds)');
INSERT INTO txt VALUES ('recertificationPeriod','German', 	'Rezertifizierungsintervall (in Tagen)');
INSERT INTO txt VALUES ('recertificationPeriod','English',  'Recertification Period (in days)');
INSERT INTO txt VALUES ('recertificationNoticePeriod','German','Rezertifizierungserinnerungsintervall (in Tagen)');
INSERT INTO txt VALUES ('recertificationNoticePeriod','English','Recertification Notice Period (in days)');
INSERT INTO txt VALUES ('recertificationDisplayPeriod','German','Rezertifizierungsanzeigeintervall (in Tagen)');
INSERT INTO txt VALUES ('recertificationDisplayPeriod','English','Recertification Display Period (in days)');
INSERT INTO txt VALUES ('ruleRemovalGracePeriod','German', 	'Frist zum L&ouml;schen der Regeln (in Tagen)');
INSERT INTO txt VALUES ('ruleRemovalGracePeriod','English', 'Rule Removal Grace Period (in days)');
INSERT INTO txt VALUES ('commentRequired',      'German', 	'Kommentar Pflichtfeld');
INSERT INTO txt VALUES ('commentRequired',      'English',  'Comment Required');
INSERT INTO txt VALUES ('language_settings',    'German', 	'Spracheinstellungen');
INSERT INTO txt VALUES ('language_settings',    'English', 	'Language Settings');
INSERT INTO txt VALUES ('apply_changes',        'German', 	'&Auml;nderungen anwenden');
INSERT INTO txt VALUES ('apply_changes',        'English', 	'Apply Changes');
INSERT INTO txt VALUES ('description',          'German', 	'Beschreibung');
INSERT INTO txt VALUES ('description',          'English', 	'Description');
INSERT INTO txt VALUES ('users_groups',         'German', 	'Nutzer/Gruppen');
INSERT INTO txt VALUES ('users_groups',         'English', 	'Users/Groups');
INSERT INTO txt VALUES ('user_action',          'German', 	'Nutzeraktion');
INSERT INTO txt VALUES ('user_action',          'English', 	'User Action');
INSERT INTO txt VALUES ('group_action',         'German', 	'Gruppenaktion');
INSERT INTO txt VALUES ('group_action',         'English', 	'Group Action');
INSERT INTO txt VALUES ('email',                'German', 	'Email');
INSERT INTO txt VALUES ('email',                'English', 	'Email');
INSERT INTO txt VALUES ('last_login',           'German', 	'Letzte Anmeldung');
INSERT INTO txt VALUES ('last_login',           'English', 	'Last Login');
INSERT INTO txt VALUES ('last_pw_change',       'German', 	'Letzte Passwort&auml;nderung');
INSERT INTO txt VALUES ('last_pw_change',       'English', 	'Last Pwd Change');
INSERT INTO txt VALUES ('pwd_chg_req',          'German', 	'PW &Auml;nd. erf.');
INSERT INTO txt VALUES ('pwd_chg_req',          'English', 	'Pwd Chg Req');
INSERT INTO txt VALUES ('project',              'German', 	'Projekt');
INSERT INTO txt VALUES ('project',              'English', 	'Project');
INSERT INTO txt VALUES ('view_all_devices',     'German', 	'Sicht auf alle Ger&auml;te');
INSERT INTO txt VALUES ('view_all_devices',     'English', 	'View All Devices');
INSERT INTO txt VALUES ('superadmin',           'German', 	'Superadmin');
INSERT INTO txt VALUES ('superadmin',           'English', 	'Superadmin');
INSERT INTO txt VALUES ('tenant_action',        'German', 	'Mandantenaktion');
INSERT INTO txt VALUES ('tenant_action',        'English', 	'Tenant Action');
INSERT INTO txt VALUES ('gateway_action',       'German', 	'Gatewayaktion');
INSERT INTO txt VALUES ('gateway_action',       'English', 	'Gateway Action');
INSERT INTO txt VALUES ('role_handling',        'German', 	'Rollenverwaltung');
INSERT INTO txt VALUES ('role_handling',        'English', 	'Role handling');
INSERT INTO txt VALUES ('group_handling',       'German', 	'Gruppenverwaltung');
INSERT INTO txt VALUES ('group_handling',       'English', 	'Group handling');
INSERT INTO txt VALUES ('read_config',          'German', 	'Lesen der Konfiguration');
INSERT INTO txt VALUES ('read_config',          'English', 	'Read Config');
INSERT INTO txt VALUES ('change_default',       'German', 	'&Auml;ndern der Voreinstellungen');
INSERT INTO txt VALUES ('change_default',       'English', 	'Change Default Settings');
INSERT INTO txt VALUES ('change_policy',        'German', 	'&Auml;ndern der Passworteinstellungen');
INSERT INTO txt VALUES ('change_policy',        'English', 	'Change Password Policy');
INSERT INTO txt VALUES ('add_user',             'German', 	'Nutzer hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_user',             'English', 	'Add user');
INSERT INTO txt VALUES ('add_user_ldap',        'German', 	'Nutzer in LDAP hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_user_ldap',        'English', 	'Add user in LDAP');
INSERT INTO txt VALUES ('add_user_local',       'German', 	'Nutzer lokal hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_user_local',       'English', 	'Add user locally');
INSERT INTO txt VALUES ('update_user_ldap',     'German', 	'Nutzer in LDAP &auml;ndern');
INSERT INTO txt VALUES ('update_user_ldap',     'English', 	'Update user in LDAP');
INSERT INTO txt VALUES ('update_user_local',    'German', 	'Nutzer lokal &auml;ndern');
INSERT INTO txt VALUES ('update_user_local',    'English', 	'Update user locally');
INSERT INTO txt VALUES ('save_user',            'German', 	'Nutzer in LDAP speichern');
INSERT INTO txt VALUES ('save_user',            'English', 	'Save user in LDAP');
INSERT INTO txt VALUES ('delete_user_ldap',     'German', 	'Nutzer in LDAP l&ouml;schen');
INSERT INTO txt VALUES ('delete_user_ldap',     'English', 	'Delete user in LDAP');
INSERT INTO txt VALUES ('delete_user_local',    'German', 	'Nutzer lokal l&ouml;schen');
INSERT INTO txt VALUES ('delete_user_local',    'English', 	'Delete user locally');
INSERT INTO txt VALUES ('fetch_groups',         'German', 	'Gruppen abholen');
INSERT INTO txt VALUES ('fetch_groups',         'English', 	'Fetch Groups');
INSERT INTO txt VALUES ('fetch_users',          'German', 	'Nutzer abholen');
INSERT INTO txt VALUES ('fetch_users',          'English', 	'Fetch Users');
INSERT INTO txt VALUES ('fetch_users_ldap',     'German', 	'Nutzer aus LDAP holen');
INSERT INTO txt VALUES ('fetch_users_ldap',     'English', 	'Fetch Users from LDAP');
INSERT INTO txt VALUES ('fetch_users_local',    'German', 	'Nutzer aus API holen');
INSERT INTO txt VALUES ('fetch_users_local',    'English', 	'Fetch Users from API');
INSERT INTO txt VALUES ('fetch_tenants',        'German', 	'Mandanten abholen');
INSERT INTO txt VALUES ('fetch_tenants',        'English', 	'Fetch Tenants');
INSERT INTO txt VALUES ('save_group',           'German', 	'Gruppe in LDAP speichern');
INSERT INTO txt VALUES ('save_group',           'English', 	'Save group in LDAP');
INSERT INTO txt VALUES ('fetch_roles',          'German', 	'Rollen abholen');
INSERT INTO txt VALUES ('fetch_roles',          'English', 	'Fetch Roles');
INSERT INTO txt VALUES ('fetch_ldap_conn',      'German', 	'LDAP-Verbindungen holen');
INSERT INTO txt VALUES ('fetch_ldap_conn',      'English', 	'Fetch LDAP connections');
INSERT INTO txt VALUES ('search_users',         'German', 	'Nutzer suchen');
INSERT INTO txt VALUES ('search_users',         'English', 	'Search Users');
INSERT INTO txt VALUES ('new_user',             'German', 	'Neuer Nutzer');
INSERT INTO txt VALUES ('new_user',             'English', 	'New User');
INSERT INTO txt VALUES ('get_tenant_data',      'German', 	'Mandantendaten abholen');
INSERT INTO txt VALUES ('get_tenant_data',      'English', 	'Get tenant data');
INSERT INTO txt VALUES ('add_tenant',           'German', 	'Mandant hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_tenant',           'English', 	'Add tenant');
INSERT INTO txt VALUES ('delete_tenant',        'German', 	'Mandant l&ouml;schen');
INSERT INTO txt VALUES ('delete_tenant',        'English', 	'Delete tenant');
INSERT INTO txt VALUES ('add_tenant_ldap',      'German', 	'Mandant in LDAP hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_tenant_ldap',      'English', 	'Add tenant in LDAP');
INSERT INTO txt VALUES ('add_tenant_local',     'German', 	'Mandant lokal hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_tenant_local',     'English', 	'Add tenant locally');
INSERT INTO txt VALUES ('delete_tenant_ldap',   'German', 	'Mandant in LDAP l&ouml;schen');
INSERT INTO txt VALUES ('delete_tenant_ldap',   'English', 	'Delete tenant in LDAP');
INSERT INTO txt VALUES ('delete_tenant_local',  'German', 	'Mandant lokal l&ouml;schen');
INSERT INTO txt VALUES ('delete_tenant_local',  'English', 	'Delete tenant locally');
INSERT INTO txt VALUES ('add_device_to_tenant', 'German', 	'Gateway dem Mandanten zuordnen');
INSERT INTO txt VALUES ('add_device_to_tenant', 'English', 	'Add gateway to tenant');
INSERT INTO txt VALUES ('delete_device_from_tenant','German','Gateway vom Mandanten l&ouml;schen');
INSERT INTO txt VALUES ('delete_device_from_tenant','English','Delete gateway from tenant');
INSERT INTO txt VALUES ('delete_ldap_conn',     'German',   'LDAP-Verbindung l&ouml;schen');
INSERT INTO txt VALUES ('delete_ldap_conn',     'English',  'Delete Ldap Connection');
INSERT INTO txt VALUES ('save_ldap_conn',       'German',   'LDAP-Verbindung speichern');
INSERT INTO txt VALUES ('save_ldap_conn',       'English',  'Save Ldap Connection');
INSERT INTO txt VALUES ('fetch_managements',    'German', 	'Managements abholen');
INSERT INTO txt VALUES ('fetch_managements',    'English', 	'Fetch Managements');
INSERT INTO txt VALUES ('delete_management',    'German', 	'Management l&ouml;schen');
INSERT INTO txt VALUES ('delete_management',    'English', 	'Delete Management');
INSERT INTO txt VALUES ('save_management',      'German', 	'Management speichern');
INSERT INTO txt VALUES ('save_management',      'English', 	'Save Management');
INSERT INTO txt VALUES ('fetch_gateways',       'German', 	'Gateways abholen');
INSERT INTO txt VALUES ('fetch_gateways',       'English', 	'Fetch Gateways');
INSERT INTO txt VALUES ('save_gateway',         'German', 	'Gateway speichern');
INSERT INTO txt VALUES ('save_gateway',         'English', 	'Save Gateway');
INSERT INTO txt VALUES ('add_device_to_tenant0','German', 	'Ger&auml;t zu Mandant 0 zuordnen');
INSERT INTO txt VALUES ('add_device_to_tenant0','English', 	'Add device to tenant 0');
INSERT INTO txt VALUES ('fetch_import_status',  'German', 	'Importstatus abholen');
INSERT INTO txt VALUES ('fetch_import_status',  'English', 	'Fetch Import Status');
INSERT INTO txt VALUES ('rollback_import',      'German', 	'Import zur&uuml;cksetzen');
INSERT INTO txt VALUES ('rollback_import',      'English', 	'Rollback Import');
INSERT INTO txt VALUES ('report_settings',      'German', 	'Reporteinstellungen');
INSERT INTO txt VALUES ('report_settings',      'English', 	'Report Settings');
INSERT INTO txt VALUES ('change_language',      'German', 	'&Auml;ndern der Passworteinstellungen');
INSERT INTO txt VALUES ('change_language',      'English', 	'Change Language');
INSERT INTO txt VALUES ('recert_settings',      'German', 	'Rezertifizierungseinstellungen');
INSERT INTO txt VALUES ('recert_settings',      'English', 	'Recertification Settings');

-- help pages
INSERT INTO txt VALUES ('filter_syntax',        'German', 	'Filtersyntax');
INSERT INTO txt VALUES ('filter_syntax',        'English', 	'Filter Syntax');
INSERT INTO txt VALUES ('report_data_output',   'German', 	'Reportdatenausgabe');
INSERT INTO txt VALUES ('report_data_output',   'English', 	'Report Data Output');
INSERT INTO txt VALUES ('left_sidebar',         'German', 	'Linke Randleiste');
INSERT INTO txt VALUES ('left_sidebar',         'English', 	'Left Sidebar');
INSERT INTO txt VALUES ('right_sidebar',        'German', 	'Rechte Randleiste');
INSERT INTO txt VALUES ('right_sidebar',        'English', 	'Right Sidebar');
INSERT INTO txt VALUES ('introduction',         'German', 	'Einleitung');
INSERT INTO txt VALUES ('introduction',         'English', 	'Introduction');
INSERT INTO txt VALUES ('graphql',              'German', 	'GraphQL');
INSERT INTO txt VALUES ('graphql',              'English', 	'GraphQL');
INSERT INTO txt VALUES ('hasura',               'German', 	'Hasura');
INSERT INTO txt VALUES ('hasura',               'English', 	'Hasura');
INSERT INTO txt VALUES ('security',             'German', 	'Sicherheit / JWT');
INSERT INTO txt VALUES ('security',             'English', 	'Security / JWT');
INSERT INTO txt VALUES ('further_reading',      'German', 	'Weiterf&uuml;hrendes');
INSERT INTO txt VALUES ('further_reading',      'English', 	'Further reading');
INSERT INTO txt VALUES ('basic_commands',       'German', 	'Wichtige Kommandos');
INSERT INTO txt VALUES ('basic_commands',       'English', 	'Basic Commands');
INSERT INTO txt VALUES ('query',                'German', 	'Wichtige Querys');
INSERT INTO txt VALUES ('query',                'English', 	'Basic Query');
INSERT INTO txt VALUES ('mutation',             'German', 	'Wichtige Mutation');
INSERT INTO txt VALUES ('mutation',             'English', 	'Basic Mutation');

-- text codes (roughly) categorized: 
-- U: user texts (explanation or confirmation texts)
-- E: error texts
-- A: Api errors
-- T: texts from external sources (Ldap, other database tables)
-- H: help pages
-- 0000-0999: General
-- 1000-1999: Reporting
-- 2000-2999: Scheduling
-- 3000-3999: Archive
-- 4000-4999: Recertification
-- 5000-5999: Settings
--            5000-5099: general
--            5100-5199: devices
--            5200-5299: authorization
--            5300-5399: defaults
--            5400-5499: personal settings
-- 6000-6999: API

-- user messages
INSERT INTO txt VALUES ('U1002', 'German',  'Sind sie sicher, dass sie folgende Reportvorlage l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U1002', 'English', 'Do you really want to delete report template');

INSERT INTO txt VALUES ('U2002', 'German',  'Sind sie sicher, dass sie folgenden Reporttermin l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U2002', 'English', 'Do you really want to delete report schedule ');

INSERT INTO txt VALUES ('U3002', 'German',  'Sind sie sicher, dass sie folgenden Report l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U3002', 'English', 'Do you really want to delete generated report ');

INSERT INTO txt VALUES ('U5001', 'German',  'Bitte eine Einstellung ausw&auml;hlen.');
INSERT INTO txt VALUES ('U5001', 'English', 'Please choose a setting.');
INSERT INTO txt VALUES ('U5011', 'German',  'Verwaltung der technischen Komponenten (nur f&uuml;r Admin)');
INSERT INTO txt VALUES ('U5011', 'English', 'Administration of technical components (only by admin)');
INSERT INTO txt VALUES ('U5012', 'German',  'Verwaltung der Nutzerautorisierung (nur f&uuml;r Admin)');
INSERT INTO txt VALUES ('U5012', 'English', 'User authorization management (only by admin)');
INSERT INTO txt VALUES ('U5013', 'German',  'Verwaltung der Voreinstellungen (nur f&uuml;r Admin)');
INSERT INTO txt VALUES ('U5013', 'English', 'Administration of default settings (only by admin)');
INSERT INTO txt VALUES ('U5014', 'German',  'Pers&ouml;nliche Nutzereinstellungen');
INSERT INTO txt VALUES ('U5014', 'English', 'Personal settings for the individual user');

INSERT INTO txt VALUES ('U5101', 'German',  'Sind sie sicher, dass sie folgendes Management l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5101', 'English', 'Are you sure you want to delete management: ');
INSERT INTO txt VALUES ('U5102', 'German',  'L&ouml;scht alle Beispielmanagements (auf "_demo" endend) und alle zugeordneten Gateways');
INSERT INTO txt VALUES ('U5102', 'English', 'Deletes all sample managements (ending with "_demo") and related Gateways');
INSERT INTO txt VALUES ('U5103', 'German',  'Sind sie sicher, dass sie folgendes Gateway l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5103', 'English', 'Are you sure you want to delete gateway: ');
INSERT INTO txt VALUES ('U5111', 'German',  'Verwaltung aller verbundenen Managements');
INSERT INTO txt VALUES ('U5111', 'English', 'Administrate the connected managements');
INSERT INTO txt VALUES ('U5112', 'German',  'Verwaltung aller verbundenen Gateways');
INSERT INTO txt VALUES ('U5112', 'English', 'Administrate the connected gateways');
INSERT INTO txt VALUES ('U5113', 'German',  'Statusanzeige aller Importjobs. M&ouml;glichkeit zum Rollback, wenn n&ouml;tig');
INSERT INTO txt VALUES ('U5113', 'English', 'Show the status of all import jobs. Possibility to rollback if necessary');

INSERT INTO txt VALUES ('U5201', 'German',  'Sind sie sicher, dass sie folgenden Nutzer l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5201', 'English', 'Are you sure you want to delete user: ');
INSERT INTO txt VALUES ('U5202', 'German',  ' Nutzer von externen LDAPs werden nur lokal gel&ouml;scht.');
INSERT INTO txt VALUES ('U5202', 'English', ' Users from external LDAPs are only deleted locally.');
INSERT INTO txt VALUES ('U5203', 'German',  'L&ouml;scht alle Beispielnutzer (auf "_demo" endend) im lokalen LDAP.');
INSERT INTO txt VALUES ('U5203', 'English', 'Deletes all sample users (ending with "_demo") in local LDAP.');
INSERT INTO txt VALUES ('U5204', 'German',  'Sind sie sicher, dass sie folgende Gruppe l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5204', 'English', 'Are you sure you want to delete group: ');
INSERT INTO txt VALUES ('U5205', 'German',  'L&ouml;scht alle Beispielgruppen (auf "_demo" endend) im lokalen LDAP.');
INSERT INTO txt VALUES ('U5205', 'English', 'Deletes all sample groups (ending with "_demo") in local LDAP.');
INSERT INTO txt VALUES ('U5206', 'German',  '(es k&ouml;nnen keine Nutzer zugeordnet werden)');
INSERT INTO txt VALUES ('U5206', 'English', '(no users can be assigned)');
INSERT INTO txt VALUES ('U5207', 'German',  '(kann nicht gel&ouml;scht werden)');
INSERT INTO txt VALUES ('U5207', 'English', '(cannot be deleted)');
INSERT INTO txt VALUES ('U5208', 'German',  '(zu allen Gateways zugeordnet)');
INSERT INTO txt VALUES ('U5208', 'English', '(linked to all gateways)');
INSERT INTO txt VALUES ('U5209', 'German',  'L&ouml;scht alle Beispielmandanten (auf "_demo" endend) im lokalen LDAP.');
INSERT INTO txt VALUES ('U5209', 'English', 'Deletes all sample tenants (ending with "_demo") in local LDAP.');
INSERT INTO txt VALUES ('U5210', 'German',  'Sind sie sicher, dass sie folgenden Mandanten l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5210', 'English', 'Are you sure you want to delete tenant: ');
INSERT INTO txt VALUES ('U5211', 'German',  'Verwaltung aller LDAP-Verbindungen');
INSERT INTO txt VALUES ('U5211', 'English', 'Administrate all LDAP connections');
INSERT INTO txt VALUES ('U5212', 'German',  'Anzeige aller Mandanten und Verkn&uuml;pfungen zu den Gateways');
INSERT INTO txt VALUES ('U5212', 'English', 'Show all tenants and link them to gateways');
INSERT INTO txt VALUES ('U5213', 'German',  'Anzeige und Verwaltung aller Nutzer des lokalen LDAPs. Anzeige aller Nutzer von externen LDAPs, die schon angemeldet waren');
INSERT INTO txt VALUES ('U5213', 'English', 'Show and administrate all users from internal LDAP. Show users from external LDAPs who have already logged in');
INSERT INTO txt VALUES ('U5214', 'German',  'Anzeige und Verwaltung aller Nutzergruppen (internes LDAP)');
INSERT INTO txt VALUES ('U5214', 'English', 'Show and administrate all user groups (internal LDAP)');
INSERT INTO txt VALUES ('U5215', 'German',  'Anzeige und Verwaltung aller Rollen (internes LDAP)');
INSERT INTO txt VALUES ('U5215', 'English', 'Show and assign all user roles (internal LDAP)');

INSERT INTO txt VALUES ('U5301', 'German',  'Einstellungen ge&auml;ndert.');
INSERT INTO txt VALUES ('U5301', 'English', 'Settings changed.');
INSERT INTO txt VALUES ('U5302', 'German',  'Einstellungen ge&auml;ndert.');
INSERT INTO txt VALUES ('U5302', 'English', 'Policy changed.');
INSERT INTO txt VALUES ('U5303', 'German',  '* Einstellungen k&ouml;nnen vom Nutzer in den pers&ouml;nlichen Einstellungen &uuml;berschrieben werden.');
INSERT INTO txt VALUES ('U5303', 'English', '* Settings can be overwritten by user in personal settings.');
INSERT INTO txt VALUES ('U5311', 'German',  'Verwaltung der Standard-Voreinstellungen f&uuml;r alle Nutzer und einige technische Parameter');
INSERT INTO txt VALUES ('U5311', 'English', 'Set default values for all users and some technical parameters');
INSERT INTO txt VALUES ('U5312', 'German',  'Verwaltung der Passwortregeln');
INSERT INTO txt VALUES ('U5312', 'English', 'Set the policy for all user passwords');

INSERT INTO txt VALUES ('U5401', 'German',  'Passwort ge&auml;ndert.');
INSERT INTO txt VALUES ('U5401', 'English', 'Password changed.');
INSERT INTO txt VALUES ('U5411', 'German',  '&Auml;nderung des pers&ouml;nlichen Anmeldepassworts');
INSERT INTO txt VALUES ('U5411', 'English', 'Change your personal login password');
INSERT INTO txt VALUES ('U5412', 'German',  'Einstellung der bevorzugten Sprache');
INSERT INTO txt VALUES ('U5412', 'English', 'Set your preferred language');
INSERT INTO txt VALUES ('U5413', 'German',  'Anpassung der pers&ouml;nlichen Reporteinstellungen');
INSERT INTO txt VALUES ('U5413', 'English', 'Adapt your personal reporting settings');
INSERT INTO txt VALUES ('U5414', 'German',  'Anpassung der pers&ouml;nlichen Rezertifizierungseinstellungen');
INSERT INTO txt VALUES ('U5414', 'English', 'Adapt your personal recertification settings');

-- error messages
INSERT INTO txt VALUES ('E0001', 'German',  'Nicht klassifizierter Fehler: ');
INSERT INTO txt VALUES ('E0001', 'English', 'Unclassified error: ');
INSERT INTO txt VALUES ('E0002', 'German',  'F&uuml;r Details in den Log-Dateien nachsehen!');
INSERT INTO txt VALUES ('E0002', 'English', 'See log for details!');
INSERT INTO txt VALUES ('E0003', 'German',  'Sitzung abgelaufen - bitte erneut anmelden');
INSERT INTO txt VALUES ('E0003', 'English', 'Session expired - please log in again');
INSERT INTO txt VALUES ('E0004', 'German',  'Ungen&uuml;gende Zugriffsrechte');
INSERT INTO txt VALUES ('E0004', 'English', 'Insufficient access rights');
INSERT INTO txt VALUES ('E0011', 'German',  'G&uuml;ltiger Nutzer aber keine Rolle zugeordnet. Bitte Administrator kontaktieren');
INSERT INTO txt VALUES ('E0011', 'English', 'Valid user but no role assigned. Please contact administrator');
INSERT INTO txt VALUES ('E0012', 'German',  'M&ouml;glicherweise ist das Backend (API oder Datenbank) nicht erreichbar. Bitte Administrator kontaktieren');
INSERT INTO txt VALUES ('E0012', 'English', 'Maybe backend (API or database) is unreachable. Please contact administrator');

INSERT INTO txt VALUES ('E1001', 'German',  'Vor dem Generieren des Reports bitte mindestens ein Device auf der linken Seite ausw&auml;hlen');
INSERT INTO txt VALUES ('E1001', 'English', 'Please select at least one device in the left side-bar before generating a report');
INSERT INTO txt VALUES ('E1002', 'German',  'Kein Report vorhanden zum Exportieren. Bitte zuerst Report generieren!');
INSERT INTO txt VALUES ('E1002', 'English', 'No generated report to export. Please generate report first!');

INSERT INTO txt VALUES ('E4001', 'German',  'Bitte Kommentar hinzuf&uuml;gen');
INSERT INTO txt VALUES ('E4001', 'English', 'Please insert a comment');

INSERT INTO txt VALUES ('E5101', 'German',  'L&ouml;schen des Managements nicht erlaubt, da noch Gateways zugeordnet sind. Diese zuerst l&ouml;schen wenn m&ouml;glich');
INSERT INTO txt VALUES ('E5101', 'English', 'Deletion of management not allowed as there are related Gateways. Delete them first if possible');
INSERT INTO txt VALUES ('E5102', 'German',  'Bitte alle Pflichtfelder ausf&uuml;llen');
INSERT INTO txt VALUES ('E5102', 'English', 'Please fill all mandatory fields');
INSERT INTO txt VALUES ('E5103', 'German',  'Port muss im Bereich 1 - 65535 liegen');
INSERT INTO txt VALUES ('E5103', 'English', 'Port has to be in the range 1 - 65535');
INSERT INTO txt VALUES ('E5104', 'German',  'Wert der Debug Stufe muss im Bereich 0 - 9 liegen');
INSERT INTO txt VALUES ('E5104', 'English', 'Value for Debug Level has to be in the range 0 - 9');
INSERT INTO txt VALUES ('E5105', 'German',  'Es gibt bereits ein Management mit derselben Konfiguration und Import aktiviert');
INSERT INTO txt VALUES ('E5105', 'English', 'There is already a management in the same configuration with import enabled');
INSERT INTO txt VALUES ('E5111', 'German',  'Es gibt bereits ein Gateway mit derselben Konfiguration und Import aktiviert');
INSERT INTO txt VALUES ('E5111', 'English', 'There is already a gateway in the same configuration with import enabled');

INSERT INTO txt VALUES ('E5207', 'German',  'kein internes LDAP gefunden');
INSERT INTO txt VALUES ('E5207', 'English', 'No internal LDAP found');
INSERT INTO txt VALUES ('E5208', 'German',  'Keine Nutzer gefunden');
INSERT INTO txt VALUES ('E5208', 'English', 'No users found');
INSERT INTO txt VALUES ('E5211', 'German',  'Dn und Passwort m&uuml;ssen gef&uuml;llt sein');
INSERT INTO txt VALUES ('E5211', 'English', 'Dn and Password have to be filled');
INSERT INTO txt VALUES ('E5212', 'German',  'Unbekannter Mandant');
INSERT INTO txt VALUES ('E5212', 'English', 'Unknown tenant');
INSERT INTO txt VALUES ('E5213', 'German',  'Nutzer konnte zum LDAP nicht hinzugef&uuml;gt werden');
INSERT INTO txt VALUES ('E5213', 'English', 'No user could be added to LDAP');
INSERT INTO txt VALUES ('E5214', 'German',  'Nutzer konnte im LDAP nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E5214', 'English', 'User could not be updated in LDAP');
INSERT INTO txt VALUES ('E5215', 'German',  'L&ouml;schen des eigenen Nutzers nicht erlaubt');
INSERT INTO txt VALUES ('E5215', 'English', 'Self deletion of user not allowed');
INSERT INTO txt VALUES ('E5216', 'German',  'Nutzer konnte im LDAP nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5216', 'English', 'User could not be deleted in LDAP');
INSERT INTO txt VALUES ('E5217', 'German',  'Passwort kann nur f&uuml;r interne Nutzer zur&uuml;ckgesetzt werden');
INSERT INTO txt VALUES ('E5217', 'English', 'Password can only be reset for internal users');
INSERT INTO txt VALUES ('E5218', 'German',  'Passwort muss ausgef&uuml;llt werden');
INSERT INTO txt VALUES ('E5218', 'English', 'Password has to be filled');
INSERT INTO txt VALUES ('E5219', 'German',  'Passwort konnte nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E5219', 'English', 'Password could not be updated');
INSERT INTO txt VALUES ('E5220', 'German',  'Sie sind als Beispielnutzer angemeldet. L&ouml;schen ist nicht m&ouml;glich');
INSERT INTO txt VALUES ('E5220', 'English', 'You are logged in as sample user. Delete not possible');
INSERT INTO txt VALUES ('E5221', 'German',  'Nutzer konnte nicht von allen Rollen und Gruppen entfernt werden');
INSERT INTO txt VALUES ('E5221', 'English', 'User could not be removed from all roles and groups');
INSERT INTO txt VALUES ('E5231', 'German',  'Keine Gruppen gefunden');
INSERT INTO txt VALUES ('E5231', 'English', 'No groups found');
INSERT INTO txt VALUES ('E5234', 'German',  'Name muss ausgef&uuml;llt sein');
INSERT INTO txt VALUES ('E5234', 'English', 'Name has to be filled');
INSERT INTO txt VALUES ('E5235', 'German',  'Name ist schon vorhanden');
INSERT INTO txt VALUES ('E5235', 'English', 'Name is already existing');
INSERT INTO txt VALUES ('E5236', 'German',  'Es konnte keine Gruppe hinzugef&uuml;gt werden');
INSERT INTO txt VALUES ('E5236', 'English', 'No group could be added');
INSERT INTO txt VALUES ('E5237', 'German',  'Gruppe konnte nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E5237', 'English', 'Group could not be updated');
INSERT INTO txt VALUES ('E5238', 'German',  'L&ouml;schen der Gruppe nicht erlaubt, da noch Nutzer zugewiesen sind');
INSERT INTO txt VALUES ('E5238', 'English', 'Deletion of group not allowed as there are still users assigned');
INSERT INTO txt VALUES ('E5239', 'German',  'Gruppe konnte nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5239', 'English', 'Group could not be deleted');
INSERT INTO txt VALUES ('E5240', 'German',  'Bitte einen Nutzer ausw&auml;hlen');
INSERT INTO txt VALUES ('E5240', 'English', 'Please select a user');
INSERT INTO txt VALUES ('E5241', 'German',  'Nutzer ist schon zu dieser Gruppe zugeordnet');
INSERT INTO txt VALUES ('E5241', 'English', 'User is already assigned to this group');
INSERT INTO txt VALUES ('E5242', 'German',  'Nutzer konnte nicht zur Gruppe im LDAP zugeordnet werden');
INSERT INTO txt VALUES ('E5242', 'English', 'User could not be added to group in LDAP');
INSERT INTO txt VALUES ('E5243', 'German',  'Nutzer konnte von keiner Gruppe in den LDAPs gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5243', 'English', 'User could not be removed from any group in LDAPs');
INSERT INTO txt VALUES ('E5244', 'German',  'Zu l&ouml;schender Nutzer nicht gefunden');
INSERT INTO txt VALUES ('E5244', 'English', 'User to delete not found');
INSERT INTO txt VALUES ('E5245', 'German',  'Nicht-Beispielnutzer zur Gruppe zugeordnet. L&ouml;schen nicht m&ouml;glich');
INSERT INTO txt VALUES ('E5245', 'English', 'Non-sample user assigned to group. Delete not possible');
INSERT INTO txt VALUES ('E5251', 'German',  'Keine Rollen gefunden');
INSERT INTO txt VALUES ('E5251', 'English', 'No roles found');
INSERT INTO txt VALUES ('E5252', 'German',  'Bitte nutzen sie ein Suchmuster mit Mindestl&auml;nge ');
INSERT INTO txt VALUES ('E5252', 'English', 'Please use pattern of min length ');
INSERT INTO txt VALUES ('E5253', 'German',  'Bitte einen richtigen Nutzer definieren');
INSERT INTO txt VALUES ('E5253', 'English', 'Please define a proper user');
INSERT INTO txt VALUES ('E5254', 'German',  'Nutzer ist dieser Rolle schon zugewiesen');
INSERT INTO txt VALUES ('E5254', 'English', 'User is already assigned to this role');
INSERT INTO txt VALUES ('E5255', 'German',  'Nutzer konnte der Rolle im LDAP nicht zugewiesen werden');
INSERT INTO txt VALUES ('E5255', 'English', 'User could not be added to role in LDAP');
INSERT INTO txt VALUES ('E5256', 'German',  'Der letzte Admin kann nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5256', 'English', 'Last admin user cannot be deleted');
INSERT INTO txt VALUES ('E5257', 'German',  'Nutzer konnte von keiner Rolle in den LDAPs gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5257', 'English', 'User could not be removed from any role in ldaps');
INSERT INTO txt VALUES ('E5258', 'German',  'Zu l&ouml;schender Nutzer nicht gefunden');
INSERT INTO txt VALUES ('E5258', 'English', 'User to delete not found');
INSERT INTO txt VALUES ('E5261', 'German',  'L&ouml;schen der LDAP-Verbindung nicht erlaubt, da sie die letzte ist');
INSERT INTO txt VALUES ('E5261', 'English', 'Deletion of LDAP Connection not allowed as it is the last one');
INSERT INTO txt VALUES ('E5262', 'German',  'L&ouml;schen der LDAP-Verbindung nicht erlaubt, da sie einen Rollensuchpfad enth&auml;lt. Wenn m&ouml;glich diesen zuerst l&ouml;schen');
INSERT INTO txt VALUES ('E5262', 'English', 'Deletion of LDAP Connection not allowed as it contains role search path. Delete it first if possible');
INSERT INTO txt VALUES ('E5263', 'German',  'Musterl&auml;nge muss >= 0 sein');
INSERT INTO txt VALUES ('E5263', 'English', 'Pattern Length has to be >= 0');
INSERT INTO txt VALUES ('E5264', 'German',  'Es gibt bereits eine LDAP-Verbindung mit derselben Adresse und Port');
INSERT INTO txt VALUES ('E5264', 'English', 'There is already an LDAP connection with the same address and port');
INSERT INTO txt VALUES ('E5271', 'German',  'Keine Gateways zum Hinzuf&uuml;gen gefunden');
INSERT INTO txt VALUES ('E5271', 'English', 'No remaining gateways found to add');
INSERT INTO txt VALUES ('E5272', 'German',  'Keine Gateways zum L&ouml;schen gefunden');
INSERT INTO txt VALUES ('E5272', 'English', 'No remaining gateways found to delete');
INSERT INTO txt VALUES ('E5281', 'German',  'Mandant konnte im LDAP nicht angelegt werden');
INSERT INTO txt VALUES ('E5281', 'English', 'Tenant could not be added in LDAP');
INSERT INTO txt VALUES ('E5282', 'German',  'Mandant konnte im LDAP nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5282', 'English', 'Tenant could not be deleted in LDAP');
INSERT INTO txt VALUES ('E5283', 'German',  'Mindestens ein Nutzer zum Mandanten zugeordnet. Bitte zuerst l&ouml;schen!');
INSERT INTO txt VALUES ('E5283', 'English', 'At least one user assigned to tenant. Delete first!');

INSERT INTO txt VALUES ('E5301', 'German',  'Konfiguration f&uuml;r Standardsprache konnte nicht gelesen werden: Wert auf Englisch gesetzt');
INSERT INTO txt VALUES ('E5301', 'English', 'Error reading Config for default language: taking default English');
INSERT INTO txt VALUES ('E5302', 'German',  'Konfiguration konnte nicht gelesen werden, Standardwert gesetzt: ');
INSERT INTO txt VALUES ('E5302', 'English', 'Error reading Config, taking default: ');

INSERT INTO txt VALUES ('E5401', 'German',  'Bitte das alte Passwort eintragen');
INSERT INTO txt VALUES ('E5401', 'English', 'Please insert the old password');
INSERT INTO txt VALUES ('E5402', 'German',  'Bitte ein neues Passwort eintragen');
INSERT INTO txt VALUES ('E5402', 'English', 'Please insert a new password');
INSERT INTO txt VALUES ('E5403', 'German',  'Das neue Passwort muss sich vom alten unterscheiden');
INSERT INTO txt VALUES ('E5403', 'English', 'New password must differ from old one');
INSERT INTO txt VALUES ('E5404', 'German',  'Bitte das neue Passwort wiederholen');
INSERT INTO txt VALUES ('E5404', 'English', 'Please insert the same new password twice');
INSERT INTO txt VALUES ('E5411', 'German',  'Passwort erfordert eine Mindestl&auml;nge von ');
INSERT INTO txt VALUES ('E5411', 'English', 'Password must have a minimal length of ');
INSERT INTO txt VALUES ('E5412', 'German',  'Passwort muss mindestens einen Grossbuchstaben enthalten');
INSERT INTO txt VALUES ('E5412', 'English', 'Password must contain at least one upper case character');
INSERT INTO txt VALUES ('E5413', 'German',  'Passwort muss mindestens einen Kleinbuchstaben enthalten');
INSERT INTO txt VALUES ('E5413', 'English', 'Password must contain at least one lower case character');
INSERT INTO txt VALUES ('E5414', 'German',  'Passwort muss mindestens eine Ziffer enthalten');
INSERT INTO txt VALUES ('E5414', 'English', 'Password must contain at least one number');
INSERT INTO txt VALUES ('E5415', 'German',  'Passwort muss mindestens ein Sonderzeichen enthalten (!?(){}=~$%&#*-+.,_)');
INSERT INTO txt VALUES ('E5415', 'English', 'Password must contain at least one special character (!?(){}=~$%&#*-+.,_)');
INSERT INTO txt VALUES ('E5421', 'German',  'Schl&uuml;ssel nicht gefunden oder Wert nicht konvertierbar: Wert wird gesetzt auf: ');
INSERT INTO txt VALUES ('E5421', 'English', 'Key not found or could not convert value to int: taking value: ');

-- errors from Api
INSERT INTO txt VALUES ('A0001', 'German',  'Ung&uuml;ltige Anmeldedaten. Nutzername darf nicht leer sein');
INSERT INTO txt VALUES ('A0001', 'English', 'Invalid credentials. Username must not be empty');
INSERT INTO txt VALUES ('A0002', 'German',  'Ung&uuml;ltige Anmeldedaten');
INSERT INTO txt VALUES ('A0002', 'English', 'Invalid credentials');

-- role descriptions
INSERT INTO txt VALUES ('T0001', 'German',  'kann nur die Anmeldeseite und Systemzustand sehen');
INSERT INTO txt VALUES ('T0001', 'English', 'anonymous users can only access the login page and health statistics');
INSERT INTO txt VALUES ('T0002', 'German',  'erlaubt dem middleware server, die n&ouml;tigen Tabellen (LDAP-Verbindungen) zu lesen');
INSERT INTO txt VALUES ('T0002', 'English', 'allows the middleware server to read necessary tables (ldap_connection)');
INSERT INTO txt VALUES ('T0003', 'German',  'Reporter mit Zugang zu grundlegenden Dateien (stm_...) und begrenzten Rechten f&uuml;r Objekt- und Regeltabellen, abh&auml;ngig von den f&uuml;r den eigenen Mandanten sichtbaren Devices');
INSERT INTO txt VALUES ('T0003', 'English', 'reporters have access to basic tables (stm_...) and limited rights for object and rule tables depending on the visible devices for the tenant the user belongs to');
INSERT INTO txt VALUES ('T0004', 'German',  'Reporter mit vollem Lesezugriff auf alle Devices');
INSERT INTO txt VALUES ('T0004', 'English', 'reporter role for full read access to all devices');
INSERT INTO txt VALUES ('T0005', 'German',  'Nutzer zum Importieren von Konfigurations&auml;nderungen in die Datenbank');
INSERT INTO txt VALUES ('T0005', 'English', 'users that can import config changes into the database');
INSERT INTO txt VALUES ('T0006', 'German',  'Nutzer zum Lesen der Datenbanktabellen f&uuml;r Backups');
INSERT INTO txt VALUES ('T0006', 'English', 'users that are able to read data tables for backup purposes');
INSERT INTO txt VALUES ('T0007', 'German',  'Nutzer mit vollem Lesezugriff auf alle Daten und Einstellungen (in der UI), aber ohne jegliche Schreibrechte');
INSERT INTO txt VALUES ('T0007', 'English', 'users that can view all data & settings (in the UI) but cannot make any changes');
INSERT INTO txt VALUES ('T0008', 'German',  '(f&uuml;r zuk&uuml;nftige Anwendung) Nutzer zum Beantragen von Firewall Changes');
INSERT INTO txt VALUES ('T0008', 'English', '(for future use) users who can request firewall changes');
INSERT INTO txt VALUES ('T0009', 'German',  '(f&uuml;r zuk&uuml;nftige Anwendung) Nutzer zum Anlegen von change request workflows');
INSERT INTO txt VALUES ('T0009', 'English', '(for future use) users who can create change request workflows');
INSERT INTO txt VALUES ('T0010', 'German',  'Nutzer zum Dokumentieren von offenen Changes');
INSERT INTO txt VALUES ('T0010', 'English', 'users who can document open changes');
INSERT INTO txt VALUES ('T0011', 'German',  'Nutzer mit vollem Zugriff auf den Firewall Orchestrator');
INSERT INTO txt VALUES ('T0011', 'English', 'users with full access rights to firewall orchestrator');
INSERT INTO txt VALUES ('T0012', 'German',  'Nutzer mit Berechtigung zum Rezertifizieren von Regeln');
INSERT INTO txt VALUES ('T0012', 'English', 'users that have the right to recertify rules');

-- template comments
INSERT INTO txt VALUES ('T0101', 'German',  'Aktuell aktive Regeln aller Gateways');
INSERT INTO txt VALUES ('T0101', 'English', 'Currently active rules of all gateways');
INSERT INTO txt VALUES ('T0102', 'German',  'Alle Regel&auml;nderungen im laufenden Jahr');
INSERT INTO txt VALUES ('T0102', 'English', 'All rule change performed in the current year');
INSERT INTO txt VALUES ('T0103', 'German',  'Anzahl der Objekte und Regeln pro Device');
INSERT INTO txt VALUES ('T0103', 'English', 'Number of objects and rules per device');
INSERT INTO txt VALUES ('T0104', 'German',  'Alle Regeln, die offene Quellen, Ziele oder Dienste haben');
INSERT INTO txt VALUES ('T0104', 'English', 'All pass rules that contain any as source, destination or service');

-- help pages
INSERT INTO txt VALUES ('H0001', 'German',  'Firewall Orchestrator ist eine Anwendung zum Erzeugen und Verwalten von verschiedenen Reports aus Konfigurationsdaten verteilter Firewallsysteme.
    Die Daten werden direkt &uuml;ber APIs von den angeschlossenen Systemen importiert. Filter, Vorlagen und Terminsetzungen helfen, die Reporterzeugung an die spezifischen Bed&uuml;rfnisse anzupassen.
    Die Reports k&ouml;nnen in verschiedenen Ausgabeformaten exportiert werden. Ausserdem wird ein Rezertifizierungsprozess unterst&uuml;tzt.
    Firewall Orchestrator hat eine interne Nutzerverwaltung, es k&ouml;nnen aber auch externe Nutzerverwaltungen angeschlossen werden.
');
INSERT INTO txt VALUES ('H0001', 'English', 'Firewall Orchestrator is an application to create and administrate different reports out of configuration data of distributed firewall systems.
    The data is imported directly via APIs from the connected systems. There are several possibilities to customize the report creation by filters, templates and scheduling.
    The reports can be exported in different output formats. Additionally a recertification workflow is supported.
    Firewall Orchestrator has an internal user management, but is also able to connect to external user management systems.
');

INSERT INTO txt VALUES ('H1001', 'German',  'Die erste Eingabezeile ist die Filterzeile, wo die Parameter f&uuml;r den Report definiert werden.
    Sie unterliegt einer speziellen <a href="/help/reporting/filter">Filtersyntax</a>.
    Sie kann komplett manuell gef&uuml;llt werden oder unterst&uuml;tzt durch <a href="/help/reporting/templates">Vorlagen</a>, welche weiter unten ausgew&auml;hlt werden k&ouml;nnen.
    In der <a href="/help/reporting/leftside">Linken Randleiste</a> werden die verf&uuml;gbaren Devices dargestellt. Eine dortige Auswahl wird automatisch in die Filterzeile &uuml;bernommen.<br>
    Nach klicken der "Report erstellen" Schaltfl&auml;che werden die <a href="/help/reporting/output">Reportdaten</a> im unteren Teil des Fensters dargestellt.
    In der <a href="/help/reporting/rightside">Rechten Randleiste</a> werden Details zu den markierten Objekten gezeigt.<br>
    Der Report kann in verschiedenen Ausgabeformaten <a href="/help/reporting/export">exportiert</a> werden.
');
INSERT INTO txt VALUES ('H1001', 'English', 'The first input line is the filter line, where the parameters for the report creation are defined.
    It is subject to a special <a href="/help/reporting/filter">Filter Syntax</a>. 
    It can be filled completely manually or supported by <a href="/help/reporting/templates">Templates</a>, which can be chosen below.
    In the <a href="/help/reporting/leftside">Left Sidebar</a> the available devices are displayed. Selections out of them are also automatically integrated to the filter line.<br>
    After selecting the "Generate Report" button the <a href="/help/reporting/output">Report Data</a> is shown in the lower part of the window.
    In the <a href="/help/reporting/rightside">Right Sidebar</a> details about the selected objects are given.<br>
    The report can be <a href="/help/reporting/export">exported</a> to different output formats.
');

INSERT INTO txt VALUES ('H2001', 'German',  'Es k&ouml;nnen Reports f&uuml;r einen bestimmten Termin oder als wiederkehrende Auftr&auml;ge festgelegt werden.
    Jeder Nutzer kann seine eigenen Terminpl&auml;ne verwalten.
');
INSERT INTO txt VALUES ('H2001', 'English', 'Reports can be scheduled for a given time or as recurring tasks.
    Every user can administrate his own report schedules.
');
INSERT INTO txt VALUES ('H2011', 'German',  'Name: Der Reportname, der im <a href="/help/archive">Archiv</a> wiederzufinden ist.');
INSERT INTO txt VALUES ('H2011', 'English', 'Name: The report name to be found in the <a href="/help/archive">Archive</a>.');
INSERT INTO txt VALUES ('H2012', 'German',  'Startdatum und -zeit: Erste Ausf&uuml;hrung des Terminauftrags.
    Bitte einige Minuten im voraus w&auml;hlen, wenn die Ausf&uuml;hrung noch heute erfolgen soll, da es einen Zeitverzug von einigen Minuten durch den Timer geben kann. 
');
INSERT INTO txt VALUES ('H2012', 'English', 'Start date and time: First execution of the schedule. 
    Be aware of selecting some minutes ahead if execution should start by today, as there may be a timer delay of some minutes.
');
INSERT INTO txt VALUES ('H2013', 'German',  'Wiederholungsintervall: Es k&ouml;nnen Abst&auml;nde in Tagen, Wochen, Monaten oder Jahren ausgew&auml;hlt werden.
    Wenn "Niemals" gew&auml;hlt wird, wird der Auftrag nur einmal ausgef&uuml;hrt. 
');
INSERT INTO txt VALUES ('H2013', 'English', 'Repetition interval: Intervals in days, weeks, months or years can be selected. 
    If "Never" is selected, only one execution is scheduled.
');
INSERT INTO txt VALUES ('H2014', 'German',  'Vorlagen: Hier muss eine der vorbereiteten <a href="/help/reporting/templates">Vorlagen</a> ausgew&auml;hlt werden.');
INSERT INTO txt VALUES ('H2014', 'English', 'Templates: Here one of the prepared <a href="/help/reporting/templates">Templates</a> has to be selected.');
INSERT INTO txt VALUES ('H2015', 'German',  'Ausgabeformate: Eines oder mehrere der verf&uuml;gbaren Formate html, pdf, json oder csv kann ausgew&auml;hlt werden.
    Wenn keines ausgew&auml;hlt wurde, wird eine json-Ausgabe vorbereitet.
');
INSERT INTO txt VALUES ('H2015', 'English', 'Output formats: One or more of the available formats html, pdf, json or csv can be selected.
    If nothing is selected, a json output is prepared.
');
INSERT INTO txt VALUES ('H2016', 'German',  'Aktiv-Kennzeichen: Nur als aktiv gekennzeichnete Auftr&auml;ge werden ausgef&uuml;hrt.
    So k&ouml;nnen Auftr&auml;ge f&uuml;r die Zukunft vorbereitet werden, bzw. vor&uuml;bergehend nicht ben&ouml;tigte Auftr&auml;ge m&uuml;ssen nicht gel&ouml;scht werden.  
');
INSERT INTO txt VALUES ('H2016', 'English', 'Active Flag: Only schedules with this flag set will be executed.
    So report schedules for future use can be prepared, resp. schedules currently not needed do not have to be deleted.
');

INSERT INTO txt VALUES ('H3001', 'German',  'Hier sind die archivierten Reports mit Name sowie Informationen zum Erzeugungsdatum und Eigent&uuml;mer zu finden.
    Sie k&ouml;nnen zum einen durch Export manuell erzeugter Reports durch Setzen des "Archiv"-Kennzeichens in <a href="/help/reporting/export">Export Report</a> erzeugt werden.
    Zum anderen finden sich hier auch die durch das <a href="/help/scheduling">Scheduling</a> erzeugten Reports.
    Die archivierten Reports k&ouml;nnen von hier heruntergeladen oder gel&ouml;scht werden.
');
INSERT INTO txt VALUES ('H3001', 'English', 'Here the archived reports can be found with name and information about creation date and owner. 
    They may be created on the one hand by exporting manually created reports with setting the flag "Archive" in <a href="/help/reporting/export">Export Report</a>.
    On the other hand here also the reports created by the <a href="/help/scheduling">Scheduling</a> can be found.
    It is possible to download or delete these archived reports.
');

INSERT INTO txt VALUES ('H4001', 'German',  'In diesem Abschnitt k&ouml;nnen Regeln re- oder dezertifiziert werden. Daf&uuml;r wird die Rolle "recertifier" (oder "admin") ben&ouml;tigt.');
INSERT INTO txt VALUES ('H4001', 'English', 'In this part rules can be re- or decertified. For this the role "recertifier" (or "admin") is necessary.');
INSERT INTO txt VALUES ('H4011', 'German',  'Im ersten Schritt muss ein Report mit den demn&auml;chst zu rezertifizierenden Regeln geladen werden.
    Der Zeitraum f&uuml;r die Vorausschau kann im Feld "F&auml;llig in" gew&auml;hlt werden.
    Diese wird im "Rezertifizierungsanzeigeintervall" in den <a href="/help/settings/recertification">Rezertifizierungseinstellungen</a> bzw. 
    in den <a href="/help/settings/defaults">Standardeinstellungen</a> initialisiert.
    Desweiteren m&uuml;ssen die zu betrachtenden Ger&auml;te in der linken Randleiste ausgew&auml;hlt werden.
');
INSERT INTO txt VALUES ('H4011', 'English', 'In the first step a report of upcoming rules to be certified has to be loaded. 
    The lookahead period for this can be chosen in the "Due within" field. 
    It is initialized by the settings value "Recertification Display Period" in the 
    <a href="/help/settings/recertification">Recertification Settings</a> resp. <a href="/help/settings/defaults">Default Settings</a>.
    Also the regarded devices have to be chosen in the left sidebar.
');
INSERT INTO txt VALUES ('H4012', 'German',  'Der Report zeigt nun alle Regeln, die im gew&auml;hlten Zeitraum zertifiziert werden m&uuml;ssen.
    Das Rezertifizierungsdatum wird errechnet aus dem letzten Rezertifizierungsdatum (falls unbekannt, wird das Erzeugungsdatum der Regel genommen)
    und dem Rezertifizierungsintervall, welches in den <a href="/help/settings/defaults">Standardeinstellungen</a> definiert wurde.
    Rezertifizierungen, die in den n&auml;chsten Tagen (definiert im Rezertifizierungserinnerungsintervall in den Standardeinstellungen) f&auml;llig sind, 
    werden in gelb, &uuml;berf&auml;llige Rezertifizierungen in rot unterlegt.
    Zus&auml;tzlich wird der letzte Rezertifizierer dargestellt ("unbekannt" zeigt an, dass noch keine Rezertifizierung stattgefunden hat).
');
INSERT INTO txt VALUES ('H4012', 'English', 'The report shows all rules that are upcoming for recertification within the selected interval.
    The recertification date is computed from the last recertification date (if unknown the rule creation date is taken)
    and the Recertification Period, defined in the <a href="/help/settings/defaults">Default Settings</a>.
    Recertifications upcoming in the next days (defined in the Recertification Notice Period in the Default Settings) are marked in yellow, overdue recertifications in red.
    Additionally the last recertifier is mentioned ("unknown" indicates that there has been no recertification so far).
');
INSERT INTO txt VALUES ('H4013', 'German',  'Der Rezertifizierer hat nun die M&ouml;glichkeit alle zu re- oder dezertifizierenden Regeln zu markieren.
    Durch klicken der "Ausgew&auml;hlte Aktionen ausf&uuml;hren"-Schaltfl&auml;che wird zun&auml;chst ein Kommentar abgefragt.
    Dieser ist ein Pflichtfeld, wenn "Kommentar Pflichtfeld" in den <a href="/help/settings/defaults">Standardeinstellungen</a> gesetzt wurde.
    Nach der Best&auml;tigung werden alle markierten Re- und Dezertifizierungen in einem Schritt ausgef&uuml;hrt.
    Danach werden nur noch die verbleibenden anstehenden Rezertifizierungen angezeigt.
');
INSERT INTO txt VALUES ('H4013', 'English', 'The recertifier has now the possibility to mark each of the displayed rules for recertification or decertification.
    After clicking the "Execute Selected Actions" button a comment is requested. 
    This has to be filled, if the setting "Comment Required" in <a href="/help/settings/defaults">Default Settings</a> is activated.
    When confirmed all selected re- and decertifications are executed in on step. 
    After that only the remaining open certifications are displayed.
');
INSERT INTO txt VALUES ('H4014', 'German',  'Dezertifizierte Regel k&ouml;nnen im Abschnitt <a href="/help/reporting">Reporting</a> mit dem Filterparameter "remove=true" dargestellt werden.');
INSERT INTO txt VALUES ('H4014', 'English', 'Decertified rules can be displayed in the <a href="/help/reporting">Reporting</a> part with the filter parameter "remove=true".');
INSERT INTO txt VALUES ('H4021', 'German',  'Dieses Rezertifizierungsszenario ist als Basis f&uuml;r weitere angepasste Abl&auml;ufe vorgesehen.');
INSERT INTO txt VALUES ('H4021', 'English', 'This recertification scenario is intended to be a base for further customized workflows.');

INSERT INTO txt VALUES ('H5001', 'German',  'Im diesem Abschnitt werden die Setup- und Verwaltungseinstellungen behandelt.
    Die meisten Einstellungen k&ouml;nnen nur von Nutzern mit der Administrator-Rolle gesehen und ge&auml;ndert werden.
    Der Auditor kann zwar die Einstellungen sehen, da er aber keine Schreibrechte hat, sind alle Schaltfl&auml;chen, die zu &Auml;nderungen f&uuml;hren w&uuml;rden, deaktiviert.
');
INSERT INTO txt VALUES ('H5001', 'English', 'In the settings section the setup and administration topics are handled.
    Most settings can only be seen and done by users with administrator role.
    The auditor is able to see the settings, but as he has no write permissions all buttons leading to changes are disabled.
');
INSERT INTO txt VALUES ('H5011', 'German',  'Im ersten Kapitel "Ger&auml;te" wird das Setup der Datenquellen behandelt: 
    Die Abschnitte <a href="/help/settings/managements">Managements</a> und <a href="/help/settings/gateways">Gateways</a> dienen der Definition der verbundenen Hardware,
    der <a href="/help/settings/import">Importstatus</a> -Abschnitt unterst&uuml;tzt das Monitoring des Datenimports.
');
INSERT INTO txt VALUES ('H5011', 'English', 'In the first chapter "Devices" the setup of the report data sources is done:
    The sections <a href="/help/settings/managements">Managements</a> and <a href="/help/settings/gateways">Gateways</a> are for the definition of the connected hardware,
    the <a href="/help/settings/import">Import Status</a> section allows the monitoring of the data import.
');
INSERT INTO txt VALUES ('H5012', 'German',  'Das Kapitel "Berechtigungen" bietet die Funktionalit&auml;t f&uuml;r die Nutzerverwaltung:
    In <a href="/help/settings/ldap">LDAP-Verbindungen</a> k&ouml;nnen externe Verbindungen zus&auml;tzlich zum internen LDAP definiert werden.
    <a href="/help/settings/tenants">Mandanten</a> k&ouml;nnen definiert und mit spezifischen Gateways verkn&uuml;pft werden.
    Interne oder externe <a href="/help/settings/users">Nutzer</a> k&ouml;nnen zu <a href="/help/settings/groups">Gruppen</a> zusammengefasst
    und zu <a href="/help/settings/roles">Rollen</a> zugeordnet werden.
');
INSERT INTO txt VALUES ('H5012', 'English', 'The chapter "Authorization" offers the functionality for the user administration:
    In <a href="/help/settings/ldap">LDAP Connections</a> external connections besides the internal LDAP can be defined.
    <a href="/help/settings/tenants">Tenants</a> can be defined and associated with specific gateways.
    Internal or external <a href="/help/settings/users">Users</a> can be assigned to <a href="/help/settings/groups">User Groups</a>
    and <a href="/help/settings/roles">Roles</a>
');
INSERT INTO txt VALUES ('H5013', 'German',  'Im Kapitel "Voreinstellungen" kann der Administrator <a href="/help/settings/defaults">Standardeinstellungen</a> vornehmen,
    die f&uuml;r alle Nutzer gelten, sowie die <a href="/help/settings/passwordpolicy">Passworteinstellungen</a> definieren, welche f&uuml;r alle Passwort&auml;nderungen g&uuml;ltig sind.
');
INSERT INTO txt VALUES ('H5013', 'English', 'In the "Defaults" chapter the administrator can define <a href="/help/settings/defaults">Default Values</a> applicable to all users
    and set a <a href="/help/settings/passwordpolicy">Password Policy</a> valid for all password changes.
');
INSERT INTO txt VALUES ('H5014', 'German',  'Das Kapitel "Pers&ouml;nlich" ist f&uuml;r alle Nutzer zug&auml;nglich. Hier k&ouml;nnen das individuelle <a href="/help/settings/password">Password</a>,
    die bevorzugte <a href="/help/settings/language">Sprache</a> und <a href="/help/settings/report">Reporting</a>-Einstellungen gesetzt werden.
    Nutzer mit Rezertifizierer-Rolle k&ouml;nnen auch ihre <a href="/help/settings/recertification">Rezertifizierungseinstellungen</a> anpassen.
');
INSERT INTO txt VALUES ('H5014', 'English', 'The "Personal" chapter is accessible by all users, where they can set their individual <a href="/help/settings/password">Password</a>,
    <a href="/help/settings/language">Language</a> and <a href="/help/settings/report">Reporting</a> preferences. 
    Users with recertifier role have also the possibility to adjust their <a href="/help/settings/recertification">Recertification Setting</a>.
');

INSERT INTO txt VALUES ('H6001', 'German',  'Firewall Orchestrator hat eine <a href="/help/API/graphql">GraphQl</a> API welche auf <a href="/help/API/hasura">Hasura</a> basiert. 
    Diese erlaubt, flexibel den Zugang zu allen Daten der Datenbank und die Granularit&auml;t der zur&uuml;ckgegebenen Daten zu steuern.
');
INSERT INTO txt VALUES ('H6001', 'English', 'Firewall Orchestrator has a <a href="/help/API/graphql">GraphQl</a> API which is based on <a href="/help/API/hasura">Hasura</a>. 
    This allows us to flexibly provide access to all data in the database and also define the level of granularity the data is returned in.
');
INSERT INTO txt VALUES ('H6011', 'German',  'Der Abschnitt "Einleitung" gibt einen kurzen &Uuml;berblick in die zugrundeliegende Technologie wie <a href="/help/API/graphql">GraphQl</a>
    und <a href="/help/API/hasura">Hasura</a>, gibt einen Einblick in die <a href="/help/API/security">Sicherheits</a>-Mechanismen sowie
    <a href="/help/API/links">weiterf&uuml;hrendes</a> Material.
');
INSERT INTO txt VALUES ('H6011', 'English', 'The section "Introduction" provides a quick overview touching basic underlying technology like <a href="/help/API/graphql">GraphQl</a> 
    and <a href="/help/API/hasura">Hasura</a>, gives some insight into <a href="/help/API/security">Security</a> mechanisms as well as 
    <a href="/help/API/links">further reading</a> material.
');
INSERT INTO txt VALUES ('H6012', 'German',  'Das Kapitel "Wichtige Kommandos" liefert detailliertere Beispiele f&uuml;r die Nutzung der API.');
INSERT INTO txt VALUES ('H6012', 'English', 'The chapter "Basic commands" gives more detailed examples for the usage of the API.');
