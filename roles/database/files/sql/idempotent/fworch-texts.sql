
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
INSERT INTO txt VALUES ('remove', 				'German',	'Entfernen');
INSERT INTO txt VALUES ('remove', 				'English',	'Remove');
INSERT INTO txt VALUES ('clone', 				'German',	'Klonen');
INSERT INTO txt VALUES ('clone', 				'English',	'Clone');
INSERT INTO txt VALUES ('edit', 				'German',	'Bearbeiten');
INSERT INTO txt VALUES ('edit', 				'English',	'Edit');
INSERT INTO txt VALUES ('set', 				    'German',	'Setzen');
INSERT INTO txt VALUES ('set', 				    'English',	'Set');
INSERT INTO txt VALUES ('add', 				    'German',	'Hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add', 				    'English',	'Add');
INSERT INTO txt VALUES ('autodiscover', 	    'German',	'Sync');
INSERT INTO txt VALUES ('autodiscover', 	    'English',	'Sync');
INSERT INTO txt VALUES ('assign', 				'German',	'Zuordnen');
INSERT INTO txt VALUES ('assign', 				'English',	'Assign');
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
INSERT INTO txt VALUES ('none',		            'German', 	'Keine(r/s)');
INSERT INTO txt VALUES ('none',		            'English', 	'None');
INSERT INTO txt VALUES ('added',                'German', 	'hinzugef&uuml;gt');
INSERT INTO txt VALUES ('added',                'English', 	'added');
INSERT INTO txt VALUES ('deleted',		        'German', 	'gel&ouml;scht');
INSERT INTO txt VALUES ('deleted',		        'English', 	'deleted');
INSERT INTO txt VALUES ('modified',		        'German', 	'ge&auml;ndert');
INSERT INTO txt VALUES ('modified',		        'English', 	'modified');
INSERT INTO txt VALUES ('id',		            'German', 	'Id');
INSERT INTO txt VALUES ('id',		            'English', 	'Id');
INSERT INTO txt VALUES ('coming_soon',		    'German', 	'(demn&auml;chst)');
INSERT INTO txt VALUES ('coming_soon',		    'English', 	'(coming soon)');
INSERT INTO txt VALUES ('in_progress',		    'German', 	'in Arbeit');
INSERT INTO txt VALUES ('in_progress',		    'English', 	'in progress');
INSERT INTO txt VALUES ('select', 				'German',	'Ausw&auml;hlen');
INSERT INTO txt VALUES ('select', 				'English',	'Select');

-- (re)login
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
INSERT INTO txt VALUES ('jwt_expiry_title',     'German', 	'JWT abgelaufen');
INSERT INTO txt VALUES ('jwt_expiry_title',     'English', 	'JWT expired');
INSERT INTO txt VALUES ('jwt_expiry_text',		'German', 	'Ihr Jwt (Session Token) ist kurz davor abzulaufen. Bitte geben Sie ihr Passwort ein, um einen neuen Jwt zu erzeugen.');
INSERT INTO txt VALUES ('jwt_expiry_text',		'English', 	'Your jwt (session token) is about to expire. Re-login to create a new jwt.');
INSERT INTO txt VALUES ('permissions_title',	'German', 	'Berechtigungen');
INSERT INTO txt VALUES ('permissions_title',	'English', 	'Permissions');
INSERT INTO txt VALUES ('permissions_text',		'German', 	'Ihre Berechtigungen wurden ge&auml;ndert. Bitte geben Sie Ihr Passwort ein, um Ihre Berechtigungen zu aktualisieren!');
INSERT INTO txt VALUES ('permissions_text',		'English', 	'Your permissions have been changed. Re-login to update your permissions.');

-- navigation
INSERT INTO txt VALUES ('reporting', 			'German',	'Reporting');
INSERT INTO txt VALUES ('reporting', 			'English',	'Reporting');
INSERT INTO txt VALUES ('settings', 			'German',	'Einstellungen');
INSERT INTO txt VALUES ('settings', 			'English',	'Settings');
INSERT INTO txt VALUES ('monitoring', 			'German',	'Monitoring');
INSERT INTO txt VALUES ('monitoring', 			'English',	'Monitoring');
INSERT INTO txt VALUES ('fworch_long',			'German',	'Firewall&nbsp;Orchestrator');
INSERT INTO txt VALUES ('fworch_long',			'English',	'Firewall&nbsp;Orchestrator');
INSERT INTO txt VALUES ('help',					'German',	'Hilfe');
INSERT INTO txt VALUES ('help', 				'English',	'Help');
INSERT INTO txt VALUES ('logout', 				'German',	'Abmelden');
INSERT INTO txt VALUES ('logout', 				'English',	'Logout');
INSERT INTO txt VALUES ('documentation', 		'German',	'Dokumentation');
INSERT INTO txt VALUES ('documentation', 		'English',	'Documentation');
INSERT INTO txt VALUES ('requests', 			'German',	'Antr&auml;ge');
INSERT INTO txt VALUES ('requests', 			'English',	'Requests');
INSERT INTO txt VALUES ('tickets', 			    'German',	'Tickets');
INSERT INTO txt VALUES ('tickets', 			    'English',	'Tickets');
INSERT INTO txt VALUES ('approvals', 			'German',	'Genehmigungen');
INSERT INTO txt VALUES ('approvals', 			'English',	'Approvals');
INSERT INTO txt VALUES ('plannings', 			'German',	'Planungen');
INSERT INTO txt VALUES ('plannings', 			'English',	'Plannings');
INSERT INTO txt VALUES ('implementations', 		'German',	'Implementierungen');
INSERT INTO txt VALUES ('implementations', 		'English',	'Implementations');
INSERT INTO txt VALUES ('reviews', 		        'German',	'Reviews');
INSERT INTO txt VALUES ('reviews', 		        'English',	'Reviews');
INSERT INTO txt VALUES ('scheduling', 			'German',	'Scheduling');
INSERT INTO txt VALUES ('scheduling', 			'English',	'Scheduling');
INSERT INTO txt VALUES ('archive', 				'German',	'Archiv');
INSERT INTO txt VALUES ('archive', 				'English',	'Archive');
INSERT INTO txt VALUES ('recertification', 		'German',	'Rezertifizierung');
INSERT INTO txt VALUES ('recertification', 		'English',	'Recertification');
INSERT INTO txt VALUES ('api', 		            'German',	'API');
INSERT INTO txt VALUES ('api', 		            'English',	'API');
INSERT INTO txt VALUES ('workflow', 			'German',	'Workflow');
INSERT INTO txt VALUES ('workflow', 			'English',	'Workflow');
INSERT INTO txt VALUES ('planning', 			'German',	'Planung');
INSERT INTO txt VALUES ('planning', 			'English',	'Planning');
INSERT INTO txt VALUES ('implementation', 		'German',	'Implementierung');
INSERT INTO txt VALUES ('implementation', 		'English',	'Implementation');

-- start
INSERT INTO txt VALUES ('welcome_to',           'German', 	'Willkommen zu Firewall Orchestrator');
INSERT INTO txt VALUES ('welcome_to',           'English', 	'Welcome to Firewall Orchestrator');
INSERT INTO txt VALUES ('whats_new_in_version',	'German', 	'Was ist neu in Firewall Orchestrator Version');
INSERT INTO txt VALUES ('whats_new_in_version',	'English', 	'Release notes Firewall Orchestrator version');
INSERT INTO txt VALUES ('whats_new_facts',	    'German', 	'
<ul>
    <li>100% Open Source - passen Sie Firewall Orchestrator an Ihre Bed&uuml;rfnisse an. Machen Sie mit.
        Der Quellcode kann auf <a href="https://github.com/CactuseSecurity/firewall-orchestrator" target="_blank">GitHub</a> eingesehen und heruntergeladen werden.</li>
    <li>GraphQL API f&uuml;r Automatisierungen</li>
    <li>Firewall-Regel Rezertifizierungsworkflow - beseitigen Sie ihre Altlasten und erf&uuml;llen Sie aktuelle regulatorische Anforderungen.</li>
    <li>F&uuml;r FortiManager und CheckPoint (Stand-Alone & MDS Manager) ist eine Auto Discovery Funktion enthalten, die ausgehend 
    vom Management-System alle definierten Domains/SubManager sowie Devices automatisch anlegt sowie &Auml;nderungen erkennt 
    und diese zur automatischen Konfigurations&auml;nderung anbietet.</li>
    <li>Einf&uuml;hrung Monitoring und Alerting Modul - folgende Ereignisse werden protokolliert, in eine Log-Datei geschrieben (zur ggf. weiteren Auswertung per SIEM)
     und dem Nutzer in der Oberfl&auml;che zur Erinnerung angezeigt:
     <ul>
        <li>&Auml;nderungen an angebundenen Firewall-Systemen, die im regelm&auml;&szlig;ig laufenden Hintergrund-Auto-Discovery-Prozess erkannt werden</li>
        <li>Beim Import auftretende Fehler</li>
        <li>Warnungen & Fehler, die in der Nutzeroberfl&auml;che angezeigt werden</li>
     </ul>
</ul>
');
INSERT INTO txt VALUES ('whats_new_facts',	    'English', 	'
<ul>
    <li>100% Open Source - adjust Firewall Orchestrator to your needs. Join the community and contribute.
        The code can be viewed/downloaded from <a href="https://github.com/CactuseSecurity/firewall-orchestrator" target="_blank">GitHub</a></li>
    <li>GraphQL API for automation</li>
    <li>Firewall rule recertification workflow - remove unnecessary rules and meet current regulatory requirements.</li>
    <li>Device Auto Discovery functionality - only specify the Super Manager (FortiManager, MDS) details, everything else (ADOMs, Domains, devices) 
    will be auto-discovered. Also changes in devices/managers will be detected and offered for auto-configuration.</li>
    <li>Introduction of Monitoring and Alerting module - the following events will be logged internally as well as written into a log file 
    (for further processing by a SIEM system) and will also be displayed in the UI as reminders to the relevant users:
     <ul>
        <li>Changes of the integrated firewall systems, which are detected by the background auto discovery process</li>
        <li>import errors</li>
        <li>all warnings and errors shown in the UI</li>
     </ul>
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
        das Einbinden <a href="/settings/managements">Ihrer eigenen Firewall-Systeme.</a>
        N.B. Stellen Sie sicher, dass Sie alle Demo-Daten (insbesondere die Demo-User) l&ouml;schen (mit Hilfe der "Beispieldaten l&ouml;schen" Option in den Einstellungen), 
        bevor Sie in den produktiven Betrieb &uuml;bergehen, da andernfalls ggf. Ihre Daten mit Default-Logins angezeigt werden k&ouml;nnten.</li>
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
        integration of <a href="/settings/managements">your own firewalls</a>.
        N.B. Please make sure to delete all demo data (using the "Remove sample data" option under settings) 
        before using Firewall Orchestrator with production data.
        Otherwise you might expose your data by providing default accounts.</li>
    <li><a href="/logout">Logout</a>: Leave Firewall Orchestrator</li>
</ul>
');

INSERT INTO txt VALUES ('getting_support',	    'German', 	'Unterst&uuml;tzung ben&ouml;tigt? Ihre Kontaktm&ouml;glichkeiten');
INSERT INTO txt VALUES ('getting_support',	    'English', 	'Do you need help? Our Contact options');
INSERT INTO txt VALUES ('support_details',	    'German', 	'
M&ouml;chten Sie einen Supportvertrag abschlie&szlig;en, um in den Genuss folgender Vorteile zu kommen?<br>
<ul>
    <li>garantierte Unterst&uuml;tzung bei Problemen mit Firewall Orchestrator</li>
    <li>Customizing: haben Sie Anpassungsw&uuml;nsche, die wir f&uuml;r Sie umsetzen sollen?</li>
</ul>
Folgende Kontaktm&ouml;glichkeiten stehen Ihnen zur Verf&uuml;gung:
<ul>
    <li><a href="https://github.com/CactuseSecurity/firewall-orchestrator/issues/new?assignees=&labels=&template=feature_request.md&title=">Feature request auf Github</a></li>
    <li>Telefon: <a href="tel:+496996233675">+49 69 962336-75</a></li>
    <li>Email: <a href="mailto:support@cactus.de">support@cactus.de</a></li>
    <li>Chat: <a href="https://fworch.cactus.de/chat">Support-Chat</a></li>
    <li>Video/Audio-Call (nach Vereinbarung): <a href="https://conf.cactus.de/fworch">https://conf.cactus.de/fworch</a></li>
</ul>
');
INSERT INTO txt VALUES ('support_details',	    'English', 	'
Do you wish to get a support contract for the following benefits?
<br>
<ul>
    <li>get a direct line to qualified support personnel</li>
    <li>Customizing: can we help your with individual changes or extensions of functionality?</li>
</ul>
Choose from the following contact options:
<ul>
    <li><a href="https://github.com/CactuseSecurity/firewall-orchestrator/issues/new?assignees=&labels=&template=feature_request.md&title=">Open a feature request on Github</a></li>
    <li>Phone: <a href="tel:+496996233675">+49 69 962336-75</a></li>
    <li>Email: <a href="mailto:support@cactus.de">support@cactus.de</a> </li>
    <li>Chat: <a href="https://fworch.cactus.de/chat">Support chat</a></li>
    <li>Video/Audio Call (contact us to arrange a time slot): <a href="https://conf.cactus.de/fworch">https://conf.cactus.de/fworch</a></li>
</ul>
');

-- reporting
INSERT INTO txt VALUES ('report_type',		    'German', 	'Report-Typ');
INSERT INTO txt VALUES ('report_type',		    'English', 	'Report Type');
INSERT INTO txt VALUES ('report_time',		    'German', 	'Report-Zeit');
INSERT INTO txt VALUES ('report_time',		    'English', 	'Report Time');
INSERT INTO txt VALUES ('change',		        'German', 	'&Auml;ndern');
INSERT INTO txt VALUES ('change',		        'English', 	'Change');
INSERT INTO txt VALUES ('shortcut',		        'German', 	'Abk&uuml;rzung');
INSERT INTO txt VALUES ('shortcut',		        'English', 	'Shortcut');
INSERT INTO txt VALUES ('now',		            'German', 	'jetzt');
INSERT INTO txt VALUES ('now',		            'English', 	'now');
INSERT INTO txt VALUES ('last',		            'German', 	'letzte');
INSERT INTO txt VALUES ('last',		            'English', 	'last');
INSERT INTO txt VALUES ('open',		            'German', 	'offen');
INSERT INTO txt VALUES ('open',		            'English', 	'open');
INSERT INTO txt VALUES ('from',		            'German', 	'ab');
INSERT INTO txt VALUES ('from',		            'English', 	'from');
INSERT INTO txt VALUES ('until',		        'German', 	'bis');
INSERT INTO txt VALUES ('until',		        'English', 	'until');
INSERT INTO txt VALUES ('this year',		    'German', 	'dieses Jahr');
INSERT INTO txt VALUES ('this year',		    'English', 	'this year');
INSERT INTO txt VALUES ('last year',		    'German', 	'letztes Jahr');
INSERT INTO txt VALUES ('last year',		    'English', 	'last year');
INSERT INTO txt VALUES ('this month',		    'German', 	'diesen Monat');
INSERT INTO txt VALUES ('this month',		    'English', 	'this month');
INSERT INTO txt VALUES ('last month',		    'German', 	'letzten Monat');
INSERT INTO txt VALUES ('last month',		    'English', 	'last month');
INSERT INTO txt VALUES ('this week',		    'German', 	'diese Woche');
INSERT INTO txt VALUES ('this week',		    'English', 	'this week');
INSERT INTO txt VALUES ('last week',		    'German', 	'letzte Woche');
INSERT INTO txt VALUES ('last week',		    'English', 	'last week');
INSERT INTO txt VALUES ('today',		        'German', 	'heute');
INSERT INTO txt VALUES ('today',		        'English', 	'today');
INSERT INTO txt VALUES ('yesterday',		    'German', 	'gestern');
INSERT INTO txt VALUES ('yesterday',		    'English', 	'yesterday');
INSERT INTO txt VALUES ('time', 			    'German',	'Zeitpunkt');
INSERT INTO txt VALUES ('time', 			    'English',	'Time');
INSERT INTO txt VALUES ('end_time', 			'German',	'Endezeit');
INSERT INTO txt VALUES ('end_time', 			'English',	'End Time');
INSERT INTO txt VALUES ('check_times', 			'German',	'Pr&uuml;fung Datumswerte');
INSERT INTO txt VALUES ('check_times', 			'English',	'Check time values');
INSERT INTO txt VALUES ('select_device',		'German', 	'Device(s) ausw&auml;hlen');
INSERT INTO txt VALUES ('select_device',		'English', 	'Select device(s)');
INSERT INTO txt VALUES ('select_all',		    'German', 	'Alle ausw&auml;hlen');
INSERT INTO txt VALUES ('select_all',		    'English', 	'Select all');
INSERT INTO txt VALUES ('clear_all',		    'German', 	'Auswahl leeren');
INSERT INTO txt VALUES ('clear_all',		    'English', 	'Clear all');
INSERT INTO txt VALUES ('generate_report',		'German', 	'Report erstellen');
INSERT INTO txt VALUES ('generate_report',		'English', 	'Generate report');
INSERT INTO txt VALUES ('stop_fetching',		'German', 	'Datenholen abbrechen');
INSERT INTO txt VALUES ('stop_fetching',		'English', 	'Stop fetching');
INSERT INTO txt VALUES ('report_data_fetch',    'German', 	'Abholen der Reportdaten');
INSERT INTO txt VALUES ('report_data_fetch',    'English', 	'Report Data Fetch');
INSERT INTO txt VALUES ('export_report',        'German', 	'Report exportieren');
INSERT INTO txt VALUES ('export_report',        'English', 	'Export Report');
INSERT INTO txt VALUES ('report_name',          'German', 	'Report-Name');
INSERT INTO txt VALUES ('report_name',          'English', 	'Report Name');
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
INSERT INTO txt VALUES ('no_report_type_selected','German', 'Kein Report-Typ ausgew&auml;hlt.');
INSERT INTO txt VALUES ('no_report_type_selected','English','No report type selected.');
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
INSERT INTO txt VALUES ('trans_source', 		'German', 	'Umgesetzte Quelle');
INSERT INTO txt VALUES ('trans_source', 		'English', 	'Translated Source');
INSERT INTO txt VALUES ('trans_destination', 	'German', 	'Umgesetztes Ziel');
INSERT INTO txt VALUES ('trans_destination', 	'English', 	'Translated Destination');
INSERT INTO txt VALUES ('trans_services', 		'German', 	'Umgesetzte Dienste');
INSERT INTO txt VALUES ('trans_services', 		'English', 	'Translated Services');
INSERT INTO txt VALUES ('actions', 				'German', 	'Aktionen');
INSERT INTO txt VALUES ('actions', 				'English', 	'Actions');
INSERT INTO txt VALUES ('track', 				'German', 	'Logging');
INSERT INTO txt VALUES ('track', 				'English', 	'Logging');
INSERT INTO txt VALUES ('disabled',				'German', 	'Deaktiviert');
INSERT INTO txt VALUES ('disabled',				'English', 	'Disabled');
INSERT INTO txt VALUES ('comment',				'German', 	'Kommentar');
INSERT INTO txt VALUES ('comment',				'English', 	'Comment');
INSERT INTO txt VALUES ('ip_address',		    'German', 	'IP-Adresse');
INSERT INTO txt VALUES ('ip_address',		    'English', 	'IP Address');
INSERT INTO txt VALUES ('members',		        'German', 	'Mitglieder');
INSERT INTO txt VALUES ('members',		        'English', 	'Members');
INSERT INTO txt VALUES ('templates',			'German', 	'Vorlagen');
INSERT INTO txt VALUES ('templates',			'English', 	'Templates');
INSERT INTO txt VALUES ('creation_date',		'German', 	'Erstelldatum');
INSERT INTO txt VALUES ('creation_date',		'English', 	'Creation Date');
INSERT INTO txt VALUES ('report_template',		'German', 	'Reportvorlage');
INSERT INTO txt VALUES ('report_template',		'English', 	'Report Template');
INSERT INTO txt VALUES ('no_of_obj',		    'German', 	'Anzahl der Objekte');
INSERT INTO txt VALUES ('no_of_obj',		    'English', 	'Number of Objects');
INSERT INTO txt VALUES ('glob_no_obj',		    'German', 	'Gesamtzahl der Objekte');
INSERT INTO txt VALUES ('glob_no_obj',		    'English', 	'Global number of Objects');
INSERT INTO txt VALUES ('total_no_obj_mgt',		'German', 	'Gesamtzahl der Objekte pro Management');
INSERT INTO txt VALUES ('total_no_obj_mgt',		'English', 	'Total number of Objects per Management');
INSERT INTO txt VALUES ('no_rules_gtw',		    'German', 	'Anzahl Regeln pro Gateway');
INSERT INTO txt VALUES ('no_rules_gtw',		    'English', 	'Number of Rules per Gateway');
INSERT INTO txt VALUES ('negated',		        'German', 	'negated');
INSERT INTO txt VALUES ('negated',		        'English', 	'negiert');
INSERT INTO txt VALUES ('network_objects',		'German', 	'Netzwerkobjekte');
INSERT INTO txt VALUES ('network_objects',		'English', 	'Network Objects');
INSERT INTO txt VALUES ('network_services',		'German', 	'Netzwerkdienste');
INSERT INTO txt VALUES ('network_services',		'English', 	'Network Services');
INSERT INTO txt VALUES ('service_objects',		'German', 	'Serviceobjekte');
INSERT INTO txt VALUES ('service_objects',		'English', 	'Service objects');
INSERT INTO txt VALUES ('user_objects',		    'German', 	'Nutzerobjekte');
INSERT INTO txt VALUES ('user_objects',		    'English', 	'User objects');
INSERT INTO txt VALUES ('rules',		        'German', 	'Regeln');
INSERT INTO txt VALUES ('rules',		        'English', 	'Rules');
INSERT INTO txt VALUES ('changes',		        'German', 	'&Auml;nderungen');
INSERT INTO txt VALUES ('changes',		        'English', 	'Changes');
INSERT INTO txt VALUES ('statistics',		    'German', 	'Statistik');
INSERT INTO txt VALUES ('statistics',		    'English', 	'Statistics');
INSERT INTO txt VALUES ('natrules',		        'German', 	'NAT-Regeln');
INSERT INTO txt VALUES ('natrules',		        'English', 	'NAT Rules');
INSERT INTO txt VALUES ('no_of_rules',		    'German', 	'Anzahl Regeln');
INSERT INTO txt VALUES ('no_of_rules',		    'English', 	'Number of Rules');
INSERT INTO txt VALUES ('collapse_all',		    'German', 	'Alles einklappen');
INSERT INTO txt VALUES ('collapse_all',		    'English', 	'Collapse all');
INSERT INTO txt VALUES ('expand_all',	        'German', 	'Alles ausklappen');
INSERT INTO txt VALUES ('expand_all',		    'English', 	'Expand all');
INSERT INTO txt VALUES ('all',		            'German', 	'Alle');
INSERT INTO txt VALUES ('all',		            'English', 	'All');
INSERT INTO txt VALUES ('rule',		            'German', 	'Regel');
INSERT INTO txt VALUES ('rule',		            'English', 	'Rule');
INSERT INTO txt VALUES ('objects',		        'German', 	'Objekte');
INSERT INTO txt VALUES ('objects',		        'English', 	'Objects');
INSERT INTO txt VALUES ('change_time',		    'German', 	'&Auml;nderungszeit');
INSERT INTO txt VALUES ('change_time',		    'English', 	'Change Time');
INSERT INTO txt VALUES ('change_type',		    'German', 	'&Auml;nderungstyp');
INSERT INTO txt VALUES ('change_type',		    'English', 	'Change Type');
INSERT INTO txt VALUES ('source_zone',		    'German', 	'Quellzone');
INSERT INTO txt VALUES ('source_zone',		    'English', 	'Source Zone');
INSERT INTO txt VALUES ('destination_zone',		'German', 	'Zielzone');
INSERT INTO txt VALUES ('destination_zone',		'English', 	'Destination Zone');
INSERT INTO txt VALUES ('anything_but',		    'German', 	'alles ausser');
INSERT INTO txt VALUES ('anything_but',		    'English', 	'anything but');
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
INSERT INTO txt VALUES ('object_fetch_warning', 'German', 	'Aufgrund des gew&auml;hlten Limits konnten nicht alle Objekte in die rechte Randleiste geladen werden');
INSERT INTO txt VALUES ('object_fetch_warning', 'English', 	'Because of the set fetch limit, not all objects could be loaded into the right side bar');
INSERT INTO txt VALUES ('template_fetch',       'German', 	'Abholen der Vorlagen');
INSERT INTO txt VALUES ('template_fetch',       'English', 	'Report Template Fetch');
INSERT INTO txt VALUES ('save_template',        'German', 	'Speichern der Vorlage');
INSERT INTO txt VALUES ('save_template',        'English', 	'Save Report Template');
INSERT INTO txt VALUES ('edit_template',        'German', 	'&Auml;ndern der Vorlage');
INSERT INTO txt VALUES ('edit_template',        'English', 	'Edit Report Template');
INSERT INTO txt VALUES ('delete_template',      'German', 	'L&ouml;schen der Vorlage');
INSERT INTO txt VALUES ('delete_template',      'English', 	'Delete Report Template');
INSERT INTO txt VALUES ('no_changes_found',	    'German', 	'Keine Changes gefunden!');
INSERT INTO txt VALUES ('no_changes_found',	    'English', 	'No changes found!');
INSERT INTO txt VALUES ('rules_report',	        'German', 	'Regel-Report');
INSERT INTO txt VALUES ('rules_report',	        'English', 	'Rules Report');
INSERT INTO txt VALUES ('natrules_report',	    'German', 	'NAT-Regel-Report');
INSERT INTO txt VALUES ('natrules_report',	    'English', 	'NAT Rules Report');
INSERT INTO txt VALUES ('changes_report',	    'German', 	'Changes-Report');
INSERT INTO txt VALUES ('changes_report',	    'English', 	'Changes Report');
INSERT INTO txt VALUES ('statistics_report',	'German', 	'Statistik-Report');
INSERT INTO txt VALUES ('statistics_report',	'English', 	'Statistics Report');
INSERT INTO txt VALUES ('generated_on',	        'German', 	'Erstellt am');
INSERT INTO txt VALUES ('generated_on',	        'English', 	'Generated on');

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
INSERT INTO txt VALUES ('count', 			    'German',	'Z&auml;hler');
INSERT INTO txt VALUES ('count', 			    'English',	'Count');
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
INSERT INTO txt VALUES ('schedule_fetch',       'German', 	'Abholen der Termine');
INSERT INTO txt VALUES ('schedule_fetch',       'English', 	'Report Schedule Fetch');
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
INSERT INTO txt VALUES ('archive_fetch',        'German', 	'Abholen der archivierten Reports');
INSERT INTO txt VALUES ('archive_fetch',        'English', 	'Archived Reports Fetch');
INSERT INTO txt VALUES ('fetch_report',		    'German', 	'Erstellten Report holen');
INSERT INTO txt VALUES ('fetch_report',		    'English', 	'Fetch downloads of generated report');
INSERT INTO txt VALUES ('delete_report',		'German', 	'Erstellten Report l&ouml;schen');
INSERT INTO txt VALUES ('delete_report',		'English', 	'Delete generated report');

-- requests
INSERT INTO txt VALUES ('request',              'German', 	'Antrag');
INSERT INTO txt VALUES ('request',              'English', 	'Request');
INSERT INTO txt VALUES ('ticket',               'German', 	'Ticket');
INSERT INTO txt VALUES ('ticket',               'English', 	'Ticket');
INSERT INTO txt VALUES ('create_ticket', 		'German',	'Antrag stellen');
INSERT INTO txt VALUES ('create_ticket', 	    'English',	'Create Ticket');
INSERT INTO txt VALUES ('all_readonly',		    'German', 	'Alle Antr&auml;ge (nur lesend)');
INSERT INTO txt VALUES ('all_readonly',		    'English', 	'All tickets (read only)');
INSERT INTO txt VALUES ('task',                 'German', 	'Aufgabe');
INSERT INTO txt VALUES ('task',                 'English', 	'Task');
INSERT INTO txt VALUES ('element',              'German', 	'Element');
INSERT INTO txt VALUES ('element',              'English', 	'Element');
INSERT INTO txt VALUES ('reason', 				'German', 	'Grund');
INSERT INTO txt VALUES ('reason', 				'English', 	'Reason');
INSERT INTO txt VALUES ('service', 			    'German', 	'Dienst');
INSERT INTO txt VALUES ('service', 			    'English', 	'Service');
INSERT INTO txt VALUES ('access', 			    'German', 	'Zugriff');
INSERT INTO txt VALUES ('access', 			    'English', 	'Access');
INSERT INTO txt VALUES ('generic',              'German', 	'Generisch');
INSERT INTO txt VALUES ('generic',              'English', 	'Generic');
INSERT INTO txt VALUES ('action', 				'German', 	'Aktion');
INSERT INTO txt VALUES ('action', 				'English', 	'Action');
INSERT INTO txt VALUES ('rule_action', 			'German', 	'Regel-Aktion');
INSERT INTO txt VALUES ('rule_action', 			'English', 	'Rule Action');
INSERT INTO txt VALUES ('create',		        'German', 	'Anlegen');
INSERT INTO txt VALUES ('create',		        'English', 	'Create');
INSERT INTO txt VALUES ('modify',		        'German', 	'&Auml;ndern');
INSERT INTO txt VALUES ('modify',		        'English', 	'Modify');
INSERT INTO txt VALUES ('svc_group', 			'German', 	'Dienstgruppe');
INSERT INTO txt VALUES ('svc_group', 			'English', 	'Service group');
INSERT INTO txt VALUES ('obj_group', 			'German', 	'Objektgruppe');
INSERT INTO txt VALUES ('obj_group', 			'English', 	'Object group');
INSERT INTO txt VALUES ('rule_modify', 			'German', 	'Regel &auml;ndern');
INSERT INTO txt VALUES ('rule_modify', 		    'English', 	'Modify Rule');
INSERT INTO txt VALUES ('never', 			    'German', 	'Niemals');
INSERT INTO txt VALUES ('never', 			    'English', 	'Never');
INSERT INTO txt VALUES ('onlyForOneDevice', 	'German', 	'Nur wenn genau ein Ger&auml;t vorhanden');
INSERT INTO txt VALUES ('onlyForOneDevice', 	'English', 	'Only if exactly one device available');
INSERT INTO txt VALUES ('forEachDevice', 		'German', 	'F&uuml;r jedes Ger&auml;t');
INSERT INTO txt VALUES ('forEachDevice', 		'English', 	'For each device');
INSERT INTO txt VALUES ('enterInReqTask',       'German', 	'Ger&auml;t im Antrag eingeben');
INSERT INTO txt VALUES ('enterInReqTask',       'English', 	'Enter device in request');
INSERT INTO txt VALUES ('add_new_request',      'German', 	'Neuen Antrag hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_new_request',      'English', 	'Add new request');
INSERT INTO txt VALUES ('fetch_requests',       'German', 	'Antr&auml;ge holen');
INSERT INTO txt VALUES ('fetch_requests',       'English', 	'Fetch requests');
INSERT INTO txt VALUES ('save_request',         'German', 	'Antrag speichern');
INSERT INTO txt VALUES ('save_request',         'English', 	'Save request');
INSERT INTO txt VALUES ('state',                'German', 	'Status');
INSERT INTO txt VALUES ('state',                'English', 	'State');
INSERT INTO txt VALUES ('tasks',                'German', 	'Aufgaben');
INSERT INTO txt VALUES ('tasks',                'English', 	'Tasks');
INSERT INTO txt VALUES ('add_task',             'German', 	'Aufgabe hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_task',             'English', 	'Add task');
INSERT INTO txt VALUES ('save_task',            'German', 	'Aufgabe speichern');
INSERT INTO txt VALUES ('save_task',            'English', 	'Save task');
INSERT INTO txt VALUES ('delete_task',          'German', 	'Aufgabe l&ouml;schen');
INSERT INTO txt VALUES ('delete_task',          'English', 	'Delete task');
INSERT INTO txt VALUES ('elements',             'German', 	'Elemente');
INSERT INTO txt VALUES ('elements',             'English', 	'Elements');
INSERT INTO txt VALUES ('add_element',          'German', 	'Element hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_element',          'English', 	'Add element');
INSERT INTO txt VALUES ('save_element',         'German', 	'Element speichern');
INSERT INTO txt VALUES ('save_element',         'English', 	'Save element');
INSERT INTO txt VALUES ('search_element',       'German', 	'Element suchen');
INSERT INTO txt VALUES ('search_element',       'English', 	'Search element');
INSERT INTO txt VALUES ('delete_element',       'German', 	'Element l&ouml;schen');
INSERT INTO txt VALUES ('delete_element',       'English', 	'Delete element');
INSERT INTO txt VALUES ('requester', 			'German',	'Antragsteller');
INSERT INTO txt VALUES ('requester', 			'English',	'Requester');
INSERT INTO txt VALUES ('promote', 		        'German',	'Status &auml;ndern');
INSERT INTO txt VALUES ('promote', 		        'English',	'Promote');
INSERT INTO txt VALUES ('promote_to', 			'German',	'Status &auml;ndern');
INSERT INTO txt VALUES ('promote_to', 			'English',	'Promote to');
INSERT INTO txt VALUES ('closed', 				'German',	'Geschlossen');
INSERT INTO txt VALUES ('closed', 			    'English',	'Closed');
INSERT INTO txt VALUES ('plan',                 'German', 	'Planen');
INSERT INTO txt VALUES ('plan',                 'English', 	'Plan');
INSERT INTO txt VALUES ('planner',              'German', 	'Planer');
INSERT INTO txt VALUES ('planner',              'English', 	'Planner');
INSERT INTO txt VALUES ('create_implementation','German', 	'Implementierung hinzuf&uuml;gen');
INSERT INTO txt VALUES ('create_implementation','English', 	'Create implementation task');
INSERT INTO txt VALUES ('implementation_tasks', 'German', 	'Implementierungsaufgaben');
INSERT INTO txt VALUES ('implementation_tasks', 'English', 	'Implementation tasks');
INSERT INTO txt VALUES ('request_elements',     'German', 	'Auftragselemente');
INSERT INTO txt VALUES ('request_elements',     'English', 	'Request elements');
INSERT INTO txt VALUES ('change_state',         'German', 	'Status &auml;ndern');
INSERT INTO txt VALUES ('change_state',         'English', 	'Change state');
INSERT INTO txt VALUES ('approval', 			'German',	'Genehmigung');
INSERT INTO txt VALUES ('approval', 			'English',	'Approval');
INSERT INTO txt VALUES ('approve', 			    'German',	'Genehmigen');
INSERT INTO txt VALUES ('approve', 			    'English',	'Approve');
INSERT INTO txt VALUES ('save_approval',        'German', 	'Genehmigung speichern');
INSERT INTO txt VALUES ('save_approval',        'English', 	'Save approval');
INSERT INTO txt VALUES ('start_approval',       'German', 	'Genehmigung beginnen');
INSERT INTO txt VALUES ('start_approval',       'English', 	'Start approval');
INSERT INTO txt VALUES ('continue_approval',    'German', 	'Genehmigung fortsetzen');
INSERT INTO txt VALUES ('continue_approval',    'English', 	'Continue approval');
INSERT INTO txt VALUES ('start_planning',       'German', 	'Planung beginnen');
INSERT INTO txt VALUES ('start_planning',       'English', 	'Start planning');
INSERT INTO txt VALUES ('continue_planning',    'German', 	'Planung fortsetzen');
INSERT INTO txt VALUES ('continue_planning',    'English', 	'Continue planning');
INSERT INTO txt VALUES ('start_implementation', 'German', 	'Implementierung beginnen');
INSERT INTO txt VALUES ('start_implementation', 'English', 	'Start implementation');
INSERT INTO txt VALUES ('continue_implementation','German', 'Implementierung fortsetzen');
INSERT INTO txt VALUES ('continue_implementation','English','Continue implementation');
INSERT INTO txt VALUES ('start_review',         'German', 	'Review beginnen');
INSERT INTO txt VALUES ('start_review',         'English', 	'Start review');
INSERT INTO txt VALUES ('continue_review',      'German',   'Review fortsetzen');
INSERT INTO txt VALUES ('continue_review',      'English',  'Continue review');
INSERT INTO txt VALUES ('implement', 		    'German',	'Implementieren');
INSERT INTO txt VALUES ('implement', 		    'English',	'Implement');
INSERT INTO txt VALUES ('implementer', 		    'German',	'Implementierer');
INSERT INTO txt VALUES ('implementer', 		    'English',	'Implementer');
INSERT INTO txt VALUES ('promote_task', 		'German',	'Auftrag-Status &auml;ndern');
INSERT INTO txt VALUES ('promote_task', 		'English',	'Promote task');
INSERT INTO txt VALUES ('promote_ticket', 		'German',	'Antrag-Status &auml;ndern');
INSERT INTO txt VALUES ('promote_ticket', 		'English',	'Promote ticket');
INSERT INTO txt VALUES ('valid_from', 		    'German',	'G&uuml;ltig ab');
INSERT INTO txt VALUES ('valid_from', 		    'English',	'Valid from');
INSERT INTO txt VALUES ('valid_to', 		    'German',	'G&uuml;ltig bis');
INSERT INTO txt VALUES ('valid_to', 		    'English',	'Valid to');
INSERT INTO txt VALUES ('add_approval',         'German', 	'Genehmigung hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_approval',         'English', 	'Add approval');
INSERT INTO txt VALUES ('approver',             'German', 	'Genehmiger');
INSERT INTO txt VALUES ('approver',             'English',  'Approver');
INSERT INTO txt VALUES ('approved',             'German', 	'Genehmigt');
INSERT INTO txt VALUES ('approved',             'English',  'Approved');
INSERT INTO txt VALUES ('opened',               'German', 	'Ge&ouml;ffnet');
INSERT INTO txt VALUES ('opened',               'English',  'Opened');
INSERT INTO txt VALUES ('deadline',             'German', 	'Deadline');
INSERT INTO txt VALUES ('deadline',             'English',  'Deadline');
INSERT INTO txt VALUES ('assign_to', 			'German',	'Weiterleiten an');
INSERT INTO txt VALUES ('assign_to', 			'English',	'Assign to');
INSERT INTO txt VALUES ('assign_group', 		'German',	'Gruppe zuweisen');
INSERT INTO txt VALUES ('assign_group', 		'English',	'Assign group');
INSERT INTO txt VALUES ('assigned', 			'German',	'Zugewiesen');
INSERT INTO txt VALUES ('assigned', 			'English',	'Assigned');
INSERT INTO txt VALUES ('back_to', 			    'German',	'Zur&uuml;ck zu');
INSERT INTO txt VALUES ('back_to', 			    'English',	'Back to');
INSERT INTO txt VALUES ('current_handler', 		'German',	'Aktueller Bearbeiter');
INSERT INTO txt VALUES ('current_handler', 	    'English',	'Current handler');
INSERT INTO txt VALUES ('handler', 		        'German',	'Bearbeiter');
INSERT INTO txt VALUES ('handler', 	            'English',	'Handler');
INSERT INTO txt VALUES ('review', 			    'German',	'Review');
INSERT INTO txt VALUES ('review', 			    'English',	'Review');
INSERT INTO txt VALUES ('verification', 		'German',	'Verifizierung');
INSERT INTO txt VALUES ('verification', 		'English',	'Verification');
INSERT INTO txt VALUES ('obj', 			        'German', 	'Obj');
INSERT INTO txt VALUES ('obj', 			        'English', 	'Obj');
INSERT INTO txt VALUES ('view', 			    'German', 	'Ansicht');
INSERT INTO txt VALUES ('view', 			    'English', 	'View');
INSERT INTO txt VALUES ('all_gateways',         'German', 	'Alle Gateways');
INSERT INTO txt VALUES ('all_gateways',         'English', 	'All Gateways');
INSERT INTO txt VALUES ('insert_ip',            'German', 	'IP einf&uuml;gen');
INSERT INTO txt VALUES ('insert_ip',            'English', 	'Insert IP');
INSERT INTO txt VALUES ('state_actions',        'German', 	'Statusaktionen');
INSERT INTO txt VALUES ('state_actions',        'English', 	'State Actions');
INSERT INTO txt VALUES ('add_action',           'German', 	'Aktion hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_action',           'English', 	'Add action');
INSERT INTO txt VALUES ('edit_action',          'German', 	'Aktion bearbeiten');
INSERT INTO txt VALUES ('edit_action',          'English', 	'Edit action');
INSERT INTO txt VALUES ('delete_action',        'German', 	'Aktion l&ouml;schen');
INSERT INTO txt VALUES ('delete_action',        'English', 	'Delete action');
INSERT INTO txt VALUES ('save_action',          'German', 	'Aktion speichern');
INSERT INTO txt VALUES ('save_action',          'English', 	'Save action');
INSERT INTO txt VALUES ('scope', 			    'German', 	'Geltungsbereich');
INSERT INTO txt VALUES ('scope', 			    'English', 	'Scope');
INSERT INTO txt VALUES ('event', 			    'German', 	'Ereignis');
INSERT INTO txt VALUES ('event', 			    'English', 	'Event');
INSERT INTO txt VALUES ('action_type', 		    'German', 	'Aktionstyp Ref');
INSERT INTO txt VALUES ('action_type', 		    'English', 	'Action type');
INSERT INTO txt VALUES ('external_params', 		'German', 	'Externe Parameter');
INSERT INTO txt VALUES ('external_params', 		'English', 	'External params');
INSERT INTO txt VALUES ('message_text', 		'German', 	'Nachricht');
INSERT INTO txt VALUES ('message_text', 		'English', 	'Message text');

-- recertification
INSERT INTO txt VALUES ('recertify',		    'German', 	'Rezertifizieren');
INSERT INTO txt VALUES ('recertify',		    'English', 	'Recertify');
INSERT INTO txt VALUES ('decertify',		    'German', 	'Dezertifizieren');
INSERT INTO txt VALUES ('decertify',		    'English', 	'Decertify');
INSERT INTO txt VALUES ('later',		        'German', 	'Sp&auml;ter');
INSERT INTO txt VALUES ('later',		        'English', 	'None');
INSERT INTO txt VALUES ('due_within',		    'German', 	'F&auml;llig in (Tagen)');
INSERT INTO txt VALUES ('due_within',		    'English', 	'Due within (days)');
INSERT INTO txt VALUES ('load_rules',		    'German', 	'Regeln anzeigen');
INSERT INTO txt VALUES ('load_rules',		    'English', 	'Show Rules');
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
INSERT INTO txt VALUES ('authorization',		'German', 	'Berechtigungen');
INSERT INTO txt VALUES ('authorization',		'English', 	'Authorization');
INSERT INTO txt VALUES ('ldap_conns',	        'German', 	'LDAP-Verbindungen');
INSERT INTO txt VALUES ('ldap_conns',	        'English', 	'LDAP Connections');
INSERT INTO txt VALUES ('tenants',		        'German', 	'Mandanten');
INSERT INTO txt VALUES ('tenants',		        'English', 	'Tenants');
INSERT INTO txt VALUES ('users',		        'German', 	'Nutzer');
INSERT INTO txt VALUES ('users',		        'English', 	'Users');
INSERT INTO txt VALUES ('groups',		        'German', 	'Interne Gruppen');
INSERT INTO txt VALUES ('groups',		        'English', 	'Internal Groups');
INSERT INTO txt VALUES ('roles',		        'German', 	'Rollen');
INSERT INTO txt VALUES ('roles',		        'English', 	'Roles');
INSERT INTO txt VALUES ('defaults',		        'German', 	'Voreinstellungen');
INSERT INTO txt VALUES ('defaults',		        'English', 	'Defaults');
INSERT INTO txt VALUES ('standards',		    'German', 	'Standardeinstellungen');
INSERT INTO txt VALUES ('standards',		    'English', 	'Defaults');
INSERT INTO txt VALUES ('password_policy',      'German', 	'Passworteinstellungen');
INSERT INTO txt VALUES ('password_policy',      'English', 	'Password Policy');
INSERT INTO txt VALUES ('state_definitions',	'German', 	'Statusdefinitionen');
INSERT INTO txt VALUES ('state_definitions',	'English', 	'State Definitions');
INSERT INTO txt VALUES ('state_matrix',	        'German', 	'Statusmatrix');
INSERT INTO txt VALUES ('state_matrix',	        'English', 	'State Matrix');
INSERT INTO txt VALUES ('customizing',		    'German', 	'Einstellungen');
INSERT INTO txt VALUES ('customizing',		    'English', 	'Customizing');
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
INSERT INTO txt VALUES ('config_path',          'German', 	'Domain');
INSERT INTO txt VALUES ('config_path',          'English', 	'Domain');
INSERT INTO txt VALUES ('super_manager',        'German', 	'Multi Domain Manager');
INSERT INTO txt VALUES ('super_manager',        'English', 	'Multi Domain Manager');
INSERT INTO txt VALUES ('no_super_manager',     'German', 	'kein Multi Domain Manager');
INSERT INTO txt VALUES ('no_super_manager',     'English', 	'no Multi Domain Manager');
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
INSERT INTO txt VALUES ('public_key',           'German', 	'&Ouml;ffentlicher Schl&uuml;ssel');
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
INSERT INTO txt VALUES ('local_rulebase',       'German', 	'Lokale Rulebase');
INSERT INTO txt VALUES ('local_rulebase',       'English', 	'Local Rulebase');
INSERT INTO txt VALUES ('global_rulebase',      'German', 	'Globale Rulebase');
INSERT INTO txt VALUES ('global_rulebase',      'English', 	'Global Rulebase');
INSERT INTO txt VALUES ('package',              'German', 	'Package');
INSERT INTO txt VALUES ('package',              'English', 	'Package');
INSERT INTO txt VALUES ('local_package',        'German', 	'Lokales Package');
INSERT INTO txt VALUES ('local_package',        'English', 	'Local Package');
INSERT INTO txt VALUES ('global_package',       'German', 	'Globales Package');
INSERT INTO txt VALUES ('global_package',       'English', 	'Global Package');
INSERT INTO txt VALUES ('details',              'German', 	'Details');
INSERT INTO txt VALUES ('details',              'English', 	'Details');
INSERT INTO txt VALUES ('import_status_details','German', 	'Importstatus-Details f&uuml;r ');
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
INSERT INTO txt VALUES ('import_id',            'German', 	'Import Id');
INSERT INTO txt VALUES ('import_id',            'English', 	'Import Id');
INSERT INTO txt VALUES ('duration',             'German', 	'Dauer[s]');
INSERT INTO txt VALUES ('duration',             'English', 	'Duration[s]');
INSERT INTO txt VALUES ('err_since_last_succ',  'German', 	'Fehler seit letztem erfolgreichen Import');
INSERT INTO txt VALUES ('err_since_last_succ',  'English', 	'Errors since last successful import');
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
INSERT INTO txt VALUES ('assign_user_to_group',	'German',	'Nutzer zu Gruppe zuordnen');
INSERT INTO txt VALUES ('assign_user_to_group',	'English',	'Assign user to group');
INSERT INTO txt VALUES ('remove_user_from_group','German',	'Nutzer von Gruppe entfernen');
INSERT INTO txt VALUES ('remove_user_from_group','English',	'Remove user from group');
INSERT INTO txt VALUES ('assign_user_group_to_role','German','Nutzer/Gruppe zu Rolle zuordnenen');
INSERT INTO txt VALUES ('assign_user_group_to_role','English','Assign user/group to role');
INSERT INTO txt VALUES ('remove_user_group_from_role','German','Nutzer/Gruppe von Rolle entfernen');
INSERT INTO txt VALUES ('remove_user_group_from_role','English','Remove user/group from role');
INSERT INTO txt VALUES ('assign_user_group',    'German', 	'Nutzer/Gruppe zuordnen');
INSERT INTO txt VALUES ('assign_user_group',    'English', 	'Assign user/group');
INSERT INTO txt VALUES ('remove_user_group',    'German', 	'Nutzer/Gruppe entfernen');
INSERT INTO txt VALUES ('remove_user_group',    'English', 	'Remove user/group');
INSERT INTO txt VALUES ('assign_user',          'German', 	'Nutzer zuordnen');
INSERT INTO txt VALUES ('assign_user',          'English', 	'Assign user');
INSERT INTO txt VALUES ('remove_user',          'German', 	'Nutzer entfernen');
INSERT INTO txt VALUES ('remove_user',          'English', 	'Remove user');
INSERT INTO txt VALUES ('get_user_from_ldap',   'German',	'Nutzer von LDAP holen');
INSERT INTO txt VALUES ('get_user_from_ldap',   'English',	'Get user from LDAP');
INSERT INTO txt VALUES ('select_from_ldap',     'German',	'von LDAP ausw&auml;hlen');
INSERT INTO txt VALUES ('select_from_ldap',     'English',	'Select from LDAP');
INSERT INTO txt VALUES ('synchronize', 			'German',	'Mit LDAP Synchronisieren');
INSERT INTO txt VALUES ('synchronize', 			'English',	'Synchronize to LDAP');
INSERT INTO txt VALUES ('delete_user',          'German', 	'Nutzer l&ouml;schen');
INSERT INTO txt VALUES ('delete_user',          'English', 	'Delete user');
INSERT INTO txt VALUES ('active_user',          'German', 	'Aktiver Nutzer');
INSERT INTO txt VALUES ('active_user',          'English', 	'Active User');
INSERT INTO txt VALUES ('user',                 'German', 	'Nutzer');
INSERT INTO txt VALUES ('user',                 'English', 	'User');
INSERT INTO txt VALUES ('group',                'German', 	'Gruppe');
INSERT INTO txt VALUES ('group',                'English', 	'Group');
INSERT INTO txt VALUES ('into_ldap',            'German', 	'in LDAP');
INSERT INTO txt VALUES ('into_ldap',            'English', 	'into LDAP');
INSERT INTO txt VALUES ('from_ldap',            'German', 	'von LDAP');
INSERT INTO txt VALUES ('from_ldap',            'English', 	'from LDAP');
INSERT INTO txt VALUES ('search_pattern',       'German', 	'Suchmuster');
INSERT INTO txt VALUES ('search_pattern',       'English', 	'Search Pattern');
INSERT INTO txt VALUES ('internal_group',       'German', 	'Interne Gruppe');
INSERT INTO txt VALUES ('internal_group',       'English', 	'Internal Group');
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
INSERT INTO txt VALUES ('test_connection',      'German', 	'Verbindung testen');
INSERT INTO txt VALUES ('test_connection',      'English', 	'Test connection');
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
INSERT INTO txt VALUES ('search_user_pwd',      'English', 	'Search User Password');
INSERT INTO txt VALUES ('write_user',           'German', 	'Schreibender Nutzer');
INSERT INTO txt VALUES ('write_user',           'English', 	'Write User');
INSERT INTO txt VALUES ('write_user_pwd',       'German', 	'Passwort Schreibender Nutzer');
INSERT INTO txt VALUES ('write_user_pwd',       'English', 	'Write User Password');
INSERT INTO txt VALUES ('tenant',               'German', 	'Mandant');
INSERT INTO txt VALUES ('tenant',               'English', 	'Tenant');
INSERT INTO txt VALUES ('global_tenant_name',   'German', 	'Globaler Mandantenname');
INSERT INTO txt VALUES ('global_tenant_name',   'English', 	'Global Tenant Name');
INSERT INTO txt VALUES ('pwMinLength',          'German', 	'Mindestl&auml;nge');
INSERT INTO txt VALUES ('pwMinLength',          'English', 	'Min Length');
INSERT INTO txt VALUES ('pwUpperCaseRequired',  'German', 	'Grossbuchstaben enthalten');
INSERT INTO txt VALUES ('pwUpperCaseRequired',  'English', 	'Upper Case Required');
INSERT INTO txt VALUES ('pwLowerCaseRequired',  'German', 	'Kleinbuchstaben enthalten');
INSERT INTO txt VALUES ('pwLowerCaseRequired',  'English', 	'Lower Case Required');
INSERT INTO txt VALUES ('pwNumberRequired',     'German', 	'Ziffern enthalten');
INSERT INTO txt VALUES ('pwNumberRequired',     'English', 	'Number Required');
INSERT INTO txt VALUES ('pwSpecialCharactersRequired','German','Sonderzeichen enthalten (!?(){}=~$%&amp;#*-+.,_)');
INSERT INTO txt VALUES ('pwSpecialCharactersRequired','English','Special Characters Required (!?(){}=~$%&amp;#*-+.,_)');
INSERT INTO txt VALUES ('default_language',     'German', 	'Standardsprache');
INSERT INTO txt VALUES ('default_language',     'English', 	'Default Language');
INSERT INTO txt VALUES ('elementsPerFetch',     'German', 	'UI - Pro Abruf geholte Elemente');
INSERT INTO txt VALUES ('elementsPerFetch',     'English', 	'UI - Elements per fetch');
INSERT INTO txt VALUES ('maxInitialFetchesRightSidebar','German','Max initiale Abrufe rechte Randleiste');
INSERT INTO txt VALUES ('maxInitialFetchesRightSidebar','English','Max initial fetches right sidebar');
INSERT INTO txt VALUES ('autoFillRightSidebar', 'German', 	'Komplettes F&uuml;llen rechte Randleiste');
INSERT INTO txt VALUES ('autoFillRightSidebar', 'English', 	'Completely auto-fill right sidebar');
INSERT INTO txt VALUES ('minCollapseAllDevices','German', 	'Devices zu Beginn eingeklappt ab');
INSERT INTO txt VALUES ('minCollapseAllDevices','English', 	'Devices collapsed at beginning from');
INSERT INTO txt VALUES ('sessionTimeout',       'German', 	'Sitzungs-Timeout (in Minuten)');
INSERT INTO txt VALUES ('sessionTimeout',       'English', 	'Session timeout (in minutes)');
INSERT INTO txt VALUES ('sessionTimeoutNoticePeriod', 'German','Benachrichtigung vor Sitzungs-Timeout (in Minuten)');
INSERT INTO txt VALUES ('sessionTimeoutNoticePeriod', 'English','Warning before session timeout (in minutes)');
INSERT INTO txt VALUES ('maxMessages',          'German', 	'Max Anzahl Nachrichten');
INSERT INTO txt VALUES ('maxMessages',          'English', 	'Max number of messages');
INSERT INTO txt VALUES ('messageViewTime',      'German', 	'Nachrichten-Anzeigedauer (in Sekunden)');
INSERT INTO txt VALUES ('messageViewTime',      'English', 	'Message view time (in seconds)');
INSERT INTO txt VALUES ('dataRetentionTime',    'German', 	'Datenaufbewahrungszeit (in Tagen)');
INSERT INTO txt VALUES ('dataRetentionTime',    'English', 	'Data retention time (in days)');
INSERT INTO txt VALUES ('dailyCheckStartAt',    'German', 	'Startzeit t&auml;glicher Check');
INSERT INTO txt VALUES ('dailyCheckStartAt',    'English', 	'Daily check start at');
INSERT INTO txt VALUES ('maxImportDuration',    'German', 	'Max erlaubte Importdauer (in Stunden)');
INSERT INTO txt VALUES ('maxImportDuration',    'English', 	'Max allowed import duration (in hours)');
INSERT INTO txt VALUES ('maxImportInterval',    'German', 	'Max erlaubtes Importintervall (in Stunden)');
INSERT INTO txt VALUES ('maxImportInterval',    'English', 	'Max import interval (in hours)');
INSERT INTO txt VALUES ('importSleepTime',      'German', 	'Importintervall (in Sekunden)');
INSERT INTO txt VALUES ('importSleepTime',      'English', 	'Import sleep time (in seconds)');
INSERT INTO txt VALUES ('importCheckCertificates',      'German', 	'Zertifikate beim Import pr&uuml;fen');
INSERT INTO txt VALUES ('importCheckCertificates',      'English', 	'Check certificates during import');
INSERT INTO txt VALUES ('importSuppressCertificateWarnings',      'German', 	'Zertifikatswarnungen unterdr&uuml;cken');
INSERT INTO txt VALUES ('importSuppressCertificateWarnings',      'English', 	'Suppress certificate warnings');
INSERT INTO txt VALUES ('fwApiElementsPerFetch','German', 	'FW API - Pro Abruf geholte Elemente');
INSERT INTO txt VALUES ('fwApiElementsPerFetch','English', 	'FW API - Elements per fetch');
INSERT INTO txt VALUES ('autoDiscoverSleepTime','German', 	'Autodiscover-Intervall (in Stunden)');
INSERT INTO txt VALUES ('autoDiscoverSleepTime','English', 	'Auto-discovery sleep time (in hours)');
INSERT INTO txt VALUES ('autoDiscoverStartAt',  'German', 	'Autodiscover-Start');
INSERT INTO txt VALUES ('autoDiscoverStartAt',  'English', 	'Auto-discovery start at');
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
INSERT INTO txt VALUES ('reqAvailableTaskTypes','German', 	'Verf&uuml;gbare Auftragstypen');
INSERT INTO txt VALUES ('reqAvailableTaskTypes','English', 	'Available Task Types');
INSERT INTO txt VALUES ('reqAllowObjectSearch', 'German', 	'Objektsuche erlauben');
INSERT INTO txt VALUES ('reqAllowObjectSearch', 'English', 	'Allow object search');
INSERT INTO txt VALUES ('reqApprovalDeadline',  'German', 	'Deadline f&uuml;r Genehmigungen (in Tagen)');
INSERT INTO txt VALUES ('reqApprovalDeadline',  'English', 	'Approval deadline (in days)');
INSERT INTO txt VALUES ('reqAutoCreateImplTasks','German', 	'Autom. Erzeugen von Implementierungs-Auftr&auml;gen');
INSERT INTO txt VALUES ('reqAutoCreateImplTasks','English', 'Auto-create implementation tasks');
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
INSERT INTO txt VALUES ('last_pw_change',       'English', 	'Last Password Change');
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
INSERT INTO txt VALUES ('add_user_local',       'German', 	'Nutzer lokal hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_user_local',       'English', 	'Add user locally');
INSERT INTO txt VALUES ('update_user',          'German', 	'Nutzer &auml;ndern');
INSERT INTO txt VALUES ('update_user',          'English', 	'Update user');
INSERT INTO txt VALUES ('update_user_local',    'German', 	'Nutzer lokal &auml;ndern');
INSERT INTO txt VALUES ('update_user_local',    'English', 	'Update user locally');
INSERT INTO txt VALUES ('save_user',            'German', 	'Nutzer in LDAP speichern');
INSERT INTO txt VALUES ('save_user',            'English', 	'Save user in LDAP');
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
INSERT INTO txt VALUES ('sync_users',           'German', 	'Nutzer synchronisieren');
INSERT INTO txt VALUES ('sync_users',           'English', 	'Synchronize Users');
INSERT INTO txt VALUES ('save_group',           'German', 	'Gruppe in LDAP speichern');
INSERT INTO txt VALUES ('save_group',           'English', 	'Save group in LDAP');
INSERT INTO txt VALUES ('fetch_roles',          'German', 	'Rollen abholen');
INSERT INTO txt VALUES ('fetch_roles',          'English', 	'Fetch Roles');
INSERT INTO txt VALUES ('fetch_data',           'German', 	'Daten holen');
INSERT INTO txt VALUES ('fetch_data',           'English', 	'Fetch data');
INSERT INTO txt VALUES ('fetch_ldap_conn',      'German', 	'LDAP-Verbindungen holen');
INSERT INTO txt VALUES ('fetch_ldap_conn',      'English', 	'Fetch LDAP connections');
INSERT INTO txt VALUES ('search_users',         'German', 	'Nutzer suchen');
INSERT INTO txt VALUES ('search_users',         'English', 	'Search Users');
INSERT INTO txt VALUES ('get_tenant_data',      'German', 	'Mandantendaten abholen');
INSERT INTO txt VALUES ('get_tenant_data',      'English', 	'Get tenant data');
INSERT INTO txt VALUES ('add_tenant',           'German', 	'Mandant hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_tenant',           'English', 	'Add tenant');
INSERT INTO txt VALUES ('edit_tenant',          'German', 	'Mandant &auml;ndern');
INSERT INTO txt VALUES ('edit_tenant',          'English', 	'Edit tenant');
INSERT INTO txt VALUES ('save_tenant',          'German', 	'Mandant speichern');
INSERT INTO txt VALUES ('save_tenant',          'English', 	'Save tenant');
INSERT INTO txt VALUES ('delete_tenant',        'German', 	'Mandant l&ouml;schen');
INSERT INTO txt VALUES ('delete_tenant',        'English', 	'Delete tenant');
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
INSERT INTO txt VALUES ('save_settings',        'German',   'Einstellungen speichern');
INSERT INTO txt VALUES ('save_settings',        'English',  'Save settings');
INSERT INTO txt VALUES ('available_states',     'German',   'Verf&uuml;gbare Stati');
INSERT INTO txt VALUES ('available_states',     'English',  'Available states');
INSERT INTO txt VALUES ('add_state',            'German',   'Status hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_state',            'English',  'Add state');
INSERT INTO txt VALUES ('edit_state',           'German',   'Status bearbeiten');
INSERT INTO txt VALUES ('edit_state',           'English',  'Edit state');
INSERT INTO txt VALUES ('select_state',         'German',   'Status ausw&auml;hlen');
INSERT INTO txt VALUES ('select_state',         'English',  'Select state');
INSERT INTO txt VALUES ('delete_state',         'German',   'Status l&ouml;schen');
INSERT INTO txt VALUES ('delete_state',         'English',  'Delete state');
INSERT INTO txt VALUES ('from_state',           'German',   'Von Status');
INSERT INTO txt VALUES ('from_state',           'English',  'From state');
INSERT INTO txt VALUES ('to_states',            'German',   'Nach Stati');
INSERT INTO txt VALUES ('to_states',            'English',  'To states');
INSERT INTO txt VALUES ('allowed_transitions',  'German',   'erlaubte &Uuml;berg&auml;nge');
INSERT INTO txt VALUES ('allowed_transitions',  'English',  'Allowed transitions');
INSERT INTO txt VALUES ('special_states',       'German',   'Spezielle Stati');
INSERT INTO txt VALUES ('special_states',       'English',  'Special states');
INSERT INTO txt VALUES ('lowest_input_state',   'German',   'Niedrigster Eingangsstatus');
INSERT INTO txt VALUES ('lowest_input_state',   'English',  'Lowest input state');
INSERT INTO txt VALUES ('lowest_started_state', 'German',   'Niedrigster Bearbeitungsstatus');
INSERT INTO txt VALUES ('lowest_started_state', 'English',  'Lowest started state');
INSERT INTO txt VALUES ('lowest_end_state',     'German',   'Niedrigster Ausgangsstatus');
INSERT INTO txt VALUES ('lowest_end_state',     'English',  'Lowest end state');
INSERT INTO txt VALUES ('derived_state',        'German',   'Abgeleiteter Status');
INSERT INTO txt VALUES ('derived_state',        'English',  'Derived state');


-- monitoring
INSERT INTO txt VALUES ('open_alerts',          'German', 	'Offene Alarme');
INSERT INTO txt VALUES ('open_alerts',          'English', 	'Open Alerts');
INSERT INTO txt VALUES ('all_alerts',           'German', 	'Alle Alarme');
INSERT INTO txt VALUES ('all_alerts',           'English', 	'All Alerts');
INSERT INTO txt VALUES ('fetch_alerts',         'German', 	'Abholen Alarme');
INSERT INTO txt VALUES ('fetch_alerts',         'English', 	'Fetch Alerts');
INSERT INTO txt VALUES ('fetch_log_entrys',     'German', 	'Abholen Log-Eintr&auml;ge');
INSERT INTO txt VALUES ('fetch_log_entrys',     'English', 	'Fetch Log Entrys');
INSERT INTO txt VALUES ('severity',             'German', 	'Schwere');
INSERT INTO txt VALUES ('severity',             'English', 	'Severity');
INSERT INTO txt VALUES ('timestamp',            'German', 	'Zeitstempel');
INSERT INTO txt VALUES ('timestamp',            'English', 	'Timestamp');
INSERT INTO txt VALUES ('title',                'German', 	'Titel');
INSERT INTO txt VALUES ('title',                'English', 	'Title');
INSERT INTO txt VALUES ('suspected_cause',      'German', 	'Vermutliche Ursache');
INSERT INTO txt VALUES ('suspected_cause',      'English', 	'Suspected Cause');
INSERT INTO txt VALUES ('device',				'German', 	'Ger&auml;t');
INSERT INTO txt VALUES ('device',				'English', 	'Device');
INSERT INTO txt VALUES ('object_type',          'German', 	'Objekt-Typ');
INSERT INTO txt VALUES ('object_type',          'English', 	'Object Type');
INSERT INTO txt VALUES ('object_name',          'German', 	'Objektname');
INSERT INTO txt VALUES ('object_name',          'English', 	'Object Name');
INSERT INTO txt VALUES ('object_uid',           'German', 	'Objekt-Uid');
INSERT INTO txt VALUES ('object_uid',           'English', 	'Object Uid');
INSERT INTO txt VALUES ('rule_uid',             'German', 	'Regel-Uid');
INSERT INTO txt VALUES ('rule_uid',             'English', 	'Rule Uid');
INSERT INTO txt VALUES ('rule_id',              'German', 	'Regel-Id');
INSERT INTO txt VALUES ('rule_id',              'English', 	'Rule Id');
INSERT INTO txt VALUES ('import',       	    'German', 	'Import');
INSERT INTO txt VALUES ('import',    	        'English', 	'Import');
INSERT INTO txt VALUES ('import_logs',          'German', 	'Import-Logs');
INSERT INTO txt VALUES ('import_logs',          'English', 	'Import Logs');
INSERT INTO txt VALUES ('import_status',       	'German', 	'Import-Status');
INSERT INTO txt VALUES ('import_status',    	'English', 	'Import Status');
INSERT INTO txt VALUES ('ui_messages',          'German', 	'UI-Nachrichten');
INSERT INTO txt VALUES ('ui_messages',          'English', 	'Ui Messages');
INSERT INTO txt VALUES ('autodiscovery',        'German', 	'Autodiscovery');
INSERT INTO txt VALUES ('autodiscovery',        'English', 	'Autodiscovery');
INSERT INTO txt VALUES ('autodiscovery_logs',   'German', 	'Autodiscovery-Logs');
INSERT INTO txt VALUES ('autodiscovery_logs',   'English', 	'Autodiscovery Logs');
INSERT INTO txt VALUES ('background_checks',    'German', 	'Hintergrund-Checks');
INSERT INTO txt VALUES ('background_checks',    'English', 	'Background Checks');
INSERT INTO txt VALUES ('daily_checks',         'German', 	'T&auml;gliche Checks');
INSERT INTO txt VALUES ('daily_checks',         'English', 	'Daily Checks');
INSERT INTO txt VALUES ('alert',                'German', 	'Alarm');
INSERT INTO txt VALUES ('alert',                'English', 	'Alert');
INSERT INTO txt VALUES ('alerts',               'German', 	'Alarme');
INSERT INTO txt VALUES ('alerts',               'English', 	'Alerts');
INSERT INTO txt VALUES ('acknowledge',          'German', 	'Best&auml;tigen');
INSERT INTO txt VALUES ('acknowledge',          'English', 	'Acknowledge');
INSERT INTO txt VALUES ('acknowledged_by',      'German', 	'Best&auml;tigt von');
INSERT INTO txt VALUES ('acknowledged_by',      'English', 	'Acknowledged by');
INSERT INTO txt VALUES ('acknowledge_alert',    'German', 	'Alarm best&auml;tigen');
INSERT INTO txt VALUES ('acknowledge_alert',    'English', 	'Acknowledge alert');
INSERT INTO txt VALUES ('acknowledge_action',   'German', 	'Aktion best&auml;tigen');
INSERT INTO txt VALUES ('acknowledge_action',   'English', 	'Acknowledge action');
INSERT INTO txt VALUES ('confirm',              'German', 	'Best&auml;tigen');
INSERT INTO txt VALUES ('confirm',              'English', 	'Confirm');
INSERT INTO txt VALUES ('found_by',             'German', 	'Gefunden von');
INSERT INTO txt VALUES ('found_by',             'English', 	'Found by');
INSERT INTO txt VALUES ('nothing_more_to_change','German', 	'Keine weiteren &Auml;nderungen gefunden');
INSERT INTO txt VALUES ('nothing_more_to_change','English', 'Nothing more to change');
INSERT INTO txt VALUES ('no_open_alerts',       'German', 	'Keine offenen Alarme');
INSERT INTO txt VALUES ('no_open_alerts',       'English', 	'Currently no open alerts');
INSERT INTO txt VALUES ('handle_alert',         'German', 	'Alarm bearbeiten');
INSERT INTO txt VALUES ('handle_alert',         'English', 	'Handle alert');
INSERT INTO txt VALUES ('new_managements',      'German', 	'Neue Managements');
INSERT INTO txt VALUES ('new_managements',      'English', 	'New Managements');
INSERT INTO txt VALUES ('new_devices',          'German', 	'Neue Gateways');
INSERT INTO txt VALUES ('new_devices',          'English', 	'New Gateways');
INSERT INTO txt VALUES ('deleted_managements',  'German', 	'Gel&ouml;schte Managements');
INSERT INTO txt VALUES ('deleted_managements',  'English', 	'Deleted Managements');
INSERT INTO txt VALUES ('deleted_devices',      'German', 	'Gel&ouml;schte Gateways');
INSERT INTO txt VALUES ('deleted_devices',      'English', 	'Deleted Gateways');
INSERT INTO txt VALUES ('analyze_actions',      'German', 	'Aktionen analysieren');
INSERT INTO txt VALUES ('analyze_actions',      'English', 	'Analyze Actions');
INSERT INTO txt VALUES ('do_all_changes',       'German', 	'Alle &Auml;nderungen ausf&uuml;hren');
INSERT INTO txt VALUES ('do_all_changes',       'English', 	'Make all changes');
INSERT INTO txt VALUES ('change_management_state','German', 'Mamagement-Status &auml;ndern');
INSERT INTO txt VALUES ('change_management_state','English','Change management state');
INSERT INTO txt VALUES ('change_device_state',  'German',   'Gateway-Status &auml;ndern');
INSERT INTO txt VALUES ('change_device_state',  'English',  'Change gateway state');
INSERT INTO txt VALUES ('disable',				'German', 	'Deaktivieren');
INSERT INTO txt VALUES ('disable',				'English', 	'Disable');
INSERT INTO txt VALUES ('nothing',              'German', 	'Nichts');
INSERT INTO txt VALUES ('nothing',              'English',  'Nothing');
INSERT INTO txt VALUES ('sample_data',          'German', 	'Beispieldaten');
INSERT INTO txt VALUES ('sample_data',	        'English', 	'Sample Data');
INSERT INTO txt VALUES ('sample_data_found_in', 'German', 	'Beispieldaten gefunden in: ');
INSERT INTO txt VALUES ('sample_data_found_in',	'English', 	'Sample data found in: ');
INSERT INTO txt VALUES ('no_sample_data_found', 'German', 	'keine Beispieldaten gefunden');
INSERT INTO txt VALUES ('no_sample_data_found',	'English', 	'no sample data found');
INSERT INTO txt VALUES ('import_issues_found',  'German', 	' Importprobleme gefunden');
INSERT INTO txt VALUES ('import_issues_found',  'English',	' import issues found');
INSERT INTO txt VALUES ('no_import_issues_found','German', 	'keine Importprobleme gefunden');
INSERT INTO txt VALUES ('no_import_issues_found','English',	'no import issues found');
INSERT INTO txt VALUES ('ran_into_exception',   'German', 	'Exception ausgel&ouml;st: ');
INSERT INTO txt VALUES ('ran_into_exception',	'English', 	'Ran into exception: ');
INSERT INTO txt VALUES ('daily_sample_data_check','German', 'T&auml;glicher Check auf Beispieldaten');
INSERT INTO txt VALUES ('daily_sample_data_check','English','Scheduled Daily Sample Data Check');
INSERT INTO txt VALUES ('daily_importer_check', 'German',   'T&auml;glicher Check der Importer');
INSERT INTO txt VALUES ('daily_importer_check', 'English',  'Scheduled Daily Importer Check');
INSERT INTO txt VALUES ('scheduled_autodiscovery','German', 'Termingesteuerte Autodiscovery');
INSERT INTO txt VALUES ('scheduled_autodiscovery','English','Scheduled Autodiscovery');
INSERT INTO txt VALUES ('manual_autodiscovery', 'German', 	'Manuelle Autodiscovery');
INSERT INTO txt VALUES ('manual_autodiscovery', 'English', 	'Manual Autodiscovery');
INSERT INTO txt VALUES ('changes_found',        'German', 	' &Auml;nderungen gefunden');
INSERT INTO txt VALUES ('changes_found',        'English',	' changes found');
INSERT INTO txt VALUES ('found_no_changes',     'German', 	'keine &Auml;nderungen gefunden');
INSERT INTO txt VALUES ('found_no_changes',     'English',	'no changes found');


-- help pages
INSERT INTO txt VALUES ('filter_syntax',        'German', 	'Filtersyntax');
INSERT INTO txt VALUES ('filter_syntax',        'English', 	'Filter Syntax');
INSERT INTO txt VALUES ('report_data_output',   'German', 	'Reportdatenausgabe');
INSERT INTO txt VALUES ('report_data_output',   'English', 	'Report Data Output');
INSERT INTO txt VALUES ('left_sidebar',         'German', 	'Linke Randleiste');
INSERT INTO txt VALUES ('left_sidebar',         'English', 	'Left Sidebar');
INSERT INTO txt VALUES ('right_sidebar',        'German', 	'Rechte Randleiste');
INSERT INTO txt VALUES ('right_sidebar',        'English', 	'Right Sidebar');
INSERT INTO txt VALUES ('api_general',          'German', 	'API allgemein');
INSERT INTO txt VALUES ('api_general',          'English', 	'API general');
INSERT INTO txt VALUES ('api_user_mgmt',        'German', 	'User Management');
INSERT INTO txt VALUES ('api_user_mgmt',        'English', 	'User Management');
INSERT INTO txt VALUES ('api_user_mgmt_head',   'German', 	'REST Dokumentation');
INSERT INTO txt VALUES ('api_user_mgmt_head',   'English', 	'REST Documentation');
INSERT INTO txt VALUES ('api_umgmt_auth',       'German', 	'Authentisierung');
INSERT INTO txt VALUES ('api_umgmt_auth',       'English', 	'Authentication');
INSERT INTO txt VALUES ('umgmt_api_explain',    'German', 	'F&uuml;r API-Abfragen rund um das Thema Benutzerauthentifizierung existiert eine eigenst&auml;ndige REST-API, deren Dokumentation sich hier findet:');
INSERT INTO txt VALUES ('umgmt_api_explain',    'English', 	'For API calls regarding user management purposes there is a dedicated REST API which is documented here:');
INSERT INTO txt VALUES ('umgmt_api_explain2',   'German', 	'Bitte beachten Sie, dass die Interaktion via "Try it out" aktuell nur f&uuml;r den initialen AuthenticationToken-Call zur Verf&uuml;gung steht. F&uuml;r alle darauffolgenden Aufrufe muss der Authentication Token (JWT) als Header wie folgt mitgeschickt werden: <br><pre>--header ''Authorization: Bearer JWT''</pre>');
INSERT INTO txt VALUES ('umgmt_api_explain2',   'English', 	'Please note that API interaction via "Try it out" is currently only possible for the AuthenticationToken call. All subsequent calls need to pass the authentication token (JWT) as header information as follows: <br><pre>--header ''Authorization: Bearer JWT''</pre>');
INSERT INTO txt VALUES ('api_fwo',              'German', 	'FWO API');
INSERT INTO txt VALUES ('api_fwo',              'English', 	'FWO API');
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
INSERT INTO txt VALUES ('sample_queries',       'German', 	'Beispiel-Querys');
INSERT INTO txt VALUES ('sample_queries',       'English', 	'Sample Queries');
INSERT INTO txt VALUES ('sample_mutation',      'German', 	'Beispiel-Mutation');
INSERT INTO txt VALUES ('sample_mutation',      'English', 	'Sample Mutation');
INSERT INTO txt VALUES ('admin_access',         'German', 	'Admin-Zugang');
INSERT INTO txt VALUES ('admin_access',         'English', 	'Admin Access');
INSERT INTO txt VALUES ('keywords',             'German', 	'Schl&uuml;sselw&ouml;rter (alternative Schreibweisen in Klammern)');
INSERT INTO txt VALUES ('keywords',             'English', 	'Keywords (alternative spellings in brackets)');
INSERT INTO txt VALUES ('operators',            'German', 	'Operatoren');
INSERT INTO txt VALUES ('operators',            'English', 	'Operators');
INSERT INTO txt VALUES ('examples',             'German', 	'Beispiele');
INSERT INTO txt VALUES ('examples',             'English', 	'Examples');
INSERT INTO txt VALUES ('jwt_corr_login',       'German', 	'JWT nach korrekter Anmeldung');
INSERT INTO txt VALUES ('jwt_corr_login',       'English', 	'Get JWT with correct login');
INSERT INTO txt VALUES ('err_incorr_login',     'German', 	'Fehler nach inkorrekter Anmeldung');
INSERT INTO txt VALUES ('err_incorr_login',     'English', 	'Error with incorrect login');
INSERT INTO txt VALUES ('get_with_admin',       'German', 	'Holen der Namen aller Firewall-Managements mit Admin-Zugang');
INSERT INTO txt VALUES ('get_with_admin',       'English', 	'Get the names of all firewall managements using admin access');
INSERT INTO txt VALUES ('get_with_jwt',         'German', 	'Holen der Namen aller Firewall-Managements mit Standard-JWT-Zugang');
INSERT INTO txt VALUES ('get_with_jwt',         'English', 	'Get the names of all firewall managements using standard JWT access');
INSERT INTO txt VALUES ('get_with_jwt_role',    'German', 	'Holen der Namen aller Firewall-Managements mit Standard-JWT-Zugang und spezifischer Rolle');
INSERT INTO txt VALUES ('get_with_jwt_role',    'English', 	'Get the names of all firewall managements using standard JWT access and specifying a certain role');
INSERT INTO txt VALUES ('get_single_dev_rules', 'German', 	'Alle aktuellen Regeln von Gateway mit ID 1 holen');
INSERT INTO txt VALUES ('get_single_dev_rules', 'English', 	'Get all current rules of gateway with ID 1');
INSERT INTO txt VALUES ('parameters',           'German', 	'Parameter');
INSERT INTO txt VALUES ('parameters',           'English',  'Parameters');
INSERT INTO txt VALUES ('introduction',         'German',   'Einf&uuml;hrung');
INSERT INTO txt VALUES ('introduction',         'English',  'Introduction');
INSERT INTO txt VALUES ('architecture',         'German',   'Die Firewall Orchestrator Architektur');
INSERT INTO txt VALUES ('architecture',         'English',  'Firewall Orchestrator Architecture');

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
-- 7000-7999: Monitoring
-- 8000-8999: Request

-- user messages
INSERT INTO txt VALUES ('U0001', 'German',  'Eingabetext wurde um nicht erlaubte Zeichen gek&uuml;rzt');
INSERT INTO txt VALUES ('U0001', 'English', 'Input text has been shortened by not allowed characters');

INSERT INTO txt VALUES ('U1002', 'German',  'Sind sie sicher, dass sie folgende Reportvorlage l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U1002', 'English', 'Do you really want to delete report template');

INSERT INTO txt VALUES ('U2002', 'German',  'Sind sie sicher, dass sie folgenden Reporttermin l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U2002', 'English', 'Do you really want to delete report schedule ');

INSERT INTO txt VALUES ('U3002', 'German',  'Sind sie sicher, dass sie folgenden Report l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U3002', 'English', 'Do you really want to delete generated report ');

INSERT INTO txt VALUES ('U5001', 'German',  'Setup und Verwaltung des Firewall Orchestrator. Bitte eine Einstellung in der linken Randleiste ausw&auml;hlen.');
INSERT INTO txt VALUES ('U5001', 'English', 'Setup and administration of Firewall Orchestrator. Please choose a setting in the left sidebar.');
INSERT INTO txt VALUES ('U5011', 'German',  'Verwaltung der technischen Komponenten (nur f&uuml;r Admin)');
INSERT INTO txt VALUES ('U5011', 'English', 'Administration of technical components (only by admin)');
INSERT INTO txt VALUES ('U5012', 'German',  'Verwaltung der Nutzerautorisierung (nur f&uuml;r Admin)');
INSERT INTO txt VALUES ('U5012', 'English', 'User authorization management (only by admin)');
INSERT INTO txt VALUES ('U5013', 'German',  'Verwaltung der Voreinstellungen (nur f&uuml;r Admin)');
INSERT INTO txt VALUES ('U5013', 'English', 'Administration of default settings (only by admin)');
INSERT INTO txt VALUES ('U5014', 'German',  'Pers&ouml;nliche Nutzereinstellungen');
INSERT INTO txt VALUES ('U5014', 'English', 'Personal settings for the individual user');
INSERT INTO txt VALUES ('U5015', 'German',  'Verwaltung der Workflow-Voreinstellungen (nur f&uuml;r Admin)');
INSERT INTO txt VALUES ('U5015', 'English', 'Administration of workflow settings (only by admin)');

INSERT INTO txt VALUES ('U5101', 'German',  'Sind sie sicher, dass sie folgendes Management l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5101', 'English', 'Are you sure you want to delete management: ');
INSERT INTO txt VALUES ('U5102', 'German',  'L&ouml;scht alle Beispielmanagements (auf "_demo" endend) und alle zugeordneten Gateways');
INSERT INTO txt VALUES ('U5102', 'English', 'Deletes all sample managements (ending with "_demo") and related Gateways');
INSERT INTO txt VALUES ('U5103', 'German',  'Sind sie sicher, dass sie folgendes Gateway l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5103', 'English', 'Are you sure you want to delete gateway: ');
INSERT INTO txt VALUES ('U5104', 'German',  'Sind sie sicher, dass sie Import zur&uuml;cksetzen wollen? Er wurde vor ');
INSERT INTO txt VALUES ('U5104', 'English', 'Do you really want to rollback import? It was started ');
INSERT INTO txt VALUES ('U5105', 'German',  ' Minuten gestartet und k&ouml;nnte noch laufen.');
INSERT INTO txt VALUES ('U5105', 'English', ' minutes ago and might still be running.');
INSERT INTO txt VALUES ('U5106', 'German',  'Der Import ist zwischenzeitlich durchgelaufen');
INSERT INTO txt VALUES ('U5106', 'English', 'The import has finished inbetween');
INSERT INTO txt VALUES ('U5107', 'German',  'Der Import wurde zur&uuml;ckgesetzt');
INSERT INTO txt VALUES ('U5107', 'English', 'The import has been rolled back');
INSERT INTO txt VALUES ('U5111', 'German',  'Verwaltung aller verbundenen Managements');
INSERT INTO txt VALUES ('U5111', 'English', 'Administrate the connected managements');
INSERT INTO txt VALUES ('U5112', 'German',  'Verwaltung aller verbundenen Gateways');
INSERT INTO txt VALUES ('U5112', 'English', 'Administrate the connected gateways');
INSERT INTO txt VALUES ('U5113', 'German',  'Statusanzeige aller Importjobs. M&ouml;glichkeit zum Rollback, wenn n&ouml;tig');
INSERT INTO txt VALUES ('U5113', 'English', 'Show the status of all import jobs. Possibility to rollback if necessary');
INSERT INTO txt VALUES ('U5114', 'German',  'Auto Discovery derzeit nicht implementiert');
INSERT INTO txt VALUES ('U5114', 'English', 'Auto discovery currently not implemented');
INSERT INTO txt VALUES ('U5115', 'German',  'per Auto Discovery gefundene &Auml;nderungen');
INSERT INTO txt VALUES ('U5115', 'English', '# changes found by auto discovery');

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
INSERT INTO txt VALUES ('U5313', 'German',  'Verwaltung der Statusdefinitionen f&uuml;r die Workflows. Vorsicht bei &Auml;nderungen an einem bereits verwendeten Workflow!');
INSERT INTO txt VALUES ('U5313', 'English', 'Set the state definitions of the workflows. Be careful when changing workflow already in use!');
INSERT INTO txt VALUES ('U5314', 'German',  'Verwaltung der Einstellungen f&uuml;r den Auftrags-Workflow. Vorsicht bei &Auml;nderungen an einem bereits verwendeten Workflow!');
INSERT INTO txt VALUES ('U5314', 'English', 'Customize the request workflow. Be careful when changing workflow already in use!');
INSERT INTO txt VALUES ('U5315', 'German',  'Sind sie sicher, dass sie die Einstellungen &auml;ndern wollen? &Auml;nderungen an bereits verwendeten Workflows k&ouml;nnen unerwartete Auswirkungen haben.');
INSERT INTO txt VALUES ('U5315', 'English', 'Are you sure you want to change the settings? Changes on workflows already in use may have unexpected consequences.');
INSERT INTO txt VALUES ('U5316', 'German',  'Definition der Statusmatrizen f&uuml;r die Workflows. Vorsicht bei &Auml;nderungen an einem bereits verwendeten Workflow!');
INSERT INTO txt VALUES ('U5316', 'English', 'Define the state matrices of the workflows. Be careful when changing workflow already in use!');
INSERT INTO txt VALUES ('U5317', 'German',  'Verwaltung der Aktionsdefinitionen f&uuml;r die Workflows. Vorsicht bei &Auml;nderungen an einem bereits verwendeten Workflow!');
INSERT INTO txt VALUES ('U5317', 'English', 'Set the action definitions of the workflows. Be careful when changing workflow already in use!');

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

INSERT INTO txt VALUES ('U5501', 'German',  'Sind sie sicher, dass sie folgenden Status l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5501', 'English', 'Are you sure you want to delete state: ');
INSERT INTO txt VALUES ('U5502', 'German',  'Sind sie sicher, dass sie folgende Aktion l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5502', 'English', 'Are you sure you want to delete action: ');

INSERT INTO txt VALUES ('U7001', 'German',  '&Uuml;berblick der Ereignisse im Firewall Orchestrator');
INSERT INTO txt VALUES ('U7001', 'English', 'Alerts and events inside Firewall Orchestrator');
INSERT INTO txt VALUES ('U7002', 'German',  'Daten sind dann verloren. Erw&auml;gen Sie eine Deaktivierung.');
INSERT INTO txt VALUES ('U7002', 'English', 'Data will be lost. Consider deactivation.');
INSERT INTO txt VALUES ('U7003', 'German',  'L&ouml;scht alle Beispieldaten (auf "_demo" endend): Managements, Gateways, Nutzer, Mandanten, Gruppen');
INSERT INTO txt VALUES ('U7003', 'English', 'Deletes all sample data (ending with "_demo"): managements, gateways, users, tenants, groups');
INSERT INTO txt VALUES ('U7101', 'German',  'Archiv der Alarme mit Best&auml;tigungen');
INSERT INTO txt VALUES ('U7101', 'English', 'View the past alerts with acknowledgements');
INSERT INTO txt VALUES ('U7201', 'German',  'Archiv der Importer-Nachrichten');
INSERT INTO txt VALUES ('U7201', 'English', 'View the past importer messages');
INSERT INTO txt VALUES ('U7301', 'German',  'Archiv der eigenen Nutzernachrichten');
INSERT INTO txt VALUES ('U7301', 'English', 'View the past own UI user messages');
INSERT INTO txt VALUES ('U7401', 'German',  'Archiv der Autodiscovery-Nachrichten');
INSERT INTO txt VALUES ('U7401', 'English', 'View the past autodiscovery messages');
INSERT INTO txt VALUES ('U7501', 'German',  'Archiv der Nachrichten der t&auml;glichen Checks');
INSERT INTO txt VALUES ('U7501', 'English', 'View the past daily check messages');

INSERT INTO txt VALUES ('U8001', 'German',  'Sind sie sicher, dass sie l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U8001', 'English', 'Are you sure you want to delete: ');
INSERT INTO txt VALUES ('U8002', 'German',  'Neue Genehmigung zum Auftrag hinzugef&uuml;gt.');
INSERT INTO txt VALUES ('U8002', 'English', 'New approval added to task.');


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
INSERT INTO txt VALUES ('E0021', 'German',  'Bitte &uuml;berpr&uuml;fen Sie ihre Einstellungen');
INSERT INTO txt VALUES ('E0021', 'English', 'Please check your settings');

INSERT INTO txt VALUES ('E1001', 'German',  'Vor dem Generieren des Reports bitte mindestens ein Device auf der linken Seite ausw&auml;hlen');
INSERT INTO txt VALUES ('E1001', 'English', 'Please select at least one device in the left side-bar before generating a report');
INSERT INTO txt VALUES ('E1002', 'German',  'Kein Report vorhanden zum Exportieren. Bitte zuerst Report generieren!');
INSERT INTO txt VALUES ('E1002', 'English', 'No generated report to export. Please generate report first!');
INSERT INTO txt VALUES ('E1003', 'German',  'Die Datenabholung wurde abgebrochen. M&ouml;glicherweise werden nicht alle verf&uuml;gbaren Daten dargestellt.');
INSERT INTO txt VALUES ('E1003', 'English', 'Data fetch was cancelled. Possibly not all available data are displayed');
INSERT INTO txt VALUES ('E1004', 'German',  'Vorlage konnte nicht gespeichert werden');
INSERT INTO txt VALUES ('E1004', 'English', 'Template could not be saved');
INSERT INTO txt VALUES ('E1005', 'German',  'Vorlage konnte nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E1005', 'English', 'Template could not be deleted');
INSERT INTO txt VALUES ('E1006', 'German',  'Bitte einen Report-Typ ausw&auml;hlen');
INSERT INTO txt VALUES ('E1006', 'English', 'Please select a report type');
INSERT INTO txt VALUES ('E1011', 'German',  'Endezeit liegt vor der Startzeit');
INSERT INTO txt VALUES ('E1011', 'English', 'End time is before start time');

INSERT INTO txt VALUES ('E2001', 'German',  'Bitte eine Vorlage ausw&auml;hlen');
INSERT INTO txt VALUES ('E2001', 'English', 'Please select a template');

INSERT INTO txt VALUES ('E4001', 'German',  'Bitte Kommentar hinzuf&uuml;gen');
INSERT INTO txt VALUES ('E4001', 'English', 'Please insert a comment');
INSERT INTO txt VALUES ('E4002', 'German',  'Keine Regeln f&uuml;r die gew&auml;hlten Kriterien gefunden');
INSERT INTO txt VALUES ('E4002', 'English', 'No rules found for given criteria');

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
INSERT INTO txt VALUES ('E5106', 'German',  'Management wurde bereits angelegt: ');
INSERT INTO txt VALUES ('E5106', 'English', 'Management has already been created: ');
INSERT INTO txt VALUES ('E5107', 'German',  'Gateway wurde bereits angelegt: ');
INSERT INTO txt VALUES ('E5107', 'English', 'Gateway has already been created: ');
INSERT INTO txt VALUES ('E5111', 'German',  'Es gibt bereits ein Gateway mit derselben Konfiguration und Import aktiviert');
INSERT INTO txt VALUES ('E5111', 'English', 'There is already a gateway in the same configuration with import enabled');
INSERT INTO txt VALUES ('E5112', 'German',  'Gateway konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E5112', 'English', 'Gateway could not be created');

INSERT INTO txt VALUES ('E5201', 'German',  'LDAP-Verbindung konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E5201', 'English', 'LDAP connection could not be created');
INSERT INTO txt VALUES ('E5202', 'German',  'LDAP-Verbindung konnte nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E5202', 'English', 'LDAP connection could not be updated');
INSERT INTO txt VALUES ('E5203', 'German',  'LDAP-Verbindung konnte nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5203', 'English', 'LDAP connection could not be deleted');
INSERT INTO txt VALUES ('E5204', 'German',  'LDAP-Verbindungen konnten nicht geholt werden');
INSERT INTO txt VALUES ('E5204', 'English', 'LDAP connections could not be fetched');
INSERT INTO txt VALUES ('E5207', 'German',  'kein internes LDAP gefunden');
INSERT INTO txt VALUES ('E5207', 'English', 'No internal LDAP found');
INSERT INTO txt VALUES ('E5208', 'German',  'Keine Nutzer gefunden');
INSERT INTO txt VALUES ('E5208', 'English', 'No users found');
INSERT INTO txt VALUES ('E5209', 'German',  'Nutzer konnten nicht geholt werden');
INSERT INTO txt VALUES ('E5209', 'English', 'Users could not be fetched');
INSERT INTO txt VALUES ('E5210', 'German',  'Nutzer (Dn) existiert bereits');
INSERT INTO txt VALUES ('E5210', 'English', 'User (Dn) is already existing');
INSERT INTO txt VALUES ('E5211', 'German',  'Name und Passwort m&uuml;ssen gef&uuml;llt sein');
INSERT INTO txt VALUES ('E5211', 'English', 'Name and Password have to be filled');
INSERT INTO txt VALUES ('E5212', 'German',  'Unbekannter Mandant');
INSERT INTO txt VALUES ('E5212', 'English', 'Unknown tenant');
INSERT INTO txt VALUES ('E5213', 'German',  'Nutzer konnte nicht hinzugef&uuml;gt werden');
INSERT INTO txt VALUES ('E5213', 'English', 'No user could be added');
INSERT INTO txt VALUES ('E5214', 'German',  'Nutzer konnte nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E5214', 'English', 'User could not be updated');
INSERT INTO txt VALUES ('E5215', 'German',  'L&ouml;schen des eigenen Nutzers nicht erlaubt');
INSERT INTO txt VALUES ('E5215', 'English', 'Self deletion of user not allowed');
INSERT INTO txt VALUES ('E5216', 'German',  'Nutzer konnte nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5216', 'English', 'User could not be deleted');
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
INSERT INTO txt VALUES ('E5246', 'German',  'Gruppe konnte der Rolle im LDAP nicht zugewiesen werden');
INSERT INTO txt VALUES ('E5246', 'English', 'Group could not be added to role in LDAP');
INSERT INTO txt VALUES ('E5251', 'German',  'Keine Rollen gefunden');
INSERT INTO txt VALUES ('E5251', 'English', 'No roles found');
INSERT INTO txt VALUES ('E5252', 'German',  'Bitte nutzen sie ein Suchmuster mit Mindestl&auml;nge ');
INSERT INTO txt VALUES ('E5252', 'English', 'Please use pattern of min length ');
INSERT INTO txt VALUES ('E5254', 'German',  'Nutzer/Gruppe ist dieser Rolle schon zugewiesen');
INSERT INTO txt VALUES ('E5254', 'English', 'User/group is already assigned to this role');
INSERT INTO txt VALUES ('E5255', 'German',  'Nutzer konnte der Rolle im LDAP nicht zugewiesen werden');
INSERT INTO txt VALUES ('E5255', 'English', 'User could not be added to role in LDAP');
INSERT INTO txt VALUES ('E5256', 'German',  'Der letzte Admin kann nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5256', 'English', 'Last admin user cannot be deleted');
INSERT INTO txt VALUES ('E5257', 'German',  'Nutzer konnte von keiner Rolle in den LDAPs gel&ouml;scht werden');
INSERT INTO txt VALUES ('E5257', 'English', 'User could not be removed from any role in ldaps');
INSERT INTO txt VALUES ('E5258', 'German',  'Zu l&ouml;schender Nutzer nicht gefunden');
INSERT INTO txt VALUES ('E5258', 'English', 'User to delete not found');
INSERT INTO txt VALUES ('E5260', 'German',  'Deaktivieren der LDAP-Verbindung nicht erlaubt, da sie die letzte ist');
INSERT INTO txt VALUES ('E5260', 'English', 'Deactivation of LDAP Connection not allowed as it is the last one');
INSERT INTO txt VALUES ('E5261', 'German',  'L&ouml;schen der LDAP-Verbindung nicht erlaubt, da sie die letzte ist');
INSERT INTO txt VALUES ('E5261', 'English', 'Deletion of LDAP Connection not allowed as it is the last one');
INSERT INTO txt VALUES ('E5262', 'German',  'L&ouml;schen der LDAP-Verbindung nicht erlaubt, da sie einen Rollensuchpfad enth&auml;lt. Wenn m&ouml;glich diesen zuerst l&ouml;schen');
INSERT INTO txt VALUES ('E5262', 'English', 'Deletion of LDAP Connection not allowed as it contains role search path. Delete it first if possible');
INSERT INTO txt VALUES ('E5263', 'German',  'Musterl&auml;nge muss >= 0 sein');
INSERT INTO txt VALUES ('E5263', 'English', 'Pattern Length has to be >= 0');
INSERT INTO txt VALUES ('E5264', 'German',  'Es gibt bereits eine LDAP-Verbindung mit derselben Adresse und Port');
INSERT INTO txt VALUES ('E5264', 'English', 'There is already an LDAP connection with the same address and port');
INSERT INTO txt VALUES ('E5265', 'German',  'Rollenverwaltung kann nur im internen Ldap erfolgen');
INSERT INTO txt VALUES ('E5265', 'English', 'Role handling can only be done in internal Ldap');
INSERT INTO txt VALUES ('E5266', 'German',  'LDAP-Verbindung Ok');
INSERT INTO txt VALUES ('E5266', 'English', 'LDAP connection Ok');
INSERT INTO txt VALUES ('E5267', 'German',  'LDAP-Verbindung nicht Ok: unbekannter Fehler');
INSERT INTO txt VALUES ('E5267', 'English', 'LDAP connection not Ok: unknown error');
INSERT INTO txt VALUES ('E5268', 'German',  'LDAP-Verbindung nicht Ok: Verbindung mit Adresse/Port/TLS nicht m&ouml;glich');
INSERT INTO txt VALUES ('E5268', 'English', 'LDAP connection not Ok: no connection with address/port/TLS');
INSERT INTO txt VALUES ('E5269', 'German',  'LDAP-Verbindung nicht Ok: Bindung mit Nutzer/Passwort f&uuml;r Suche nicht m&ouml;glich');
INSERT INTO txt VALUES ('E5269', 'English', 'LDAP connection not Ok: no binding for search user/password');
INSERT INTO txt VALUES ('E5270', 'German',  'LDAP-Verbindung nicht Ok: Bindung mit Nutzer/Passwort Schreibender Nutzer nicht m&ouml;glich');
INSERT INTO txt VALUES ('E5270', 'English', 'LDAP connection not Ok: no binding for write user/password');
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
INSERT INTO txt VALUES ('E5284', 'German',  'Mandanten konnten nicht vom LDAP geholt werden');
INSERT INTO txt VALUES ('E5284', 'English', 'Tenants could not be fetched from LDAP');
INSERT INTO txt VALUES ('E5285', 'German',  'Mandant konnte nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E5285', 'English', 'Tenant could not be updated');

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
INSERT INTO txt VALUES ('E5415', 'German',  'Passwort muss mindestens ein Sonderzeichen enthalten (!?(){}=~$%&amp;#*-+.,_)');
INSERT INTO txt VALUES ('E5415', 'English', 'Password must contain at least one special character (!?(){}=~$%&amp;#*-+.,_)');
INSERT INTO txt VALUES ('E5421', 'German',  'Schl&uuml;ssel nicht gefunden oder Wert nicht konvertierbar: Wert wird gesetzt auf: ');
INSERT INTO txt VALUES ('E5421', 'English', 'Key not found or could not convert value to int: taking value: ');

INSERT INTO txt VALUES ('E6001', 'German', 	'Der Relogin war nicht erfolgreich. Haben Sie ein falsches Passwort eingegeben? Schauen Sie f&uuml;r Details bitte in die Logs.');
INSERT INTO txt VALUES ('E6001', 'English', 'Re-login unsuccessful. Did you enter a wrong password? See log for details!');

INSERT INTO txt VALUES ('E7001', 'German',  'Aktion wurde bereits durchgef&uuml;hrt');
INSERT INTO txt VALUES ('E7001', 'English', 'Action has already been processed');
INSERT INTO txt VALUES ('E7002', 'German',  'Bitte zuerst Aktion ausf&uuml;hren von Alarm ');
INSERT INTO txt VALUES ('E7002', 'English', 'Please apply first action of alert ');
INSERT INTO txt VALUES ('E7003', 'German',  'Bitte zuerst zugeh&ouml;rige Gateways l&ouml;schen');
INSERT INTO txt VALUES ('E7003', 'English', 'Please delete related gateways first');
INSERT INTO txt VALUES ('E7011', 'German',  'Import l&auml;uft zu lange');
INSERT INTO txt VALUES ('E7011', 'English', 'Import running too long');
INSERT INTO txt VALUES ('E7012', 'German',  'Kein Import f&uuml;r aktives Management');
INSERT INTO txt VALUES ('E7012', 'English', 'No Import for active management');
INSERT INTO txt VALUES ('E7013', 'German',  'Letzter erfolgreicher Import zu lange her');
INSERT INTO txt VALUES ('E7013', 'English', 'Last successful import too long ago');

INSERT INTO txt VALUES ('E8001', 'German',  'Antrag konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E8001', 'English', 'Request could not be created');
INSERT INTO txt VALUES ('E8002', 'German',  'Antrag konnte nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E8002', 'English', 'Request could not be updated');
INSERT INTO txt VALUES ('E8003', 'German',  'Aufgabe konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E8003', 'English', 'Task could not be created');
INSERT INTO txt VALUES ('E8004', 'German',  'Aufgabe konnte nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E8004', 'English', 'Task could not be updated');
INSERT INTO txt VALUES ('E8005', 'German',  'Aufgabe konnte nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E8005', 'English', 'Task could not be deleted');
INSERT INTO txt VALUES ('E8006', 'German',  'Element konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E8006', 'English', 'Element could not be created');
INSERT INTO txt VALUES ('E8007', 'German',  'Element konnte nicht ge&auml;ndert werden');
INSERT INTO txt VALUES ('E8007', 'English', 'Element could not be updated');
INSERT INTO txt VALUES ('E8008', 'German',  'Element konnte nicht gel&ouml;scht werden');
INSERT INTO txt VALUES ('E8008', 'English', 'Element could not be deleted');
INSERT INTO txt VALUES ('E8009', 'German',  'Genehmigung konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E8009', 'English', 'Approval could not be created');
INSERT INTO txt VALUES ('E8010', 'German',  'Bitte Gruppe ausw&auml;hlen');
INSERT INTO txt VALUES ('E8010', 'English', 'Please select group');
INSERT INTO txt VALUES ('E8011', 'German',  'Aktion konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E8011', 'English', 'Action could not be created');


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
INSERT INTO txt VALUES ('T0010', 'German',  'wie reporter-viewall, aber mit Erlaubnis, Devices anzulegen und zu &auml;ndern');
INSERT INTO txt VALUES ('T0010', 'English', 'like reporter-viewall, but allowed to create and update devices');
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
INSERT INTO txt VALUES ('T0105', 'German',  'Aktuell aktive NAT-Regeln aller Gateways');
INSERT INTO txt VALUES ('T0105', 'English', 'Currently active NAT rules of all gateways');

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
    In der <a href="/help/reporting/leftside">Linken Randleiste</a> werden die verf&uuml;gbaren Report-Typen und Devices sowie der Reportzeitraum dargestellt.<br>
    Nach klicken der "Report erstellen" Schaltfl&auml;che werden die <a href="/help/reporting/output">Reportdaten</a> im unteren Teil des Fensters dargestellt.
    In der <a href="/help/reporting/rightside">Rechten Randleiste</a> werden Details zu den markierten Objekten gezeigt.<br>
    Der Report kann in verschiedenen Ausgabeformaten <a href="/help/reporting/export">exportiert</a> werden.
');
INSERT INTO txt VALUES ('H1001', 'English', 'The first input line is the filter line, where the parameters for the report creation are defined.
    It is subject to a special <a href="/help/reporting/filter">Filter Syntax</a>. 
    It can be filled completely manually or supported by <a href="/help/reporting/templates">Templates</a>, which can be chosen below.
    In the <a href="/help/reporting/leftside">Left Sidebar</a> the available report types and devices as well as the reporting time are displayed.<br>
    After selecting the "Generate Report" button the <a href="/help/reporting/output">Report Data</a> is shown in the lower part of the window.
    In the <a href="/help/reporting/rightside">Right Sidebar</a> details about the selected objects are given.<br>
    The report can be <a href="/help/reporting/export">exported</a> to different output formats.
');
INSERT INTO txt VALUES ('H1101', 'German',  '<li> Alle Filter sind schreibungsunabh&auml;ngig.</li>
    <li> Es gibt verschiedene Varianten f&uuml;r die meisten Schl&uuml;sselw&ouml;rter, z.B. k&ouml;nnen DestinationPort-Filter geschrieben werden als:
        port, dport, dst_port, dst-port, dest-port, destination-port, dest_port, destination_port</li>
    <li> Alle Filterausdr&uuml;cke m&uuml;ssen logisch mit den Operatoren: and, or, not miteinander kombiniert werden.</li>
    <li> Klammern k&ouml;nnen genutzt werden, um die Filterausdr&uuml;cke zu strukturieren.</li>
    <li> Anf&uuml;hrungszeichen (") k&ouml;nnen optional f&uuml;r Wertdefinitionen genutzt werden. Wenn Leerzeichen im Wert vorkommen (z.B. f&uuml;r Datum/Zeit-Werte), m&uuml;ssen sie genutzt werden.</li>
    <li> Ein Gateway muss ausgew&auml;hlt werden. Dies kann manuell oder &uuml;ber die linke Randleiste, von wo die Auswahl automatisch in den Filter integriert wird, erfolgen.</li>
    <li> Zeitfilterung funktioniert zur Zeit nur f&uuml;r Zeitpunkte vor dem letzten Import, der einen Config Change gefunden hat. </li>
    <li> Regeln werden immer in voller Tiefe durchsucht, d.h. alle Gruppen in Quell-, Ziel- und Dienstfeldern werden aufgel&ouml;st.
        Zur Zeit gibt es noch keine M&ouml;glichkeit, nur auf der obersten Regelebene zu suchen.</li>
');
INSERT INTO txt VALUES ('H1101', 'English', '<li> All filtering is case insensitive.</li>
    <li> There are multiple variants for most keywords, e.g. DestinationPort filters can be written as:
        port, dport, dst_port, dst-port, dest-port, destination-port, dest_port, destination_port</li>
    <li> All filter statements must be logically combined using either: and, or, not.</li>
    <li> Brackets can be used for structuring the filter statement.</li>
    <li> Quotation marks (") can be used optionally for the value definition. If there are white spaces in the value (e.g. for date/time values) the quotation marks have to be used.</li>
    <li> A gateway has to be selected. This can be done manually or via the left sidebar, from where the selection is automatically integrated to the filter.</li>
    <li> Time filtering currently only works for points in time before the last import that found a config change. </li>
    <li> Rules are always deep-searched, meaning all groups in source, destination and service fields are resolved.
        There is currently no option to only search at the rule top-level.</li>
');
INSERT INTO txt VALUES ('H1111', 'German',  '<li>gateway (gw, firewall, fw, device, dev): Zus&auml;tzlich zu der in der <a href="/help/reporting/leftside">Linken Randleiste</a> zu t&auml;tigenden Auswahl spezifischer Devices
    kann hier noch die Auswahl weiter nach Namen eingeschr&auml;nkt werden. </li>
    <li>management (mgmt, manager, mgm, mgr)</li>
    <li>disabled</li>
    <li>source (src)</li>
    <li>destination (dst, dest)</li>
    <li>service (svc, srv)</li>
    <li>protocol (proto)</li>
    <li>destinationport (port, dport, dst_port, dst-port, dest-port, destination-port, dest_port, destination_port)</li>
    <li>action (act, enforce)</li>
    <li>remove: M&ouml;gliche Werte: true/false. Wenn "true", werden nur dezertifizierte Regeln gesucht</li>
    <li>recertdisplay (recertdisp): Definiert den Zeitraum f&uuml;r die Vorausschau (in Tagen) f&uuml;r die n&auml;chste Rezertifizierung. Nur Regeln in diesem Zeitfenster werden gesucht.</li>
    <li>fulltext (full, fulltextsearch, fts, text, textsearch)</li>
');
INSERT INTO txt VALUES ('H1111', 'English', '<li>gateway (gw, firewall, fw, device, dev): Additionally to the specific device selection in the <a href="/help/reporting/leftside">left sidebar</a>
    the selected devices can be further restricted here by device names.</li>
    <li>management (mgmt, manager, mgm, mgr)</li>
    <li>disabled</li>
    <li>source (src)</li>
    <li>destination (dst, dest)</li>
    <li>service (svc, srv)</li>
    <li>protocol (proto)</li>
    <li>destinationport (port, dport, dst_port, dst-port, dest-port, destination-port, dest_port, destination_port)</li>
    <li>action (act, enforce)</li>
    <li>remove: Possible Values: true/false. If "true", only decertified rules are searched</li>
    <li>recertdisplay (recertdisp): Defines the lookahead period (in days) for next recertification. Only rules in this time range are searched.</li>
    <li>fulltext (full, fulltextsearch, fts, text, textsearch)</li>
');
INSERT INTO txt VALUES ('H1131', 'German',  '<li>and (&)</li><li>or (|)</li><li>not (!)</li><li>eq (=, :)</li><li>neq</li><li>(</li><li>)</li>');
INSERT INTO txt VALUES ('H1131', 'English', '<li>and (&)</li><li>or (|)</li><li>not (!)</li><li>eq (=, :)</li><li>neq</li><li>(</li><li>)</li>');
INSERT INTO txt VALUES ('H1141', 'German',  '<li> Volltextsuche</li>
    <ul><li>cactus - durchsucht die Felder "source, destination, service" nach dem String "cactus".</li>
    <li>fulltext=cactus - das gleiche wie oben</li></ul>
');
INSERT INTO txt VALUES ('H1141', 'English', '<li> full text searches</li><ul>
    <li>cactus - searches the fields "source, destination, service" for the string "cactus".</li>
    <li>fulltext=cactus - same as above</li></ul>
');
INSERT INTO txt VALUES ('H1143', 'German',  '<li> Suche nach spezifischem Regelinhalt</li><ul>
    <li>src=cactus</li><li>src=subnet</li><li>dst=daba</li><li>svc=valve_udp</li><li>action=accept</li><li>not action=drop</li><li>disabled=true</li><li>dst=10.222.0.10/31</li></ul>
');
INSERT INTO txt VALUES ('H1143', 'English', '<li> Specific rule content searches</li><ul>
    <li>src=cactus</li><li>src=subnet</li><li>dst=daba</li><li>svc=valve_udp</li><li>action=accept</li><li>not action=drop</li><li>disabled=true</li><li>dst=10.222.0.10/31</li></ul>
');
INSERT INTO txt VALUES ('H1144', 'German',  '<li> Filtern nach Gateways oder Managements</li><ul>
    <li>gateway=forti and src=cactus</li><li>gateway=forti or gateway=check</li><li>not gateway=check</li></ul>
');
INSERT INTO txt VALUES ('H1144', 'English', '<li> filter for gateways or managements</li><ul>
    <li>gateway=forti and src=cactus</li><li>gateway=forti or gateway=check</li><li>not gateway=check</li></ul>
');
INSERT INTO txt VALUES ('H1201', 'German',  'Vorlagen k&ouml;nnen genutzt werden, um wiederkehrende Reports zu definieren. Diese werden f&uuml;r das Scheduling ben&ouml;tigt.
    Jeder Nutzer kann seine eigenen Vorlagen definieren und sie mit anderen teilen.<br>
    Beim Anlegen einer neuen Vorlage &uuml;ber die Schaltfl&auml;che "Als Vorlage speichern" wird ein Pop-Up-Fenster ge&ouml;ffnet, in dem Name und ein Kommentar vergeben werden k&ouml;nnen.
    Die aktuell ausgew&auml;hlten fixen Filterkriterien aus der <a href="/help/reporting/leftside">Linken Randleiste</a> sowie die <a href="/help/reporting/filter">Filterleiste</a>
    werden automatisch &uuml;bernommen, letztere kann hier noch weiter angepasst werden.<br>
    Es werden einige vordefinierte Vorlagen f&uuml;r verschiedene Reporttypen angeboten:
');
INSERT INTO txt VALUES ('H1201', 'English', 'Templates can be used to define recurring reports. They have to be defined if they shall be used for the scheduling.
    Every user can define his own templates and share them with others.<br>
    When creating a new template by using the "Save as Template" button a pop-up window is opened, where a name and a comment can be assigned.
    The currently selected fixed filter criteria from the <a href="/help/reporting/leftside">left sidebar</a> as well as from the <a href="/help/reporting/filter">filter line</a>
    are automatically imported, the latter can be further adapted.<br>
    There are some predefined templates for the different report types:
');
INSERT INTO txt VALUES ('H1202', 'German',  'Um sie direkt in der UI zu nutzen, m&uuml;ssen zus&auml;tzlich Devices ausgew&auml;hlt werden. Bei der Nutzung im Scheduling gelten alle Devices als ausgew&auml;hlt.
    Diese Vorlagen k&ouml;nnen als Basis f&uuml;r die Erzeugung eigener Vorlagen genutzt werden.
');
INSERT INTO txt VALUES ('H1202', 'English', 'For using them directly on the UI, devices have to be selected additionally. Used in scheduling, all devices are regarded as selected.
    These templates can be used as basis for the creation of own self-defined templates.
');
INSERT INTO txt VALUES ('H1211', 'German',  'Einfache Statistik: Etwas Statistik &uuml;ber Netzwerk-, Dienst- und Nutzerobjekte aller Devices.');
INSERT INTO txt VALUES ('H1211', 'English', 'Basic Statistics: Some statistics about network, service and user objects and rules of all devices.');
INSERT INTO txt VALUES ('H1212', 'German',  'Compliance: Durchlassregeln mit "any": Alle Durchlassregeln, die "any" als Quelle, Ziel oder Dienst enthalten.');
INSERT INTO txt VALUES ('H1212', 'English', 'Compliance: Pass rules with "any": All pass rules that contain "any" as source, destination or service.');
INSERT INTO txt VALUES ('H1213', 'German',  'Aktuelle Regeln: Aktuell aktive Regeln aller ausgew&auml;hlten Devices.');
INSERT INTO txt VALUES ('H1213', 'English', 'Current Rules: Currently active rules of all selected devices.');
INSERT INTO txt VALUES ('H1214', 'German',  'Regel&auml;nderungen des aktuellen Jahres: Alle im aktuellen Jahr ge&auml;nderten Regeln in den ausgew&auml;hlten Devices.');
INSERT INTO txt VALUES ('H1214', 'English', 'This year&apos;s Rule Changes: All rule change performed in the current year in the selected devices.');
INSERT INTO txt VALUES ('H1215', 'German',  'Aktuelle NAT Regeln: Aktuell aktive NAT-Regeln aller ausgew&auml;hlten Devices.');
INSERT INTO txt VALUES ('H1215', 'English', 'Current NAT Rules: Currently active NAT rules of all selected devices.');
INSERT INTO txt VALUES ('H1301', 'German',  'Direkt nach der Erzeugung oder vom <a href="/help/archive">Archiv</a> aus k&ouml;nnen Reports in verschiedenen Ausgabeformaten exportiert werden:');
INSERT INTO txt VALUES ('H1301', 'English', 'Directly after creation or from the <a href="/help/archive">archive</a> reports can be exported to different output formats:');
INSERT INTO txt VALUES ('H1302', 'German',  '<li>pdf</li><li>html</li><li>csv (noch nicht unterst&uuml;tzt)</li><li>json</li>');
INSERT INTO txt VALUES ('H1302', 'English', '<li>pdf</li><li>html</li><li>csv (currently not supported)</li><li>json</li>');
INSERT INTO txt VALUES ('H1303', 'German',  'Nach bet&auml;tigen des "Report exportieren"-Auswahlfeldes kann eines oder mehrere dieser Formate ausgew&auml;hlt werden.
    Auch kann der Report mit einem Namen versehen und <a href="/help/archive">archiviert</a> werden.
    Ein weiteres Ausgabefenster erlaubt dann das separate Abholen der ausgew&auml;hlten Ausgabedateien.
');
INSERT INTO txt VALUES ('H1303', 'English', 'After clicking the "Export Report" button one or more of them can be selected.
    Also the possibility to name and save the report in the <a href="/help/archive">archive</a> is given.
    Another Popup allows then to download the selected output files separately.    
');
INSERT INTO txt VALUES ('H1401', 'German',  'Im unteren Teil der Hauptseite werden die Ausgabedaten des generierten Reports dargestellt.
    Unerw&uuml;nschte Spalten k&ouml;nnen mit der jeweiligen "-" Schaltfl&auml;che ausgeblendet werden.
    Wenn dargestellt, k&ouml;nnen die Spalten auch zum Sortieren oder Filtern genutzt werden.<br>
    Die zur Verf&uuml;gung stehenden Datenspalten sind:
');
INSERT INTO txt VALUES ('H1401', 'English', 'In the lower part of the main page the output data of the generated report is displayed.
    Unwanted columns can be removed by clicking on the respective "-" button. 
    If diplayed the columns can be used for sorting or filtering.<br>
    The available data columns are:
');
INSERT INTO txt VALUES ('H1402', 'German',  '<li>Nummer</li><li>Name</li><li>Quellzone</li><li>Quelle</li><li>Zielzone</li>
    <li>Ziel</li><li>Dienste</li><li>Aktion</li><li>Logging</li><li>Aktiviert</li><li>UID</li><li>Kommentar</li>
');
INSERT INTO txt VALUES ('H1402', 'English', '<li>Number</li><li>Name</li><li>Source Zone</li><li>Source</li><li>Destination Zone</li>
    <li>Destination</li><li>Services</li><li>Action</li><li>Logging</li><li>Enabled</li><li>UID</li><li>Comment</li>
');
INSERT INTO txt VALUES ('H1501', 'German',  'Hier werden die fixen Kriterien f&uuml;r die Auswahl zur Reporterstellung dargestellt.
    Weiteren Kriterien k&ouml;nnen &uuml;ber die <a href="/help/reporting/filter">Filterleiste</a> hinzugef&uuml;gt werden.
');
INSERT INTO txt VALUES ('H1501', 'English', 'Here all fixed criteria for reporting are displayed.
    Further criteria can be added via the <a href="/help/reporting/filter">Filter line</a>.
');
INSERT INTO txt VALUES ('H1502', 'German', 'Anzeige aller zur Verf&uuml;gung stehenden Report-Typen. Bitte einen ausw&auml;hlen.');
INSERT INTO txt VALUES ('H1502', 'English', 'Selection of all available report types. Please select one.');
INSERT INTO txt VALUES ('H1503', 'German',  'Auflistung aller verf&uuml;gbaren Devices.
    Die Ansicht kann f&uuml;r unterschiedliche Nutzer entsprechend der <a href="/help/settings/tenants">Mandantenzuordnung</a> variieren.
    F&uuml;r eine Reporterstellung muss hier eine Auswahl getroffen werden. Die dargestellten Devices k&ouml;nnen ein- oder ausgeklappt werden.
    Ab welcher Mindestanzahl die Darstellung zu Beginn eingeklappt ist, kann individuell in den <a href="/help/settings/report">Reporting-Einstellungen</a> definiert werden.
');
INSERT INTO txt VALUES ('H1503', 'English', 'Display of all available devices.
    This view may differ for the different users according to the <a href="/help/settings/tenants">tenant assignments</a>.
    For the creation of a report a selection out of them has to be done. The displayed devices can be collapsed or expanded.
    In the <a href="/help/settings/report">Report Settings</a> it is possible to define the minimum number, where the display starts collapsed.
');
INSERT INTO txt VALUES ('H1504', 'German',  'Anzeige der gew&auml;hlten Reportzeit bzw. des gew&auml;hlten Reportzeitraums in Abh&auml;ngigkeit vom gew&auml;hlten Report-Typ.
    Vorgabewerte sind "jetzt" bzw. "dieses Jahr". &Uuml;ber die "&Auml;ndern"-Schaltfl&auml;che kann dies in einem entsprechenden Popup-Fenster angepasst werden:
');
INSERT INTO txt VALUES ('H1504', 'English', 'Display of the selected report time resp. time range, depending on the selected report type.
    Default values are "now" resp. "this year". By using the "Change" button this can be adapted in a pop-up window:
');
INSERT INTO txt VALUES ('H1505', 'German',  'F&uuml;r Report-Typen, welche die Angabe eines Zeitpunktes erfordern, gibt es zwei Optionen:
    Auswahl eines bestimmten Zeitpunktes mit dem Date-Picker oder die Nutzung des Vorgabewertes "jetzt".
');
INSERT INTO txt VALUES ('H1505', 'English', 'For report types requiring a report time there are two options: 
    Selecting a particular time with the date/time picker or using the default value "now".
');
INSERT INTO txt VALUES ('H1506', 'German',  'F&uuml;r Report-Typen, die Zeitintervalle ben&ouml;tigen, kann gew&auml;hlt werden zwischen:');
INSERT INTO txt VALUES ('H1506', 'English', 'For report types requiring a time range a selection can be done between:');
INSERT INTO txt VALUES ('H1507', 'German',  'Vordefinierte Abk&uuml;rzungen "dieses Jahr", "letztes Jahr", "dieser Monat", "letzter Monat", "diese Woche", "letzte Woche", "heute" oder "gestern"');
INSERT INTO txt VALUES ('H1507', 'English', 'Predefined shortcuts "this year", "last year", "this month", "last month", "this week", "last week", "today" or "yesterday"');
INSERT INTO txt VALUES ('H1508', 'German',  'Zeitintervalle in Tagen, Wochen, Monaten oder Jahren relativ zum aktuellen Zeitpunkt');
INSERT INTO txt VALUES ('H1508', 'English', 'Time intervals in days, weeks, months or years in relation to the actual time');
INSERT INTO txt VALUES ('H1509', 'German',  'Absolute Start- und Endezeiten. Beide Grenzen k&ouml;nnen durch setzen der "offen"-Markierung ausser Kraft gesetzt werden.');
INSERT INTO txt VALUES ('H1509', 'English', 'Absolute start and end times. Both limits can be separately omitted by setting the "open" checkbox.');
INSERT INTO txt VALUES ('H1601', 'German',  'Die rechte Randleiste hat zwei Reiter: Unter "Alle" werden alle aktuell abgeholten Objekte dargestellt,
    w&auml;hrend unter "Regel" nur die in der Reportausgabe ausgew&auml;hlten Regeln gezeigt werden.<br>
    Folgende Daten werden dargestellt, gruppiert nach den ausgew&auml;hlten Devices:
');
INSERT INTO txt VALUES ('H1601', 'English', 'There are two Tabs shown in the right sidebar: The "All" tab displays all currently fetched objects,
    while in the "Rule" tab only the objects of rules selected in the report output are shown.<br>
    The following data are displayed grouped by the selected devices:
');
INSERT INTO txt VALUES ('H1602', 'German',  '<li>Netzwerkobjekte</li><li>Dienste</li><li>Nutzer</li>');
INSERT INTO txt VALUES ('H1602', 'English', '<li>Network objects</li><li>Services</li><li>Users</li>');

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
INSERT INTO txt VALUES ('H2017', 'German',  'Eigent&uuml;mer: Ersteller dieses Termins.');
INSERT INTO txt VALUES ('H2017', 'English', 'Owner: Creator of this schedule.');
INSERT INTO txt VALUES ('H2018', 'German',  'Z&auml;hler: Z&auml;hlt, wie viele Reports mit diesem Terminauftrag bereits erstellt wurden.');
INSERT INTO txt VALUES ('H2018', 'English', 'Count: Counts how many reports have already been created with this schedule.');

INSERT INTO txt VALUES ('H3001', 'German',  'Hier sind die archivierten Reports mit Name sowie Informationen zu Erzeugungsdatum, Typ, Vorlage (nur bei termingesteuerten Reports), 
    Eigent&uuml;mer sowie eine kurze Beschreibung des Inhalts zu finden.
    Sie k&ouml;nnen zum einen durch Export manuell erzeugter Reports durch Setzen des "Archiv"-Kennzeichens in <a href="/help/reporting/export">Export Report</a> erzeugt werden.
    Zum anderen finden sich hier auch die durch das <a href="/help/scheduling">Scheduling</a> erzeugten Reports.
    Die archivierten Reports k&ouml;nnen von hier heruntergeladen oder gel&ouml;scht werden.
');
INSERT INTO txt VALUES ('H3001', 'English', 'Here the archived reports can be found with name and information about creation date, type, template (only at scheduled reports),
    owner and a short description about the content. 
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
    Die Abschnitte <a href="/help/settings/managements">Managements</a> und <a href="/help/settings/gateways">Gateways</a> dienen der Definition der verbundenen Hardware.
');
INSERT INTO txt VALUES ('H5011', 'English', 'In the first chapter "Devices" the setup of the report data sources is done:
    The sections <a href="/help/settings/managements">Managements</a> and <a href="/help/settings/gateways">Gateways</a> are for the definition of the connected hardware.
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
INSERT INTO txt VALUES ('H5101', 'German',  'Admins k&ouml;nnen mehrere unterschiedliche Managements einrichten und verwalten.<br>
    Die "Klonen"-Schaltfl&auml;che unterst&uuml;tzt beim Definieren eines neuen Managements, indem Daten von einem existierenden kopiert werden.
    Vor dem Speichern muss sich mindestens einer der Parameter Hostname, Port oder Config Path von den existierenden Managements unterscheiden, wenn die Auswahl "Import Deaktiviert" nicht gesetzt ist.
');
INSERT INTO txt VALUES ('H5101', 'English', 'Admins can create and administrate several different managements.<br>
    The clone button helps defining new managements by copying the data from existing ones.
    Before saving at least one of the parameters Hostname, Port or Config Path has to be different from the existing managements if the Import Disabled flag is not set.
');
INSERT INTO txt VALUES ('H5102', 'German',  'Folgende Firewallprodukte k&ouml;nnen integriert werden:
    <ul>
        <li>Legacy Zugriff via ssh
            <ul>
                <li>Check Point R5x/R6x/R7x - Management Server (SmartCenter)</li>
                <li>FortiGateStandalone 5ff - FortiGate ohne FortiManager</li>
                <li>Barracuda Firewall Control Center Vx - Firewall-Management</li>
                <li>phion netfence 3.x - Firewallgateway</li>
                <li>JUNOS 10 - 17 - Firewallgateway</li>
                <li>Netscreen 5.x/6.x - Firewallgateway</li>
            </ul>
        </li>            
        <li>API Zugriff via https
            <ul>
                <li>Check Point R8x - SmartCenter</li>
                <li>Check Point R8x - Multi Domain Server (MDS)</li>
                <li>FortiManager 5ff - FortiManager. F&uuml;r diesen Management-Typ kann die komplette Struktur (ADOM, FortiGateway Devices) mittels AutoDiscovery automatisch ausgelesen werden.</li>
            </ul>
        </li>            
    </ul>
');
INSERT INTO txt VALUES ('H5102', 'English', 'The following firewall products can be integrated:<ul>
    <ul>
        <li>Legacy access via ssh
            <ul>
                <li>Check Point R5x/R6x/R7x - management server (SmartCenter)</li>
                <li>FortiGateStandalone 5ff - FortiGate without FortiManager</li>
                <li>Barracuda Firewall Control Center Vx - firewall management</li>
                <li>phion netfence 3.x - firewall gateway</li>
                <li>JUNOS 10 - 17 - firewall gateway</li>
                <li>Netscreen 5.x/6.x - firewall gateway</li></ul>
            </ul>
        </li>            
        <li>API access via https
            <ul>
                <li>Check Point R8x - SmartCenter</li>
                <li>Check Point R8x - MDS (Multi Domain Server)</li>
                <li>FortiManager 5ff - FortiManager - for this management type the complete infrastructure (ADOM, FortiGateway devices) can be auto discovered.</li>
            </ul>
        </li>            
    </ul>
');
INSERT INTO txt VALUES ('H5103', 'German',  'F&uuml;r Firewallgateways ohne separates Management oder im Falle, dass das zentrale Management nicht in den Firewall Orchestrator eingebunden werden kann,
    werden die Details des Gateways als Management und gleichzeitig auch als Gateway eingetragen.
');
INSERT INTO txt VALUES ('H5103', 'English', 'For firewall gateways without a separate management or in case the central management cannot be integrated into Firewall Orchestrator 
    you may enter the details of the gateway here as a management system as well and then add it again as a gateway.
');
INSERT INTO txt VALUES ('H5104', 'German',  'Wenn Beispieldaten (definiert durch die Endung "_demo" vom Namen) existieren, wird eine Schaltfl&auml;che angezeigt, um diese und alle verkn&uuml;pften <a href="/help/settings/gateways">Gateways</a> zu l&ouml;schen.');
INSERT INTO txt VALUES ('H5104', 'English', 'If there are sample data (defined by the ending "_demo" of the name), a button is displayed to delete them and all related <a href="/help/settings/gateways">gateways</a>.');
INSERT INTO txt VALUES ('H5111', 'German',  'Name*: Name des Managements. <br>
    F&uuml;r die meisten Firewalls ist dies ein willk&uuml;rlicher Name. Ausnahmen sind direkt verbundene Gateways von Fortigate, Netscreen und Juniper.
    Hier muss der Name des Firewallgateways eingetragen werden.<br>
    Ein Management dessen Name mit "_demo" endet, wird beim Bet&auml;tigen der "Beispieldaten l&ouml;schen"-Schaltfl&auml;che gel&ouml;scht.
');
INSERT INTO txt VALUES ('H5111', 'English', 'Name*: Name of the mangement. <br>
    For most firewalls this is an arbitrary name. Exceptions are Fortigate, Netscreen and Juniper directly connected gateways.
    Here the name give needs to be the name of the firewall gateway.<br>
    A management whose name ends with "_demo" will be deleted when using the "Remove Sample Data" button.
');
INSERT INTO txt VALUES ('H5112', 'German',  'Kommentar: Optionale Beschreibung des Managements.');
INSERT INTO txt VALUES ('H5112', 'English', 'Comment: Optional description of this management.');
INSERT INTO txt VALUES ('H5113', 'German',  'Ger&auml;tetyp*: bitte das korrekte Produkt von der Liste ausw&auml;hlen (siehe oben)');
INSERT INTO txt VALUES ('H5113', 'English', 'Device Type*: Select correct product from a list of available types, see above.');
INSERT INTO txt VALUES ('H5114', 'German',  'Hostname*: Adresse des Hosts (entweder IP-Addresse oder aufl&ouml;sbarer Name). 
    F&uuml;r Check Point R8x MDS Installationen die Addresse des MDS-Servers f&uuml;r alle Domains benutzen.<br>
    F&uuml;r Fortinet, Barradua, Juniper muss die IP vom aufl&ouml;sbaren Namen des Firewallgateways spezifiziert werden.
');
INSERT INTO txt VALUES ('H5114', 'English', 'Hostname*: Address of the host (either IP address or resolvable name). 
    For Check Point R8x MDS installations use the address of the MDS server for all domains.<br>
    For Fortinet, Barradua, Juniper you need to specify the IP of resolvable name of the firewall gateway.
');
INSERT INTO txt VALUES ('H5115', 'German',  'Port*: Port-Nummer des Hosts.<br>
    Wenn das Ziel Check Point R8x ist, wird die Verbindung via API aufgebaut. Die Standard-Port-Nummer ist 443. Denken Sie daran, den API-Zugang auf Ihrem Check Point Managment zu aktivieren.<br>
    Wenn das Ziel nicht Check Point R8x ist, braucht Firewall Orchestrator einen ssh-basierten Zugang. Die Standard-Port-Nummer ist 22.
');
INSERT INTO txt VALUES ('H5115', 'English', 'Port*: Port number of the host.<br>
    If the target is Check Point R8x the connection is established via API. The default port number is 443. Remember to enable API access on your Check Point managment.<br>
    If the target is not Check Point R8x Firewall Orchestrator needs ssh-based access. The default port number is 22.
');
INSERT INTO txt VALUES ('H5116', 'German',  'Import Nutzer*: Der Nutzer, der zum Anmelden am Firewall Management benutzt wird.
    Er muss vorher auf dem Firewallsystem angelegt sein und vollen Lesezugriff auf das System besitzen.<br>
    Auf Check Point R8x wird empfohlen, das vordefinierte "Read Only All"-Profil (sowohl globales als auch Domainmanagement) zu verwenden.
');
INSERT INTO txt VALUES ('H5116', 'English', 'Import User*: The user used to login to the firewall management. 
    This user needs to be created on the firewall system in advance and needs full read access to the system.<br>
    On Check Point R8x we recommend using the predefined "Read Only All" profile (both global and domain management) for the user.
');
INSERT INTO txt VALUES ('H5117', 'German',  'Privater Schl&uuml;ssel* / Passwort*: F&uuml;r den ssh-Zugang hier den privaten ssh-Schl&uuml;ssel hinterlegen (Schl&uuml;ssel muss unverschl&uuml;sselt und ohne Passphrase sein)<br>
    F&uuml;r den API-Zugang ist dies das Passwort des API-Nutzers.
');
INSERT INTO txt VALUES ('H5117', 'English', 'Login Secret* / Password*: For ssh access enter the private ssh key (key needs to be unencrypted without passphrase)<br>
    For API access this is the password of the API user.
');
INSERT INTO txt VALUES ('H5118', 'German',  '&Ouml;ffentlicher Schl&uuml;ssel: Dieses Feld muss nur f&uuml;r Netscreen-Firewalls gef&uuml;llt werden - dieses System ben&ouml;tigt auch den &ouml;ffentlichen Schl&uuml;ssel zum Anmelden.');
INSERT INTO txt VALUES ('H5118', 'English', 'Public Key: This field only needs to be filled for netscreen firewalls - this system also needs the public key for successful login.');
INSERT INTO txt VALUES ('H5119', 'German',  'Domain: Firewall Domain Name <br>
    f&uuml;r Check Point R8x MDS / Fortimanager Installationen, andernfall leer lassen.
');
INSERT INTO txt VALUES ('H5119', 'English', 'Domain: Firewall Domain Name<br>
    Empty except for Check Point R8x MDS / Fortimanager installations.
');
INSERT INTO txt VALUES ('H5120', 'German',  'Importer Hostname: Der Name des Servers, auf dem der Importprozess laufen soll.
    Muss individuell konfiguriert werden, wenn mehrere verteilte Importmodule laufen sollen, so dass nicht jeder Importer alle Managements importiert.
');
INSERT INTO txt VALUES ('H5120', 'English', 'Importer Hostname: This must be the name of the server, the import process should run on. 
    Needs to be individually configured if you want to have multiple distributed import modules, so that not every importer imports all managements.
');
INSERT INTO txt VALUES ('H5121', 'German',  'Debug Stufe (0-9): Erlaubt individuelle Debug-Granularit&auml;t pro Management.');
INSERT INTO txt VALUES ('H5121', 'English', 'Debug Level (0-9): For allowing for individual debug granularity per management.');
INSERT INTO txt VALUES ('H5122', 'German',  'Import Deaktiviert: Schalter um den Datenimport zu deaktivieren.');
INSERT INTO txt VALUES ('H5122', 'English', 'Import Disabled: Flag if the data import needs to be disabled.');
INSERT INTO txt VALUES ('H5123', 'German',  'Nicht sichtbar: Wenn gesetzt ist dieses Management nicht mit Standard-Reporter-Rolle sichtbar.');
INSERT INTO txt VALUES ('H5123', 'English', 'Hide in UI: If set, this management is not visible to the standard reporter role.');
INSERT INTO txt VALUES ('H5141', 'German',  'Admins k&ouml;nnen mehrere unterschiedliche Gateways einrichten und verwalten.<br>
    Die "Klonen"-Schaltfl&auml;che unterst&uuml;tzt beim Definieren eines neuen Gateways, indem Daten von einem existierenden kopiert werden.
    Vor dem Speichern muss sich mindestens einer der Parameter Ger&auml;tetyp, Management oder Rulebase von den existierenden Gateways unterscheiden, wenn die Auswahl "Import Deaktiviert" nicht gesetzt ist.
');
INSERT INTO txt VALUES ('H5141', 'English', 'Admins can create and administrate several different gateways.<br>
    The clone button helps defining new gateways by copying the data from existing ones.
    Before saving at least one of the parameters Device Type, Management or Rulebase has to be different from the existing gateways if the Import Disabled flag is not set.
');
INSERT INTO txt VALUES ('H5151', 'German',  'Name*: Name des Gateways. F&uuml;r Fortinet muss dies der reale Name des Firewallgateways sein wie in der Config definiert.');
INSERT INTO txt VALUES ('H5151', 'English', 'Name*: Name of the Gateway. For Fortinet this must be the real name of the firewall gateway as defined in the config.');
INSERT INTO txt VALUES ('H5152', 'German',  'Kommentar: Optionaler Kommentar zu diesem Gateway.');
INSERT INTO txt VALUES ('H5152', 'English', 'Comment: Optional comment regarding this gateway.');
INSERT INTO txt VALUES ('H5153', 'German',  'Ger&auml;tetyp*: Auswahlliste der verf&uuml;gbaren Typen. F&uuml;r die verf&uuml;gbaren Typen siehe
    <a href="/help/settings/managements">Managementeinstellungen</a>.
');
INSERT INTO txt VALUES ('H5153', 'English', 'Device Type*: Out of a list of available types. For a list of available device types see 
    <a href="/help/settings/managements">management settings</a>.
');
INSERT INTO txt VALUES ('H5154', 'German',  'Management*: W&auml;hlen Sie das Management, welches dieses Gateway kontrolliert. Wenn zu einem Beispielmanagement zugeordnet, wird es mitgel&ouml;scht, wenn die "Beispieldaten l&ouml;schen"-Schaltfl&auml;che bei den Managementeinstellungen bet&auml;tigt wird.');
INSERT INTO txt VALUES ('H5154', 'English', 'Management*: Select the management system that controls this gateway. If related to a sample management this Gateway will also be deleted when using the "Remove Sample Data" button on the management settings page.');
INSERT INTO txt VALUES ('H5155', 'German',  'Lokale Rulebase* / Lokales Package*: Hier wird der Name der Rulebase hinterlegt.
    <ul>
        <li>F&uuml;r Check Point R8x kommt hierhin der Name der top level Zugriffsschicht (default ist "Network").</li>
        <li>F&uuml;r Check Point R8x MDS wird hier der Name der global policy Schicht eingetragen, gefolgt vom Namen der domain policy, gertrennt durch "/", z.B. "global-policy-layer-name/domain-policy-layer-name".</li>
        <li>F&uuml;r Fortinet-Systeme muss jedes Gateway (auch jede vdom) als separates Management mit einem einzelnen Gateway eingeragen werden.
            Bei vdoms sind sowohl Management-Name, Gateway-Name als auch Regelwerksname wie folgt zu bilden: Systemname___vdom-Name (Trennzeichen: 3x Unterstrich) 
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H5155', 'English', 'Local Rulebase* / Local Package*: Enter the name of the rulebase here. 
    <ul>
        <li>For Check Point R8x the top level access layer name goes here (default is "Network").</li>
        <li>For Check Point R8x MDS enter the name of the global policy layer followed by the name of the domain policy separated by "/", e.g. "global-policy-layer-name/domain-policy-layer-name".</li>
        <li>For Fortinet systems every gateway (and every vdom) must be defined as a separate management system with a single gateway.
            When dealing with vdoms set management name, gateway name and rulebase name as follows: system name___vdom name (separator: 3x underscore) 
            </li>
    </ul>
');
INSERT INTO txt VALUES ('H5156', 'German',  'Globale Rulebase / Globales Package: Hier wird der Name der Globalen Rulebase hinterlegt.');
INSERT INTO txt VALUES ('H5156', 'English', 'Global Rulebase / Global Package: Enter the name of the global rulebase here.');
INSERT INTO txt VALUES ('H5157', 'German',  'Package: Hier wird ggf. der Name des Package hinterlegt (nur f&uuml;r Check Point).');
INSERT INTO txt VALUES ('H5157', 'English', 'Package: Enter the name of the Package here (only for Check Point).');
INSERT INTO txt VALUES ('H5158', 'German',  'Import Deaktiviert: Schalter um den Datenimport zu deaktivieren.');
INSERT INTO txt VALUES ('H5158', 'English', 'Import Disabled: Flag if the data import is disabled.');
INSERT INTO txt VALUES ('H5159', 'German',  'Nicht sichtbar: Wenn gesetzt ist dieses Gateway nicht mit Standard-Reporter-Rolle sichtbar.');
INSERT INTO txt VALUES ('H5159', 'English', 'Hide in UI: If set, this gateway is not visible to the standard reporter role.');
INSERT INTO txt VALUES ('H5171', 'German',  'Hier wird ein &Uuml;berblick &uuml;ber den Status der Importjobs der verschiedenen Managements gegeben.
    Dabei werden Managements, die Auff&auml;lligkeiten aufweisen (wie sie auch vom <a href="/help/monitoring/daily_checks">T&auml;glichen Check</a> beanstandet w&uuml;rden), rot unterlegt und zuerst aufgelistet,
    danach folgen gelb unterlegt die laufenden Imports, dann erst die &uuml;brigen Managements.
');
INSERT INTO txt VALUES ('H5171', 'English', 'The status of the import jobs for the different managements is displayed here.
    Managements which show anomalies (which would also lead to alerts in the <a href="/help/monitoring/daily_checks">Daily Check</a>) are highlighted in red and listed first,
    followed by running imports highlighted in yellow, finally the remaining managements.
');
INSERT INTO txt VALUES ('H5181', 'German',  'Neu anzeigen: Aktualisiert die dargestellten Daten.');
INSERT INTO txt VALUES ('H5181', 'English', 'Refresh: Updates the displayed data.');
INSERT INTO txt VALUES ('H5182', 'German',  'Details: F&uuml;r das ausgew&auml;hlte Management wird hier eine genauere &Uuml;bersicht &uuml;ber die Import-Ids, Start/Stop-Zeiten, 
    Dauer und Fehler des ersten, letzten erfolgreichen und letzten Imports gegeben, sowie die Anzahl der Fehler seit dem letzten erfolgreichen Import.
');
INSERT INTO txt VALUES ('H5182', 'English', 'Details: For the selected management a detailed view on import ids, start/stop times, 
    duration and errors of the first, last successful and last import, as well as the number of errors since the last successful import.
');
INSERT INTO txt VALUES ('H5183', 'German',  'Letzter Unvollendeter: Die Startzeit eines aktuell laufenden Imports falls vorhanden.
    L&auml;uft der Import schon l&auml;nger als 5 Minuten, wird eine Schaltfl&auml;che zum Zur&uuml;cksetzen angeboten. Sie soll f&uuml;r h&auml;ngengebliebene Imports genutzt werden.
    Da ein erfolgreicher Import einige Minuten dauern kann, sollte der Rollback nicht zu fr&uuml;h angestossen werden.
');
INSERT INTO txt VALUES ('H5183', 'English', 'Last incomplete: The start time of an import actually running, if there is one.
    If the import is already running longer than 5 minutes, a button is displayed to rollback the import. This is intended to be used for hanging imports.
    As a successful import may take some minutes, be careful not to rollback too early.
');
INSERT INTO txt VALUES ('H5184', 'German',  'Letzter Erfolg: Die Stopzeit des letzten erfolgreichen Imports.');
INSERT INTO txt VALUES ('H5184', 'English', 'Last success: The stop time of the last successful import.');
INSERT INTO txt VALUES ('H5185', 'German',  'Letzter Import: Die Stopzeit des letzten Imports.');
INSERT INTO txt VALUES ('H5185', 'English', 'Last import: The stop time of the last import.');
INSERT INTO txt VALUES ('H5186', 'German',  'Erfolg: Zeigt an, ob der letzte Import erfolgreich war.');
INSERT INTO txt VALUES ('H5186', 'English', 'Success: Flag showing the success of the last import.');
INSERT INTO txt VALUES ('H5187', 'German',  'Fehler: Zeigt die Fehlermeldung, falls der letzte Import nicht erfolgreich war.');
INSERT INTO txt VALUES ('H5187', 'English', 'Errors: Is only filled with an error message, if the success flag is false.');
INSERT INTO txt VALUES ('H5201', 'German',  'Admins k&ouml;nnen mehrere unterschiedliche Ldap-Verbindungen einrichten und verwalten. Sie k&ouml;nnen alle zur Nutzerauthentifizierung genutzt werden.<br>
    Das interne Ldap (Bestandteil der Installation) wird mindestens f&uuml;r die Rollenzuordnung ben&ouml;tigt, kann aber auch f&uuml;r Nutzerauthentifizierung und Nutzergruppenverwaltung genutzt werden.<br>
    Die Ldap-Verbindungen k&ouml;nnen hinzugef&uuml;gt, ge&auml;ndert oder gel&ouml;scht werden.
    Eine L&ouml;schung ist nur zul&auml;ssig, wenn es nicht das interne Ldap (definiert durch den gesetzten Rollensuchpfad) und nicht das letzte vorhandene Ldap ist.<br>
    Die "Klonen"-Schaltfl&auml;che unterst&uuml;tzt beim Definieren einer neuen Ldap-Verbindung, indem Daten von einem existierenden kopiert werden.
    Vor dem Speichern muss mindestens Adresse oder Portnummer ge&auml;ndert werden.<br>
    Beim Editieren kann mit der "Verbindung testen"-Schaltfl&auml;che gepr&uuml;ft werden, ob eine Verbindung mit den aktuellen Parametern aufgebaut werden kann.
');
INSERT INTO txt VALUES ('H5201', 'English', 'Admins can create and administrate several different Ldap connections. All of them can be used for user authentication.<br>
    The internal Ldap (part of the initial installation) is needed at least for role assignment, but can also be used for user authentication and user group handling.<br>
    The Ldap connections can be added, changed and deleted.
    Deletion is only allowed, if it is not the internal Ldap (defined by the existence of a role search path) and if it is not the last Ldap.<br>
    The clone button helps defining new Ldaps by copying the data from existing ones. Before saving at least the address or port number have to be changed.<br>
    While editing, a "Test connection" button helps checking, if a connection can be built up with the actual parameters.
');
INSERT INTO txt VALUES ('H5210', 'German',  'Name*: Name des verbundenen Ldap. Kann frei gew&auml;hlt werden. Wenn nicht vergeben, wird der Host (Adresse:Port) dargestellt.');
INSERT INTO txt VALUES ('H5210', 'English', 'Name*: Name of the connected Ldap to be freely given. If not assigned the Host (Address:Port) is displayed.');
INSERT INTO txt VALUES ('H5211', 'German',  'Adresse*: Adresse des verbundenen Ldap (z.B. IP-Adresse)');
INSERT INTO txt VALUES ('H5211', 'English', 'Address*: Address of the connected Ldap (e.g. IP address).');
INSERT INTO txt VALUES ('H5212', 'German',  'Port*: Portnummer des verbundenen Ldap.');
INSERT INTO txt VALUES ('H5212', 'English', 'Port*: Port number of the connected Ldap.');
INSERT INTO txt VALUES ('H5213', 'German',  'Tls: Zeigt an, ob TLS in der Kommunikation verwendet wird.');
INSERT INTO txt VALUES ('H5213', 'English', 'Tls: Flag if TLS is used for communication.');
INSERT INTO txt VALUES ('H5214', 'German',  'Mandantenebene: Wenn Mandanten Teil des Distinguished Name (Dn) des Nutzers sind, definiert diese Zahl die Pfadtiefe, wo dieser zu finden ist. 
    Das beginnt mit 1 f&uuml;r das erste Element von rechts. Wenn keine Mandanten genutzt werden, auf 0 setzen.
');
INSERT INTO txt VALUES ('H5214', 'English', 'Tenant Level: If tenants are part of the distinguished names (Dn) of the user, this number defines the level in the path, where they are found.
    Starting with 1 for the first Dn element from the right. Set to 0 if no tenants are used.
');
INSERT INTO txt VALUES ('H5215', 'German',  'Typ*: Implementierungstyp des Ldap, welcher die Syntax des Zugangs festlegt. Zur Zeit werden "OpenLdap" und "ActiveDirectory" unterst&uuml;tzt.
    "Default" ist eine &Uuml;bermenge von verschiedenen Syntax-Varianten, die m&ouml;glicherweise weiterhilft, wenn die anderen nicht anwendbar sind.
');
INSERT INTO txt VALUES ('H5215', 'English', 'Type*: Implementation type of the Ldap, which defines the syntax of the access. Currently "OpenLdap" and "ActiveDirectory" are supported.
    "Default" is a supergroup of several syntax variants, which may be appropriate, if the others are not applicable.
');
INSERT INTO txt VALUES ('H5216', 'German',  'Suchmusterl&auml;nge: Minimale L&auml;nge f&uuml;r Suchmuster im Ldap.
    Um zu grosse Treffermengen in Systemen mit vielen Nutzern zu vermeiden, wird eine L&auml;nge von mindestens 3 empfohlen.
    F&uuml;r Systeme mit geringer Nutzerzahl kann der Wert auf 0 gesetzt werden.
');
INSERT INTO txt VALUES ('H5216', 'English', 'Pattern Length: Defines a minimal length for patterns for searches in the Ldap.
    To avoid a high hit rate in Ldaps with high amounts of users a length of at least 3 is recommended.
    For systems with few users the value can be set to 0.
');
INSERT INTO txt VALUES ('H5217', 'German',  'Suchpfad Nutzer*: Der Distinguished name (Dn) des Wurzelverzeichnisses des Nutzersuchbaums.');
INSERT INTO txt VALUES ('H5217', 'English', 'User Search Path*: The distinguished name (Dn) of the root of the users search tree.');
INSERT INTO txt VALUES ('H5218', 'German',  'Suchpfad Rollen: Der Distinguished name (Dn) des Wurzelverzeichnisses des Rollensuchbaums. Da Rollen nur vom internen Ldap verwaltet werden, sollte dieser Parameter nur dort gesetzt werden.');
INSERT INTO txt VALUES ('H5218', 'English', 'Role Search Path: The distinguished name (Dn) of the root of the role search tree. As the roles are administrated only by the internal Ldap this parameter should only be set there.');
INSERT INTO txt VALUES ('H5219', 'German',  'Suchpfad Gruppen: Der Distinguished name (Dn) des Wurzelverzeichnisses des Gruppensuchbaums. Dieser Parameter sollte f&uuml;r das interne Ldap nur gesetzt werden, wenn Gruppen verwendet werden.');
INSERT INTO txt VALUES ('H5219', 'English', 'Group Search Path: The distinguished name (Dn) of the root of the group search tree. This parameter should only be set for the internal Ldap, if user group handling is used.');
INSERT INTO txt VALUES ('H5220', 'German',  'Nutzer f&uuml;r Suche*: Der Distinguished name (Dn) des Nutzers, der die Rechte f&uuml;r Suchen im Ldap hat.');
INSERT INTO txt VALUES ('H5220', 'English', 'Search User*: The distinguished name (Dn) of the user having the rights performing searches in the Ldap.');
INSERT INTO txt VALUES ('H5221', 'German',  'Passwort Nutzer f&uuml;r Suche*: Passwort des f&uuml;r Suchen verwendeten Nutzers.');
INSERT INTO txt VALUES ('H5221', 'English', 'Search User Password*: The password for the search user.');
INSERT INTO txt VALUES ('H5222', 'German',  'Schreibender Nutzer: Der Distinguished name (Dn) des Nutzers, der die Rechte f&uuml;r Schreiboperationen im Ldap hat. Notwendig nur f&uuml;r das interne Ldap, um Nutzer und Gruppen zu verwalten.');
INSERT INTO txt VALUES ('H5222', 'English', 'Write User: The distinguished name (Dn) of the user having the rights performing write operations in the Ldap. Necessary only for the internal Ldap to administrate users and groups.');
INSERT INTO txt VALUES ('H5223', 'German',  'Passwort Schreibender Nutzer: Passwort des zum Schreiben verwendeten Nutzers.');
INSERT INTO txt VALUES ('H5223', 'English', 'Write User Password: The password for the write user.');
INSERT INTO txt VALUES ('H5224', 'German',  'Mandant: Wenn das Ldap nur f&uuml;r einen Mandanten genutzt werden soll, kann dieser hier ausgew&auml;hlt werden.');
INSERT INTO txt VALUES ('H5224', 'English', 'Tenant: If the Ldap is used only for one tenant, it can be selected here.');
INSERT INTO txt VALUES ('H5225', 'German',  'Globaler Mandantenname: Wenn das Ldap Mandanten nutzt (Mandantenebene > 0), kann dieser hier ein Name f&uuml;r den Globalen Mandanten gesetzt werden.
    Dieser wird dann im Distinguished Name (Dn) an entsprechender Stelle eingesetzt.
');
INSERT INTO txt VALUES ('H5225', 'English', 'Global Tenant Name: If the Ldap is using tenants (Tenant Level > 0), the name of the Global Tenant can be set here.
    This name will be used in the respective position in the Distinguished Name (Dn).
');
INSERT INTO txt VALUES ('H5226', 'German',  'Aktiv: Wenn das Ldap nicht auf aktiv gesetzt ist, wird es f&uuml;r andere Aktionen (Autorisierungen, Rollenzuweisung etc.) nicht ber&uuml;cksichtigt.');
INSERT INTO txt VALUES ('H5226', 'English', 'Active: If not set to active, the Ldap is not involved in other actions (authorization, role assignment etc.).');
INSERT INTO txt VALUES ('H5231', 'German',  'Die verf&uuml;gbaren Mandanten werden hier mit den zugeordneten Gateways dargestellt.<br>
    Es ist m&ouml;glich, Mandanten im lokalen Ldap sowie Verkn&uuml;pfungen zu den vorhandenen <a href="/help/settings/gateways">Gateways</a> anzulegen oder zu l&ouml;schen.
    Wenn Beispieldaten (definiert durch die Endung "_demo" vom Mandantennamen) existieren, wird eine Schaltfl&auml;che angezeigt, um diese zu l&ouml;schen.
');
INSERT INTO txt VALUES ('H5231', 'English', 'The available tenants are listed in the table with the related gateways.<br>
    It is possible to add or delete tenants in the local LDAP and relationships to the defined <a href="/help/settings/gateways">gateways</a>.
    If there are sample data (defined by the ending "_demo" of the tenant name), a button is displayed to delete them.
');
INSERT INTO txt VALUES ('H5241', 'German',  'Mandantenaktion: M&ouml;glichkeit zum L&ouml;schen des Mandanten vom lokalen Ldap. Ausnahme ist der Globale Mandant, der nicht gel&ouml;scht werden darf.');
INSERT INTO txt VALUES ('H5241', 'English', 'Tenant Action: Possibility to delete tenant from local Ldap. The Global Tenant is excepted from this as it should not be deleted.');
INSERT INTO txt VALUES ('H5242', 'German',  'Gatewayaktion: M&ouml;glichkeit zum Anlegen oder L&ouml;schen von Zuweisungen von Gateways zu Mandanten. Ausnahme ist der Globale Mandant, da er automatisch zu allen Gateways zugeordnet ist.');
INSERT INTO txt VALUES ('H5242', 'English', 'Gateway Action: Possibility to add or delete assignments from gateways to tenants. The Global Tenant is excepted from this as it is automatically related to all gateways.');
INSERT INTO txt VALUES ('H5243', 'German',  'Name: Name des Mandanten');
INSERT INTO txt VALUES ('H5243', 'English', 'Name: Tenant name');
INSERT INTO txt VALUES ('H5244', 'German',  'Kommentar: Hier kann ein Kommentar eingetragen werden.');
INSERT INTO txt VALUES ('H5244', 'English', 'Comment: Possibility to enter a comment.');
INSERT INTO txt VALUES ('H5245', 'German',  'Projekt: Hier kann ein Projekt eingetragen werden.');
INSERT INTO txt VALUES ('H5245', 'English', 'Project: Possibility to enter a project.');
INSERT INTO txt VALUES ('H5246', 'German',  'Sicht auf alle Ger&auml;te: Zeigt an, dass der Mandant alle Ger&auml;te sehen darf.');
INSERT INTO txt VALUES ('H5246', 'English', 'View All Devices: Flag indicating that this tenant has view on all devices.');
INSERT INTO txt VALUES ('H5247', 'German',  'Superadmin: Zeigt an, dass es sich um den Superadmin handelt.');
INSERT INTO txt VALUES ('H5247', 'English', 'Superadmin: Flag indicating the superadmin.');
INSERT INTO txt VALUES ('H5248', 'German',  'Gateways: Alle mit diesem Mandanten verkn&uuml;pften Gateways.');
INSERT INTO txt VALUES ('H5248', 'English', 'Gateways: All gateways related to this tenant.');
INSERT INTO txt VALUES ('H5261', 'German',  'Hier werden alle dem System bekannten Nutzer dargestellt.
    Das sind alle im internen Ldap angelegten Nutzer, sowie Nutzer von externen Ldaps, die sich schon mindestens einmal angemeldet haben.<br>
    Der Administrator kann Nutzer anlegen, &auml;ndern oder l&ouml;schen. Beim Anlegen besteht auch die M&ouml;glichkeit, sofort Gruppen- und Rollenzugeh&ouml;rigkeiten festzulegen.
    Weitere Gruppen- und Rollenzuordnungen k&ouml;nnen dann in den Abschnitten <a href="/help/settings/groups">Gruppen</a> bzw. <a href="/help/settings/roles">Rollen</a> erfolgen.<br>
    Wenn Beispieldaten (definiert durch die Endung "_demo" vom Nutzernamen) existieren, wird eine Schaltfl&auml;che angezeigt, um diese zu l&ouml;schen.
');
INSERT INTO txt VALUES ('H5261', 'English', 'Here all users known to the system are displayed. 
    These are all users defined in the internal Ldap and users from external Ldaps who have already logged in at least once.<br>
    The administrator can add, change or delete users. When adding there is the possibility to assign group or role memberships.
    Further memberships can be administrated in the <a href="/help/settings/groups">groups</a> resp. <a href="/help/settings/roles">roles</a> sections.<br>
    If there are sample data (defined by the ending "_demo" of the user name), a button is displayed to delete them.
');
INSERT INTO txt VALUES ('H5271', 'German',  'Aktionen: Nutzer k&ouml;nnen geklont, ge&auml;ndert oder gel&ouml;scht werden.
    Ausserdem kann der Administrator das Passwort der Nutzer zur&uuml;cksetzen und ein neues setzen, welches den Vorgaben der <a href="/help/settings/passwordpolicy">Passworteinstellungen</a> gen&uuml;gen muss.
');
INSERT INTO txt VALUES ('H5271', 'English', 'Actions: Users can be cloned, edited or deleted. 
    Additionally the administrator has the possibility to reset the password of the users and set a new password which has to comply with the <a href="/help/settings/passwordpolicy">Password Policy</a>.
');
INSERT INTO txt VALUES ('H5272', 'German',  'Name: Nutzername.');
INSERT INTO txt VALUES ('H5272', 'English', 'Name: Name of the user.');
INSERT INTO txt VALUES ('H5273', 'German',  'Mandant: Mandant, zu dem der Nutzer zugeordnet ist.');
INSERT INTO txt VALUES ('H5273', 'English', 'Tenant: Tenant the user belongs to.');
INSERT INTO txt VALUES ('H5274', 'German',  'Email: Email-Adresse des Nutzers.');
INSERT INTO txt VALUES ('H5274', 'English', 'Email: Email address of the user.');
INSERT INTO txt VALUES ('H5275', 'German',  'Sprache: Bevorzugte Sprache des Nutzers. Wird nur dargestellt, wenn der Nutzer sich bereits mindestens einmal angemeldet hat.');
INSERT INTO txt VALUES ('H5275', 'English', 'Language: Preferred language of the user. Is only displayed, when the user has already logged in at least once.');
INSERT INTO txt VALUES ('H5276', 'German',  'Letzte Anmeldung: Datum und Zeit der letzten Anmeldung des Nutzers.');
INSERT INTO txt VALUES ('H5276', 'English', 'Last login: Last login date and time of the user.');
INSERT INTO txt VALUES ('H5277', 'German',  'Letzte Passwort&auml;nderung: Datum und Zeit der letzten Passwort&auml;nderung. Die kann vom Admin oder vom Nutzer selbst gemacht worden sein.');
INSERT INTO txt VALUES ('H5277', 'English', 'Last Password Change: Date and time of the last password change. This may have been made by the admin or the user itself.');
INSERT INTO txt VALUES ('H5278', 'German',  'PW &Auml;nd. erf.: Zeigt an, dass der Nutzer beim n&auml;chsten Anmelden sein Passwort &auml;ndern muss.
    Der Nutzer muss dann das Passwort in einem separaten Fenster &auml;ndern, bevor er in die eigentliche Anwendung weitergeleitet wird.
    Der Schalter wird gesetzt, wenn ein neuer Nutzer angelegt oder das Passwort vom Admin zur&uuml;ckgesetzt wurde,
    ausser f&uuml;r Nutzer mit Auditor-Rolle, da diese keinerlei &Auml;nderungen im System machen d&uuml;rfen.
');
INSERT INTO txt VALUES ('H5278', 'English', 'Pwd Chg Req: Flag that the user has to change his password at next login.
    The user is then forced to change the password in a separate popup window before he can proceed to the application.
    The flag is set when a new user is added or when the admin has reset the password, 
    except for users with auditor role, because that role is not allowed to make any changes in the system.
');
INSERT INTO txt VALUES ('H5301', 'German',  'Der Admin kann Nutzergruppen im internen Ldap definieren. Dabei besteht die M&ouml;glichkeit, sie gleich einer Rolle zuzuordnen.
    Weitere Rollenzuordnungen k&ouml;nnen dann unter <a href="/help/settings/roles">Rollen</a> erfolgen.<br>
    Wenn Beispieldaten (definiert durch die Endung "_demo" vom Gruppennamen) existieren, wird eine Schaltfl&auml;che angezeigt, um diese zu l&ouml;schen.
    Die L&ouml;schung ist nicht m&ouml;glich, wenn Nutzer, die nicht als Beispielnutzer gekennzeichnet sind (Name endet nicht auf "_demo"), der Gruppe zugeordnet sind.
');
INSERT INTO txt VALUES ('H5301', 'English', 'Groups of users can be defined by the admin in the internal Ldap. When adding there is the possibility to assign a role membership.
    Further memberships can be administrated in the <a href="/help/settings/roles">roles</a> section.<br>
    If there are sample data (defined by the ending "_demo" of the group name), a button is displayed to delete them.
    The deletion is only possible, if there are no non-sample users (user name not ending with "_demo") assigned to the group.
');
INSERT INTO txt VALUES ('H5311', 'German',  'Gruppenaktionen: Hier k&ouml;nnen selbstdefinierte Gruppen ge&auml;ndert (zur Zeit nur umbenannt) oder gel&ouml;scht werden.');
INSERT INTO txt VALUES ('H5311', 'English', 'Group actions: Here is the possibility to edit (currently only rename) or delete self defined user groups.');
INSERT INTO txt VALUES ('H5312', 'German',  'Nutzeraktionen: Hier k&ouml;nnen dem System bekannte Nutzer (siehe <a href="/help/settings/users">Nutzereinstellungen</a>) der Gruppe zugeordnet oder von dieser entfernt werden.');
INSERT INTO txt VALUES ('H5312', 'English', 'User actions: Here users known to the system (see <a href="/help/settings/users">User settings</a>) can be assigned to or removed from the user groups.');
INSERT INTO txt VALUES ('H5313', 'German',  'Name: Name der Nutzergruppe.');
INSERT INTO txt VALUES ('H5313', 'English', 'Name: Name of the user group.');
INSERT INTO txt VALUES ('H5314', 'German',  'Nutzer: Liste der der Gruppe zugeordneten Nutzer.');
INSERT INTO txt VALUES ('H5314', 'English', 'Users: List of assigned users to the group.');
INSERT INTO txt VALUES ('H5331', 'German',  'Alle definierten Rollen werden mit einer kurzen Erkl&auml;rung dargestellt.<br>
    Der Admin kann Nutzer oder Nutzergruppen den Rollen zuweisen bzw. von diesen entfernen.
');
INSERT INTO txt VALUES ('H5331', 'English', 'All defined roles in the system are displayed with a short explanation.<br>
    The admin can assign or remove users or user groups to/from the roles.
');
INSERT INTO txt VALUES ('H5341', 'German',  'Aktionen: Der Admin hat die M&ouml;glichkeit, Nutzer den Rollen zuzuordnen oder sie von ihnen zu entfernen,
    ausser f&uuml;r "anonymous" oder "middleware-server", welche nur intern genutzt werden kann.
    Das Hinzuf&uuml;gen der Nutzer kann auf drei Arten erfolgen:
');
INSERT INTO txt VALUES ('H5341', 'English', 'Actions: The admin can add or delete users from the roles, 
    except for "anonymous" or "middleware-server" which can only be used internally.
    For adding users there are three possibilities:
');
INSERT INTO txt VALUES ('H5342', 'German',  'Name: Rollenname');
INSERT INTO txt VALUES ('H5342', 'English', 'Name: Name of the role');
INSERT INTO txt VALUES ('H5343', 'German',  'Beschreibung: Kurze Beschreibung des vorgesehenen Einsatzgebietes der Rolle.');
INSERT INTO txt VALUES ('H5343', 'English', 'Description: Short description of the intended scope of the role.');
INSERT INTO txt VALUES ('H5344', 'German',  'Nutzer/Gruppen: Zugeordnete Nutzer oder Nutzergruppen.');
INSERT INTO txt VALUES ('H5344', 'English', 'Users/Groups: Assigned users or user groups.');
INSERT INTO txt VALUES ('H5351', 'German',  'Suche in einem der <a href="/help/settings/ldap">verbundenen Ldaps</a>.
    M&ouml;glicherweise ist ein Suchstring der in der Ldap-Verbindung definierten Mindestl&auml;nge einzutragen.
    Die Syntax daf&uuml;r ist dieselbe, die auch f&uuml;r eine direkte Suche im Ldap erwartet wird.
');
INSERT INTO txt VALUES ('H5351', 'English', 'Search in one of the <a href="/help/settings/ldap">connected Ldaps</a>.
    A search string may be necessary with the minimal length defined in the Ldap connection. 
    For that the syntax is the same as searching directly in the connected Ldap.
');
INSERT INTO txt VALUES ('H5352', 'German',  'Auswahl aus der Liste der bekannten Nutzer, wie sie in den <a href="/help/settings/users">Nutzereinstellungen</a> dargestellt wird.');
INSERT INTO txt VALUES ('H5352', 'English', 'Select from the list of known users also displayed in the <a href="/help/settings/users">users settings</a>.');
INSERT INTO txt VALUES ('H5353', 'German',  'Auswahl aus der Liste der internen Gruppen, wie sie in den <a href="/help/settings/groups">Gruppeneinstellungen</a> dargestellt wird.');
INSERT INTO txt VALUES ('H5353', 'English', 'Select from the list of internal groups also displayed in the <a href="/help/settings/groups">groups settings</a>.');
INSERT INTO txt VALUES ('H5401', 'German',  'Der Admin kann verschiedene Standardwerte definieren, die dann f&uuml;r alle Nutzer gelten.<br>
    Manche von ihnen k&ouml;nnen in den individuellen Nutzereinstellungen &uuml;berschrieben werden.
');
INSERT INTO txt VALUES ('H5401', 'English', 'The admin can define several default values, which are valid for all users.<br>
    Some of them can be overwritten in the individual settings of each user.
');
INSERT INTO txt VALUES ('H5411', 'German',  'Standardsprache: Die Sprache, die neuen Nutzern beim ersten Anmelden zugewiesen wird.
    Nach dem Anmelden kann jeder Nutzer seine eigene bevorzugte <a href="/help/settings/language">Sprache</a> definieren.
');
INSERT INTO txt VALUES ('H5411', 'English', 'Default Language: The language which every user gets at first login. 
    After login each user can define its own preferred <a href="/help/settings/language">language</a>.
');
INSERT INTO txt VALUES ('H5412', 'German',  'Pro Abruf geholte Elemente: Definiert die (maximale) Anzahl der Objekte, die bei der Reporterzeugung und beim Aufbau der rechten Randleiste in einem Schritt geholt werden.
    Dies kann genutzt werden, um die Performanz zu optimieren, wenn n&ouml;tig.
');
INSERT INTO txt VALUES ('H5412', 'English', 'Elements per fetch: Defines the (maximum) number of objects which are fetched in one step for the report creation and the build up of the right sidebar.
    This can be used to optimize performance if necessary.
');
INSERT INTO txt VALUES ('H5413', 'German',  'Max initiale Abrufe rechte Randleiste: Definiert die (maximale) Anzahl an Abrufen w&auml;hrend der Initialisierung der rechten Randleiste.
    Dies kann genutzt werden, um die Performanz zu optimieren, wenn n&ouml;tig.
');
INSERT INTO txt VALUES ('H5413', 'English', 'Max initial fetches right sidebar: Defines the (maximum) number of fetches during initialization of the right sidebar.
    This can be used to optimize performance if necessary.
');
INSERT INTO txt VALUES ('H5414', 'German',  'Komplettes F&uuml;llen rechte Randleiste: Erzwingt, dass die rechte Randleiste immer komplett gef&uuml;llt wird.
    Kann gesetzt werden, wenn die Gesamtzahl der Objekte moderat ist, so dass keine Performanzprobleme zu erwarten sind.
');
INSERT INTO txt VALUES ('H5414', 'English', 'Completely auto-fill right sidebar: A flag to enforce that the right sidebar is always filled completely.
    It can be set, if the total amount of objects is moderate, so there are no performance issues expected.
');
INSERT INTO txt VALUES ('H5415', 'German',  'Datenaufbewahrungszeit (in Tagen): Legt fest, wie lange die Daten in der Datenbank gehalten werden (wird noch nicht unterst&uuml;tzt).');
INSERT INTO txt VALUES ('H5415', 'English', 'Data retention time (in days): Defines how long the data is kept in the database (currently not supported).');
INSERT INTO txt VALUES ('H5416', 'German',  'Importintervall (in Sekunden): Zeitintervall zwischen zwei Imports (wird noch nicht unterst&uuml;tzt)');
INSERT INTO txt VALUES ('H5416', 'English', 'Import sleep time (in seconds): Time between import loops (currently not supported).');
INSERT INTO txt VALUES ('H5417', 'German',  'Rezertifizierungsintervall (in Tagen): Maximale Zeit, nach der eine Regel rezertifiziert werden soll.');
INSERT INTO txt VALUES ('H5417', 'English', 'Recertification Period (in days): Maximum time, after when a rule should be recertified.');
INSERT INTO txt VALUES ('H5418', 'German',  'Rezertifizierungserinnerungsintervall (in Tagen): Zeit vor dem F&auml;lligkeitsdatum, ab der eine Regel als f&auml;llig hervorgehoben werden soll.');
INSERT INTO txt VALUES ('H5418', 'English', 'Recertification Notice Period (in days): Time before the due date when the rule should be marked as upcoming recertification.');
INSERT INTO txt VALUES ('H5419', 'German',  'Rezertifizierungsanzeigeintervall (in Tagen): Vorausschauintervall f&uuml;r f&auml;llige Rezertifizierungen.');
INSERT INTO txt VALUES ('H5419', 'English', 'Recertification Display Period (in days): Lookahead period for upcoming recertifications.');
INSERT INTO txt VALUES ('H5420', 'German',  'Frist zum L&ouml;schen der Regeln (in Tagen): Maximale Zeit, die dem fwadmin zum L&ouml;schen der dezertifizierten Regeln einger&auml;umt wird.');
INSERT INTO txt VALUES ('H5420', 'English', 'Rule Removal Grace Period (in days): Maximum time the fwadmin has to remove the decertified rules.');
INSERT INTO txt VALUES ('H5421', 'German',  'Kommentar Pflichtfeld: Legt fest, dass das Kommentarfeld f&uuml;r Re- und Dezertifizierungen gef&uuml;llt sein muss.');
INSERT INTO txt VALUES ('H5421', 'English', 'Comment Required: A non-empty comment for the re- or decertification is required.');
INSERT INTO txt VALUES ('H5422', 'German',  'Devices zu Beginn eingeklappt ab: Legt fest, ab wievielen Devices (Managements + Gateways) diese in der linken Randleiste zun&auml;chst eingeklappt dargestellt werden.');
INSERT INTO txt VALUES ('H5422', 'English', 'Devices collapsed at beginning from: defines from which number of devices (managements + gateways) they are displayed collapsed in the left sidebar at beginning.');
INSERT INTO txt VALUES ('H5423', 'German',  'Nachrichten-Anzeigedauer (in Sekunden): legt fest, wie lange Erfolgs-Nachrichten dargestellt werden, bis sie automatisch ausgeblendet werden.
    Fehler-Nachrichten erscheinen dreimal so lange. Beim Wert 0 werden die Nachrichten nicht automatisch ausgeblendet.
    Die Nutzer-Meldungen k&ouml;nnen auch danach noch unter <a href="/help/monitoring/ui_messages">UI-Nachrichten</a> eingesehen werden.
');
INSERT INTO txt VALUES ('H5423', 'English', 'Message view time (in seconds): defines how long success messages are displayed, until they fade out automatically.
    Error messages are displayed 3 times as long. Value 0 means that the messages do not fade out.
    All user messages can still be reviewed at <a href="/help/monitoring/ui_messages">UI Messages</a>.
');
INSERT INTO txt VALUES ('H5424', 'German',  'Startzeit t&auml;glicher Check: legt die Zeit fest, wann der t&auml;gliche Check durchgef&uuml;hrt werden soll.');
INSERT INTO txt VALUES ('H5424', 'English', 'Daily check start at: defines the time when the daily check should happen.');
INSERT INTO txt VALUES ('H5425', 'German',  'FW API - Pro Abruf geholte Elemente: Definiert die (maximale) Anzahl der Objekte, die beim Import &uuml;ber die FWO-API in einem Schritt geholt werden.
    Dies kann genutzt werden, um die Performanz zu optimieren, wenn n&ouml;tig.
');
INSERT INTO txt VALUES ('H5425', 'English', 'FW API - Elements per fetch: Defines the (maximum) number of objects which are fetched in one step during import via the FWO-API.
    This can be used to optimize performance if necessary.
');
INSERT INTO txt VALUES ('H5426', 'German',  'Autodiscover-Intervall (in Stunden): legt das Intervall fest, in dem die Autodiscovery durchgef&uuml;hrt werden soll.');
INSERT INTO txt VALUES ('H5426', 'English', 'Auto-discovery sleep time (in hours): defines the interval in which the autodiscovery should be performed.');
INSERT INTO txt VALUES ('H5427', 'German',  'Autodiscover-Start: legt eine Bezugszeit fest, ab dem die Intervalle f&uuml;r die Autodiscovery gerechnet werden.');
INSERT INTO txt VALUES ('H5427', 'English', 'Auto-discovery start at: defines a referential time from which the autodiscovery intervals are calculated.');
INSERT INTO txt VALUES ('H5431', 'German',  'Der Administrator kann Vorgaben f&uuml;r Passw&ouml;rter definieren, gegen die alle neuen Passw&ouml;rter aller (internen) Nutzer gepr&uuml;ft werden.');
INSERT INTO txt VALUES ('H5431', 'English', 'The admin user can define a password policy, against which all new passwords of all (internal) users are checked.');
INSERT INTO txt VALUES ('H5441', 'German',  'Mindestl&auml;nge: Minimale L&auml;nge des Passworts');
INSERT INTO txt VALUES ('H5441', 'English', 'Min Length: Minimal length of the password.');
INSERT INTO txt VALUES ('H5442', 'German',  'Grossbuchstaben enthalten: Das Passwort muss mindestens einen Grossbuchstaben enthalten.');
INSERT INTO txt VALUES ('H5442', 'English', 'Upper Case Required: There has to be at least one character in upper case in the password.');
INSERT INTO txt VALUES ('H5443', 'German',  'Kleinbuchstaben enthalten: Das Passwort muss mindestens einen Kleinbuchstaben enthalten.');
INSERT INTO txt VALUES ('H5443', 'English', 'Lower Case Required: There has to be at least one character in lower case in the password.');
INSERT INTO txt VALUES ('H5444', 'German',  'Ziffern enthalten: Das Passwort muss mindestens eine Ziffer enthalten.');
INSERT INTO txt VALUES ('H5444', 'English', 'Number Required: There has to be at least one number in the password.');
INSERT INTO txt VALUES ('H5445', 'German',  'Sonderzeichen enthalten: Das Passwort muss mindestens ein Sonderzeichen enthalten. M&ouml;gliche Werte: !?(){}=~$%&amp;#*-+.,_');
INSERT INTO txt VALUES ('H5445', 'English', 'Special Characters Required: There has to be at least one special character in the password. Possible values are: !?(){}=~$%&amp;#*-+.,_');
INSERT INTO txt VALUES ('H5451', 'German',  'Jeder Nutzer (ausser Demo-Nutzer) kann sein eigenes Passwort &auml;ndern.<br>
    Bitte das alte Passwort einmal und das neue Passwort zweimal eingeben, um Eingabefehler zu vermeiden.
    Das neue Passwort muss sich vom alten unterscheiden und wird gegen die <a href="/help/settings/passwordpolicy">Passworteinstellungen</a> gepr&uuml;ft.
');
INSERT INTO txt VALUES ('H5451', 'English', 'Every user (except demo user) can change his own password.<br>
    Please insert the old password once and the new password twice to avoid input mistakes.
    The new password has to be different from the old one and is checked against the <a href="/help/settings/passwordpolicy">Password Policy</a>.
');
INSERT INTO txt VALUES ('H5461', 'German',  'Jeder Nutzer kann seine eigene bevorzugte Sprache f&uuml;r die Anwendung einstellen.<br>
    Alle Texte werden in dieser Sprache dargestellt, soweit verf&uuml;gbar. Wenn nicht, wird die Standardsprache verwendet. Wenn der Text auch dort nicht verf&uuml;gbar ist, wird Englisch genutzt.
    Die Standardsprache beim ersten Anmelden kann vom Admin f&uuml;r alle Nutzer in den <a href="/help/settings/defaults">Standardeinstellungen</a> definiert werden.<br><br>
    Zur Zeit verf&uuml;gbar:
');
INSERT INTO txt VALUES ('H5461', 'English', 'Every user can set his own preferred language of the application.<br>
    All texts are displayed in this language if available. If not, the default language is used. If the text is not available there either, English is used.
    The default language at first login can be defined by the admin for all users in the <a href="/help/settings/defaults">Default Settings</a>.<br><br>
    Currently available:
');
INSERT INTO txt VALUES ('H5471', 'German',  'Jeder Nutzer kann einige pers&ouml;nliche Voreinstellungen f&uuml;r die Reporteinstellungen &uuml;berschreiben.
    Ausgangswert ist der vom Admin in den <a href="/help/settings/defaults">Standardeinstellungen</a> gesetzte Wert.
');
INSERT INTO txt VALUES ('H5471', 'English', 'Every user can overwrite some personal settings for the report creation.
    The default value is set by the admin in the <a href="/help/settings/defaults">Default Settings</a>.
');
INSERT INTO txt VALUES ('H5481', 'German',  'Ein Rezertifizierer kann einige pers&ouml;nliche Voreinstellungen f&uuml;r den Rezertifizierungsreport &uuml;berschreiben.
    Ausgangswert ist der vom Admin in den <a href="/help/settings/defaults">Standardeinstellungen</a> gesetzte Wert.
');
INSERT INTO txt VALUES ('H5481', 'English', 'A recertifier can overwrite some personal settings for the recertification report. 
    The default value is set by the admin in the <a href="/help/settings/defaults">Default Settings</a>.
');

INSERT INTO txt VALUES ('H6001', 'German',  'Firewall Orchestrator verf&uuml;gt &uuml;ber zwei APIs:
    <ul>
        <li>Die Haupt- (oder FWO) API, die den Zugriff auf die Firewall-Nutzdaten erlaubt.</li>
        <li>Die User Management API, mit deren Hilfe der die Firewall Orchestrator Nutzer ausgelesen oder ge&auml;ndert werden k&ouml;nnen.</li>
    </ul>
    Die FWO API ist eine <a href="/help/API/graphql">GraphQl</a> API, welche auf <a href="/help/API/hasura">Hasura</a> basiert. 
    Diese erlaubt es, flexibel den Zugang zu allen Daten der Datenbank und die Granularit&auml;t der zur&uuml;ckgegebenen Daten zu steuern.
    <br>
    <br>
    Die User Management API erm&ouml;glicht sowohl die Benutzer-Authentifizierung als auch das Anlegen von lokalen Nutzern sowie die Vergabe von Berechtigungen in Form von Rollen oder Tenant-Zugeh&ouml;rigkeit auf Nutzer- und Nutzergruppenebene.
    <br><br>
    Beim Testen der API-Zugriffe ohne g&uuml;ltiges Zertifikat kann der "--insecure" parameter bei den angegebenen curl Beispielen verwendet werden.
');
INSERT INTO txt VALUES ('H6001', 'English', 'Firewall Orchestrator features two APIs:
    <ul>
        <li>The main (or FWO) API which allows access to the firewall configuration data</li>
        <li>The User Management API which can be used to handle Firewall Orchestrator users</li>
    </ul>

    The FWO API is a <a href="/help/API/graphql">GraphQl</a> API which is based on <a href="/help/API/hasura">Hasura</a>. 
    This allows us to flexibly provide access to all data in the database and also define the level of granularity the data is returned in.<br>
    <br>
    The User Management API allows user authentication as well as user manipulation such as listing, adding, deleting, changing users and 
    their access permissions (roles and tenant memberships).
    <br><br>
    Note that when API testing without a valid certificate installed for your API, consider using the "--insecure" parameter for your curl test calls.
');
INSERT INTO txt VALUES ('H6101', 'German',  'GraphQL nutzt einen leicht anderen Ansatz als REST, indem es keine fixen Entry points zur API definiert.
    Stattdessen hat man die Freiheit, eine exakt auf die gew&uuml;nschte Detailtiefe angepasste Query zu nutzen. 
');
INSERT INTO txt VALUES ('H6101', 'English', 'GraphQL uses a slightly different approach as REST, not defining fixed entry points to the API.
    Instead you are free to use a custom query specifying exactly which level of detail you want to return each time.
');
INSERT INTO txt VALUES ('H6102', 'German',  'GraphQL bietet eine interaktive Web-Oberfl&auml;che, die genutzt werden kann, um Querys und Mutations zu erstellen und zu testen.<br>
    Sie kann unter folgendem Link erreicht werden: <code>https://"Name ihrer Firewall Orchestrator-Instanz":9443/api/</code>.
');
INSERT INTO txt VALUES ('H6102', 'English', 'GraphQL provides you with an interactive web user interface that can be used to construct and test queries as well as mutations.<br>
    It can be accesses via the following link: <code>https://"name of your firewall orchestrator instance":9443/api/</code>.
');
INSERT INTO txt VALUES ('H6103', 'German',  'Das Admin Kennwort kann auf dem API-Server in folgender Datei gefunden werden:');
INSERT INTO txt VALUES ('H6103', 'English', 'Note that the admin secret can be found on the API server in the following file:');
INSERT INTO txt VALUES ('H6201', 'German',  '<a href="https://hasura.io/" target="_blank">Hasura</a> stellt einen Link zur darunterliegenden PostgreSQL-Datenbank zur Verf&uuml;gung.<br>
    Es implementiert eine Zugriffskontrollschicht und k&ouml;nnte bei Bedarf auch einen REST API Zugang zur Verf&uuml;gung stellen.
');
INSERT INTO txt VALUES ('H6201', 'English', '<a href="https://hasura.io/" target="_blank">Hasura</a> provides the link to the underlying PostgreSQL database.<br>
    It implements the access control layer and could also provide a REST API interface if needed.
');
INSERT INTO txt VALUES ('H6301', 'German',  'Der Zugang zur API wird standardm&auml;ssig durch Nutzername/Passwort-Anmeldedaten kontrolliert, was zur Erzeugung eines JSON Web Token (JWT) f&uuml;hrt.
    Der JWT kann nur f&uuml;r eine begrenzte Zeit genutzt werden (Standard = 2 Stunden), um auf die dahinterliegende API zuzugreifen.
    Nach dieser Zeitperiode (unabh&auml;ngig von der Aktivit&auml;t) muss ein erneutes Anmelden erfolgen, da der JWT nicht l&auml;nger als g&uuml;ltig anerkannt wird.<br><br>
    Es k&ouml;nnen die gleichen Zugangsdaten wie f&uuml;r die Web-Oberfl&auml;che genutzt werden, und es gelten die gleichen Einschr&auml;nkungen und Sichten entsprechend dem rollenbasierten Zugriffsmodell.
');
INSERT INTO txt VALUES ('H6301', 'English', 'Login to the API is controlled by providing standard username password credentials which results in the generation of a JSON Web Token (JWT).
    The JWT can be used for a limited time (default = 2 hours) to access the API afterwards.
    After that time period (independant of activity) you need to login again, as the JWT is no longer considered valid.<br><br>
    You may use the same credentials you also use for accessing the web user interface and will have the same restrictions and views based on the role based access model.
');
INSERT INTO txt VALUES ('H6401', 'German',  'Mehr zur im Firewall Orchestrator eingesetzten API kann unter folgenden Seiten gefunden werden:');
INSERT INTO txt VALUES ('H6401', 'English', 'More resources around the API deployed in Firewall Orchestrator can be found at the following sites:');
INSERT INTO txt VALUES ('H6402', 'German',  '<li><a href="https://hasura.io/" target="_blank">Hasura Homepage</a></li>
    <li><a href="https://graphql.org/" target="_blank">GraphQL Homepage</a></li>
    <li><a href="https://www.howtographql.com/basics/1-graphql-is-the-better-rest/" target="_blank">Vergleich GraphQL vs. REST</a></li>
');
INSERT INTO txt VALUES ('H6402', 'English', '<li><a href="https://hasura.io/" target="_blank">Hasura home page</a></li>
    <li><a href="https://graphql.org/" target="_blank">GraphQL home page</a></li>
    <li><a href="https://www.howtographql.com/basics/1-graphql-is-the-better-rest/" target="_blank">Comparison GraphQL vs. REST</a></li>
');
INSERT INTO txt VALUES ('H6501', 'German',  'Der Middlewareserver liefert den JWT f&uuml;r die Authentifizierung gegen&uuml;ber der API.');
INSERT INTO txt VALUES ('H6501', 'English', 'The middleware server provides the JWT for authentication against the API.');
INSERT INTO txt VALUES ('H6601', 'German',  'Es gibt keine spezielle Abmeldefunktionalit&auml;t. Wenn der JWT ung&uuml;ltig wird, kann die API einfach nicht mehr damit genutzt werden.');
INSERT INTO txt VALUES ('H6601', 'English', 'There is no specific logout functionality. When the JWT becomes invalid, API simply can no longer be made with this JWT.');
INSERT INTO txt VALUES ('H6701', 'German',  '(Bitte ihren aktuellen JWT in der Query einsetzen. Der hier angegebene JWT ist nicht mehr g&uuml;ltig.)');
INSERT INTO txt VALUES ('H6701', 'English', '(Note that the query will not work as the sample JWT is not valid anymore. Please use a current JWT.)');
INSERT INTO txt VALUES ('H6702', 'German',  'Ergebnis auf einem System mit Beispieldaten:');
INSERT INTO txt VALUES ('H6702', 'English', 'Result on a system with demo data:');
INSERT INTO txt VALUES ('H6801', 'German',  'Folgende Mutation setzt die Sprache vom Nutzer mit der Id 1 auf Deutsch:');
INSERT INTO txt VALUES ('H6801', 'English', 'The following mutation sets the language of user with id 1 to German:');
INSERT INTO txt VALUES ('H6901', 'German',  'Anlegen eines Reporting Nutzers (&uuml;ber die Web-Oberfl&auml;che (z.B. reportscheduler) mit der Rolle "reporter_viewall")');
INSERT INTO txt VALUES ('H6901', 'English', 'Create reporting user via web interface (e.g. reportscheduler) assigning the rule "reporter_viewall"');
INSERT INTO txt VALUES ('H6902', 'German',  'Erstellen der zeitgesteuerten Report-Generierung (report_schedule_id wird zur&uuml;ckgeliefert)');
INSERT INTO txt VALUES ('H6902', 'English', 'Create a (recurring) report, noting the returned report_schedule_id');
INSERT INTO txt VALUES ('H6903', 'German',  'Herunterladen des generierten Reports');
INSERT INTO txt VALUES ('H6903', 'English', 'Download the generated report');
INSERT INTO txt VALUES ('H6904', 'German',  'Die im folgenden beschriebenen Schritte k&ouml;nnen alle auch via Web-Oberfl&auml;che durchgef&uuml;hrt werden. Hier ist jeweils der Weg mittels API-Call angegeben.');
INSERT INTO txt VALUES ('H6904', 'English', 'The following steps can both be executed via API as well as via web UI. Here we list the API-only way.');
INSERT INTO txt VALUES ('H6905', 'German',  'Beschaffung der notwendigen ID-Informationen (User-ID, Report Template ID)');
INSERT INTO txt VALUES ('H6905', 'English', 'Information gathering (User-ID, Report Template ID)');
INSERT INTO txt VALUES ('H6906', 'German',  'Anmelden zur Generierung eines g&uuml;ltigen JWT f&uuml;r die folgenden Schritte');
INSERT INTO txt VALUES ('H6906', 'English', 'Login to get a JWT for the steps further below');
INSERT INTO txt VALUES ('H6907', 'German',  'Auflisten bereits vorhandener Reports im Archiv (hier der letzte generierte zum Schedule)');
INSERT INTO txt VALUES ('H6907', 'English', 'List generated reports in archive (here we get the last one generated for the respective schedule)');

INSERT INTO txt VALUES ('H7001', 'German', 'Im diesem Reiter werden die Monitoringwerkzeuge zur Verf&uuml;gung gestellt.
    Die meisten Abschnitte k&ouml;nnen nur von Nutzern mit den verschiedenen Administrator-Rollen gesehen und genutzt werden.
    Der Auditor kann zwar die Einstellungen sehen, da er aber keine Schreibrechte hat, sind alle Schaltfl&auml;chen, die zu &Auml;nderungen f&uuml;hren w&uuml;rden, deaktiviert.
');
INSERT INTO txt VALUES ('H7001', 'English', 'In this tab monitoring tools are provided.
    Most sections can only be seen and used by users with the different administrator roles.
    The auditor is able to see the monitoring tools, but as he has no write permissions all buttons leading to changes are disabled.
');
INSERT INTO txt VALUES ('H7011', 'German', 'Im ersten Kapitel "Alarme" werden alle Ereignisse behandelt, die eine &Uuml;berpr&uuml;fung oder ein Eingreifen eines Administrators erfordern: 
    Der Abschnitt <a href="/help/monitoring/open_alerts">Offene Alarme</a> dient als &Uuml;bersicht &uuml;ber alle noch zu behandelnden Ereignisse, w&auml;hrend in <a href="/help/monitoring/all_alerts">Alle Alarme</a> 
    auch die bereits bearbeiteten Alarme mitsamt dem jeweiligen Bearbeiter eingesehen werden k&ouml;nnen.
');
INSERT INTO txt VALUES ('H7011', 'English', 'In the first chapter "Alerts" all events that need a check or intervention by an administrator are handled:
    The section <a href="/help/monitoring/open_alerts">Open Alerts</a> gives a dashboard for all events to be handled, whereas in <a href="/help/monitoring/all_alerts">All Alerts</a>
    also the already handled alerts and the respective acknowledger can be viewed.
');
INSERT INTO txt VALUES ('H7012', 'German', 'Das Kapitel "Hintergrund-Checks" zeigt die Ausgaben der regelm&auml;ssigen automatischen Pr&uuml;fungen:
    In <a href="/help/monitoring/autodiscovery">Autodiscovery</a> wird die Konfiguration der Managements und Gateways mit dem aktuellen Stand im Quellsystem abgeglichen.
    Die <a href="/help/monitoring/daily_checks">T&auml;glichen Checks</a> &uuml;berpr&uuml;fen sonstige Systemzust&auml;nde, insbesondere den Import-Status der einzelnen Managements.
');
INSERT INTO txt VALUES ('H7012', 'English', 'The chapter "Background Checks" displays the outcome of regular automatic checks:
    In <a href="/help/monitoring/autodiscovery">Autodiscovery</a> the management and gateway configuration is compared to the actual state in the source system.
    <a href="/help/monitoring/daily_checks">Daily Checks</a> inspect other system conditions, especially the import status of the different managements.
');
INSERT INTO txt VALUES ('H7013', 'German', 'Im Kapitel "Import" wird der Datenimport &uuml;berwacht:
    <a href="/help/monitoring/import_status">Import-Status</a> erlaubt einen &Uuml;berblick &uuml;ber einige Parameter der verschiedenen importierenden systeme,
    w&auml;hrend <a href="/help/monitoring/import_logs">Import-Logs</a> die wichtigen Ausgaben der Datenimporte festh&auml;lt.
');
INSERT INTO txt VALUES ('H7013', 'English', 'In the "Import" chapter the data import is monitored: 
    <a href="/help/monitoring/import_status">Import Status</a> allows a view on several parameters of the different importing systems, 
    whereas <a href="/help/monitoring/import_logs">Import Logs</a> records noteworthy outcomes of the data imports.
');
INSERT INTO txt VALUES ('H7014', 'German', 'Das Kapitel "Pers&ouml;nlich" ist f&uuml;r alle Nutzer zug&auml;nglich. 
    Unter <a href="/help/monitoring/ui_messages">UI-Nachrichten</a> werden alle Fehler- und Erfolgsmeldungen des jeweiligen Nutzers festgehalten.
');
INSERT INTO txt VALUES ('H7014', 'English', 'The "Personal" chapter is accessible by all users.
    <a href="/help/monitoring/ui_messages">Ui Messages</a> records all error and success messages of the actual user.
');
INSERT INTO txt VALUES ('H7101', 'German', 'Verschiedene Komponenten des Firewall Orchestrator k&ouml;nnen Alarme ausl&ouml;sen, wenn eine &Uuml;berpr&uuml;fung oder ein Eingreifen durch einen Administrator erforderich ist.
    Je nach Alarmtyp werden unter "Details" weitere Informationen oder Handlungsoptionen angeboten. Durch Auswahl der "Best&auml;tigen"-Schaltfl&auml;che verschwindet der Alarm aus der &Uuml;bersicht, der Best&auml;tigende wird mit Zeitstempel im Alarm protokolliert.
    Der Alarm kann dann weiterhin unter "Alle Alarme" eingesehen werden, die Details sind dann aber nicht mehr verf&uuml;gbar. Wird ein Alarm wiederholt ausgel&ouml;st (z.B. bei der <a href="/help/monitoring/autodiscovery">Autodiscovery</a> 
    oder beim <a href="/help/monitoring/daily_checks">T&auml;glichen Check</a>), so wird der bereits existierende, soweit zuzuordnen, automatisch best&auml;tigt.
');
INSERT INTO txt VALUES ('H7101', 'English', 'Several components of Firewall Orchestrator can raise alerts, if a check or intervention by an administrator is necessary.
    Depending on the alert type "Details" offers further information or options for actions. By using the "Acknowledge"-button the alert is removed from the open alerts list, the acknowledger is protocolled with timestamp in the alert.
    The alert can still be viewed in "All Alerts", but the details are not available anymore. If an alert is raised again (e.g. from <a href="/help/monitoring/autodiscovery">Autodiscovery</a>
    or <a href="/help/monitoring/daily_checks">Daily Check</a>), the already existing alert is acknowledged automatically as far as possible.
');
INSERT INTO txt VALUES ('H7102', 'German', 'Wenn vom <a href="/help/monitoring/daily_checks">T&auml;glichen Check</a> Beispieldaten gefunden werden, gibt es hier die M&ouml;glichkeit, sie in einem Schritt komplett zu l&ouml;schen.
    Besteht Unsicherheit, ob noch einzelne Daten ben&ouml;tigt werden, k&ouml;nnen diese auch in den einzelnen Rubriken <a href="/help/settings/managements">Managements</a>, <a href="/help/settings/tenants">Mandanten</a>,
    <a href="/help/settings/users">Nutzer</a> und <a href="/help/settings/groups">Gruppen</a> getrennt gel&ouml;scht werden.
');
INSERT INTO txt VALUES ('H7102', 'English', 'If sample data is found by the <a href="/help/monitoring/daily_checks">Daily Check</a>, here the user has the option to delete all sample data in one step.
    If there is uncertainity about data still to be needed, they can also be handled separately in the different settings chapters <a href="/help/settings/managements">Managements</a>, <a href="/help/settings/tenants">Tenants</a>,
    <a href="/help/settings/users">Users</a>, and <a href="/help/settings/groups">Groups</a>.
');
INSERT INTO txt VALUES ('H7103', 'German', 'Alle auf der UI auftretenden Systemfehler (aber keine Benutzerfehler) werden als Alarm protokolliert.');
INSERT INTO txt VALUES ('H7103', 'English', 'All system errors (but not user errors) occuring on the UI are recorded as alerts.');
INSERT INTO txt VALUES ('H7104', 'German', 'Werden beim <a href="/help/monitoring/daily_checks">T&auml;glichen Check</a> beim Import einzelner Managements Unregelm&auml;ssigkeiten festgestellt (langlaufende, &uuml;berf&auml;llige oder ganz ausgebliebene Importe), 
    wird im Alarm eine detailliertere &Uuml;bersicht &uuml;ber den Import-Status bzw. die M&ouml;glichkeit des Rollbacks (im Falle eines langlaufenden Imports) des jeweiligen Managements angeboten.
');
INSERT INTO txt VALUES ('H7104', 'English', 'When the <a href="/help/monitoring/daily_checks">Daily Check</a> finds irregularities in the import of a management (long-running, overdue or completely missing imports),
    a detailled overview of the import status, resp. an option to rollback the import (in case of a long-running import) is offered in the alert.
');
INSERT INTO txt VALUES ('H7105', 'German', 'Wenn der automatische Lauf der <a href="/help/monitoring/autodiscovery">Autodiscovery</a> &Auml;nderungen in der Device-Konfiguration feststellt (hinzugekommene oder verschwundene Ger&auml;te),
    wird f&uuml;r jede einzelne &Auml;nderung ein Alarm ausgel&ouml;st. Unter "Details" wird dann die jeweilige Aktion zur Anpassung im Firewall Orchestrator zur Ausf&uuml;hrung angeboten. 
    Dabei ist zu ber&uuml;cksichtigen, dass Managements zuerst angelegt werden m&uuml;ssen, bevor Gateways zugeordnet werden k&ouml;nnen, 
    bzw. dass Gateways gel&ouml;scht oder deaktiviert sein m&uuml;ssen bevor die entsprechende Aktion mit dem &uuml;bergeordneten Management erfolgen kann.
    Deshalb k&ouml;nnen vorgeschlagene Aktionen deaktiviert sein, dann bitte zuerst die vorausgesetzten Aktionen durchf&uuml;hren. 
    Beim Anlegen eines Managements oder Gateways wird automatisch gepr&uuml;ft, ob dieses schon vorhanden ist, und dann nur reaktiviert zu werden braucht.
    Bei nicht mehr vorhandenen Managements oder Gateways werden die Alternativen Deaktivieren oder vollst&auml;ndiges L&ouml;schen angeboten (bei letzterem werden auch alle importierten Daten entfernt!).
');
INSERT INTO txt VALUES ('H7105', 'English', 'Whenever an <a href="/help/monitoring/autodiscovery">Autodiscovery</a> background job finds changes in the device configuration (newly added or removed devices),
    an alert is raised for each single change. In "Details" the respective action to adapt Firewall Orchestrator configuration is offered.
    It has to be taken into account, that managements have to be created before gateways can be assigned to them,
    resp. that gateways have to be deleted or deactivated before the resp. action can be performed with the parent management.
    That is why the proposed actions may be deactivated, then perform the presumed actions first.
    When creating a new management or gateway there is an automatic check, if it already exists and only needs to be reactivated.
    For removed managements or gateways the alternatives "deactivation" or "complete deletion" are offered (the latter also removes all imported data!).
');
INSERT INTO txt VALUES ('H7151', 'German', 'Hier sind alle jemals ausgel&ouml;sten Alarme mit Zeitstempel und Information zur Best&auml;tigung protokolliert. 
    Die jeweiligen Details sind aber nicht mehr verf&uuml;gbar, um das Ausf&uuml;hren nicht mehr aktueller Aktionen zu vermeiden.
');
INSERT INTO txt VALUES ('H7151', 'English', 'All alerts ever raised are recorded here with timestamp and information about the acknowledgement.
    The respective details are not available any more to avoid re-execution of out-dated actions.
');
INSERT INTO txt VALUES ('H7152', 'German', 'Quelle, Code und Management-Id sind wesentlich zur Identifizierung wiederkehrender Alarme.');
INSERT INTO txt VALUES ('H7152', 'English', 'Source, Code and Management Id are relevant to identify recurring alerts.');
INSERT INTO txt VALUES ('H7153', 'German', 'Der Best&auml;tigende wird mit der Zeit der Best&auml;tigung protokolliert. 
    Ist dort "System" eingetragen, weist das auf die automatische Best&auml;tigung bei wiederkehrenden Alarmen hin.
');
INSERT INTO txt VALUES ('H7153', 'English', 'The acknowledger is recorded together with the time of acknowledgement.
    "System" as acknowledger indicates the automatic Acknowledgement at recurring alerts.
');
INSERT INTO txt VALUES ('H7201', 'German', 'Hier werden die Ergebnisse der automatischen und der manuell angestossenen Autodiscovery protokolliert.
    Zu jedem Lauf werden die Anzahl der gefundenen &Auml;nderungen bzw. die Fehlermeldungen dargestellt.
    Angegebene Management-Ids und -Namen beziehen sich auf den jeweiligen Multi Domain Manager.
    Die gegebenenfalls erforderlichen Anpassungen der Konfiguration im Firewall Orchestrator werden &uuml;ber die <a href="/help/monitoring/open_alerts">Offenen Alarme</a> (bei der automatischen Autodiscovery)
    oder den Dialog in den <a href="/help/settings/managements">Management-Einstellungen</a> (bei der manuell angestossenen Autodiscovery) abgewickelt.
    Startzeit und Zeitabstand der automatischen Autodiscovery-L&auml;ufe k&ouml;nnen (vom Administrator) in den <a href="/help/settings/defaults">Standardeinstellungen</a> festgelegt werden.
');
INSERT INTO txt VALUES ('H7201', 'English', 'Results of the automatic and the manual Autodiscovery are recorded here.
    For each run, number of found changes resp. error messages are displayed.
    Management Ids and Names refer to the respective Multi Domain Manager.
    The required changes in the configuration of the Firewall Orchestrator are handled in <a href="/help/monitoring/open_alerts">Open Alerts</a> (for the automatic Autodiscovery)
    or in the Dialogue of the <a href="/help/settings/managements">Management settings</a> (in case of the manually initialized Autodiscovery).
    Start time and time interval of the automatic Autodiscovery can be defined in the <a href="/help/settings/defaults">Default settings</a> (by the administrator).
');
INSERT INTO txt VALUES ('H7251', 'German', 'In den T&auml;glichen Checks werden allgemeine Parameter des Firewall Orchestrator-Systems gepr&uuml;ft.
    Der t&auml;gliche Startzeitpunkt kann (vom Administrator) in den <a href="/help/settings/defaults">Standardeinstellungen</a> festgelegt werden.
');
INSERT INTO txt VALUES ('H7251', 'English', 'In the daily checks general parameters of the Firewall Orchestrator system are checked.
    The daily start time can be defined in the <a href="/help/settings/defaults">Default settings</a> (by the administrator).
');
INSERT INTO txt VALUES ('H7252', 'German', 'Beispieldaten (erkennbar an den Endungen auf "_demo") sollten nur in einer initialen Kennenlernphase des Firewall Orchestrator-Systems genutzt werden.
    In Produktivumgebung sollten sie nicht mehr vorkommen. Deshalb wird das System darauf gepr&uuml;ft und gegebenenfalls ein Alarm ausgel&ouml;st. Im Protokoll wird aufgef&uuml;hrt, in welchen Datenbereichen 
    (Managements, Mandanten, Nutzer oder Gruppen) Beispieldaten gefunden wurden.
');
INSERT INTO txt VALUES ('H7252', 'English', 'Sample data (defined by the ending "_demo") should only be used in an initial learning phase of the Firewall Orchestrator system.
    In productive environments they should not be present. Therefore the system is checked and if necessary an alert is raised. The record contains information about the data domain
    (managements, tenants, users or groups), where sample data were found.
');
INSERT INTO txt VALUES ('H7253', 'German', 'Die Ergebnisse der Pr&uuml;fung des Import-Status der aktiven Managements sind hier protokolliert. Werden Anomalien wie &uuml;berlange Import-Zeiten oder fehlende Imports festgestellt,
    werden einzelne Alarme ausgel&ouml;st, die unter <a href="/help/monitoring/open_alerts">Offenen Alarme</a> analysiert und behandelt werden k&ouml;nnen. Hier wird lediglich die Anzahl der gefundenen Probleme protokolliert.
');
INSERT INTO txt VALUES ('H7253', 'English', 'Results of the Import status checks of the active managements are recorded here. If anomalies as overdue or missing imports are found,
    separate alerts are raised, which can be analysed and handled at <a href="/help/monitoring/open_alerts">Open Alerts</a>.
');
INSERT INTO txt VALUES ('H7301', 'German', 'Hier werden die Ausgaben der verschiedenen Importe protokolliert.
');
INSERT INTO txt VALUES ('H7301', 'English', 'Here the output of the different imports are documented.
');
INSERT INTO txt VALUES ('H7302', 'German', 'Schwere
');
INSERT INTO txt VALUES ('H7302', 'English', 'Severity
');
INSERT INTO txt VALUES ('H7303', 'German', 'Beschreibung
');
INSERT INTO txt VALUES ('H7303', 'English', 'Description
');
INSERT INTO txt VALUES ('H7304', 'German', 'ImportId
');
INSERT INTO txt VALUES ('H7304', 'English', 'ImportId
');
INSERT INTO txt VALUES ('H7305', 'German', 'Vermutliche Ursache
');
INSERT INTO txt VALUES ('H7305', 'English', 'Suspected Cause
');
INSERT INTO txt VALUES ('H7306', 'German', 'Management
');
INSERT INTO txt VALUES ('H7306', 'English', 'Management
');
INSERT INTO txt VALUES ('H7307', 'German', 'Gateway
');
INSERT INTO txt VALUES ('H7307', 'English', 'Gateway
');
INSERT INTO txt VALUES ('H7308', 'German', 'Regel
');
INSERT INTO txt VALUES ('H7308', 'English', 'Rule
');
INSERT INTO txt VALUES ('H7309', 'German', 'Objekt
');
INSERT INTO txt VALUES ('H7309', 'English', 'Object
');
INSERT INTO txt VALUES ('H7401', 'German', 'Hier werden alle Nachrichten, die als Erfolgs- oder Fehlermeldungen beim jeweiligen Nutzer erschienen sind, aufgelistet.
    Die Meldungen k&ouml;nnen nur vom Nutzer selbst eingesehen werden, mit Ausnahme der Systemfehler, die als Alarm bei den Administratoren gemeldet werden.
');
INSERT INTO txt VALUES ('H7401', 'English', 'All messages are listed here, which have been displayed for the respective user.
    The messages can be seen only by the user itself, except system errors which have raised an alert to be handled by the administrators.
');
