
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

-- enum values
INSERT INTO txt VALUES ('master', 			    'German', 	'Master');
INSERT INTO txt VALUES ('master', 			    'English', 	'Master');
INSERT INTO txt VALUES ('access', 			    'German', 	'Zugriff');
INSERT INTO txt VALUES ('access', 			    'English', 	'Access');
INSERT INTO txt VALUES ('generic',              'German', 	'Generisch');
INSERT INTO txt VALUES ('generic',              'English', 	'Generic');
INSERT INTO txt VALUES ('rule_modify',          'German',   'Regel &auml;ndern');
INSERT INTO txt VALUES ('rule_modify',          'English',  'Modify Rule');
INSERT INTO txt VALUES ('rule_delete',          'German',   'Regel l&ouml;schen');
INSERT INTO txt VALUES ('rule_delete',          'English',  'Delete Rule');
INSERT INTO txt VALUES ('group_create',         'German',   'Gruppe anlegen');
INSERT INTO txt VALUES ('group_create',         'English',  'Create Group');
INSERT INTO txt VALUES ('group_modify',         'German',   'Gruppe &auml;ndern');
INSERT INTO txt VALUES ('group_modify',         'English',  'Modify Group');
INSERT INTO txt VALUES ('group_delete',         'German',   'Gruppe l&ouml;schen');
INSERT INTO txt VALUES ('group_delete',         'English',  'Delete Group');
INSERT INTO txt VALUES ('None',			        'German', 	'Keine(r/s)');
INSERT INTO txt VALUES ('None',			        'English', 	'None');
INSERT INTO txt VALUES ('OnSet',			    'German', 	'Beim Erreichen');
INSERT INTO txt VALUES ('OnSet',			    'English', 	'On set');
INSERT INTO txt VALUES ('OnLeave',			    'German', 	'Beim Verlassen');
INSERT INTO txt VALUES ('OnLeave',			    'English', 	'On leave');
INSERT INTO txt VALUES ('OfferButton',			'German', 	'Schaltfl&auml;che anbieten');
INSERT INTO txt VALUES ('OfferButton',			'English', 	'Offer button');
INSERT INTO txt VALUES ('DoNothing',			'German', 	'Keine Aktion');
INSERT INTO txt VALUES ('DoNothing',			'English', 	'Do Nothing');
INSERT INTO txt VALUES ('AutoPromote',			'German', 	'Autom. Weiterleitung');
INSERT INTO txt VALUES ('AutoPromote',			'English', 	'Auto-forward');
INSERT INTO txt VALUES ('AddApproval',			'German', 	'Genehmigung hinzuf&uuml;gen');
INSERT INTO txt VALUES ('AddApproval',			'English', 	'Add approval');
INSERT INTO txt VALUES ('SetAlert',			    'German', 	'Alarm ausl&ouml;sen');
INSERT INTO txt VALUES ('SetAlert',			    'English', 	'Set alert');
INSERT INTO txt VALUES ('TrafficPathAnalysis',  'German', 	'Pfadanalyse');
INSERT INTO txt VALUES ('TrafficPathAnalysis',  'English', 	'Path Analysis');
INSERT INTO txt VALUES ('ExternalCall',			'German', 	'Externer Aufruf');
INSERT INTO txt VALUES ('ExternalCall',			'English', 	'External call');
INSERT INTO txt VALUES ('Ticket',			    'German', 	'Ticket');
INSERT INTO txt VALUES ('Ticket',			    'English', 	'Ticket');
INSERT INTO txt VALUES ('RequestTask',			'German', 	'fachlicher Auftrag');
INSERT INTO txt VALUES ('RequestTask',			'English', 	'Request Task');
INSERT INTO txt VALUES ('ImplementationTask',	'German', 	'Implementierungs-Auftrag');
INSERT INTO txt VALUES ('ImplementationTask',	'English', 	'Implementation Task');
INSERT INTO txt VALUES ('Approval',			    'German', 	'Genehmigung');
INSERT INTO txt VALUES ('Approval',			    'English', 	'Approval');
INSERT INTO txt VALUES ('never', 			    'German', 	'Niemals');
INSERT INTO txt VALUES ('never', 			    'English', 	'Never');
INSERT INTO txt VALUES ('onlyForOneDevice', 	'German', 	'Nur eines wenn Ger&auml;t vorhanden');
INSERT INTO txt VALUES ('onlyForOneDevice', 	'English', 	'Only one if device available');
INSERT INTO txt VALUES ('forEachDevice', 		'German', 	'F&uuml;r jedes Ger&auml;t');
INSERT INTO txt VALUES ('forEachDevice', 		'English', 	'For each device');
INSERT INTO txt VALUES ('enterInReqTask',       'German', 	'Ger&auml;t im Antrag eingeben');
INSERT INTO txt VALUES ('enterInReqTask',       'English', 	'Enter device in request');
INSERT INTO txt VALUES ('afterPathAnalysis',    'German', 	'Nach Pfadanalyse');
INSERT INTO txt VALUES ('afterPathAnalysis',    'English', 	'After path analysis');
INSERT INTO txt VALUES ('WriteToDeviceList',    'German', 	'In Ger&auml;teliste eintragen');
INSERT INTO txt VALUES ('WriteToDeviceList',    'English', 	'Write to device list');
INSERT INTO txt VALUES ('DisplayFoundDevices',  'German', 	'Gefundene Ger&auml;te darstellen');
INSERT INTO txt VALUES ('DisplayFoundDevices',  'English', 	'Display found devices');
INSERT INTO txt VALUES ('Sunday',               'German', 	'Sonntag');
INSERT INTO txt VALUES ('Sunday',               'English', 	'Sunday');
INSERT INTO txt VALUES ('Monday',               'German', 	'Montag');
INSERT INTO txt VALUES ('Monday',               'English', 	'Monday');
INSERT INTO txt VALUES ('Tuesday',              'German', 	'Dienstag');
INSERT INTO txt VALUES ('Tuesday',              'English', 	'Tuesday');
INSERT INTO txt VALUES ('Wednesday',            'German', 	'Mittwoch');
INSERT INTO txt VALUES ('Wednesday',            'English', 	'Wednesday');
INSERT INTO txt VALUES ('Thursday',             'German', 	'Donnerstag');
INSERT INTO txt VALUES ('Thursday',             'English', 	'Thursday');
INSERT INTO txt VALUES ('Friday',               'German', 	'Freitag');
INSERT INTO txt VALUES ('Friday',               'English', 	'Friday');
INSERT INTO txt VALUES ('Saturday',             'German', 	'Samstag');
INSERT INTO txt VALUES ('Saturday',             'English', 	'Saturday');
INSERT INTO txt VALUES ('Rules', 			    'German', 	'Regel-Report');
INSERT INTO txt VALUES ('Rules', 			    'English', 	'Rules Report');
INSERT INTO txt VALUES ('Changes', 			    'German', 	'Changes-Report');
INSERT INTO txt VALUES ('Changes', 			    'English', 	'Changes Report');
INSERT INTO txt VALUES ('Statistics', 			'German', 	'Statistik-Report');
INSERT INTO txt VALUES ('Statistics', 			'English', 	'Statistics Report');
INSERT INTO txt VALUES ('NatRules', 			'German', 	'NAT-Regel-Report');
INSERT INTO txt VALUES ('NatRules', 			'English', 	'NAT Rules Report');
INSERT INTO txt VALUES ('ResolvedRules', 	    'German', 	'Regel-Report (aufgel&ouml;st)');
INSERT INTO txt VALUES ('ResolvedRules', 		'English', 	'Rules Report (resolved)');
INSERT INTO txt VALUES ('ResolvedRulesTech', 	'German', 	'Regel-Report (technisch)');
INSERT INTO txt VALUES ('ResolvedRulesTech', 	'English', 	'Rules Report (technical)');
INSERT INTO txt VALUES ('Recertification', 	    'German', 	'Rezertifizierungs-Report');
INSERT INTO txt VALUES ('Recertification', 		'English', 	'Recertification Report');
INSERT INTO txt VALUES ('ResolvedChanges', 		'German', 	'Changes-Report (aufgel&ouml;st)');
INSERT INTO txt VALUES ('ResolvedChanges', 		'English', 	'Changes Report (resolved)');
INSERT INTO txt VALUES ('ResolvedChangesTech', 	'German', 	'Changes-Report (technisch)');
INSERT INTO txt VALUES ('ResolvedChangesTech', 	'English', 	'Changes Report (technical)');
INSERT INTO txt VALUES ('UnusedRules', 	        'German', 	'Unbenutzte-Regel-Report');
INSERT INTO txt VALUES ('UnusedRules', 	        'English', 	'Unused Rules Report');
INSERT INTO txt VALUES ('mixed', 	            'German', 	'Gemischt');
INSERT INTO txt VALUES ('mixed', 	            'English', 	'Mixed');
INSERT INTO txt VALUES ('exclusive', 	        'German', 	'Exklusiv');
INSERT INTO txt VALUES ('exclusive', 	        'English', 	'Exclusive');
INSERT INTO txt VALUES ('AppRole', 	            'German', 	'App Rolle');
INSERT INTO txt VALUES ('AppRole', 	            'English', 	'App Role');
INSERT INTO txt VALUES ('AppZone', 	            'German', 	'Anwendungszone');
INSERT INTO txt VALUES ('AppZone', 	            'English', 	'App Zone');
INSERT INTO txt VALUES ('NetworkZone', 	        'German', 	'Netzwerkzone');
INSERT INTO txt VALUES ('NetworkZone', 	        'English', 	'Network Zone');
INSERT INTO txt VALUES ('NetworkArea', 	        'German', 	'Netzwerkarea');
INSERT INTO txt VALUES ('NetworkArea', 	        'English', 	'Network Area');
INSERT INTO txt VALUES ('Connection', 	        'German', 	'Verbindung');
INSERT INTO txt VALUES ('Connection', 	        'English', 	'Connection');
INSERT INTO txt VALUES ('AppServer', 	        'German', 	'App Server');
INSERT INTO txt VALUES ('AppServer', 	        'English', 	'App Server');
INSERT INTO txt VALUES ('ServiceGroup', 	    'German', 	'Dienstgruppe');
INSERT INTO txt VALUES ('ServiceGroup', 	    'English', 	'Service Group');
INSERT INTO txt VALUES ('Service', 	            'German', 	'Dienst');
INSERT INTO txt VALUES ('Service', 	            'English', 	'Service');
INSERT INTO txt VALUES ('Insert', 	            'German', 	'Einf&uuml;gen');
INSERT INTO txt VALUES ('Insert', 	            'English', 	'Insert');
INSERT INTO txt VALUES ('Update', 	            'German', 	'&Auml;ndern');
INSERT INTO txt VALUES ('Update', 	            'English', 	'Update');
INSERT INTO txt VALUES ('Delete', 	            'German', 	'L&ouml;schen');
INSERT INTO txt VALUES ('Delete', 	            'English', 	'Delete');
INSERT INTO txt VALUES ('Assign', 	            'German', 	'Zuweisen');
INSERT INTO txt VALUES ('Assign', 	            'English', 	'Assign');
INSERT INTO txt VALUES ('Unassign', 	        'German', 	'Zuweisung aufheben');
INSERT INTO txt VALUES ('Unassign', 	        'English', 	'Unassign');
INSERT INTO txt VALUES ('MarkDeleted', 	        'German', 	'Als gel&ouml;scht markieren');
INSERT INTO txt VALUES ('MarkDeleted', 	        'English', 	'Mark Deleted');
INSERT INTO txt VALUES ('Reactivate', 	        'German', 	'Reaktivieren');
INSERT INTO txt VALUES ('Reactivate', 	        'English', 	'Reactivate');
INSERT INTO txt VALUES ('SimpleText', 	        'German', 	'Einfacher Text (kein &Auml;nderungsreport)');
INSERT INTO txt VALUES ('SimpleText', 	        'English', 	'Simple Text (no Change Report)');
INSERT INTO txt VALUES ('HtmlInBody', 	        'German', 	'Html in Email');
INSERT INTO txt VALUES ('HtmlInBody', 	        'English', 	'Html in email body');
INSERT INTO txt VALUES ('PdfAsAttachment', 	    'German', 	'Pdf als Anhang');
INSERT INTO txt VALUES ('PdfAsAttachment', 	    'English', 	'Pdf as Attachment');
INSERT INTO txt VALUES ('HtmlAsAttachment', 	'German', 	'Html als Anhang');
INSERT INTO txt VALUES ('HtmlAsAttachment', 	'English', 	'Html as Attachment');
INSERT INTO txt VALUES ('CsvAsAttachment', 	    'German', 	'Csv als Anhang');
INSERT INTO txt VALUES ('CsvAsAttachment', 	    'English', 	'Csv as Attachment');
INSERT INTO txt VALUES ('JsonAsAttachment', 	'German', 	'Json als Anhang');
INSERT INTO txt VALUES ('JsonAsAttachment', 	'English', 	'Json as Attachment');

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
INSERT INTO txt VALUES ('commit_changes', 		'German',	'&Auml;nderungen &uuml;bernehmen');
INSERT INTO txt VALUES ('commit_changes', 	    'English',	'Commit changes');
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
INSERT INTO txt VALUES ('jwt_expiry_title',     'German', 	'JWT l&auml;uft bald ab');
INSERT INTO txt VALUES ('jwt_expiry_title',     'English', 	'JWT about to expire');
INSERT INTO txt VALUES ('jwt_expiry_text',		'German', 	'Ihr Jwt (Session Token) ist kurz davor abzulaufen. Bitte geben Sie ihr Passwort ein, um einen neuen Jwt zu erzeugen.');
INSERT INTO txt VALUES ('jwt_expiry_text',		'English', 	'Your jwt (session token) is about to expire. Please enter your password to generate a new jwt.');
INSERT INTO txt VALUES ('jwt_expired_title',     'German', 	'JWT abgelaufen');
INSERT INTO txt VALUES ('jwt_expired_title',     'English', 'JWT expired');
INSERT INTO txt VALUES ('jwt_expired_text',		'German', 	'Ihr Jwt (Session Token) ist abgelaufen, wodurch es zu einem Fehler kam. Bitte geben Sie ihr Passwort ein, um einen neuen Jwt zu erzeugen.');
INSERT INTO txt VALUES ('jwt_expired_text',		'English', 	'Your jwt (session token) is expired. This lead to an error. Please enter your password to create a new jwt.');
INSERT INTO txt VALUES ('permissions_title',	'German', 	'Berechtigungen');
INSERT INTO txt VALUES ('permissions_title',	'English', 	'Permissions');
INSERT INTO txt VALUES ('permissions_text',		'German', 	'Ihre Berechtigungen wurden ge&auml;ndert. Bitte geben Sie Ihr Passwort ein, um Ihre Berechtigungen zu aktualisieren!');
INSERT INTO txt VALUES ('permissions_text',		'English', 	'Your permissions have been changed. Re-login to update your permissions.');
INSERT INTO txt VALUES ('login_importer_error',	'German', 	'Nutzer mit der Rolle "Importer" d&uuml;rfen sich nicht an der Benutzeroberfl&auml;che anmelden. Diese Rolle dient einzig dem Importieren von eingebundenen Ger&auml;ten.');
INSERT INTO txt VALUES ('login_importer_error',	'English', 	'Users with role "importer" are not allowed to log into the user interface. The only purpose of this role is to import included devices.');

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
    <li>Modellierung von Soll-Kommunikationsprofilen z.B. nach Anwendung getrennt (Know Your Application)</li>
    <li>Web-Oberfl&auml;che: Compliance Matrix Modul</li>
    <li>Web-Oberfl&auml;che: Report f&uuml;r ungenutzte Regeln mit L&ouml;sch-Ticket-Integration</li>
    <li>Reporting: Email-Benachrichtigung bei sicherheitsrelevanten &Auml;nderungen inklusive &Auml;nderungsreport</li>
    <li>Importer Check Point: Grundlegender Inline-Layer-Support</li>
    <li>Importer Modul f&uuml;r Stand-Alone FortiGate Import via REST API</li>
    <li>Firewall-Regel-Rezertifizierungsmodul: beseitigen Sie ihre Altlasten und erf&uuml;llen Sie aktuelle regulatorische Anforderungen.</li>
    <li>Details: siehe <a target="_blank" href="https://github.com/CactuseSecurity/firewall-orchestrator/tree/main/documentation/revision-history-main.md">
        https://github.com/CactuseSecurity/firewall-orchestrator/tree/main/documentation/revision-history-main.md</li>
</ul>
');
INSERT INTO txt VALUES ('whats_new_facts',	    'English', 	'
<ul>
    <li>Modelling module for defining target communication profiles for segregated networks (e.g. per application: Know Your Application)</li>
    <li>Web User Interface: Compliance Matrix Modul</li>
    <li>Web User Interface: Report for unused rules directly integration delete rule workflow</li>
    <li>Reporting: Notification on detecting security relevant changes of firewall rules including change report in emails</li>
    <li>Importer Check Point: basic inline layer support</li>
    <li>Importer: Module for stand-alone FortiGate import via REST API</li>
    <li>Rule recertification module: remove unnecessary rules and meet current regulatory requirements.</li>
    <li>Details: see <a target="_blank" href="https://github.com/CactuseSecurity/firewall-orchestrator/tree/main/documentation/revision-history-main.md">
        https://github.com/CactuseSecurity/firewall-orchestrator/tree/main/documentation/revision-history-main.md</li>
</ul>
');

INSERT INTO txt VALUES ('getting_started',	    'German', 	'Einstiegshilfe');
INSERT INTO txt VALUES ('getting_started',	    'English', 	'Quick start');
INSERT INTO txt VALUES ('getting_started_facts',	    'German', 	'
Die folgenden Hauptmen&uuml;punkte stehen (je nach Rollenzugeh&ouml;rigkeit) zur Verf&uuml;gung:<ul>
    <li><a href="/report/generation">Reporting</a>: Erlaubt das einmalige Generieren verschiedener Reports sowie eine regelm&auml;&szlig Generierung</li>
    <li><a href="/request/ticketsoverview">Workflow</a>: Ticketsystem zur Beantragung von &Auml;nderungen</li>
    <li><a href="/certification">Rezertifizierung</a>: Workflow zur Bereinigung des Regelwerks um nicht mehr ben&ouml;tigte Regeln</li>
    <li><a href="/networkmodelling">Modellierung</a>: Erlaubt die verteilte Modellierung von Kommunikationsverbindungen (Soll-Zustand)</li>
    <li><a href="/network_analysis">Netzanalyse</a>: Pfadanalyse - welche Firewalls liegen zwischen zwei IP-Adressen?</li>
    <li><a href="/compliance/zones/configuration">Compliance</a>: Definition von Zonenmatrix und Zugriffs-Compliance</li>
    <li><a href="/monitoring">Monitoring</a>: Alarmierung, Log-Files, Import-Status, ...</li>
    <li><a href="/help" target="_blank">Hilfeseiten</a>: Benutzerhandbuch</li>
    <li><a href="/settings">Einstellungen</a>: Alle Einstellungen wie z.B. Sprache der Benutzeroberfl&auml;che oder
        das Einbinden <a href="/settings/managements">Ihrer eigenen Firewall-Systeme</a>.<br>
        N.B. Stellen Sie sicher, dass Sie alle Demo-Daten (insbesondere die Demo-User) l&ouml;schen (mit Hilfe der "Beispieldaten l&ouml;schen" Option in den Einstellungen), 
        bevor Sie in den produktiven Betrieb &uuml;bergehen, da andernfalls ggf. Ihre Daten mit Default-Logins angezeigt werden k&ouml;nnten.</li>
    <li><a href="/logout">Abmelden</a>: Firewall Orchestrator verlassen</li>
</ul>
');
INSERT INTO txt VALUES ('getting_started_facts',	    'English', 	'
The following top-level menu items are available (depending on role memberships):
<ul>
    <li><a href="/report/generation">Reporting</a>: Ad-hoc generation and scheduling of reports</li>
    <li><a href="/request/ticketsoverview">Workflow</a>: Ticketing system for requesting and processing firewall changes</li>
    <li><a href="/certification">Recertification</a>: Rulebase sanitization by continuously cleaning up existing rules</li>
    <li><a href="/networkmodelling">Modelling</a>: Allows for distributed modelling of network connections (target state)</li>
    <li><a href="/network_analysis">Network Analysis</a>: Path analysis - which firewall is crossed when routing between two IP addresses?</li>
    <li><a href="/compliance/zones/configuration">Compliance</a>: Defining zone matrix and access compliance</li>
    <li><a href="/monitoring">Monitoring</a>: Alarms, log files, import status, ...</li>
    <li><a href="/help" target="_blank">Help</a>: Manual pages</li>
    <li><a href="/settings">Settings</a>: All settings like e.g. language of the user interface or 
        integration of <a href="/settings/managements">your own firewalls</a>.<br>
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
INSERT INTO txt VALUES ('tenant_view',		    'German', 	'Mandantenansicht');
INSERT INTO txt VALUES ('tenant_view',		    'English', 	'Tenant View');
INSERT INTO txt VALUES ('unused_days',		    'German', 	'Unbenutzt seit (in Tagen)');
INSERT INTO txt VALUES ('unused_days',		    'English', 	'Unused since (in days)');
INSERT INTO txt VALUES ('generation',		    'German', 	'Generierung');
INSERT INTO txt VALUES ('generation',		    'English', 	'Generation');
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
INSERT INTO txt VALUES ('tenant_vis_devices',   'German', 	'Mandanten-Firewalls');
INSERT INTO txt VALUES ('tenant_vis_devices',	'English', 	'Tenant firewalls');
INSERT INTO txt VALUES ('edit_vis_devices',		'German', 	'Devices f&uuml;r Mandant');
INSERT INTO txt VALUES ('edit_vis_devices',		'English', 	'Devices for tenant');
INSERT INTO txt VALUES ('hide',         		'German', 	'versteckt');
INSERT INTO txt VALUES ('hide',         		'English', 	'hidden');
INSERT INTO txt VALUES ('shared',         		'German', 	'geteilt');
INSERT INTO txt VALUES ('shared',         		'English', 	'shared');
INSERT INTO txt VALUES ('unfiltered',      		'German', 	'ungefiltert');
INSERT INTO txt VALUES ('unfiltered',      		'English', 	'unfiltered');
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
INSERT INTO txt VALUES ('page_format',          'German', 	'Seitenformat');
INSERT INTO txt VALUES ('page_format',          'English', 	'Page Format');
INSERT INTO txt VALUES ('width',                'German', 	'Breite (mm)');
INSERT INTO txt VALUES ('width',                'English', 	'Width (mm)');
INSERT INTO txt VALUES ('height',               'German', 	'H&ouml;he (mm)');
INSERT INTO txt VALUES ('height',               'English', 	'Height (mm)');
INSERT INTO txt VALUES ('includes_json',        'German', 	'(beinhaltet JSON)');
INSERT INTO txt VALUES ('includes_json',        'English', 	'(includes JSON)');
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
INSERT INTO txt VALUES ('ip_addresses',		    'German', 	'IP-Adressen');
INSERT INTO txt VALUES ('ip_addresses',		    'English', 	'IP Addresses');
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
INSERT INTO txt VALUES ('negated',		        'German', 	'nicht');
INSERT INTO txt VALUES ('negated',		        'English', 	'not');
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
INSERT INTO txt VALUES ('rule_deleted',         'German', 	'Regel gel&ouml;scht');
INSERT INTO txt VALUES ('rule_deleted',         'English', 	'Rule deleted');
INSERT INTO txt VALUES ('rule_added',           'German', 	'Regel hinzugef&uuml;gt');
INSERT INTO txt VALUES ('rule_added',           'English', 	'Rule added');
INSERT INTO txt VALUES ('rule_modified',        'German', 	'Regel modifiziert');
INSERT INTO txt VALUES ('rule_modified',        'English', 	'Rule modified');
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
INSERT INTO txt VALUES ('report_duration',		'German', 	'Report-Generierung in');
INSERT INTO txt VALUES ('report_duration',		'English', 	'Report generation took');
INSERT INTO txt VALUES ('seconds',		        'German', 	'Sekunden');
INSERT INTO txt VALUES ('seconds',		        'English', 	'seconds');
INSERT INTO txt VALUES ('minutes',		        'German', 	'Minuten');
INSERT INTO txt VALUES ('minutes',		        'English', 	'minutes');
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
INSERT INTO txt VALUES ('generated_on',	        'German', 	'Erstellt am');
INSERT INTO txt VALUES ('generated_on',	        'English', 	'Generated on');
INSERT INTO txt VALUES ('date_of_config',	    'German', 	'Zeit der Konfiguration');
INSERT INTO txt VALUES ('date_of_config',	    'English', 	'Time of configuration');
INSERT INTO txt VALUES ('create_delete_ticket', 'German',	'L&ouml;schantrag stellen');
INSERT INTO txt VALUES ('create_delete_ticket', 'English',	'Create Delete Ticket');
INSERT INTO txt VALUES ('rules_to_delete',	    'German', 	'Zu l&ouml;schende Regeln');
INSERT INTO txt VALUES ('rules_to_delete',	    'English', 	'Rules to delete');
INSERT INTO txt VALUES ('delete_unused_rule',	'German', 	'Unbenutzte Regel l&ouml;schen');
INSERT INTO txt VALUES ('delete_unused_rule',   'English', 	'Delete unused rule');
INSERT INTO txt VALUES ('delete_unused_rules',	'German', 	'Unbenutzte Regeln l&ouml;schen');
INSERT INTO txt VALUES ('delete_unused_rules',  'English', 	'Delete unused rules');

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
INSERT INTO txt VALUES ('schedule_fetch',       'German', 	'Laden der Termine');
INSERT INTO txt VALUES ('schedule_fetch',       'English', 	'Report Schedule Fetch');
INSERT INTO txt VALUES ('save_scheduled_report','German',	'Termin speichern');
INSERT INTO txt VALUES ('save_scheduled_report','English',	'Save scheduled report');
INSERT INTO txt VALUES ('edit_scheduled_report','German',	'Termin bearbeiten');
INSERT INTO txt VALUES ('edit_scheduled_report','English',	'Edit scheduled report');
INSERT INTO txt VALUES ('delete_scheduled_report','German',	'Termin l&ouml;schen');
INSERT INTO txt VALUES ('delete_scheduled_report','English','Delete scheduled report');
INSERT INTO txt VALUES ('schedule_tile',		'German',	'Terminplan');
INSERT INTO txt VALUES ('schedule_tile',		'English',	'Report schedule');
INSERT INTO txt VALUES ('schedule_upd_err_msg',	'German',	'Das Laden der terminierten Reports f&uuml;hrte zu einem Fehler.');
INSERT INTO txt VALUES ('schedule_upd_err_msg',	'English',	'Loading the scheduled reports resulted in an error.');

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
INSERT INTO txt VALUES ('archive_tile',			'German',	'Report Archivierung');
INSERT INTO txt VALUES ('archive_tile',			'English',	'Report archiving');
INSERT INTO txt VALUES ('archive_upd_err_msg',	'German',	'Das Laden der archivierten Reports f&uuml;hrte zu einem Fehler.');
INSERT INTO txt VALUES ('archive_upd_err_msg',	'English',	'Loading the archived reports resulted in an error.');

-- workflow
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
INSERT INTO txt VALUES ('add_new_request',      'German', 	'Neuen Antrag hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_new_request',      'English', 	'Add new request');
INSERT INTO txt VALUES ('fetch_requests',       'German', 	'Antr&auml;ge holen');
INSERT INTO txt VALUES ('fetch_requests',       'English', 	'Fetch requests');
INSERT INTO txt VALUES ('init_environment',     'German', 	'Umgebung initialisieren');
INSERT INTO txt VALUES ('init_environment',     'English', 	'Init environment');
INSERT INTO txt VALUES ('start_work',           'German', 	'Arbeit beginnen');
INSERT INTO txt VALUES ('start_work',           'English', 	'Start work');
INSERT INTO txt VALUES ('save_request',         'German', 	'Antrag speichern');
INSERT INTO txt VALUES ('save_request',         'English', 	'Save request');
INSERT INTO txt VALUES ('state',                'German', 	'Status');
INSERT INTO txt VALUES ('state',                'English', 	'State');
INSERT INTO txt VALUES ('tasks',                'German', 	'Aufgaben');
INSERT INTO txt VALUES ('tasks',                'English', 	'Tasks');
INSERT INTO txt VALUES ('display_task',         'German', 	'Aufgabe darstellen');
INSERT INTO txt VALUES ('display_task',         'English', 	'Display task');
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
INSERT INTO txt VALUES ('create_implementation','German', 	'Einzelne Implementierung hinzuf&uuml;gen');
INSERT INTO txt VALUES ('create_implementation','English', 	'Create single implementation task');
INSERT INTO txt VALUES ('auto_create_impltasks','German', 	'Implementierungen autom. erzeugen');
INSERT INTO txt VALUES ('auto_create_impltasks','English', 	'Autocreate implementation tasks');
INSERT INTO txt VALUES ('cleanup_impltasks',    'German', 	'Alle Implementierungen aufr&auml;umen');
INSERT INTO txt VALUES ('cleanup_impltasks',    'English', 	'Clean up all implementation tasks');
INSERT INTO txt VALUES ('all_impltasks',        'German', 	'Alle Implementierungen');
INSERT INTO txt VALUES ('all_impltasks',        'English', 	'All implementation tasks');
INSERT INTO txt VALUES ('check_impltasks',      'German', 	'Implementierungen pr&uuml;fen');
INSERT INTO txt VALUES ('check_impltasks',      'English', 	'Check implementation tasks');
INSERT INTO txt VALUES ('impltask_created',     'German', 	'Implementierung angelegt');
INSERT INTO txt VALUES ('impltask_created',     'English', 	'Implementation task created');
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
INSERT INTO txt VALUES ('assign1', 			    'German',	'Zuweisen');
INSERT INTO txt VALUES ('assign1', 			    'English',	'Assign');
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
INSERT INTO txt VALUES ('insert_port',          'German', 	'Port einf&uuml;gen');
INSERT INTO txt VALUES ('insert_port',          'English', 	'Insert port');
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
INSERT INTO txt VALUES ('phase', 			    'German', 	'Phase');
INSERT INTO txt VALUES ('phase', 			    'English', 	'Phase');
INSERT INTO txt VALUES ('task_type', 			'German', 	'Tasktyp');
INSERT INTO txt VALUES ('task_type', 			'English', 	'Task type');
INSERT INTO txt VALUES ('action_type', 		    'German', 	'Aktionstyp');
INSERT INTO txt VALUES ('action_type', 		    'English', 	'Action type');
INSERT INTO txt VALUES ('external_params', 		'German', 	'Externe Parameter');
INSERT INTO txt VALUES ('external_params', 		'English', 	'External params');
INSERT INTO txt VALUES ('message_text', 		'German', 	'Nachricht');
INSERT INTO txt VALUES ('message_text', 		'English', 	'Message text');
INSERT INTO txt VALUES ('to_state', 		    'German', 	'Zielstatus');
INSERT INTO txt VALUES ('to_state', 		    'English', 	'to State');
INSERT INTO txt VALUES ('automatic', 		    'German', 	'Automatisch');
INSERT INTO txt VALUES ('automatic', 		    'English', 	'Automatic');
INSERT INTO txt VALUES ('free_text', 			'German', 	'Freitext');
INSERT INTO txt VALUES ('free_text', 			'English', 	'Free Text');
INSERT INTO txt VALUES ('back_to_ticket', 		'German', 	'Zur&uuml;ck zum Ticket');
INSERT INTO txt VALUES ('back_to_ticket', 		'English', 	'Back to ticket');
INSERT INTO txt VALUES ('confirm_cancel', 		'German', 	'Abbruch best&auml;tigen');
INSERT INTO txt VALUES ('confirm_cancel', 		'English', 	'Confirm cancel');
INSERT INTO txt VALUES ('priority', 		    'German', 	'Priorit&auml;t');
INSERT INTO txt VALUES ('priority', 		    'English', 	'Priority');
INSERT INTO txt VALUES ('comments',				'German', 	'Kommentare');
INSERT INTO txt VALUES ('comments',				'English', 	'Comments');
INSERT INTO txt VALUES ('button_text',			'German', 	'Schaltertext');
INSERT INTO txt VALUES ('button_text',			'English', 	'Button Text');
INSERT INTO txt VALUES ('path_analysis',		'German', 	'Pfadanalyse');
INSERT INTO txt VALUES ('path_analysis',		'English', 	'Path analysis');

-- network analysis
INSERT INTO txt VALUES ('network_analysis', 	'German',	'Netzanalyse');
INSERT INTO txt VALUES ('network_analysis', 	'English',	'Network Analysis');
INSERT INTO txt VALUES ('wrong_ip_address',	    'German', 	'Keine g&uuml;ltige IP Adresse');
INSERT INTO txt VALUES ('wrong_ip_address',		'English', 	'no valid ip address');
INSERT INTO txt VALUES ('gws_found',		    'German', 	'Gateways im Pfad');
INSERT INTO txt VALUES ('gws_found',		    'English', 	'Gateways in path');
INSERT INTO txt VALUES ('no_gws_found',		    'German', 	'Keine Gateways im Pfad gefunden');
INSERT INTO txt VALUES ('no_gws_found',		    'English', 	'No gateways found');
INSERT INTO txt VALUES ('search_route',		    'German', 	'Gateways im Pfad suchen');
INSERT INTO txt VALUES ('search_route',		    'English', 	'Search gateways in path');
INSERT INTO txt VALUES ('clear_input',		    'German', 	'Eingabe l&ouml;schen');
INSERT INTO txt VALUES ('clear_input',     	    'English', 	'Delete input');
INSERT INTO txt VALUES ('search_access',		'German', 	'Zugriff pr&uuml;fen');
INSERT INTO txt VALUES ('search_access',		'English', 	'Check access');

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
INSERT INTO txt VALUES ('missing_owner_id',		'German', 	'Fehlende Eigent&uuml;mer ID');
INSERT INTO txt VALUES ('missing_owner_id',		'English', 	'missing owner id');
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
INSERT INTO txt VALUES ('recert_history',       'German',   'Rezertifizierungshistorie');
INSERT INTO txt VALUES ('recert_history',       'English',  'Recertification history');
INSERT INTO txt VALUES ('recertified_by',       'German',   'rezertifiziert von');
INSERT INTO txt VALUES ('recertified_by',       'English',  'recertified by');
INSERT INTO txt VALUES ('decertified_by',       'German',   'dezertifiziert von');
INSERT INTO txt VALUES ('decertified_by',       'English',  'decertified by');
INSERT INTO txt VALUES ('as_owner',             'German',   'als Eigent&uuml;mer');
INSERT INTO txt VALUES ('as_owner',             'English',  'as Owner');
INSERT INTO txt VALUES ('decert_date',          'German',   'Dezertifizierungsdatum');
INSERT INTO txt VALUES ('decert_date',          'English',  'Decertification date');
INSERT INTO txt VALUES ('recert_comment',       'German',   'Zertifizierungskommentar');
INSERT INTO txt VALUES ('recert_comment',       'English',  'Certification comment');
INSERT INTO txt VALUES ('ip_matches',           'German',   'IP-Adress-&Uuml;bereinstimmung');
INSERT INTO txt VALUES ('ip_matches',           'English',  'IP address match');
INSERT INTO txt VALUES ('overdue_recert',       'German',   'nur&nbsp;&uuml;berf&auml;llige&nbsp;Regeln');
INSERT INTO txt VALUES ('overdue_recert',       'English',  'only&nbsp;overdue&nbsp;rules');
INSERT INTO txt VALUES ('recert_parameter',     'German',   'Rezertifizierungsparameter');
INSERT INTO txt VALUES ('recert_parameter',     'English',  'Recertification Parameters');
INSERT INTO txt VALUES ('without_owner',        'German',   'nur Regeln ohne Eigent&uuml;mer');
INSERT INTO txt VALUES ('without_owner',        'English',  'only ownerless rules');
INSERT INTO txt VALUES ('show_any_match',       'German',   'Any-Regeln anzeigen');
INSERT INTO txt VALUES ('show_any_match',       'English',  'show any rules');
INSERT INTO txt VALUES ('single_line_per_rule', 'German',   'eine Zeile pro Regel');
INSERT INTO txt VALUES ('single_line_per_rule', 'English',  'one line per rule');
INSERT INTO txt VALUES ('recalc_recerts',       'German',   'Neuberechnung offene Rezertifizierungen');
INSERT INTO txt VALUES ('recalc_recerts',       'English',  'Recalculate open recertifications');
INSERT INTO txt VALUES ('recalc_now',           'German',   'Jetzt neu berechnen');
INSERT INTO txt VALUES ('recalc_now',           'English',  'Recalculate now');
INSERT INTO txt VALUES ('at_startup',           'German',   'Beim Hochfahren');
INSERT INTO txt VALUES ('at_startup',           'English',  'At startup');
INSERT INTO txt VALUES ('daily',                'German',   'T&auml;glich');
INSERT INTO txt VALUES ('daily',                'English',  'Daily');

-- network modelling
INSERT INTO txt VALUES ('network_modelling', 	'German',	'Netzwerkmodellierung');
INSERT INTO txt VALUES ('network_modelling', 	'English',	'Network Modelling');
INSERT INTO txt VALUES ('modelling', 	        'German',	'Modellierung');
INSERT INTO txt VALUES ('modelling', 	        'English',	'Modelling');
INSERT INTO txt VALUES ('application', 	        'German',	'Applikation');
INSERT INTO txt VALUES ('application', 	        'English',	'Application');
INSERT INTO txt VALUES ('applications', 	    'German',	'Applikationen');
INSERT INTO txt VALUES ('applications', 	    'English',	'Applications');
INSERT INTO txt VALUES ('library', 	            'German',	'Bibliothek');
INSERT INTO txt VALUES ('library', 	            'English',	'Library');
INSERT INTO txt VALUES ('app_server', 	        'German',	'App Server');
INSERT INTO txt VALUES ('app_server', 	        'English',	'App Server');
INSERT INTO txt VALUES ('app_role', 	        'German',	'App Rolle');
INSERT INTO txt VALUES ('app_role', 	        'English',	'App Role');
INSERT INTO txt VALUES ('app_roles', 	        'German',	'App Rollen');
INSERT INTO txt VALUES ('app_roles', 	        'English',	'App Roles');
INSERT INTO txt VALUES ('preview', 	            'German',	'Vorschau');
INSERT INTO txt VALUES ('preview', 	            'English',	'Preview');
INSERT INTO txt VALUES ('comm_profile', 	    'German',	'Kommunikationsprofil');
INSERT INTO txt VALUES ('comm_profile', 	    'English',	'Communication Profile');
INSERT INTO txt VALUES ('connection', 	        'German',	'Verbindung');
INSERT INTO txt VALUES ('connection', 	        'English',	'Connection');
INSERT INTO txt VALUES ('connections', 	        'German',	'Verbindungen');
INSERT INTO txt VALUES ('connections', 	        'English',	'Connections');
INSERT INTO txt VALUES ('add_connection', 	    'German',	'Verbindung hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_connection', 	    'English',	'Add Connection');
INSERT INTO txt VALUES ('edit_connection', 	    'German',	'Verbindung bearbeiten');
INSERT INTO txt VALUES ('edit_connection', 	    'English',	'Edit Connection');
INSERT INTO txt VALUES ('save_connection', 	    'German',	'Verbindung speichern');
INSERT INTO txt VALUES ('save_connection', 	    'English',	'Save Connection');
INSERT INTO txt VALUES ('delete_connection', 	'German',	'Verbindung l&ouml;schen');
INSERT INTO txt VALUES ('delete_connection', 	'English',	'Delete Connection');
INSERT INTO txt VALUES ('remove_connection', 	'German',	'Verbindung entfernen');
INSERT INTO txt VALUES ('remove_connection', 	'English',	'Remove Connection');
INSERT INTO txt VALUES ('add_interface', 	    'German',	'Schnittstelle hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_interface', 	    'English',	'Add Interface');
INSERT INTO txt VALUES ('func_reason', 	        'German',	'Fachliche Begr&uuml;ndung');
INSERT INTO txt VALUES ('func_reason', 	        'English',	'Functional Reason');
INSERT INTO txt VALUES ('to_source', 	        'German',	'Zu Quelle');
INSERT INTO txt VALUES ('to_source', 	        'English',	'To Source');
INSERT INTO txt VALUES ('to_dest', 	            'German',	'Zu Ziel');
INSERT INTO txt VALUES ('to_dest', 	            'English',	'To Destination');
INSERT INTO txt VALUES ('to_service', 	        'German',	'Zu Dienst');
INSERT INTO txt VALUES ('to_service', 	        'English',	'To Service');
INSERT INTO txt VALUES ('data_inconsistent',    'German', 	'Daten inkonsistent');
INSERT INTO txt VALUES ('data_inconsistent',    'English', 	'Data inconsistent');
INSERT INTO txt VALUES ('add_app_role', 	    'German',	'Neue App Rolle');
INSERT INTO txt VALUES ('add_app_role', 	    'English',	'New App Role');
INSERT INTO txt VALUES ('edit_app_role', 	    'German',	'App Rolle bearbeiten');
INSERT INTO txt VALUES ('edit_app_role', 	    'English',	'Edit App Role');
INSERT INTO txt VALUES ('save_app_role', 	    'German',	'App Rolle speichern');
INSERT INTO txt VALUES ('save_app_role', 	    'English',	'Save App Role');
INSERT INTO txt VALUES ('delete_app_role', 	    'German',	'App Rolle l&ouml;schen');
INSERT INTO txt VALUES ('delete_app_role', 	    'English',	'Delete App Role');
INSERT INTO txt VALUES ('to_app_role', 	        'German',	'Zu App Rolle');
INSERT INTO txt VALUES ('to_app_role', 	        'English',	'To App Role');
INSERT INTO txt VALUES ('add_service', 	        'German',	'Dienst hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_service', 	        'English',	'Add Service');
INSERT INTO txt VALUES ('edit_service', 	    'German',	'Dienst bearbeiten');
INSERT INTO txt VALUES ('edit_service', 	    'English',	'Edit Service');
INSERT INTO txt VALUES ('save_service', 	    'German',	'Dienst speichern');
INSERT INTO txt VALUES ('save_service', 	    'English',	'Save Service');
INSERT INTO txt VALUES ('delete_service', 	    'German',	'Dienst l&ouml;schen');
INSERT INTO txt VALUES ('delete_service', 	    'English',	'Delete Service');
INSERT INTO txt VALUES ('ext_request', 	        'German',	'Externer Antrag');
INSERT INTO txt VALUES ('ext_request', 	        'English',	'External Request');
INSERT INTO txt VALUES ('area', 	            'German',	'Area');
INSERT INTO txt VALUES ('area', 	            'English',	'Area');
INSERT INTO txt VALUES ('interface', 	        'German',	'Schnittstelle');
INSERT INTO txt VALUES ('interface', 	        'English',	'Interface');
INSERT INTO txt VALUES ('interfaces', 	        'German',	'Schnittstellen');
INSERT INTO txt VALUES ('interfaces', 	        'English',	'Interfaces');
INSERT INTO txt VALUES ('provided_interfaces', 	'German',	'Bereitgestellte Schnittstellen');
INSERT INTO txt VALUES ('provided_interfaces', 	'English',	'Provided Interfaces');
INSERT INTO txt VALUES ('remove_interface', 	'German',	'Schnittstelle entfernen');
INSERT INTO txt VALUES ('remove_interface', 	'English',	'Remove Interface');
INSERT INTO txt VALUES ('use',                  'German', 	'Benutzen');
INSERT INTO txt VALUES ('use',                  'English', 	'Use');
INSERT INTO txt VALUES ('services_group', 	    'German',	'Dienstgruppe');
INSERT INTO txt VALUES ('services_group', 	    'English',	'Service Group');
INSERT INTO txt VALUES ('services_groups', 	    'German',	'Dienstgruppen');
INSERT INTO txt VALUES ('services_groups', 	    'English',	'Service Groups');
INSERT INTO txt VALUES ('to_services_group', 	'German',	'Zu Dienstgruppe');
INSERT INTO txt VALUES ('to_services_group', 	'English',	'To Service Group');
INSERT INTO txt VALUES ('add_service_group', 	'German',	'Dienstgruppe hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_service_group', 	'English',	'Add Service Group');
INSERT INTO txt VALUES ('edit_service_group', 	'German',	'Dienstgruppe bearbeiten');
INSERT INTO txt VALUES ('edit_service_group', 	'English',	'Edit Service Group');
INSERT INTO txt VALUES ('save_service_group', 	'German',	'Dienstgruppe speichern');
INSERT INTO txt VALUES ('save_service_group', 	'English',	'Save Service Group');
INSERT INTO txt VALUES ('delete_service_group', 'German',	'Dienstgruppe l&ouml;schen');
INSERT INTO txt VALUES ('delete_service_group', 'English',	'Delete Service Group');
INSERT INTO txt VALUES ('add_app_server', 	    'German',	'Neuer App Server');
INSERT INTO txt VALUES ('add_app_server', 	    'English',	'New App Server');
INSERT INTO txt VALUES ('edit_app_server', 	    'German',	'App Server bearbeiten');
INSERT INTO txt VALUES ('edit_app_server', 	    'English',	'Edit App Server');
INSERT INTO txt VALUES ('save_app_server', 	    'German',	'App Server speichern');
INSERT INTO txt VALUES ('save_app_server', 	    'English',	'Save App Server');
INSERT INTO txt VALUES ('delete_app_server', 	'German',	'App Server l&ouml;schen');
INSERT INTO txt VALUES ('delete_app_server', 	'English',	'Delete App Server');
INSERT INTO txt VALUES ('created_by',		    'German', 	'Erstellt von');
INSERT INTO txt VALUES ('created_by',		    'English', 	'Created by');
INSERT INTO txt VALUES ('log_change',		    'German', 	'&Auml;nderung loggen');
INSERT INTO txt VALUES ('log_change',		    'English', 	'Log Change');
INSERT INTO txt VALUES ('show_history',		    'German', 	'&Auml;nderungshistorie');
INSERT INTO txt VALUES ('show_history',		    'English', 	'Show History');
INSERT INTO txt VALUES ('changed_by',		    'German', 	'Ge&auml;ndert von');
INSERT INTO txt VALUES ('changed_by',		    'English', 	'Changed by');
INSERT INTO txt VALUES ('object_id',            'German', 	'Objekt-Id');
INSERT INTO txt VALUES ('object_id',            'English', 	'Object Id');
INSERT INTO txt VALUES ('predef_services',      'German', 	'Vordefinierte Dienste');
INSERT INTO txt VALUES ('predef_services',      'English', 	'Predefined Services');
INSERT INTO txt VALUES ('search_interface',     'German', 	'Schnittstelle suchen');
INSERT INTO txt VALUES ('search_interface',     'English', 	'Search Interface');
INSERT INTO txt VALUES ('used_interface',       'German', 	'Genutzte Schnittstelle');
INSERT INTO txt VALUES ('used_interface',       'English', 	'Used Interface');
INSERT INTO txt VALUES ('reactivate',           'German', 	'Reaktivieren');
INSERT INTO txt VALUES ('reactivate',           'English', 	'Reactivate');
INSERT INTO txt VALUES ('search_nw_object',     'German', 	'Netzwerkobjekt suchen');
INSERT INTO txt VALUES ('search_nw_object',     'English', 	'Search Network Object');
INSERT INTO txt VALUES ('remove_nw_object', 	'German',	'Netzwerkobjekt entfernen');
INSERT INTO txt VALUES ('remove_nw_object', 	'English',	'Remove Network Object');
INSERT INTO txt VALUES ('is_in_use', 	        'German',	'Wird benutzt');
INSERT INTO txt VALUES ('is_in_use', 	        'English',	'Is in use');
INSERT INTO txt VALUES ('deactivate',           'German', 	'Deaktivieren');
INSERT INTO txt VALUES ('deactivate',           'English', 	'Deactivate');
INSERT INTO txt VALUES ('common_service',       'German', 	'Common Service');
INSERT INTO txt VALUES ('common_service',       'English', 	'Common Service');
INSERT INTO txt VALUES ('common_services',      'German', 	'Common Services');
INSERT INTO txt VALUES ('common_services',      'English', 	'Common Services');
INSERT INTO txt VALUES ('add_common_service', 	'German',	'Common Service hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_common_service', 	'English',	'Add Common Service');
INSERT INTO txt VALUES ('regular_connection',   'German', 	'Standard-Verbindung');
INSERT INTO txt VALUES ('regular_connection',   'English', 	'Regular Connection');
INSERT INTO txt VALUES ('regular_connections', 	'German',	'Standard-Verbindungen');
INSERT INTO txt VALUES ('regular_connections', 	'English',	'Regular Connections');
INSERT INTO txt VALUES ('show_all',             'German', 	'Alle darstellen');
INSERT INTO txt VALUES ('show_all',             'English', 	'Show all');

-- compliance
INSERT INTO txt VALUES ('compliance',			'German', 	'Compliance');
INSERT INTO txt VALUES ('compliance',			'English', 	'Compliance');
INSERT INTO txt VALUES ('network_zones',		'German', 	'Netzwerkzonen');
INSERT INTO txt VALUES ('network_zones',		'English', 	'Network zones');
INSERT INTO txt VALUES ('matrix',				'German', 	'Matrix');
INSERT INTO txt VALUES ('matrix',				'English', 	'Matrix');
INSERT INTO txt VALUES ('checks',				'German', 	'&Uuml;berpr&uuml;fung');
INSERT INTO txt VALUES ('checks',				'English', 	'Checks');
INSERT INTO txt VALUES ('check',				'German', 	'&Uuml;berpr&uuml;fen');
INSERT INTO txt VALUES ('check',				'English', 	'Check');
INSERT INTO txt VALUES ('zone_comm_matrix',		'German', 	'Netzwerkzonen-Kommunikationsmatrix');
INSERT INTO txt VALUES ('zone_comm_matrix',		'English', 	'Network zone communication matrix');
INSERT INTO txt VALUES ('network_zone_config',	'German', 	'Netzwerkzonen-Konfiguration');
INSERT INTO txt VALUES ('network_zone_config',	'English', 	'Network zone configuration');
INSERT INTO txt VALUES ('network_zone_check',	'German', 	'Netzwerkzonen-Compliancepr&uuml;fung');
INSERT INTO txt VALUES ('network_zone_check',	'English', 	'Network zone compliance check');
INSERT INTO txt VALUES ('allowed_communication','German', 	'Erlaubte Kommunikation');
INSERT INTO txt VALUES ('allowed_communication','English', 	'Allowed communication');
INSERT INTO txt VALUES ('subzones',				'German', 	'Subzonen');
INSERT INTO txt VALUES ('subzones',				'English', 	'Subzones');
INSERT INTO txt VALUES ('superzone',			'German', 	'Superzone');
INSERT INTO txt VALUES ('superzone',			'English', 	'Superzone');
INSERT INTO txt VALUES ('edit_zone_title',		'German', 	'Netzwerkzone editieren');
INSERT INTO txt VALUES ('edit_zone_title',		'English', 	'Edit network zone');
INSERT INTO txt VALUES ('add_ip_addresses',		'German', 	'IP Adresse(n) hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_ip_addresses',		'English', 	'Add IP Addresses');
INSERT INTO txt VALUES ('delete_zone_title',	'German', 	'Netzwerkzone l&ouml;schen');
INSERT INTO txt VALUES ('delete_zone_title',	'English', 	'Delete network zone');
INSERT INTO txt VALUES ('delete_zone_text',		'German', 	'Sind Sie sich sicher, dass sie die Netzwerkzone % l&ouml;schen wollen?');
INSERT INTO txt VALUES ('delete_zone_text',		'English', 	'Are you sure you want to delete the network zone %?');
INSERT INTO txt VALUES ('to',					'German', 	'Nach');
INSERT INTO txt VALUES ('to',					'English', 	'To');
INSERT INTO txt VALUES ('allowed_comm_dests',   'German',   'Erlaubte Kommunikation (Nach)');
INSERT INTO txt VALUES ('allowed_comm_dests',   'English',  'Allowed communication (to)');
INSERT INTO txt VALUES ('allowed_comm_srcs',    'German',   'Erlaubte Kommunikation (Von)');
INSERT INTO txt VALUES ('allowed_comm_srcs',    'English',  'Allowed communication (from)');
INSERT INTO txt VALUES ('relogin',              'German', 	'Erneut anmelden');
INSERT INTO txt VALUES ('relogin',              'English', 	'Re-Login');
INSERT INTO txt VALUES ('relogin_error',        'German', 	'Fehler bei der erneuten Anmeldung');
INSERT INTO txt VALUES ('relogin_error',        'English', 	'Re-Login error');
INSERT INTO txt VALUES ('internet_local_zone',  'German', 	'Internet / Lokal');
INSERT INTO txt VALUES ('internet_local_zone',  'English', 	'Internet / Local');
INSERT INTO txt VALUES ('rule_conform',         'German', 	'Regelkonform');
INSERT INTO txt VALUES ('rule_conform',     	'English', 	'In accordance with the rules');
INSERT INTO txt VALUES ('rule_violations',     	'German', 	'Regelverletzungen');
INSERT INTO txt VALUES ('rule_violations',     	'English', 	'Rule violations');
INSERT INTO txt VALUES ('no_network_zones',     'German', 	'Es existieren bisher keine Netzwerkzonen. Bitte legen Sie diese im Abschnitt "Konfiguration" an.');
INSERT INTO txt VALUES ('no_network_zones',     'English', 	'No network zones exist yet. Please create them in the "Configuration" section.');

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
INSERT INTO txt VALUES ('defaults',		        'German', 	'Weitere Einstellungen');
INSERT INTO txt VALUES ('defaults',		        'English', 	'Further settings');
INSERT INTO txt VALUES ('standards',		    'German', 	'Standardeinstellungen');
INSERT INTO txt VALUES ('standards',		    'English', 	'Defaults');
INSERT INTO txt VALUES ('password_policy',      'German', 	'Passworteinstellungen');
INSERT INTO txt VALUES ('password_policy',      'English', 	'Password Policy');
INSERT INTO txt VALUES ('email_settings',       'German', 	'Email-Einstellungen');
INSERT INTO txt VALUES ('email_settings',       'English', 	'Email settings');
INSERT INTO txt VALUES ('importer_settings',    'German', 	'Importer-Einstellungen');
INSERT INTO txt VALUES ('importer_settings',    'English', 	'Importer settings');
INSERT INTO txt VALUES ('edit_email',           'German', 	'Email-Einstellungen editieren');
INSERT INTO txt VALUES ('edit_email',           'English', 	'Edit email settings');
INSERT INTO txt VALUES ('email_sender',         'German', 	'Email-Absendeadresse');
INSERT INTO txt VALUES ('email_sender',         'English', 	'Email sender address');
INSERT INTO txt VALUES ('email_auth_user',      'German', 	'Email-Nutzer');
INSERT INTO txt VALUES ('email_auth_user',      'English', 	'Email auth user');
INSERT INTO txt VALUES ('email_auth_pwd',       'German', 	'Email-Nutzer Passwort');
INSERT INTO txt VALUES ('email_auth_pwd',       'English', 	'Email user password');
INSERT INTO txt VALUES ('email_enc_method',     'German', 	'Email-Verschl&uuml;sselung');
INSERT INTO txt VALUES ('email_enc_method',     'English', 	'Email encryption');
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
INSERT INTO txt VALUES ('add_new_credential',   'German', 	'Neue Login-Daten hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_new_credential',   'English', 	'Add new credentials');
INSERT INTO txt VALUES ('edit_credential',      'German', 	'Login-Daten bearbeiten');
INSERT INTO txt VALUES ('edit_credential',      'English', 	'Edit credentials');
INSERT INTO txt VALUES ('delete_credential',    'German', 	'Login-Daten l&ouml;schen');
INSERT INTO txt VALUES ('delete_credential',    'English', 	'Delete credentials');
INSERT INTO txt VALUES ('host',                 'German', 	'Host');
INSERT INTO txt VALUES ('host',                 'English', 	'Host');
INSERT INTO txt VALUES ('hostname',             'German', 	'Hostname');
INSERT INTO txt VALUES ('hostname',             'English', 	'Hostname');
INSERT INTO txt VALUES ('port',                 'German', 	'Port');
INSERT INTO txt VALUES ('port',                 'English', 	'Port');
INSERT INTO txt VALUES ('config_path',          'German', 	'Domain');
INSERT INTO txt VALUES ('config_path',          'English', 	'Domain');
INSERT INTO txt VALUES ('cloud_client_id',      'German', 	'Cloud Client ID');
INSERT INTO txt VALUES ('cloud_client_id',      'English', 	'Cloud Client ID');
INSERT INTO txt VALUES ('cloud_client_secret',  'German', 	'Cloud Client Secret');
INSERT INTO txt VALUES ('cloud_client_secret',  'English', 	'Cloud Client Secret');
INSERT INTO txt VALUES ('cloud_sub_id',         'German', 	'Cloud Subscription ID');
INSERT INTO txt VALUES ('cloud_sub_id',         'English', 	'Cloud Subscription ID');
INSERT INTO txt VALUES ('cloud_tenant_id',      'German', 	'Cloud Tenant ID');
INSERT INTO txt VALUES ('cloud_tenant_id',      'English', 	'Cloud Tenant ID');
INSERT INTO txt VALUES ('unused',               'German', 	'ungenutzt');
INSERT INTO txt VALUES ('unused',               'English', 	'unused');
INSERT INTO txt VALUES ('domain_uid',           'German', 	'Domain UID');
INSERT INTO txt VALUES ('domain_uid',           'English', 	'Domain UID');
INSERT INTO txt VALUES ('super_manager',        'German', 	'Multi Domain Manager');
INSERT INTO txt VALUES ('super_manager',        'English', 	'Multi Domain Manager');
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
INSERT INTO txt VALUES ('login_secret',         'German', 	'Passwort');
INSERT INTO txt VALUES ('login_secret',         'English', 	'Password');
INSERT INTO txt VALUES ('private_key',          'German', 	'Privater Schl&uuml;ssel');
INSERT INTO txt VALUES ('private_key',          'English', 	'Private Key');
INSERT INTO txt VALUES ('public_key',           'German', 	'&Ouml;ffentlicher Schl&uuml;ssel');
INSERT INTO txt VALUES ('public_key',           'English', 	'Public Key');
INSERT INTO txt VALUES ('import_credential',    'German', 	'Import Login-Daten');
INSERT INTO txt VALUES ('import_credential',    'English', 	'Import Credentials');
INSERT INTO txt VALUES ('is_key_pair',          'German', 	'Schl&uuml;sselpaar?');
INSERT INTO txt VALUES ('is_key_pair',          'English', 	'Key pair?');
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
INSERT INTO txt VALUES ('refresh', 				'German',	'Aktualisieren');
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
INSERT INTO txt VALUES ('assign_user_group_to_role','German','Nutzer/Gruppe zu Rolle zuordnen');
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
INSERT INTO txt VALUES ('select_from_ldap',     'German',	'Von LDAP ausw&auml;hlen');
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
INSERT INTO txt VALUES ('owner_group',          'German', 	'Eigent&uuml;mergruppe');
INSERT INTO txt VALUES ('owner_group',          'English', 	'Owner Group');
INSERT INTO txt VALUES ('into_ldap',            'German', 	'in LDAP');
INSERT INTO txt VALUES ('into_ldap',            'English', 	'into LDAP');
INSERT INTO txt VALUES ('from_ldap',            'German', 	'Von LDAP');
INSERT INTO txt VALUES ('from_ldap',            'English', 	'From LDAP');
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
INSERT INTO txt VALUES ('test_email_connection','German', 	'Email-Verbindung testen');
INSERT INTO txt VALUES ('test_email_connection','English', 	'Test email connection');
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
INSERT INTO txt VALUES ('unusedTolerance',      'German', 	'Als unbenutzt gewertet nach (in Tagen)');
INSERT INTO txt VALUES ('unusedTolerance',      'English', 	'Regarded as unused from (in days)');
INSERT INTO txt VALUES ('creationTolerance',    'German', 	'Toleranz ab Erzeugungsdatum (in Tagen)');
INSERT INTO txt VALUES ('creationTolerance',    'English', 	'Tolerance from creation date (in days)');
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
INSERT INTO txt VALUES ('impChangeNotifyRecipients','German', 	'Empf&auml;nger-Email-Adressen f&uuml;r &Auml;nderungen');
INSERT INTO txt VALUES ('impChangeNotifyRecipients','English', 	'Recipient email addresses for change notifications');
INSERT INTO txt VALUES ('impChangeNotifySubject',   'German', 	'Titel der &Auml;nderungsbenachrichtigung');
INSERT INTO txt VALUES ('impChangeNotifySubject',   'English',  'Subject of change notification emails');
INSERT INTO txt VALUES ('impChangeNotifyBody',      'German', 	'Text der &Auml;nderungsbenachrichtigung');
INSERT INTO txt VALUES ('impChangeNotifyBody',      'English', 	'Body of change notification emails');
INSERT INTO txt VALUES ('impChangeNotifyActive',    'German', 	'&Auml;nderungsbenachrichtigung aktiv?');
INSERT INTO txt VALUES ('impChangeNotifyActive',    'English', 	'Change notification active?');
INSERT INTO txt VALUES ('impChangeNotifyType','German', 	'&Auml;nderungsbenachrichtigungstyp');
INSERT INTO txt VALUES ('impChangeNotifyType','English', 	'Change notification type');
INSERT INTO txt VALUES ('impChangeNotifySleepTime','German','&Auml;nderungsbenachrichtigungs-Intervall (in Sekunden)');
INSERT INTO txt VALUES ('impChangeNotifySleepTime','English','Change notification sleep time (in seconds)');
INSERT INTO txt VALUES ('impChangeNotifyStartAt',  'German','&Auml;nderungsbenachrichtigungs-Start');
INSERT INTO txt VALUES ('impChangeNotifyStartAt',  'English','Change notification start at');
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
INSERT INTO txt VALUES ('recAutocreateDeleteTicket','German','Autom. Anlegen L&ouml;schantrag');
INSERT INTO txt VALUES ('recAutocreateDeleteTicket','English','Autocreate delete rule ticket');
INSERT INTO txt VALUES ('recDeleteRuleTicketTitle','German','Titel f&uuml;r L&ouml;schantrag');
INSERT INTO txt VALUES ('recDeleteRuleTicketTitle','English','Title for delete rule ticket');
INSERT INTO txt VALUES ('recDeleteRuleTicketReason','German','Grund f&uuml;r L&ouml;schantrag');
INSERT INTO txt VALUES ('recDeleteRuleTicketReason','English','Reason for delete rule ticket');
INSERT INTO txt VALUES ('recDeleteRuleReqTaskTitle','German','Titel f&uuml;r L&ouml;schauftrag');
INSERT INTO txt VALUES ('recDeleteRuleReqTaskTitle','English','Title for delete rule task');
INSERT INTO txt VALUES ('recDeleteRuleReqTaskReason','German','Grund f&uuml;r L&ouml;schauftrag');
INSERT INTO txt VALUES ('recDeleteRuleReqTaskReason','English','Reason for delete rule task');
INSERT INTO txt VALUES ('recDeleteRuleTicketPriority','German','Priorit&auml;t f&uuml;r L&ouml;schantrag');
INSERT INTO txt VALUES ('recDeleteRuleTicketPriority','English','Priority for delete rule ticket');
INSERT INTO txt VALUES ('recDeleteRuleInitState','German',  'Initialer Status f&uuml;r L&ouml;schantrag');
INSERT INTO txt VALUES ('recDeleteRuleInitState','English', 'Initial state for delete rule ticket');
INSERT INTO txt VALUES ('recCheckActive',       'German','Rezert Check - aktiv');
INSERT INTO txt VALUES ('recCheckActive',       'English','Recert Check - active');
INSERT INTO txt VALUES ('recCheckEmailSubject', 'German','Rezert Check - Email Betreff');
INSERT INTO txt VALUES ('recCheckEmailSubject', 'English','Recert Check - Email subject');
INSERT INTO txt VALUES ('recCheckEmailUpcomingText','German','Rezert Check - Text anstehend');
INSERT INTO txt VALUES ('recCheckEmailUpcomingText','English','Recert Check - text upcoming');
INSERT INTO txt VALUES ('recCheckEmailOverdueText','German','Rezert Check - Text &uuml;berf&auml;llig');
INSERT INTO txt VALUES ('recCheckEmailOverdueText','English','Recert Check - text overdue');
INSERT INTO txt VALUES ('recert_check_every',   'German', 	'Rezert Check alle');
INSERT INTO txt VALUES ('recert_check_every',   'English', 	'Recert Check every');
INSERT INTO txt VALUES ('each_on',              'German', 	'jeweils am');
INSERT INTO txt VALUES ('each_on',              'English', 	'each on');
INSERT INTO txt VALUES ('undefined',		    'German', 	'nicht definiert');
INSERT INTO txt VALUES ('undefined',		    'English', 	'undefined');
INSERT INTO txt VALUES ('reqAvailableTaskTypes','German', 	'Verf&uuml;gbare Auftragstypen');
INSERT INTO txt VALUES ('reqAvailableTaskTypes','English', 	'Available Task Types');
INSERT INTO txt VALUES ('reqAllowObjectSearch', 'German', 	'Objektsuche erlauben');
INSERT INTO txt VALUES ('reqAllowObjectSearch', 'English', 	'Allow object search');
INSERT INTO txt VALUES ('reqAllowManualOwnerAdmin','German', 'Manuelle Eigent&uuml;merverwaltung erlauben');
INSERT INTO txt VALUES ('reqAllowManualOwnerAdmin','English','Allow manual owner administration');
INSERT INTO txt VALUES ('ruleOwnershipMode',    'German',   'Regel-Eigent&uuml;merschaftsmodus');
INSERT INTO txt VALUES ('ruleOwnershipMode',    'English',  'Rule Ownership Mode');
INSERT INTO txt VALUES ('reqPriorities',        'German', 	'Priorit&auml;ten');
INSERT INTO txt VALUES ('reqPriorities',        'English', 	'Priorities');
INSERT INTO txt VALUES ('reqAutoCreateImplTasks','German', 	'Autom. Erzeugen von Implementierungs-Auftr&auml;gen');
INSERT INTO txt VALUES ('reqAutoCreateImplTasks','English', 'Auto-create implementation tasks');
INSERT INTO txt VALUES ('reqActivatePathAnalysis','German', 'Pfadanalyse aktivieren');
INSERT INTO txt VALUES ('reqActivatePathAnalysis','English','Activate Path Analysis');
INSERT INTO txt VALUES ('numeric_prio', 		'German', 	'Numerische Priorit&auml;t');
INSERT INTO txt VALUES ('numeric_prio', 		'English', 	'Numeric Priority');
INSERT INTO txt VALUES ('ticket_deadline',      'German', 	'Ticket-Deadline (in Tagen)');
INSERT INTO txt VALUES ('ticket_deadline',      'English',  'Ticket Deadline (in days)');
INSERT INTO txt VALUES ('approval_deadline_days','German', 	'Genehmigungs-Deadline (in Tagen)');
INSERT INTO txt VALUES ('approval_deadline_days','English', 'Approval Deadline (in days)');
INSERT INTO txt VALUES ('approval_deadline',    'German', 	'Genehmigungs-Deadline');
INSERT INTO txt VALUES ('approval_deadline',    'English',  'Approval Deadline');
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
INSERT INTO txt VALUES ('fetch_credentials',    'German', 	'Login-Daten abholen');
INSERT INTO txt VALUES ('fetch_credentials',    'English', 	'Fetch import credentials');
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
INSERT INTO txt VALUES ('lowest_end_state',     'English',  'Lowest exit state');
INSERT INTO txt VALUES ('derived_state',        'German',   'Abgeleiteter Status');
INSERT INTO txt VALUES ('derived_state',        'English',  'Derived state');
INSERT INTO txt VALUES ('select_action',        'German',   'Aktion ausw&auml;hlen');
INSERT INTO txt VALUES ('select_action',        'English',  'Select action');
INSERT INTO txt VALUES ('owners',               'German',   'Eigent&uuml;mer');
INSERT INTO txt VALUES ('owners',               'English',  'Owners');
INSERT INTO txt VALUES ('add_owner',            'German',   'Eigent&uuml;mer hinzuf&uuml;gen');
INSERT INTO txt VALUES ('add_owner',            'English',  'Add owner');
INSERT INTO txt VALUES ('edit_owner',           'German',   'Eigent&uuml;mer bearbeiten');
INSERT INTO txt VALUES ('edit_owner',           'English',  'Edit owner');
INSERT INTO txt VALUES ('delete_owner',         'German',   'Eigent&uuml;mer l&ouml;schen');
INSERT INTO txt VALUES ('delete_owner',         'English',  'Delete owner');
INSERT INTO txt VALUES ('recert_interval',      'German',   'Rezertintervall (in Tagen)');
INSERT INTO txt VALUES ('recert_interval',      'English',  'Recert Interval (in days)');
INSERT INTO txt VALUES ('ext_app_id',           'German',   'Externe Anwendungs-Id');
INSERT INTO txt VALUES ('ext_app_id',           'English',  'External Application Id');
INSERT INTO txt VALUES ('comm_svc_possible',    'German',   'Common Service zugelassen');
INSERT INTO txt VALUES ('comm_svc_possible',    'English',  'Common Service Possible');
INSERT INTO txt VALUES ('dn',                   'German',   'Vollst&auml;ndiger Name');
INSERT INTO txt VALUES ('dn',                   'English',  'Distinguished Name');
INSERT INTO txt VALUES ('main_responsible',     'German',   'Hauptverantwortlicher (DN)');
INSERT INTO txt VALUES ('main_responsible',     'English',  'Main responsible person (DN)');
INSERT INTO txt VALUES ('set_default',          'German',   'als Vorgabewert setzen');
INSERT INTO txt VALUES ('set_default',          'English',  'Set as Default');
INSERT INTO txt VALUES ('reset_to_default',     'German',   'auf Vorgabewerte zur&uuml;cksetzen');
INSERT INTO txt VALUES ('reset_to_default',     'English',  'Reset to Default');
INSERT INTO txt VALUES ('option',		        'German', 	'Option');
INSERT INTO txt VALUES ('option',		        'English', 	'Option');
INSERT INTO txt VALUES ('customize_texts',		'German', 	'Texte anpassen');
INSERT INTO txt VALUES ('customize_texts',		'English', 	'Customize Texts');
INSERT INTO txt VALUES ('ignore_helptexts',		'German', 	'Hilfetexte ignorieren');
INSERT INTO txt VALUES ('ignore_helptexts',		'English', 	'Ignore help texts');
INSERT INTO txt VALUES ('key',                  'German', 	'Schl&uuml;ssel');
INSERT INTO txt VALUES ('key',                  'English', 	'Key');
INSERT INTO txt VALUES ('text',                 'German', 	'Text');
INSERT INTO txt VALUES ('text',                 'English', 	'Text');
INSERT INTO txt VALUES ('custom_text',          'German', 	'Angepasster Text');
INSERT INTO txt VALUES ('custom_text',          'English', 	'Custom Text');
INSERT INTO txt VALUES ('allowServerInConn',    'German', 	'Server in Verbindung erlauben');
INSERT INTO txt VALUES ('allowServerInConn',    'English', 	'Allow Servers in Connection');
INSERT INTO txt VALUES ('allowServiceInConn',   'German', 	'Einfache Dienste in Verbindung erlauben');
INSERT INTO txt VALUES ('allowServiceInConn',   'English', 	'Allow Simple Services in Connection');
INSERT INTO txt VALUES ('overviewDisplayLines', 'German', 	'Max. Anzahl Zeilen in &Uuml;bersicht');
INSERT INTO txt VALUES ('overviewDisplayLines', 'English', 	'Max. Number of Rows in Overview');
INSERT INTO txt VALUES ('reducedProtocolSet',   'German',   'Reduzierten Protokollset darstellen');
INSERT INTO txt VALUES ('reducedProtocolSet',   'English',  'Display reduced Protocol set');
INSERT INTO txt VALUES ('importAppDataPath',    'German', 	'Pfad und Name von Appdaten-Import (ohne Endung)');
INSERT INTO txt VALUES ('importAppDataPath',    'English', 	'Path and Name of App data import (without ending)');
INSERT INTO txt VALUES ('importAppDataSleepTime','German', 	'Import Appdaten-Intervall (in Stunden)');
INSERT INTO txt VALUES ('importAppDataSleepTime','English', 'Import App data sleep time (in hours)');
INSERT INTO txt VALUES ('importAppDataStartAt', 'German', 	'Import Appdaten-Start');
INSERT INTO txt VALUES ('importAppDataStartAt', 'English', 	'Import App data start at');
INSERT INTO txt VALUES ('importSubnetDataPath', 'German', 	'Pfad und Name von Subnetzdaten-Import (ohne Endung)');
INSERT INTO txt VALUES ('importSubnetDataPath', 'English', 	'Path and Name of subnet data import (without ending)');
INSERT INTO txt VALUES ('importSubnetDataSleepTime','German','Import Subnetzdaten-Intervall (in Stunden)');
INSERT INTO txt VALUES ('importSubnetDataSleepTime','English','Import Subnet data sleep time (in hours)');
INSERT INTO txt VALUES ('importSubnetDataStartAt','German', 'Import Subnetzdaten-Start');
INSERT INTO txt VALUES ('importSubnetDataStartAt','English','Import Subnet data start at');
INSERT INTO txt VALUES ('networkAreaRequired',  'German',   'Netzwerkarea vorgeschrieben');
INSERT INTO txt VALUES ('networkAreaRequired',  'English',  'Network Area Required');
INSERT INTO txt VALUES ('fixedPartLength',      'German',   'L&auml;nge fixer Teil');
INSERT INTO txt VALUES ('fixedPartLength',      'English',  'Fixed Part Length');
INSERT INTO txt VALUES ('freePartLength',       'German',   'L&auml;nge freier Teil');
INSERT INTO txt VALUES ('freePartLength',       'English',  'Free Part Length');
INSERT INTO txt VALUES ('networkAreaPattern',   'German',   'Muster Netzwerkarea');
INSERT INTO txt VALUES ('networkAreaPattern',   'English',  'Network Area Pattern');
INSERT INTO txt VALUES ('appRolePattern',       'German',   'Muster App Rolle');
INSERT INTO txt VALUES ('appRolePattern',       'English',  'App Role Pattern');
INSERT INTO txt VALUES ('import_source',        'German',   'Importquelle');
INSERT INTO txt VALUES ('import_source',        'English',  'Import Source');

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
INSERT INTO txt VALUES ('daily_recert_check',   'German',   'T&auml;glicher Rezertifizierungs-Check');
INSERT INTO txt VALUES ('daily_recert_check',   'English',  'Scheduled Daily Recertification Check');
INSERT INTO txt VALUES ('emails_sent',          'German',   ' Emails versendet');
INSERT INTO txt VALUES ('emails_sent',          'English',  ' emails sent');
INSERT INTO txt VALUES ('scheduled_autodiscovery','German', 'Termingesteuerte Autodiscovery');
INSERT INTO txt VALUES ('scheduled_autodiscovery','English','Scheduled Autodiscovery');
INSERT INTO txt VALUES ('manual_autodiscovery', 'German', 	'Manuelle Autodiscovery');
INSERT INTO txt VALUES ('manual_autodiscovery', 'English', 	'Manual Autodiscovery');
INSERT INTO txt VALUES ('changes_found',        'German', 	' &Auml;nderungen gefunden');
INSERT INTO txt VALUES ('changes_found',        'English',	' changes found');
INSERT INTO txt VALUES ('found_no_changes',     'German', 	'keine &Auml;nderungen gefunden');
INSERT INTO txt VALUES ('found_no_changes',     'English',	'no changes found');
INSERT INTO txt VALUES ('scheduled_app_import', 'German',   'Termingesteuerter App-Import');
INSERT INTO txt VALUES ('scheduled_app_import', 'English',  'Scheduled App Import');
INSERT INTO txt VALUES ('scheduled_subnet_import','German', 'Termingesteuerter Subnetz-Import');
INSERT INTO txt VALUES ('scheduled_subnet_import','English','Scheduled Subnet Import');
INSERT INTO txt VALUES ('imp_change_notification','German', '&Auml;nderungsbenachrichtigung');
INSERT INTO txt VALUES ('imp_change_notification','English','Import Change Notification');
INSERT INTO txt VALUES ('credentials',           'German', 	'Login-Daten');
INSERT INTO txt VALUES ('credentials',           'English', 'Credentials');

-- help pages
INSERT INTO txt VALUES ('report_types',         'German', 	'Report-Typen');
INSERT INTO txt VALUES ('report_types',         'English', 	'Report types');
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
INSERT INTO txt VALUES ('phases_roles', 	    'German', 	'Phasen und Rollen');
INSERT INTO txt VALUES ('phases_roles', 		'English', 	'Phases and Roles');
INSERT INTO txt VALUES ('task_types', 			'German', 	'Auftragstypen');
INSERT INTO txt VALUES ('task_types', 			'English', 	'Task Types');
INSERT INTO txt VALUES ('state_handling', 		'German', 	'Status-Verwaltung');
INSERT INTO txt VALUES ('state_handling', 		'English', 	'State Handling');
INSERT INTO txt VALUES ('checklist', 		    'German', 	'Checkliste');
INSERT INTO txt VALUES ('checklist',            'English', 	'Checklist');
INSERT INTO txt VALUES ('requirements',         'German', 	'Voraussetzungen');
INSERT INTO txt VALUES ('requirements',         'English', 	'Requirements');
INSERT INTO txt VALUES ('recert_logic',         'German', 	'Logik und Konfiguration');
INSERT INTO txt VALUES ('recert_logic', 		'English', 	'Logic and Configuration');
INSERT INTO txt VALUES ('configuration',        'German', 	'Konfiguration');
INSERT INTO txt VALUES ('configuration',        'English', 	'Configuration');
INSERT INTO txt VALUES ('owner_import',         'German', 	'Eigent&uuml;mer-Import');
INSERT INTO txt VALUES ('owner_import',         'English', 	'Owner Import');
INSERT INTO txt VALUES ('import_interfaces',    'German', 	'Import-Schnittstellen');
INSERT INTO txt VALUES ('import_interfaces',    'English', 	'Import Interfaces');
INSERT INTO txt VALUES ('import_app_data',      'German', 	'Applikationsdaten-Import');
INSERT INTO txt VALUES ('import_app_data',      'English', 	'Application Data Import');
INSERT INTO txt VALUES ('import_subnet_data',   'German', 	'Subnetzdaten-Import');
INSERT INTO txt VALUES ('import_subnet_data',   'English', 	'Subnet Data Import');
INSERT INTO txt VALUES ('general',              'German', 	'Allgemein');
INSERT INTO txt VALUES ('general',              'English', 	'General');
INSERT INTO txt VALUES ('naming_convention',    'German',   'Namenskonvention');
INSERT INTO txt VALUES ('naming_convention',    'English',  'Naming Convention');


-- text codes (roughly) categorized: 
-- U: user texts (explanation or confirmation texts)
-- E: error texts
-- A: Api errors
-- T: texts from external sources (Ldap, other database tables)
-- C: Contextual Info (Tooltips)
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
--            5500-5599: workflow module
-- 6000-6999: API
-- 7000-7999: Monitoring
-- 8000-8999: Workflow
-- 9000-9999: Modelling

-- generic success messages
INSERT INTO txt VALUES ('S_add_title',          'German',   'Erstellen erfolgreich');
INSERT INTO txt VALUES ('S_add_title',          'English',  'Creation successful');
INSERT INTO txt VALUES ('S_add_message',        'German',   'Das Erstellen des Elements wurde erfolgreich abgeschlossen.');
INSERT INTO txt VALUES ('S_add_message',        'English',  'The creation of the element has been completed successfully.');

INSERT INTO txt VALUES ('S_modify_title',       'German',   'Modifizieren erfolgreich');
INSERT INTO txt VALUES ('S_modify_title',       'English',  'Modification successful');
INSERT INTO txt VALUES ('S_modify_message',     'German',   'Das Modifizieren des Elements wurde erfolgreich abgeschlossen.');
INSERT INTO txt VALUES ('S_modify_message',     'English',  'The modification of the element has been completed successfully.');

INSERT INTO txt VALUES ('S_delete_title',       'German',   'L&ouml;schen erfolgreich');
INSERT INTO txt VALUES ('S_delete_title',       'English',  'Deletion successful');
INSERT INTO txt VALUES ('S_delete_message',     'German',   'Das L&ouml;schen des Elements wurde erfolgreich abgeschlossen.');
INSERT INTO txt VALUES ('S_delete_message',     'English',  'The deletion of the element has been completed successfully.');

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
INSERT INTO txt VALUES ('U5108', 'German',  'L&ouml;scht alle Beispiel-Logindaten (auf "_demo" endend)');
INSERT INTO txt VALUES ('U5108', 'English', 'Deletes all sample credentials (ending with "_demo")');
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
INSERT INTO txt VALUES ('U5116', 'German',  'Verwaltung der Login-Daten der eingebundenen Management-Systeme');
INSERT INTO txt VALUES ('U5116', 'English', 'Manage credentials for login to connected firewall management systems');
INSERT INTO txt VALUES ('U5117', 'German',  'Sind sie sicher, dass sie folgende Login-Daten l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5117', 'English', 'Are you sure you want to delete credentials: ');

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
INSERT INTO txt VALUES ('U5216', 'German',  'Anzeige und Verwaltung aller Eigent&uuml;mer');
INSERT INTO txt VALUES ('U5216', 'English', 'Show and administrate all owners');
INSERT INTO txt VALUES ('U5217', 'German',  'Sind sie sicher, dass sie folgenden Eigent&uuml;mer l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U5217', 'English', 'Are you sure you want to delete owner: ');
INSERT INTO txt VALUES ('U5218', 'German',  'L&ouml;scht alle Beispiel-Eigent&uuml;mer (auf "_demo" endend)');
INSERT INTO txt VALUES ('U5218', 'English', 'Deletes all sample owners (ending with "_demo")');

INSERT INTO txt VALUES ('U5301', 'German',  'Einstellungen ge&auml;ndert.');
INSERT INTO txt VALUES ('U5301', 'English', 'Settings changed.');
INSERT INTO txt VALUES ('U5302', 'German',  'Einstellungen ge&auml;ndert.');
INSERT INTO txt VALUES ('U5302', 'English', 'Policy changed.');
INSERT INTO txt VALUES ('U5303', 'German',  '* Einstellungen k&ouml;nnen vom Nutzer in den pers&ouml;nlichen Einstellungen &uuml;berschrieben werden.');
INSERT INTO txt VALUES ('U5303', 'English', '* Settings can be overwritten by user in personal settings.');
INSERT INTO txt VALUES ('U5304', 'German',  '* Einstellungen k&ouml;nnen vom Nutzer in den pers&ouml;nlichen Einstellungen oder in den Eigent&uuml;mer-Einstellungen &uuml;berschrieben werden.');
INSERT INTO txt VALUES ('U5304', 'English', '* Settings can be overwritten by user in personal settings or by the owner settings.');
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
INSERT INTO txt VALUES ('U5318', 'German',  'Sind sie sicher, dass sie die Einstellungen zur&uuml;cksetzen wollen? &Auml;nderungen an den Workflows gehen verloren.');
INSERT INTO txt VALUES ('U5318', 'English', 'Are you sure you want to reset the settings? Changes on workflows get lost.');
INSERT INTO txt VALUES ('U5319', 'German',  'Server f&uuml;r ausgehende Emails zur Benachrichtigung verwalten.');
INSERT INTO txt VALUES ('U5319', 'English', 'Manage email server for outgoing user notifications.');
INSERT INTO txt VALUES ('U5320', 'German',  'Mehrere Email-Adressen mit Komma trennen');
INSERT INTO txt VALUES ('U5320', 'English', 'Multiple email addresses can be separated by using commas');
INSERT INTO txt VALUES ('U5321', 'German',  'Anzeige und &Uuml;berschreiben aller dargestellten Texte');
INSERT INTO txt VALUES ('U5321', 'English', 'Show and overwrite all shown texts');
INSERT INTO txt VALUES ('U5322', 'German',  'Verwaltung der Voreinstellungen f&uuml;r die Netzwerk-Modellierung');
INSERT INTO txt VALUES ('U5322', 'English', 'Administration of default settings for network modelling');

INSERT INTO txt VALUES ('U5401', 'German',  'Passwort ge&auml;ndert.');
INSERT INTO txt VALUES ('U5401', 'English', 'Password changed.');
INSERT INTO txt VALUES ('U5402', 'German',  'Test-Email gesendet.');
INSERT INTO txt VALUES ('U5402', 'English', 'Test email sent.');
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
INSERT INTO txt VALUES ('U7003', 'German',  'L&ouml;scht alle Beispieldaten (auf "_demo" endend): Managements, Login-Daten, Gateways, Nutzer, Mandanten, Gruppen, Eigent&uuml;mer');
INSERT INTO txt VALUES ('U7003', 'English', 'Deletes all sample data (ending with "_demo"): managements, credentials, gateways, users, tenants, groups, owners');
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

INSERT INTO txt VALUES ('U8001', 'German',  'Sind sie sicher, dass sie Folgendes l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U8001', 'English', 'Are you sure you want to delete: ');
INSERT INTO txt VALUES ('U8002', 'German',  'Neue Genehmigung zum Auftrag hinzugef&uuml;gt.');
INSERT INTO txt VALUES ('U8002', 'English', 'New approval added to task.');
INSERT INTO txt VALUES ('U8003', 'German',  'Sind sie sicher, dass sie abbrechen wollen? Bereits erzeugte Auftr&auml;ge gehen verloren.');
INSERT INTO txt VALUES ('U8003', 'English', 'Are you sure you want to cancel? Already Created tasks will be lost.');

INSERT INTO txt VALUES ('U9001', 'German',  'Sind sie sicher, dass sie folgende Verbindung l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U9001', 'English', 'Are you sure you want to delete connection: ');
INSERT INTO txt VALUES ('U9002', 'German',  'Sind sie sicher, dass sie folgende App Rolle l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U9002', 'English', 'Are you sure you want to delete App Role: ');
INSERT INTO txt VALUES ('U9003', 'German',  'Sind sie sicher, dass sie folgenden Dienst l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U9003', 'English', 'Are you sure you want to delete service: ');
INSERT INTO txt VALUES ('U9004', 'German',  'Sind sie sicher, dass sie folgende Dienstgruppe l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U9004', 'English', 'Are you sure you want to delete service group: ');
INSERT INTO txt VALUES ('U9005', 'German',  'Sind sie sicher, dass sie folgende App Server reaktivieren wollen: ');
INSERT INTO txt VALUES ('U9005', 'English', 'Are you sure you want to reactivate App Server: ');
INSERT INTO txt VALUES ('U9006', 'German',  'Sind sie sicher, dass sie folgendes Objekt aus der Auswahlliste l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U9006', 'English', 'Are you sure you want to delete following object from selection list: ');
INSERT INTO txt VALUES ('U9007', 'German',  'Der folgende App Server wird verwendet. Sind sie sicher, dass sie ihn deaktivieren wollen: ');
INSERT INTO txt VALUES ('U9007', 'English', 'Following App Server is in use. Are you sure you want to deactivate it: ');
INSERT INTO txt VALUES ('U9008', 'German',  'Sind sie sicher, dass sie folgenden App Server l&ouml;schen wollen: ');
INSERT INTO txt VALUES ('U9008', 'English', 'Are you sure you want to delete App Server: ');

-- generic error messages --
INSERT INTO txt VALUES ('E_load_title',         'German',   'Fehler beim Laden');
INSERT INTO txt VALUES ('E_load_title',         'English',  'Loading error');
INSERT INTO txt VALUES ('E_load_message',       'German',   'Beim Laden der Elemente ist ein unerwarteter Fehler aufgetreten. Bitte melden Sie den Fehler, wenn er nicht erkl&auml;rbar ist.');
INSERT INTO txt VALUES ('E_load_message',       'English',  'An unexpected error occurred while loading the items. Please report the error if it cannot be resolved.');

INSERT INTO txt VALUES ('E_add_title',          'German',   'Fehler beim Erstellen');
INSERT INTO txt VALUES ('E_add_title',          'English',  'Creation error');
INSERT INTO txt VALUES ('E_add_message',        'German',   'Bei der Erstellung des Elements ist ein unerwarteter Fehler aufgetreten. Bitte melden Sie den Fehler, wenn er nicht erkl&auml;rbar ist.');
INSERT INTO txt VALUES ('E_add_message',        'English',  'An unexpected error occurred while creating the item. Please report the error if it cannot be resolved.');

INSERT INTO txt VALUES ('E_modify_title',       'German',   'Fehler beim Modifizieren');
INSERT INTO txt VALUES ('E_modify_title',       'English',  'Modification error');
INSERT INTO txt VALUES ('E_modify_message',     'German',   'Bei der Modifizierung des Elements ist ein unerwarteter Fehler aufgetreten. Bitte melden Sie den Fehler, wenn er nicht erkl&auml;rbar ist.');
INSERT INTO txt VALUES ('E_modify_message',     'English',  'An unexpected error occurred while modifying the item. Please report the error if it cannot be resolved.');

INSERT INTO txt VALUES ('E_delete_title',       'German',   'Fehler beim L&ouml;schen');
INSERT INTO txt VALUES ('E_delete_title',       'English',  'Deletion error');
INSERT INTO txt VALUES ('E_delete_message',     'German',   'Bei der L&ouml;schung des Elements ist ein unerwarteter Fehler aufgetreten. Bitte melden Sie den Fehler, wenn er nicht erkl&auml;rbar ist.');
INSERT INTO txt VALUES ('E_delete_message',     'English',  'An unexpected error occurred while deleting the item. Please report the error if it cannot be resolved.');

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
INSERT INTO txt VALUES ('E4003', 'German',  'Keine &Auml;nderungen f&uuml;r die gew&auml;hlten Kriterien gefunden');
INSERT INTO txt VALUES ('E4003', 'English', 'No changes found for given criteria');
INSERT INTO txt VALUES ('E4004', 'German',  'Nutzungsdaten nicht unterst&uuml;tzt f&uuml;r: ');
INSERT INTO txt VALUES ('E4004', 'English', 'Usage data not supported for: ');

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
INSERT INTO txt VALUES ('E5108', 'German',  'Email-Adresse muss "@"-Zeichen enthalten.');
INSERT INTO txt VALUES ('E5108', 'English', 'Email address must contain "@"-sign.');
INSERT INTO txt VALUES ('E5109', 'German',  'Bitte keine Leerzeichen im Namen verwenden.');
INSERT INTO txt VALUES ('E5109', 'English', 'Please do not use spaces in the name.');
INSERT INTO txt VALUES ('E5111', 'German',  'Es gibt bereits ein Gateway mit derselben Konfiguration und Import aktiviert');
INSERT INTO txt VALUES ('E5111', 'English', 'There is already a gateway in the same configuration with import enabled');
INSERT INTO txt VALUES ('E5112', 'German',  'Gateway konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E5112', 'English', 'Gateway could not be created');
INSERT INTO txt VALUES ('E5117', 'German',  'L&ouml;schen der Login-Daten nicht m&ouml;glich, da diese von einem Management verwendet werden. Dort zuerst andere Login-Daten ausw&auml;hlen');
INSERT INTO txt VALUES ('E5117', 'English', 'Deletion of credentials not allowed as they are in use by one or more management devices. Change the management credentials before deleting them.');

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
INSERT INTO txt VALUES ('E5211', 'German',  'Name, Passwort und initiale Rolle m&uuml;ssen gef&uuml;llt sein');
INSERT INTO txt VALUES ('E5211', 'English', 'Name, password and initial role have to be filled');
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
INSERT INTO txt VALUES ('E5291', 'German',  'Eigent&uuml;mer konnte nicht gespeichert werden');
INSERT INTO txt VALUES ('E5291', 'English', 'Owner could not be saved');
INSERT INTO txt VALUES ('E5292', 'German',  'Dn oder Gruppe muss gef&uuml;llt sein');
INSERT INTO txt VALUES ('E5292', 'English', 'Dn or group has to be filled');

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

INSERT INTO txt VALUES ('E6001', 'German', 	'Der Re-Login war nicht erfolgreich. Haben Sie ein falsches Passwort eingegeben? Schauen Sie f&uuml;r Details bitte in die Logs.');
INSERT INTO txt VALUES ('E6001', 'English', 'Re-login failed. Did you enter a wrong password? See log for details.');

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
INSERT INTO txt VALUES ('E8012', 'German',  'Kommentar konnte nicht angelegt werden');
INSERT INTO txt VALUES ('E8012', 'English', 'Comment could not be created');
INSERT INTO txt VALUES ('E8013', 'German',  'Regel-Uid ist auf diesem Ger&auml;t nicht vorhanden: ');
INSERT INTO txt VALUES ('E8013', 'English', 'Rule Uid does not exist on this device: ');
INSERT INTO txt VALUES ('E8014', 'German',  'Die Verarbeitung dieses Auftragstyps ist nicht aktiviert. Bitte Administrator kontaktieren.');
INSERT INTO txt VALUES ('E8014', 'English', 'The handling of this Task Type is not activated. Please contact administrator.');

INSERT INTO txt VALUES ('E8101', 'German',  'Email-Versand kann nicht getestet werden, da der aktuell angemeldete Nutzer keine Email-Adresse hinterlegt hat.');
INSERT INTO txt VALUES ('E8101', 'English', 'Sending of emails cannot be tested because the logged-in user does not have an email address.');

INSERT INTO txt VALUES ('E9001', 'German',  'Sie haben Modellier-Rechte f&uuml;r keine Applikation.');
INSERT INTO txt VALUES ('E9001', 'English', 'You have no modeller rights for any application.');
INSERT INTO txt VALUES ('E9002', 'German',  'Keine Area verf&uuml;gbar.');
INSERT INTO txt VALUES ('E9002', 'English', 'No areas available.');
INSERT INTO txt VALUES ('E9003', 'German',  'Id schon vergeben. Bitte eine andere ausw&auml;hlen.');
INSERT INTO txt VALUES ('E9003', 'English', 'Id already used. Please choose another.');
INSERT INTO txt VALUES ('E9004', 'German',  'Dienst und Quelle oder Ziel m&uuml;ssen ausgef&uuml;llt sein.');
INSERT INTO txt VALUES ('E9004', 'English', 'Service and source or Destination have to be filled.');
INSERT INTO txt VALUES ('E9005', 'German',  'Der Schnittstellentyp kann nicht zwischen Quelle und Ziel ge&auml;ndert werden.');
INSERT INTO txt VALUES ('E9005', 'English', 'The Interface type cannot be changed between Source and Destination.');
INSERT INTO txt VALUES ('E9006', 'German',  'Quelle, Dienst und Ziel m&uuml;ssen gef&uuml;llt sein.');
INSERT INTO txt VALUES ('E9006', 'English', 'Source, Service and Destination have to be filled.');
INSERT INTO txt VALUES ('E9007', 'German',  'Dienst kann nicht gel&ouml;scht werden, da er in Benutzung ist: ');
INSERT INTO txt VALUES ('E9007', 'English', 'Service cannot be deleted because it is in use: ');
INSERT INTO txt VALUES ('E9008', 'German',  'Dienstgruppe kann nicht gel&ouml;scht werden, da sie in Benutzung ist: ');
INSERT INTO txt VALUES ('E9008', 'English', 'Service Group cannot be deleted because it is in use: ');
INSERT INTO txt VALUES ('E9009', 'German',  'App Rolle kann nicht gel&ouml;scht werden, da sie in Benutzung ist: ');
INSERT INTO txt VALUES ('E9009', 'English', 'App Role cannot be deleted because it is in use: ');
INSERT INTO txt VALUES ('E9010', 'German',  'IP-Adresse wurde schon verwendet.');
INSERT INTO txt VALUES ('E9010', 'English', 'IP address is already used.');

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
INSERT INTO txt VALUES ('T0010', 'German',  'wie reporter-viewall, aber mit Erlaubnis, Devices anzulegen und zu &auml;ndern; desweiteren Berechtigungen als Planer und Implementer');
INSERT INTO txt VALUES ('T0010', 'English', 'like reporter-viewall, but allowed to create and update devices; furthermore permissions as planner and implementer');
INSERT INTO txt VALUES ('T0011', 'German',  'Nutzer mit vollem Zugriff auf den Firewall Orchestrator');
INSERT INTO txt VALUES ('T0011', 'English', 'users with full access rights to firewall orchestrator');
INSERT INTO txt VALUES ('T0012', 'German',  'Nutzer mit Berechtigung zum Rezertifizieren von Regeln');
INSERT INTO txt VALUES ('T0012', 'English', 'users that have the right to recertify rules');
INSERT INTO txt VALUES ('T0013', 'German',  'NNutzer mit Berechtigung zum Anlegen von Antr&auml;gen');
INSERT INTO txt VALUES ('T0013', 'English', 'users that have the right to create requests');
INSERT INTO txt VALUES ('T0014', 'German',  'Nutzer mit Berechtigung zum Genehmigen von Antr&auml;gen');
INSERT INTO txt VALUES ('T0014', 'English', 'users that have the right to approve requests');
INSERT INTO txt VALUES ('T0015', 'German',  'Nutzer mit Berechtigung zum Planen von Auftr&auml;gen');
INSERT INTO txt VALUES ('T0015', 'English', 'users that have the right to plan requests');
INSERT INTO txt VALUES ('T0016', 'German',  'Nutzer mit Berechtigung zum Implementieren von Auftr&auml;gen');
INSERT INTO txt VALUES ('T0016', 'English', 'users that have the right to implement requests');
INSERT INTO txt VALUES ('T0017', 'German',  'Nutzer mit Berechtigung zum Review von Auftr&auml;gen');
INSERT INTO txt VALUES ('T0017', 'English', 'users that have the right to review requests');
INSERT INTO txt VALUES ('T0018', 'German',  'Nutzer mit Berechtigung zum Modellieren von Applikationen');
INSERT INTO txt VALUES ('T0018', 'English', 'users that have the right to model applications');

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
INSERT INTO txt VALUES ('T0106', 'German',  'Aktuell aktive unbenutzte Regeln aller Gateways');
INSERT INTO txt VALUES ('T0106', 'English', 'Currently active unused rules of all gateways');
INSERT INTO txt VALUES ('T0107', 'German',  'Aktuell aktive Regeln, die zur Rezertifizierung anstehen');
INSERT INTO txt VALUES ('T0107', 'English', 'Currently active rules with upcoming recertification');

-- Contextual Info (Tooltips)
INSERT INTO txt VALUES ('C9000', 'German',  'Dieses Objekt wurde deaktiviert und sollte von der App Rolle entfernt werden.');
INSERT INTO txt VALUES ('C9000', 'English', 'This object was deactivated and should be removed from App Role.');
INSERT INTO txt VALUES ('C9001', 'German',  'Dieses Objekt wurde deaktiviert und sollte von der Verbindung entfernt werden.');
INSERT INTO txt VALUES ('C9001', 'English', 'This object was deactivated and should be removed from Connection.');
INSERT INTO txt VALUES ('C9002', 'German',  'Dieser App Server wurde noch in keiner App Rolle oder Verbindung verwendet.');
INSERT INTO txt VALUES ('C9002', 'English', 'This App Server was not used in any App Role or Connection.');
INSERT INTO txt VALUES ('C9003', 'German',  'Diese App wurde deaktiviert.');
INSERT INTO txt VALUES ('C9003', 'English', 'This app was deactivated.');
INSERT INTO txt VALUES ('C9004', 'German',  'In dieser App wurden noch keine Verbindungen angelegt.');
INSERT INTO txt VALUES ('C9004', 'English', 'In this app no connections have been created.');


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
    Mit Hilfe der Tabs am oberen Bildschirmrand kann zwischen der Report-Generierung, dem Report-Scheduling und dem Report-Archiv umgeschaltet werden.
');
INSERT INTO txt VALUES ('H1001', 'English', 'The first input line is the filter line, where the parameters for the report creation are defined.
    It is subject to a special <a href="/help/reporting/filter">Filter Syntax</a>. 
    It can be filled completely manually or supported by <a href="/help/reporting/templates">Templates</a>, which can be chosen below.
    In the <a href="/help/reporting/leftside">Left Sidebar</a> the available report types and devices as well as the reporting time are displayed.<br>
    After selecting the "Generate Report" button the <a href="/help/reporting/output">Report Data</a> is shown in the lower part of the window.
    In the <a href="/help/reporting/rightside">Right Sidebar</a> details about the selected objects are given.<br>
    The report can be <a href="/help/reporting/export">exported</a> to different output formats.
    Using the tabs at the top of the screen you may switch between report generation, report scheduling and the report archive.
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
INSERT INTO txt VALUES ('H1102', 'German',  'Folgende Report-Typen stehen zur Auswahl:
<ul>
    <li>Regeln - Anzeige von Zugriffsregeln; Default-Report-Zeitpunkt: jetzt</li>
    <li>Regeln (aufgel&ouml;st) - Anzeige von Zugriffsregeln, wobei s&auml;mtliche Gruppen in Quelle, Ziel und Dienst aufgel&ouml;st werden. 
     Dies erm&ouml;glicht einen Export in einer einzigen Tabelle ohne Hilfstabellen, in denen die Objekt-Definitionen stehen. Default-Report-Zeitpunkt: jetzt</li>
    <li>Regeln (technisch) - wie der aufgel&ouml;ste Regel-Report, nur dass Objektnamen nicht angezeigt werden. Default-Report-Zeitpunkt: jetzt</li>
    <li>Unbenutzte Regeln - Anzeige aller Regeln die das letztemal vor einem vorgegebenen Zeitpunkt benutzt wurden. Ger&auml;te, die keine Nutzungsinformation liefern, werden ignoriert.
    Falls der Reporter auch die Rolle "requester" hat, wird bei Selektion ausgegebener Regeln eine Schaltfl&auml;che zur Erzeugung eines L&ouml;schantrags angeboten.</li>
    <li>NAT-Regeln - Anzeige der NAT-Regeln und nicht der Zugriffsregeln. Default-Report-Zeitpunkt: jetzt</li>
    <li>Rezertifizierung - Anzeige aller Regeln mit anstehenden Rezertifizierungen. Der Default-Report-Zeitraum kann in den Einstellungen gesetzt werden</li>
    <li>&Auml;nderungen - Anzeige von &Auml;nderungen in einem bestimmten Zeitraum. Default-Report-Zeitraum: dieses Jahr</li>
    <li>&Auml;nderungen (aufgel&ouml;st) - Anzeige von &Auml;nderungen in einem bestimmten Zeitraum, wobei s&auml;mtliche Gruppen in Quelle, Ziel und Dienst aufgel&ouml;st werden. Default-Report-Zeitraum: dieses Jahr</li>
    <li>&Auml;nderungen (technisch)- wie der aufgel&ouml;ste &Auml;nderungs-Report, nur dass Objektnamen nicht angezeigt werden. Default-Report-Zeitraum: dieses Jahr</li>
    <li>Statistik - Anzeige von Statistikdaten &uuml;ber Anzahl von Objekten und Regeln. Default-Report-Zeitpunkt: jetzt</li>
</ul>
');
INSERT INTO txt VALUES ('H1102', 'English',  'Choose from the following report types:
<ul>
    <li>Rules - display access rules; default report time: now</li>
    <li>Rules (resolved) - display access rules but not showing any group structure but only resolved group content. Default report time: now</li>
    <li>Rules (technical) - display access rules, resolving groups and not showing object names. Default report time: now<</li>
    <li>Unused Rules - display all rules where the rule last hit lies before a given time. Devices delivering no usage information are disregarded.
    If the reporter has also a requester role, a button to create a delete rule request is offered after selecting reported rules.</li>
    <li>NAT Rules - display NAT rules instead of access rules. Default report time: now</li>
    <li>Recertification - display all rules where recertifications are upcoming. Default report interval can be defined in settings</li>
    <li>Changes - display all changes in a defined time interval. Default report interval: this year</li>
    <li>Changes (resolved) - display all changes in a defined time interval but not showing any group structure but only resolved group content. Default report interval: this year</li>
    <li>Changes (technical) - display all changes in a defined time interval resolving groups and not showing object names. Default report interval: this year</li>
    <li>Statistics - display statistical data on the number of objects and rules. Default report time: now</li>
</ul>
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
    <li>lasthit (last-hit, last-used, last-usage, last-use): Filtern nach Regel-Nutzung - aktuell unterst&uuml;tzt f&uuml;r FortiManager und Check Point >=R80.</li>
    <li>not-used-for-days (unused, unused-days, not-used): nicht genutzt seit der vorgegebenen Anzahl von Tagen oder gar nicht</li>
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
    <li>lasthit (last-hit, last-used, last-usage, last-use): filter by rule usage - supported for FortiManager and Check Point >=R80 only.</li>
    <li>not-used-for-days (unused, unused-days, not-used): not used for the given number of days or never</li>
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
INSERT INTO txt VALUES ('H1145', 'German',  '<li> Filtern nach letzter Verwendung der Regeln</li><ul>
    <li>lasthit<2023-01-01 - zeigt nur Regeln, die vor dem Jahr 2023 verwendet wurden sowie auch alle Regeln, die noch nie verwendet wurden</li>
    <li>lasthit>2022-12-31 - zeigt nur Regeln, die in 2023 (oder sp&auml;ter) verwendet wurden. Regeln ohne jegliche Verwendung werden nicht angezeigt.</li></ul>
');
INSERT INTO txt VALUES ('H1145', 'English', '<li> filter for last hit of rules</li><ul>
    <li>lasthit<2023-01-01 - only shows rules with hits before the year 2023 including those rules which have no hits at all</li>
    <li>lasthit>2022-12-31 - only shows rules which have hits in 2023 (or later). Rules without any hits are not shown.</li></ul>
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
INSERT INTO txt VALUES ('H1211', 'German',  'Basic Statistics: Etwas Statistik &uuml;ber Netzwerk-, Dienst- und Nutzerobjekte aller Devices.');
INSERT INTO txt VALUES ('H1211', 'English', 'Basic Statistics: Some statistics about network, service and user objects and rules of all devices.');
INSERT INTO txt VALUES ('H1212', 'German',  'Compliance: Pass rules with "any": Alle Durchlassregeln, die "any" als Quelle, Ziel oder Dienst enthalten.');
INSERT INTO txt VALUES ('H1212', 'English', 'Compliance: Pass rules with "any": All pass rules that contain "any" as source, destination or service.');
INSERT INTO txt VALUES ('H1213', 'German',  'Current Rules: Aktuell aktive Regeln aller ausgew&auml;hlten Devices.');
INSERT INTO txt VALUES ('H1213', 'English', 'Current Rules: Currently active rules of all selected devices.');
INSERT INTO txt VALUES ('H1214', 'German',  'This year&apos;s Rule Changes: Alle im aktuellen Jahr ge&auml;nderten Regeln in den ausgew&auml;hlten Devices.');
INSERT INTO txt VALUES ('H1214', 'English', 'This year&apos;s Rule Changes: All rule change performed in the current year in the selected devices.');
INSERT INTO txt VALUES ('H1215', 'German',  'Current NAT Rules: Aktuell aktive NAT-Regeln aller ausgew&auml;hlten Devices.');
INSERT INTO txt VALUES ('H1215', 'English', 'Current NAT Rules: Currently active NAT rules of all selected devices.');
INSERT INTO txt VALUES ('H1216', 'German',  'Last year&apos;s Unused Rules: Aktuell aktive und seit mindestens einem Jahr unbenutzte Regeln aller Gateways.');
INSERT INTO txt VALUES ('H1216', 'English', 'Last year&apos;s Unused Rules: Currently active and for at least one year unused rules of all gateways.');
INSERT INTO txt VALUES ('H1217', 'German',  'Next Month&apos;s Recertifications: Aktuell aktive Regeln, die im n&auml;chsten Monat zur Rezertifizierung anstehen.');
INSERT INTO txt VALUES ('H1217', 'English', 'Next Month&apos;s Recertifications: Currently active rules with upcoming recertification next month.');
INSERT INTO txt VALUES ('H1301', 'German',  'Direkt nach der Erzeugung oder vom <a href="/help/reporting/archive">Archiv</a> aus k&ouml;nnen Reports in verschiedenen Ausgabeformaten exportiert werden:');
INSERT INTO txt VALUES ('H1301', 'English', 'Directly after creation or from the <a href="/help/reporting/archive">archive</a> reports can be exported to different output formats:');
INSERT INTO txt VALUES ('H1302', 'German',  '<li>pdf</li><li>html</li><li>csv (aktuell nur f&uuml;r die aufgel&ouml;sten und technischen Report-Typen unterst&uuml;tzt)</li><li>json</li>');
INSERT INTO txt VALUES ('H1302', 'English', '<li>pdf</li><li>html</li><li>csv (currently only supported for resolved and technical report types)</li><li>json</li>');
INSERT INTO txt VALUES ('H1303', 'German',  'Nach bet&auml;tigen des "Report exportieren"-Auswahlfeldes kann eines oder mehrere dieser Formate ausgew&auml;hlt werden.
    Bei Aktivierung der pdf-Ausgabe wird desweiteren das Seitenformat zur Auswahl angeboten.<br>
    Auch kann der Report mit einem Namen versehen und <a href="/help/reporting/archive">archiviert</a> werden.
    Ein weiteres Ausgabefenster erlaubt dann das separate Abholen der ausgew&auml;hlten Ausgabedateien.
');
INSERT INTO txt VALUES ('H1303', 'English', 'After clicking the "Export Report" button one or more of them can be selected.
    When selecting the pdf export, the page format is offered for selection.<br>
    Also the possibility to name and save the report in the <a href="/help/reporting/archive">archive</a> is given.
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
INSERT INTO txt VALUES ('H1403', 'German',  'Zus&auml;tzlich werden in einzelnen Reporttypen weitere Spalten dargestellt:
    <ul>
        <li>Changes report: &Auml;nderungszeit, &Auml;nderungstyp (Regelnummerierung entf&auml;llt daf&uuml;r)</li>
        <li>Unbenutzte-Regel-Report: Letzter Treffer</li>
        <li>NAT-Regel-Report: Umgesetzte Quelle, Umgesetztes Ziel, Umgesetzte Dienste</li>
        <li>Rezertifizierungs-Report: Datum n&auml;chste Rezertifizierung, Eigent&uuml;mer, IP-Adress-&Uuml;bereinstimmung, Letzter Treffer</li>
    </ul>
');
INSERT INTO txt VALUES ('H1403', 'English', 'Additionally in the different Report Types further columns are displayed:
    <ul>
        <li>Changes report: Change Time, Change Type (but no rule numbering)</li>
        <li>Unused Rules Report: Last Hit</li>
        <li>NAT Rules Report: Translated Source, Translated Destination, Translated Services</li>
        <li>Recertification Report: Next Recertification Date, Owner, IP address match, Last Hit</li>
    </ul>
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
    Im Unbenutzte-Regel-Report werden Devices, die keine Nutztungsinformationen liefern, bei Reporterstellung automatisch deselektiert.
');
INSERT INTO txt VALUES ('H1503', 'English', 'Display of all available devices.
    This view may differ for the different users according to the <a href="/help/settings/tenants">tenant assignments</a>.
    For the creation of a report a selection out of them has to be done. The displayed devices can be collapsed or expanded.
    In the <a href="/help/settings/report">Report Settings</a> it is possible to define the minimum number, where the display starts collapsed.
    In the Unused Rules Report devices not delivering usage information are deselected automatically during report creation.
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
INSERT INTO txt VALUES ('H1506', 'German',  'F&uuml;r Report-Typen, die Zeitintervalle ben&ouml;tigen (nicht Unbenutzte-Regel- und Rezertifizierungs-Report), kann gew&auml;hlt werden zwischen:');
INSERT INTO txt VALUES ('H1506', 'English', 'For report types requiring a time range (not Unused Rule or Recertification Report) a selection can be done between:');
INSERT INTO txt VALUES ('H1507', 'German',  'Vordefinierte Abk&uuml;rzungen "dieses Jahr", "letztes Jahr", "dieser Monat", "letzter Monat", "diese Woche", "letzte Woche", "heute" oder "gestern"');
INSERT INTO txt VALUES ('H1507', 'English', 'Predefined shortcuts "this year", "last year", "this month", "last month", "this week", "last week", "today" or "yesterday"');
INSERT INTO txt VALUES ('H1508', 'German',  'Zeitintervalle in Tagen, Wochen, Monaten oder Jahren relativ zum aktuellen Zeitpunkt');
INSERT INTO txt VALUES ('H1508', 'English', 'Time intervals in days, weeks, months or years in relation to the actual time');
INSERT INTO txt VALUES ('H1509', 'German',  'Absolute Start- und Endezeiten. Beide Grenzen k&ouml;nnen durch setzen der "offen"-Markierung ausser Kraft gesetzt werden.');
INSERT INTO txt VALUES ('H1509', 'English', 'Absolute start and end times. Both limits can be separately omitted by setting the "open" checkbox.');
INSERT INTO txt VALUES ('H1510', 'German',  'Nur beim Unbenutzte-Regel-Report: Unbenutzt seit: Hier wird die Anzahl von Tagen eingegeben, seitdem die anzuzeigenden Regeln nicht mehr benutzt wurden.
    Regeln, die noch keine letzte Nutzung protokolliert haben, werden ebenfalls dargestellt, falls sie &auml;lter als eine in den <a href="/help/settings/report">Reporting-Einstellungen</a> definierte Toleranzzeit sind.
    Dort kann auch der Default-Wert f&uuml;r den Zeitraum der Nichtbenutzung gesetzt werden. 
');
INSERT INTO txt VALUES ('H1510', 'English', 'Only for Unused Rules Report: Unused since: Here the number of days is given, where the rules to be displayed have not be used.
    Rules never used are also displayed if their creation date is older than a tolerance interval defined in the <a href="/help/settings/report">Report Settings</a>.
    There also the default value for the unused interval can be defined.
');
INSERT INTO txt VALUES ('H1511', 'German',  'Nur beim Rezertifizierungs-Report: Rezertifizierungsparameter
    <ul><li>F&auml;llig in: Hier wird festgelegt, wie weit die Suche nach zu rezertifizierenden Regeln gehen soll (in Tagen).
        Der Default-Wert kann sowohl vom Administrator in den <a href="/help/settings/recertificationgeneral">Allgemeinen</a>
        als auch vom jeweiligen Nutzer in den <a href="/help/settings/recertificationpersonal">Pers&ouml;nlichen</a> Rezertifizierungseinstellungen festgelegt werden.</li>
    <li>Eigent&uuml;mer: Hier kann aus den dem Nutzer zugeordneten Eigent&uuml;merschaften ausgew&auml;hlt werden.</li>
    <li>Any-Regeln anzeigen: Wenn das H&auml;kchen gesetzt ist, werden auch Regeln mit Ip 0.0.0.0 in Quelle oder Ziel dargestellt.
        Beim deselektieren wird ein exkludierender Ausdruck zur Filterzeile hinzugef&uuml;gt.</li></ul>
');
INSERT INTO txt VALUES ('H1511', 'English', 'Only for Recertification Report: Recertification Parameters
    <ul><li>Due within: Select how far ahead should be searched for rules to be recertified (in days).
        The default value can be set by the administrator in the <a href="/help/settings/recertificationgeneral">General</a>
        as well as in the <a href="/help/settings/recertificationpersonal">Personal</a> Recertification Settings by the user.</li>
    <li>Owner: Select the certifying owner out of the ownerships related to the user.</li>
    <li>Show any rules: If flag is set, rules with Ip 0.0.0.0 in source or destination are shown.
        When deselecting an excluding statement is added to the filter line.</li></ul>
');
INSERT INTO txt VALUES ('H1601', 'German',  'Die rechte Randleiste hat drei Reiter: Unter "Alle" werden alle aktuell abgeholten Objekte dargestellt,
    w&auml;hrend unter "Report" nur die Objekte der im Report vorkommenden Regeln gezeigt werden.
    Im Reiter "Regel" sind dann nur die Objekte der in der Reportausgabe ausgew&auml;hlten Regeln dargestellt.<br>
    ("Alle"- und "Regel"-Reiter werden mit derselben Funktionalit&auml;t auch im Rezertifizierungsdialog angeboten).<br>
    Folgende Daten werden soweit verf&uuml;gbar dargestellt, gruppiert nach den ausgew&auml;hlten Devices:
');
INSERT INTO txt VALUES ('H1601', 'English', 'There are three Tabs shown in the right sidebar: The "All" tab displays all currently fetched objects,
    whereas in the "Report" tab only the objects of the rules of the report are shown.
    In the "Rule" tab only objects of rules selected in the report output are dispalyed.<br>
    ("All" and "Rule" tab are also offered in the Recertification dialogue with the same functionality).<br>
    The following data are displayed if available, grouped by the selected devices:
');
INSERT INTO txt VALUES ('H1602', 'German',  '<li>Netzwerkobjekte: Name, Typ, IP, Zone, ggf. Gruppenmitglieder, zuletzt ge&auml;ndert, Kommentar</li>
    <li>Dienste: Name, Typ, UID, Quellport, Zielport, Protokoll, Timeout, ggf. Gruppenmitglieder, zuletzt ge&auml;ndert, Kommentar</li>
    <li>Nutzer: Name, Typ, UID, realer Name, ggf. Gruppenmitglieder, zuletzt ge&auml;ndert, Kommentar</li>
');
INSERT INTO txt VALUES ('H1602', 'English', '<li>Network objects: Name, type, IP, zone, group members if applicable, last changed, comment</li>
    <li>Services: Name, type, UID, source port, destination port, protocol, timeout, group members if applicable, last changed, comment</li>
    <li>Users: Name, type, UID, real name, group members if applicable, last changed, comment</li>
');

INSERT INTO txt VALUES ('H2001', 'German',  'Es k&ouml;nnen Reports f&uuml;r einen bestimmten Termin oder als wiederkehrende Auftr&auml;ge festgelegt werden.
    Jeder Nutzer kann seine eigenen Terminpl&auml;ne verwalten.
');
INSERT INTO txt VALUES ('H2001', 'English', 'Reports can be scheduled for a given time or as recurring tasks.
    Every user can administrate his own report schedules.
');
INSERT INTO txt VALUES ('H2011', 'German',  'Name: Der Reportname, der im <a href="/help/reporting/archive">Archiv</a> wiederzufinden ist.');
INSERT INTO txt VALUES ('H2011', 'English', 'Name: The report name to be found in the <a href="/help/reporting/archive">Archive</a>.');
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
    Zum anderen finden sich hier auch die durch das <a href="/help/reporting/scheduling">Scheduling</a> erzeugten Reports.
    Die archivierten Reports k&ouml;nnen von hier heruntergeladen oder gel&ouml;scht werden.
');
INSERT INTO txt VALUES ('H3001', 'English', 'Here the archived reports can be found with name and information about creation date, type, template (only at scheduled reports),
    owner and a short description about the content. 
    They may be created on the one hand by exporting manually created reports with setting the flag "Archive" in <a href="/help/reporting/export">Export Report</a>.
    On the other hand here also the reports created by the <a href="/help/reporting/scheduling">Scheduling</a> can be found.
    It is possible to download or delete these archived reports.
');

INSERT INTO txt VALUES ('H4011', 'German',  'Im ersten Schritt muss ein Report mit den demn&auml;chst zu rezertifizierenden Regeln geladen werden.
    Der Zeitraum f&uuml;r die Vorausschau kann im Feld "F&auml;llig in" gew&auml;hlt werden.
    Diese wird im "Rezertifizierungsanzeigeintervall" in den <a href="/help/settings/recertificationpersonal">pers&ouml;nlichen</a> bzw. 
    in den <a href="/help/settings/recertificationgeneral">allgemeinen</a> Rezertifizierungseinstellungen initialisiert.
    Desweiteren m&uuml;ssen die zu betrachtenden Ger&auml;te in der linken Randleiste ausgew&auml;hlt werden.
');
INSERT INTO txt VALUES ('H4011', 'English', 'In the first step a report of upcoming rules to be certified has to be loaded. 
    The lookahead period for this can be chosen in the "Due within" field. 
    It is initialized by the settings value "Recertification Display Period" in the 
    <a href="/help/settings/recertificationpersonal">personal</a> resp. <a href="/help/settings/recertificationgeneral">general</a> Recertification Settings.
    Also the regarded devices have to be chosen in the left sidebar.
');
INSERT INTO txt VALUES ('H4012', 'German',  'Der Report zeigt nun alle Regeln, die im gew&auml;hlten Zeitraum zertifiziert werden m&uuml;ssen.
    Das Rezertifizierungsdatum wird errechnet aus dem letzten Rezertifizierungsdatum (falls unbekannt, wird das Erzeugungsdatum der Regel genommen)
    und dem Rezertifizierungsintervall, welches in den <a href="/help/settings/recertificationgeneral">Rezertifizierungseinstellungen</a> definiert wurde.
    Rezertifizierungen, die in den n&auml;chsten Tagen (definiert im Rezertifizierungserinnerungsintervall in den Standardeinstellungen) f&auml;llig sind, 
    werden in gelb, &uuml;berf&auml;llige Rezertifizierungen in rot unterlegt.
    Zus&auml;tzlich wird der letzte Rezertifizierer dargestellt ("unbekannt" zeigt an, dass noch keine Rezertifizierung stattgefunden hat).
');
INSERT INTO txt VALUES ('H4012', 'English', 'The report shows all rules that are upcoming for recertification within the selected interval.
    The recertification date is computed from the last recertification date (if unknown the rule creation date is taken)
    and the Recertification Period, defined in the <a href="/help/settings/recertificationgeneral">Recertification Settings</a>.
    Recertifications upcoming in the next days (defined in the Recertification Notice Period in the Default Settings) are marked in yellow, overdue recertifications in red.
    Additionally the last recertifier is mentioned ("unknown" indicates that there has been no recertification so far).
');
INSERT INTO txt VALUES ('H4013', 'German',  'Der Rezertifizierer hat nun die M&ouml;glichkeit alle zu re- oder dezertifizierenden Regeln zu markieren.
    Durch klicken der "Ausgew&auml;hlte Aktionen ausf&uuml;hren"-Schaltfl&auml;che wird zun&auml;chst ein Kommentar abgefragt.
    Dieser ist ein Pflichtfeld, wenn "Kommentar Pflichtfeld" in den <a href="/help/settings/recertificationgeneral">Rezertifizierungseinstellungen</a> gesetzt wurde.
    Nach der Best&auml;tigung werden alle markierten Re- und Dezertifizierungen in einem Schritt ausgef&uuml;hrt.
    Danach werden nur noch die verbleibenden anstehenden Rezertifizierungen angezeigt.
');
INSERT INTO txt VALUES ('H4013', 'English', 'The recertifier has now the possibility to mark each of the displayed rules for recertification or decertification.
    After clicking the "Execute Selected Actions" button a comment is requested. 
    This has to be filled, if the setting "Comment Required" in <a href="/help/settings/recertificationgeneral">Recertification Settings</a> is activated.
    When confirmed all selected re- and decertifications are executed in on step. 
    After that only the remaining open certifications are displayed.
');
INSERT INTO txt VALUES ('H4014', 'German',  'Dezertifizierte Regel k&ouml;nnen im Abschnitt <a href="/help/reporting">Reporting</a> mit dem Filterparameter "remove=true" dargestellt werden.');
INSERT INTO txt VALUES ('H4014', 'English', 'Decertified rules can be displayed in the <a href="/help/reporting">Reporting</a> part with the filter parameter "remove=true".');
INSERT INTO txt VALUES ('H4021', 'German',  'Dieses Rezertifizierungsszenario ist als Basis f&uuml;r weitere angepasste Abl&auml;ufe vorgesehen.');
INSERT INTO txt VALUES ('H4021', 'English', 'This recertification scenario is intended to be a base for further customized workflows.');

INSERT INTO txt VALUES ('H4031', 'German',  'In diesem Modul k&ouml;nnen Nutzer Firewall-Regeln re- oder dezertifizieren.
    Daf&uuml;r wird die Rolle "recertifier" ben&ouml;tigt. Auditor- und Admin-Nutzer k&ouml;nnen hier nur lesend auf die anstehenden Rezertifizierungen zugreifen.<br>
    Aktuell gibt es zwei verschiedene Optionen:
    <ol>
        <li> In der einfachen Variante kann ein Nutzer mit recertifier Rolle jede beliebige Regel rezertifizieren. 
            Diese Option eignet sich &uuml;blicherweise nur f&uuml;r kleine Installationen mit wenigen Firewall-Regeln.
        </li>
        <li> In gr&ouml;&szlig;eren Umgebungen ist es sinnvoll, die Rezertifizierung dezentral zu organisieren. 
            Daf&uuml;r wird eine Eigent&uuml;merstruktur unterst&uuml;tzt, die einer Liste von Eigent&uuml;mern die Verantwortlichkeit f&uuml;r jeweils einen Teil des Netzwerks in Form von IP-Adressen bzw. IP-Subnetzen zuweist. 
            Dies kann z.B. eine Aufteilung auf Applikationsbasis sein, es ist aber genauso auch eine Unterteilung nach Subnetzen auf Basis von Betriebseinheiten denkbar.
            Je nach Quelle und Ziel sind alle Eigent&uuml;mer, die f&uuml;r einen Teil der Regel in Form von IP-Adressen verantwortlich sind, auch f&uuml;r die Rezertifizierung der Regel verantwortlich.
            Es ist also im Allgemeinen eine Gruppe von Eigent&uuml;mern f&uuml;r die Rezertifizierung einer Regel verantwortlich.
        </li>
    </ol>
    Eine Regel gilt erst dann als vollst&auml;ndig rezertifiziert, wenn alle Eigent&uuml;mer die Regel rezertifiziert haben.<br>
    Im Reporting-Modul steht au&szlig;erdem ein Rezertifizierungs-Report zur Verf&uuml;gung.
    Desweiteren kann in den <a href="/help/settings/recertificationgeneral">Einstellungen</a> konfiguriert werden, dass bei Dezertifizierung durch alle Eigent&uuml;mer automatisch ein L&ouml;schauftrag erzeugt wird.
');
INSERT INTO txt VALUES ('H4031', 'English', '
    In this module, users can recertify or decertify firewall rules.
    The "recertifier" role is required for this. Auditor and admin users have read-only access to the pending recertifications here.<br>
    Currently there are two different options:
    <ol>
        <li> In the simple variant, a user with recertifier role can recertify any rule. 
            This option is usually suitable only for small installations with few firewall rules.
        </li>
        <li> In larger environments, it makes sense to organize recertification in a decentralized manner. 
            For this purpose, an ownership structure is supported that assigns responsibility for a part of the network in the form of IP addresses or IP subnets to a list of owners. 
            This can, for example, be a division on an application basis, but a subdivision by subnets based on operating units is just as conceivable.
            Depending on the source and destination, all owners who are responsible for a part of the rule in the form of IP addresses are also responsible for recertifying the rule.
            Thus, in general, a group of owners is responsible for the recertification of a rule.
        </li>
    </ol>
    A rule is not considered fully recertified until all owners have recertified the rule.<br>
    A recertification report is also available in the reporting module.
    Furtheron it can be configured in the <a href="/help/settings/recertificationgeneral">settings</a> that after decertification by all owners a delete request is created automatically.
');

INSERT INTO txt VALUES ('H4032', 'German',  '
F&uuml;r das ordnungsgem&auml;&szlig;e Funktionieren des Rezertifzierungsprozesses sind die folgenden Konfigurationen einmalig zu Beginn vorzunehmen.
<br><br>
<H4>Berechtigungen</H4>
<ul>
    <li>Angemeldete Nutzer m&uuml;ssen die Rolle "Recertifier" besitzen</li>
    <li>Angemeldete Nutzer m&uuml;ssen Mitglied eines Eigent&uuml;mers sein (entweder direkt unter "Hauptverantwortlicher (DN)" oder als Mitglieder einer Eigent&uuml;mergruppe)</li>
</ul>
<H4>Konfiguration der Eigent&uuml;mer</H4>
    M&ouml;chte man keine Verteilung der Zust&auml;ndigkeit auf mehrere Eigent&uuml;mer vornehmen, so ist es ausreichend, dem Eigent&uuml;mer "Super-Owner" eine Gruppe von Rezertifizierern zuzuordnen,
    die f&uuml;r die Rezertifizierung aller Firewall-Regeln verantwortlich sind.
    Um den Super-Owner editieren zu k&ouml;nnen, 
    ist im Men&uuml; "Einstellungen" - "Weitere Einstellungen" - "Standardeinstellungen" die Option "Manuelle Eigent&uuml;merverwaltung erlauben" zu aktivieren.
    <br><br>
    Soll hingegen eine dezentrale Struktur aufgebaut werden, so k&ouml;nnen die Eigent&uuml;mer entweder im Firewall Orchestrator selbst definiert werden (nicht empfohlen) 
    oder aber man importiert eine im Unternehmen vorhandene Eigent&uuml;merliste, wodurch der Pflegeaufwand zum Aktuell-Halten dieser Zuordnung nicht dem Betreiber der Firewall-Infrastruktur zuf&auml;llt.
    <br>
    Es wird hierbei empfohlen, im Men&uuml; "Einstellungen" - "Weitere Einstellungen" - "Standardeinstellungen" die Option "Manuelle Eigent&uuml;merverwaltung erlauben" zu deaktivieren,
    um sicherzustellen, dass keine lokalen &Auml;nderungen vorgenommen werden, sondern die Eigent&uuml;mer-Pflege ausschlie&szlig;lich im externen Tool stattfindet.
    <br>
    F&uuml;r die Verwaltung in einem externen System ist der Import der Eigent&uuml;mer und deren IP-Adressen via 
    <a href="/help/API">Firewall Orchestrator API</a> vorzunehmen.
    <br><br>
    Die Definition der Eigent&uuml;mer im Firewall Orchestrator selbst ist recht simpel. 
    Hierzu muss lediglich im Men&uuml; "Einstellungen" - "Weitere Einstellungen" - "Standardeinstellungen" die Option "Manuelle Eigent&uuml;merverwaltung erlauben" aktiviert werden.
    <br>
    Anschlie&szlig;end ist es im Men&uuml; "Einstellungen" - "Eigent&uuml;mer" m&ouml;glich, Eigent&uuml;mer manuell hinzuzuf&uuml;gen, zu bearbeiten oder zu l&ouml;schen.
</ul>
');
INSERT INTO txt VALUES ('H4032', 'English', '
For the proper functioning of the recertification process, the following configurations must be made once at the beginning.
<br><br>
<H4>Permissions</H4>
<ul>
    <li>Logged in users must have the role "Recertier"</li>
    <li>Logged in users must be members of an owner (either directly under "Full name*:" or as members of an owner group)</li>
</ul>
<H4>Owner configuration</H4>
    If one does not want to distribute responsibility among several owners, it is sufficient to assign a group of recertifiers to the "Super-Owner" owner,
    who are responsible for the recertification of all firewall rules.
    To be able to edit the super-owner 
    the "Allow manual owner management" option must be enabled in the "Settings" - "Additional settings" - "Default settings" menu.
    <br><br>
    If, on the other hand, a decentralized structure is to be set up, the owners can either be defined in the Firewall Orchestrator itself (not recommended) 
    or you can import an existing list of owners in the company, which means that the maintenance effort for keeping this assignment up to date does not fall to the operator of the firewall infrastructure.
    <br>
  In this case, it is recommended to deactivate the "Allow manual owner administration" option in the "Settings" - "Additional settings" - "Default settings" menu,
    to ensure that no local changes are made, but that owner maintenance takes place exclusively in the external tool.
    <br>
    For management in an external system, importing owners and their IP addresses should be done via the
    <a href="/help/API">Firewall Orchestrator API</a>.
    <br><br>
    Defining the owners in Firewall Orchestrator itself is quite simple. 
    All that is required is to activate the "Allow manual owner administration" option in the "Settings" - "Additional settings" - "Default settings" menu.
    <br>
    After that, in the "Settings" - "Owners" menu it is possible to add, edit or delete owners manually.
</ul>
');

INSERT INTO txt VALUES ('H4033', 'German',  '
Zum Testen der Rezertifizierungsfunktionalit&auml;t werden standardm&auml;&szlig;ig einige Demo-Daten zur Verf&uuml;gung gestellt.
<br>
Das sind sowohl Nutzer (userX_demo) als auch Eigent&uuml;mer (ownerX_demo).
<br>
Login erfolgt mit user1_demo (Passwort cactus1) oder user2_demo (Passwort cactus2)
<br><br>
<ol>
    <li>
        Im ersten Schritt kann in der linken Randleiste eine Filterung vorgenommen werden (F&auml;lligkeit der Rezertifizierung, Eigent&uuml;mer, Firewall-Ger&auml;te) 
        und anschlie&szlig;end mit der Schaltfl&auml;che "Regeln anzeigen" eine Liste der zu rezertifizierenden Regeln generiert werden.
    </li>
    <li> 
        Der Zeitraum f&uuml;r die Vorausschau kann im Feld "F&auml;llig in" gew&auml;hlt werden. 
        Der Default-Wert kann &uuml;ber Ver&auml;ndern des "Rezertifizierungsanzeigeintervalls" in den pers&ouml;nlichen bzw. in den allgemeinen Rezertifizierungseinstellungen gesetzt werden. 
    </li>
    <li>
        Bei Rezertifizierungen, die &uuml;berf&auml;llig sind, wird das Datum in rot angezeigt.<br>
        Der Rezertifizierer hat nun die M&ouml;glichkeit, beliebige zu re- oder dezertifizierenden Regeln zu markieren. <br>
        Durch klicken der "Ausgew&auml;hlte Aktionen ausf&uuml;hren"-Schaltfl&auml;che wird zun&auml;chst ein (optionaler - konfigurierbar) Kommentar abgefragt, und 
        nach Best&auml;tigung werden alle markierten Re- und Dezertifizierungen in einem Schritt ausgef&uuml;hrt.<br>
        Anschlie&szlig;end werden nur noch die verbliebenen anstehenden Rezertifizierungen angezeigt.
    </li>
    <li>
        Sollte die Option zum Re- bzw. Dezertifizierung nicht angezeigt werden, 
        sollten die <a href="/help/settings/roles">Berechtigungen</a> &uuml;berpr&uuml;ft werden bzw. wenn der Nutzer Mitglied mehrerer Eigent&uuml;mergruppen ist, 
        muss zun&auml;chst einer der Eigent&uuml;mer ausgew&auml;hlt werden, um speziell f&uuml;r diesen rezertifizieren zu k&ouml;nnen. <br>
        In diesem Fall empfiehlt es sich f&uuml;r einen Eigent&uuml;mer nach dem anderen zu rezertifizieren.
    </li>
</ol>
');
INSERT INTO txt VALUES ('H4033', 'English', '
To test the recertification functionality, some demo data is provided by default.
<br>
These are both users (userX_demo) and owners (ownerX_demo).
<br>
Login with user1_demo (password cactus1) or user2_demo (password cactus2)
<br><br>
<ol>
    <li>
        In the first step, a filtering can be made in the left sidebar (recertification due date, owner, firewall devices). 
        and then a list of rules to be recertified can be generated using the "Show rules" button.
    </li>
    <li> 
       The period for the preview can be selected in the "Due within" field. 
       The default value can be set by changing the "Recertification display interval" in the personal or in the general recertification settings. 
    </li>
    <li>
        For recertifications that are overdue, the date is displayed in red.<br>
        The recertifier now has the option to mark any rules to be re- or decertified.<br>
        By clicking the "Execute selected actions" button, a (optional - configurable) comment will be requested first, and 
        after confirmation all marked recertifications and decertifications are executed in one step.<br>
        Afterwards, only the remaining pending recertifications are displayed.
    </li>
    <li>
        If the option to recertify or decertify is not displayed, the <a href="/help/settings/roles">permissions</a>  should be checked or if the user is a member of several owner groups, 
        one of the owners must be selected first in order to be able to recertify specifically for this owner.<br>
        In this case, it is recommended for one owner at a time to recertify.
    </li>
</ol>
');

INSERT INTO txt VALUES ('H4034', 'German',  '
<H4>IP-Adress-Matching</H4>
 	Jede Regel (Quelle und Ziel) wird mit den f&uuml;r einen Eigent&uuml;mer definierten IP-Adressen auf &Uuml;berlappung gepr&uuml;ft.<br>
 	Bei einer &Uuml;berlappung ist der Eigent&uuml;mer f&uuml;r die Rezertifizierung der Regel verantwortlich und ein entsprechender Eintrag wird
 	abh&auml;ngig vom Rezertifizierungsintervall des Eigent&uuml;mers erzeugt.
 	<br><br>
 	Alle Regeln, die Netzwerkobjekte enthalten, die keinem Eigent&uuml;mer zugeordnet werden k&ouml;nnen, sind automatisch dem Super-Owner zugewiesen.
 	<br>
    Dieser ist somit f&uuml;r deren Rezertifzierung verantwortlich.
<br><br>
<H4>F&auml;lligkeitsdatum</H4>
    Das Rezertifizierungsdatum wird errechnet aus dem letzten Rezertifizierungsdatum (falls unbekannt, wird das Erzeugungsdatum der Regel genommen)
    und dem Rezertifizierungsintervall des jeweiligen Eigent&uuml;mers.
<br><br>
<H4>Konfiguration</H4>
    Siehe auch <a href="/help/settings/recertificationgeneral">Rezertifizierungseinstellungen</a>.
	<br><br>
    Es ist m&ouml;glich, die einmal n&auml;chtliche laufende Neuberechnung der anstehenden Rezertifizierungen manuell anzusto&szlig;en. 
    <br>
	Hierzu dient die Schaltfl&auml;che Einstellungen - Weitere Einstellungen - Rezertifizierung - "Neuberechnung offene Rezertifizierungen".
');
INSERT INTO txt VALUES ('H4034', 'English', '
<H4>IP Adress Matching</H4>
 	Each rule (source and destination) is checked for overlap with the IP addresses defined for an owner.<br>
 	In case of an overlap, the owner is responsible for the recertification of the rule and a corresponding entry will be
 	generated depending on the recertification interval of the owner.
 	<br><br>
        All rules that contain network objects that cannot be assigned to an owner are automatically assigned to the super-owner.
 	<br>
    The latter is thus responsible for their recertification.
<br><br>
<H4>Due Date</H4>
   The recertification date is calculated from the last recertification date (if unknown, the creation date of the rule is taken)
    and the recertification interval defined for the respective owner. 
<br><br>
<H4CKonfiguration</H4>
    Also see <a href="/help/settings/recertificationgeneral">Recertification Settings</a>.
	<br><br>
   It is possible to manually trigger the once-a-night running recalculation of pending recertifications. 
    <br>
	The button Settings - Further settings - Recertification - "Recalculation of open recertifications" is used for this purpose.
');

INSERT INTO txt VALUES ('H4035', 'German',  '
Der initiale Owner-Import via API kann mit folgendem beispielhaften API-Befehl mit admin-Berechtigungen erfolgen:
<br><br>
<pre>
mutation addOwners($owners:[owner_insert_input!]!) {
  insert_owner(
    objects: $owners
  ) {
    returning {
      id
    }
  }
}
</pre>
<br>
Variablen
<br>
<pre>
{
  "owners": [
      {
        "name": "5",
        "recert_interval": 365,
        "dn":"x",
        "group_dn":"x",
        "app_id_external": "app-5",
        "owner_networks": {"data": [{"ip": "10.5.0.0/16"},{"ip": "10.9.0.0/16"}]}
      },
      {
        "name": "6",
        "recert_interval": 30,
        "dn":"x",
        "group_dn":"x",
        "app_id_external": "app-6",
        "owner_networks": {"data": [{"ip": "10.6.0.0/16"}]}
      },
      {
        "name": "7",
        "recert_interval": 90,
        "dn":"x",
        "group_dn":"x",
        "app_id_external": "app-7",
        "owner_networks": {"data": [{"ip": "10.7.0.0/16"}]}
      }
  ]
}
</pre>
<br><br>

Einzelne Owner k&ouml;nnen auch beispielsweise mit folgendem API-Befehl aktualisiert werden:
<br><br>
<pre>
mutation addSingleOwner {
  insert_owner(
    objects: [
      {
        name: "sechs"
        recert_interval: 222
        dn: "a"
        group_dn: "b"
        app_id_external: "app-sechs"
        owner_networks: {
          data: [{ ip: "10.69.0.0/16" }, { ip: "10.9.0.0/16" }]
          on_conflict: {
            constraint: owner_network_ip_unique
            update_columns: [ip]
          }
        }
      }
    ]
    on_conflict: {
      constraint: owner_name_unique
      update_columns: [recert_interval, dn, group_dn]
    }
  ) {
    returning {
      id
    }
  }
}
</pre>
');
INSERT INTO txt VALUES ('H4035', 'English', '
The initial owner import via API can be done using the following examplary API command (with admin permissions):
<br><br>
<pre>
mutation addOwners($owners:[owner_insert_input!]!) {
  insert_owner(
    objects: $owners
  ) {
    returning {
      id
    }
  }
}
</pre>
<br>
Variables
<br>
<pre>
{
  "owners": [
      {
        "name": "5",
        "recert_interval": 365,
        "dn":"x",
        "group_dn":"x",
        "app_id_external": "app-5",
        "owner_networks": {"data": [{"ip": "10.5.0.0/16"},{"ip": "10.9.0.0/16"}]}
      },
      {
        "name": "6",
        "recert_interval": 30,
        "dn":"x",
        "group_dn":"x",
        "app_id_external": "app-6",
        "owner_networks": {"data": [{"ip": "10.6.0.0/16"}]}
      },
      {
        "name": "7",
        "recert_interval": 90,
        "dn":"x",
        "group_dn":"x",
        "app_id_external": "app-7",
        "owner_networks": {"data": [{"ip": "10.7.0.0/16"}]}
      }
  ]
}
</pre>
<br><br>

Single owners can be updated using the following API command:
<br><br>
<pre>
mutation addSingleOwner {
  insert_owner(
    objects: [
      {
        name: "sechs"
        recert_interval: 222
        dn: "a"
        group_dn: "b"
        app_id_external: "app-sechs"
        owner_networks: {
          data: [{ ip: "10.69.0.0/16" }, { ip: "10.9.0.0/16" }]
          on_conflict: {
            constraint: owner_network_ip_unique
            update_columns: [ip]
          }
        }
      }
    ]
    on_conflict: {
      constraint: owner_name_unique
      update_columns: [recert_interval, dn, group_dn]
    }
  ) {
    returning {
      id
    }
  }
}
</pre>
');

INSERT INTO txt VALUES ('H5001', 'German',  'In diesem Abschnitt werden die Setup- und Verwaltungseinstellungen behandelt.
    Die meisten Einstellungen k&ouml;nnen nur von Nutzern mit der Administrator-Rolle gesehen und ge&auml;ndert werden.
    Der Auditor kann zwar die Einstellungen sehen, da er aber keine Schreibrechte hat, sind alle Schaltfl&auml;chen, die zu &Auml;nderungen f&uuml;hren w&uuml;rden, deaktiviert.
');
INSERT INTO txt VALUES ('H5001', 'English', 'In the settings section the setup and administration topics are handled.
    Most settings can only be seen and done by users with administrator role.
    The auditor is able to see the settings, but as he has no write permissions all buttons leading to changes are disabled.
');
INSERT INTO txt VALUES ('H5011', 'German',  'Im ersten Kapitel "Ger&auml;te" wird das Setup der Datenquellen behandelt: 
    Die Abschnitte <a href="/help/settings/managements">Managements</a> und <a href="/help/settings/gateways">Gateways</a> dienen der Definition der verbundenen Hardware.
    Hinzu kommt die Verwaltung der <a href="/help/settings/credentials">Import-Zugangsdaten</a>.
');
INSERT INTO txt VALUES ('H5011', 'English', 'In the first chapter "Devices" the setup of the report data sources is done:
    The sections <a href="/help/settings/managements">Managements</a> and <a href="/help/settings/gateways">Gateways</a> are for the definition of the connected hardware.
    Additionally there is the administration of the <a href="/help/settings/credentials">Import Credentials</a>.
');
INSERT INTO txt VALUES ('H5012', 'German',  'Das Kapitel "Berechtigungen" bietet die Funktionalit&auml;t f&uuml;r die Nutzerverwaltung:
    In <a href="/help/settings/ldap">LDAP-Verbindungen</a> k&ouml;nnen externe Verbindungen zus&auml;tzlich zum internen LDAP definiert werden.
    <a href="/help/settings/tenants">Mandanten</a> k&ouml;nnen definiert und mit spezifischen Gateways verkn&uuml;pft werden.
    Interne oder externe <a href="/help/settings/users">Nutzer</a> k&ouml;nnen zu <a href="/help/settings/groups">Gruppen</a> zusammengefasst
    und zu <a href="/help/settings/roles">Rollen</a> zugeordnet werden, ausserdem gibt es eine &Uuml;bersicht der vorhandenen <a href="/help/settings/owners">Eigent&uuml;mer</a>.
');
INSERT INTO txt VALUES ('H5012', 'English', 'The chapter "Authorization" offers the functionality for the user administration:
    In <a href="/help/settings/ldap">LDAP Connections</a> external connections besides the internal LDAP can be defined.
    <a href="/help/settings/tenants">Tenants</a> can be defined and associated with specific gateways.
    Internal or external <a href="/help/settings/users">Users</a> can be assigned to <a href="/help/settings/groups">User Groups</a>
    and <a href="/help/settings/roles">Roles</a>, additionally there is an overview of the <a href="/help/settings/owners">owners</a>.
');
INSERT INTO txt VALUES ('H5013', 'German',  'Im Kapitel "Voreinstellungen" kann der Administrator <a href="/help/settings/defaults">Standardeinstellungen</a> vornehmen,
    die f&uuml;r alle Nutzer gelten, sowie die <a href="/help/settings/email">Email-</a>, <a href="/help/settings/importer">Importer-</a> und
    <a href="/help/settings/passwordpolicy">Passworteinstellungen</a> definieren. Hinzu kommen die modulspezifischen 
    <a href="/help/settings/recertificationgeneral">Allgemeinen Rezertifizierungs-</a> und <a href="/help/settings/modelling">Modellierungseinstellungen</a>.
');
INSERT INTO txt VALUES ('H5013', 'English', 'In the "Defaults" chapter the administrator can define <a href="/help/settings/defaults">Default Values</a> applicable to all users
    and define <a href="/help/settings/email">email-</a>, <a href="/help/settings/importer">importer-</a> and  <a href="/help/settings/passwordpolicy">Password Policy</a> settings.
    Additionally there are the module specific <a href="/help/settings/recertificationgeneral">General Recertification</a> and <a href="/help/settings/modelling">Modelling Settings</a>.
');
INSERT INTO txt VALUES ('H5014', 'German',  'Das Kapitel "Pers&ouml;nlich" ist f&uuml;r alle Nutzer zug&auml;nglich. Hier k&ouml;nnen das individuelle <a href="/help/settings/password">Password</a>,
    die bevorzugte <a href="/help/settings/language">Sprache</a> und <a href="/help/settings/report">Reporting</a>-Einstellungen gesetzt werden.
    Nutzer mit Rezertifizierer-Rolle k&ouml;nnen auch ihre <a href="/help/settings/recertificationpersonal">Rezertifizierungseinstellungen</a> anpassen.
');
INSERT INTO txt VALUES ('H5014', 'English', 'The "Personal" chapter is accessible by all users, where they can set their individual <a href="/help/settings/password">Password</a>,
    <a href="/help/settings/language">Language</a> and <a href="/help/settings/report">Reporting</a> preferences. 
    Users with recertifier role have also the possibility to adjust their <a href="/help/settings/recertificationpersonal">Recertification Setting</a>.
');
INSERT INTO txt VALUES ('H5015', 'German',  'Das Kapitel "Workflow" dient dem Administrator, einen Workflow aufzusetzen. Dazu geh&ouml;rt die Definition der angebotenen <a href="/help/settings/stateactions">Aktionen</a>,
    der verwendeten <a href="/help/settings/statedefinitions">Stati</a> und den Status&uuml;berg&auml;ngen in den zentralen <a href="/help/settings/statematrix">Status-Matrizen</a>.
    In den <a href="/help/settings/workflowcustomizing">Einstellungen</a> k&ouml;nnen allgemeine Voreinstellungen zu den Workflows vorgenommen werden.
');
INSERT INTO txt VALUES ('H5015', 'English', 'The "Workflow" chapter helps the administrator to set up a workflow. This includes the definition of the offered <a href="/help/settings/stateactions">actions</a>,
    the used <a href="/help/settings/statedefinitions">states</a>, and the state transitions in the central <a href="/help/settings/statematrix">state matrices</a>. 
    In <a href="/help/settings/workflowcustomizing">customizing</a> general workflow settings can be done.
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
                <li>FortiGate stand-alone (via REST API)</li>
                <li>FortiManager 5ff - FortiManager. F&uuml;r diesen Management-Typ kann die komplette Struktur (ADOM, FortiGateway Devices) mittels AutoDiscovery automatisch ausgelesen werden.</li>
                <li>Palo Alto Firewalls (nicht Panorama)</li>
                <li>Azure Firewall</li>
                <li>Cisco FirePower Management Center</li>
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
                <li>FortiGate StandAlone (via REST API)</li>                
                <li>FortiManager 5ff - FortiManager - for this management type the complete infrastructure (ADOM, FortiGateway devices) can be auto discovered.</li>
                <li>Palo Alto Firewalls (not Panorama)</li>
                <li>Azure Firewall</li>
                <li>Cisco FirePower Management Center</li>
            </ul>
        </li>            
    </ul>
');
INSERT INTO txt VALUES ('H5103', 'German',  'F&uuml;r Firewallgateways ohne separates Management oder im Falle, dass das zentrale Management nicht in den Firewall Orchestrator eingebunden werden kann,
    werden die Details des Gateways als Management und gleichzeitig auch als Gateway eingetragen.<br>
    Im Falle Fortigate Legacy (via ssh): Um einen vollst&auml;ndigen Datenimport zu gew&auml;hrleisten, bitte in der Fortigate config den Seitenumbruch deaktivieren, damit beim Kommando "show full-configuration" die komplette Config ausgegeben wird.
');
INSERT INTO txt VALUES ('H5103', 'English', 'For firewall gateways without a separate management or in case the central management cannot be integrated into Firewall Orchestrator 
    you may enter the details of the gateway here as a management system as well and then add it again as a gateway.<br>
    In the case of legacy Fortigate (via ssh): To get the entire data imported, disable pagination in the Fortigate config to allow get command "show full-configuration" to retrieve the complete config.
');
INSERT INTO txt VALUES ('H5104', 'German',  'Wenn Beispieldaten (definiert durch die Endung "_demo" vom Namen) existieren, wird eine Schaltfl&auml;che angezeigt, um diese und alle verkn&uuml;pften <a href="/help/settings/gateways">Gateways</a> zu l&ouml;schen.');
INSERT INTO txt VALUES ('H5104', 'English', 'If there are sample data (defined by the ending "_demo" of the name), a button is displayed to delete them and all related <a href="/help/settings/gateways">gateways</a>.');
INSERT INTO txt VALUES ('H5111', 'German',  'Name*: Name des Managements. <br>
    F&uuml;r die meisten Firewalls ist dies ein willk&uuml;rlicher Name. Ausnahmen sind direkt verbundene Gateways von Fortigate, Netscreen und Juniper.
    Hier muss der Name des Firewallgateways eingetragen werden.<br>
    Da es zu Problemen mit dem perl-Importer kommen kann, sollten Leerzeichen im Namen von Legacy-Systemen nicht verwendet werden.
    Ein Management dessen Name mit "_demo" endet, wird beim Bet&auml;tigen der "Beispieldaten l&ouml;schen"-Schaltfl&auml;che gel&ouml;scht.
');
INSERT INTO txt VALUES ('H5111', 'English', 'Name*: Name of the mangement. <br>
    For most firewalls this is an arbitrary name. Exceptions are Fortigate, Netscreen and Juniper directly connected gateways.
    Here the name give needs to be the name of the firewall gateway.<br>
    Do not use spaces in the management name of legacy systems as perl importer cannot cope with spaces here.
    A management whose name ends with "_demo" will be deleted when using the "Remove Sample Data" button.
');
INSERT INTO txt VALUES ('H5112', 'German',  'Kommentar: Optionale Beschreibung des Managements.');
INSERT INTO txt VALUES ('H5112', 'English', 'Comment: Optional description of this management.');
INSERT INTO txt VALUES ('H5113', 'German',  'Ger&auml;tetyp*: bitte das korrekte Produkt von der Liste ausw&auml;hlen (siehe oben)');
INSERT INTO txt VALUES ('H5113', 'English', 'Device Type*: Select correct product from a list of available types, see above.');
INSERT INTO txt VALUES ('H5114', 'German',  'Hostname*: Adresse des Hosts (entweder IP-Addresse oder aufl&ouml;sbarer Name).<br>
    Wenn die native Konfiguration eines Firewall-Systems als JSON-Datei zu Testzwecken eingelesen werden soll, ist hier die URI in einem der folgenden Formate anzugeben:<br>
    https://..., http://..., file://...<br>
    F&uuml;r Check Point R8x MDS Installationen die Addresse des MDS-Servers f&uuml;r alle Domains benutzen.<br>
    F&uuml;r alle Firewall-Plattformen, die kein separates Management-System besitzen, muss die IP oder der aufl&ouml;sbare Name des Firewallgateways spezifiziert werden.
');
INSERT INTO txt VALUES ('H5114', 'English', 'Hostname*: Address of the host (either IP address or resolvable name).<br>
    For reading the native firewall config from a JSON file (for testing purposes), enter the URI of the file (https://..., http://..., file://...)<br>
    For Check Point R8x MDS installations use the address of the MDS server for all domains.<br>
    For all firewall platforms which do not possess a separate management, use the IP address or the resolvable name of the firewall gateway.
');
INSERT INTO txt VALUES ('H5115', 'German',  'Port*: Port-Nummer des Hosts.<br>
    Wenn das Ziel Check Point R8x, FortiManager, Azure oder Cisco FirePower ist, wird die Verbindung via API aufgebaut. Die Standard-Port-Nummer ist 443. Denken Sie daran, den API-Zugang auf Ihrem Firewall Managment zu aktivieren.<br>
    Wenn das Ziel eine andere Plattform ist, braucht Firewall Orchestrator einen ssh-basierten Zugang. Die Standard-Port-Nummer ist in diesem Fall 22.
');
INSERT INTO txt VALUES ('H5115', 'English', 'Port*: Port number of the host.<br>
    If the target is Check Point R8x, FortiManager, Azure or Cisco FirePower the connection is established via API. The default port number is 443. Remember to enable API access on your firewall managment.<br>
    If the target any other platform Firewall Orchestrator needs ssh-based access. The default port number here is 22.
');
INSERT INTO txt VALUES ('H5116', 'German',  'Login-Daten*: Zugangsdaten f&uuml;r den Import-Nutzer des Managements.<br>
    Hier kann ein Satz <a href="/help/settings/credentials">Zugangsdaten</a> ausgew&auml;hlt werden, der zum Login auf dem Management dient.
');
INSERT INTO txt VALUES ('H5116', 'English', 'Import Credentials*: User/Password combination for logging into the management.<br>
    Choose a set of <a href="/help/settings/credentials">credentials</a> which will be used to get the management''s configuration.
');
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

INSERT INTO txt VALUES ('H5130', 'German',  'Hier werden die Zugangsdaten f&uuml;r den Import der Firewall-Konfigurationen verwaltet.
Diese k&ouml;nnen auch f&uuml;r den Zugriff auf mehrere Firewall-Managements verwendet werden.
Ein L&ouml;schen ist erst m&ouml;glich, wenn die Zugangsdaten nirgends mehr verwendet werden.
    <br>
    F&uuml;r den FortiGate Stand-Alone Import via REST API: 
    <ol>
        <li>Im FortiGate Web Interface: Erstelle ein Read Only Admin Profile z.B. "ro_admin"</li>
        <li>Im FortiGate Web Interface: Erstelle einen "REST API Admin" e.g. "fworch" mit "ro_admin" Profil und kopiere den API Schl&uuml;ssel</li>
        <li>In der Firewall Orchestrator WebUI: erstelle neue Import Login-Daten mit username "fworch" und Passwort = API Schl&uuml;ssel</li>
    </ol>
');
INSERT INTO txt VALUES ('H5130', 'English', 'Manage credentials for importing firewall configuration data.
Credentials can be used for logging in to one or multiple firewall managements.
Credentials can only be deleted when they are not used for importing any management.
    <br>
    For FortiGate stand-alone import via REST API:
    <ol>
        <li>In FortiGate Web UI: Create Read Only Admin Profile e.g. "ro_admin"</li>
        <li>In FortiGate Web UI: Create new "REST API Admin" e.g. "fworch" with "ro_admin" profile and copy API key</li>
        <li>In Firewall Orchestrator UI create new credentials with username "fworch" and password = API key</li>
    </ol>
');
INSERT INTO txt VALUES ('H5131', 'German',  'Name*: Ein beliebiger Name, der diese Zugangsdaten eindeutig beschreibt.
');
INSERT INTO txt VALUES ('H5131', 'English', 'Name*: An arbitrary name you can assign to your credetials.
');
INSERT INTO txt VALUES ('H5132', 'German',  'Import Nutzer*: Der Nutzer, der zum Anmelden am Firewall Management benutzt wird.
    Er muss vorher auf dem Firewallsystem angelegt sein und vollen Lesezugriff auf das System besitzen.<br>
    Auf Check Point R8x wird empfohlen, das vordefinierte "Read Only All"-Profil (sowohl globales als auch Domainmanagement) zu verwenden.
');
INSERT INTO txt VALUES ('H5132', 'English', 'Username*: The user used to login to the firewall management. 
    This user needs to be created on the firewall system in advance and needs full read access to the system.<br>
    On Check Point R8x we recommend using the predefined "Read Only All" profile (both global and domain management) for the user.
');
INSERT INTO txt VALUES ('H5133', 'German',  'Privater Schl&uuml;ssel* / Passwort*: F&uuml;r den ssh-Zugang hier den privaten ssh-Schl&uuml;ssel hinterlegen (Schl&uuml;ssel muss unverschl&uuml;sselt und ohne Passphrase sein).
    Bitte f&uuml;r ssh-basierten legacy FortiGate Zugriff kein RSA benutzen, da es hier ein Problem mit RSA-Schl&uuml;sseln zu geben scheint.<br>
    F&uuml;r den API-Zugang ist hier das Passwort des API-Nutzers einzutragen.
');
INSERT INTO txt VALUES ('H5133', 'English', 'Login Secret* / Password*: For ssh access enter the private ssh key (key needs to be unencrypted without passphrase).
    For legacy ssh based FortiGate, do not use RSA as there seems to be a problem with RSA keys.<br>
    For API access insert the password of the API user here.
');
INSERT INTO txt VALUES ('H5134', 'German',  '&Ouml;ffentlicher Schl&uuml;ssel: Dieses Feld muss nur f&uuml;r Netscreen-Firewalls gef&uuml;llt werden - dieses System ben&ouml;tigt auch den &ouml;ffentlichen Schl&uuml;ssel zum Anmelden.');
INSERT INTO txt VALUES ('H5134', 'English', 'Public Key: This field only needs to be filled for netscreen firewalls - this system also needs the public key for successful login.');
INSERT INTO txt VALUES ('H5135', 'German',  'Schl&uuml;sselpaar*: Handelt es sich bei diesen Login-Daten um ein SSH Public-Key Paar oder um ein Standard-Passwort.
');
INSERT INTO txt VALUES ('H5135', 'English', 'Key Pair*: Do these credentials consist of a private/public SSH key pair or do they contain a standard password.
');
INSERT INTO txt VALUES ('H5136', 'German',  'Cloud Client ID: Nur f&uuml;r Cloud Instanzen (Azure) ben&ouml;tigt - f&uuml;r alle anderen Plattformen kann dieses Feld leer gelassen werden.
');
INSERT INTO txt VALUES ('H5136', 'English', 'Cloud Client ID: If you have a cloud installation (e.g. Azure) - enter your Azure client ID here. For all other installations, leave this field empty.
');
INSERT INTO txt VALUES ('H5137', 'German',  'Cloud Client Secret: Nur f&uuml;r Cloud Instanzen (Azure) ben&ouml;tigt - f&uuml;r alle anderen Plattformen kann dieses Feld leer gelassen werden.
');
INSERT INTO txt VALUES ('H5137', 'English', 'Cloud Client Secret: If you have a cloud installation (e.g. Azure) - enter your Azure client secret here. For all other installations, leave this field empty.
');

INSERT INTO txt VALUES ('H5141', 'German',  'Admins k&ouml;nnen mehrere unterschiedliche Gateways einrichten und verwalten.<br>
    Die "Klonen"-Schaltfl&auml;che unterst&uuml;tzt beim Definieren eines neuen Gateways, indem Daten von einem existierenden kopiert werden.
    Vor dem Speichern muss sich mindestens einer der Parameter Ger&auml;tetyp, Management oder Rulebase von den existierenden Gateways unterscheiden, wenn die Auswahl "Import Deaktiviert" nicht gesetzt ist.
');
INSERT INTO txt VALUES ('H5141', 'English', 'Admins can create and administrate several different gateways.<br>
    The clone button helps defining new gateways by copying the data from existing ones.
    Before saving at least one of the parameters Device Type, Management or Rulebase has to be different from the existing gateways if the Import Disabled flag is not set.
');
INSERT INTO txt VALUES ('H5151', 'German',  'Name*: Name des Gateways. F&uuml;r Legacy Fortinet (ssh) muss dies der reale Name des Firewallgateways sein wie in der Config definiert.');
INSERT INTO txt VALUES ('H5151', 'English', 'Name*: Name of the Gateway. For legacy Fortinet (ssh) this must be the real name of the firewall gateway as defined in the config.');
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
        <li>F&uuml;r Legacy Fortinet-Systeme muss jedes Gateway (auch jede vdom) als separates Management mit einem einzelnen Gateway eingeragen werden.
            Bei vdoms sind sowohl Management-Name, Gateway-Name als auch Regelwerksname wie folgt zu bilden: Systemname___vdom-Name (Trennzeichen: 3x Unterstrich)</li>
        <li>Im Falle von FortiGate Stand-Alone (Import via REST API) wird dieses Feld automatisch ausgef&uuml;llt.</li>
    </ul>
');
INSERT INTO txt VALUES ('H5155', 'English', 'Local Rulebase* / Local Package*: Enter the name of the rulebase here. 
    <ul>
        <li>For Check Point R8x the top level access layer name goes here (default is "Network").</li>
        <li>For Check Point R8x MDS enter the name of the global policy layer followed by the name of the domain policy separated by "/", e.g. "global-policy-layer-name/domain-policy-layer-name".</li>
        <li>For legacy Fortinet systems every gateway (and every vdom) must be defined as a separate management system with a single gateway.
            When dealing with vdoms set management name, gateway name and rulebase name as follows: system name___vdom name (separator: 3x underscore)</li>
        <li>For FortiGatte stand-alne (import via REST API) this field is filled-in automatically.</li>
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
INSERT INTO txt VALUES ('H5181', 'German',  'Aktualisieren: Aktualisiert die dargestellten Daten.');
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
INSERT INTO txt VALUES ('H5214', 'English', 'Tenant Level: If tenants are part of the distinguished name (Dn) of the user, this number defines the level in the path, where they are found.
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
INSERT INTO txt VALUES ('H5249', 'German',  'IP-Adressen: Im Editiermodus k&ouml;nnen einem Mandanten (ausser dem Globalen Mandanten) zwecks Filterung IP-Adressen zugeordnet werden.');
INSERT INTO txt VALUES ('H5249', 'English', 'IP Addresses: In the edit mode IP addresses can be assigned to the tenant (except global tenant) for the purpose of filtering.');
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
INSERT INTO txt VALUES ('H5279', 'German',  'Von LDAP: Ldap, in dem der Nutzer angelegt ist. Dies kann sowohl das interne, als auch ein in den <a href="/help/settings/ldap">Ldap-Einstellungen</a> definiertes externes Ldap sein.');
INSERT INTO txt VALUES ('H5279', 'English', 'From LDAP: Ldap, where the user is registered. This can be the internal as well as an external Ldap as defined in the <a href="/help/settings/ldap">Ldap Settings</a>.');
INSERT INTO txt VALUES ('H5280', 'German',  'In LDAP: Ldap, in dem der Nutzer angelegt werden soll. Angeboten werden sowohl das interne, als auch alle in den <a href="/help/settings/ldap">Ldap-Einstellungen</a> definierten externen Ldaps, in denen Schreibrechte bestehen.');
INSERT INTO txt VALUES ('H5280', 'English', 'Into LDAP: Ldap, where the user should be registered. Offered are the internal as well as all external Ldaps as defined in the <a href="/help/settings/ldap">Ldap Settings</a>, where write permissions are given.');
INSERT INTO txt VALUES ('H5301', 'German',  'Der Admin kann Nutzergruppen im internen Ldap definieren. Dabei besteht die M&ouml;glichkeit, sie gleich einer Rolle zuzuordnen.
    Weitere Rollenzuordnungen k&ouml;nnen dann unter <a href="/help/settings/roles">Rollen</a> erfolgen.<br>
');
INSERT INTO txt VALUES ('H5301', 'English', 'Groups of users can be defined by the admin in the internal Ldap. When adding there is the possibility to assign a role membership.
    Further memberships can be administrated in the <a href="/help/settings/roles">roles</a> section.<br>
');
INSERT INTO txt VALUES ('H5302', 'German',  'Wenn Beispieldaten (definiert durch die Endung "_demo" vom Gruppennamen) existieren, wird eine Schaltfl&auml;che angezeigt, um diese zu l&ouml;schen.
    Die L&ouml;schung ist nicht m&ouml;glich, wenn Nutzer, die nicht als Beispielnutzer gekennzeichnet sind (Name endet nicht auf "_demo"), der Gruppe zugeordnet sind.
');
INSERT INTO txt VALUES ('H5302', 'English', 'If there are sample data (defined by the ending "_demo" of the group name), a button is displayed to delete them.
    The deletion is only possible, if there are no non-sample users (user name not ending with "_demo") assigned to the group.
');
INSERT INTO txt VALUES ('H5311', 'German',  'Gruppenaktionen: Hier k&ouml;nnen selbstdefinierte Gruppen ge&auml;ndert (zur Zeit nur umbenannt) oder gel&ouml;scht werden.');
INSERT INTO txt VALUES ('H5311', 'English', 'Group actions: Here is the possibility to edit (currently only rename) or delete self defined user groups.');
INSERT INTO txt VALUES ('H5312', 'German',  'Nutzeraktionen: Hier k&ouml;nnen dem System bekannte Nutzer (siehe <a href="/help/settings/users">Nutzereinstellungen</a>) oder aus einem zu durchsuchenden Ldap der Gruppe zugeordnet bzw. von dieser entfernt werden.');
INSERT INTO txt VALUES ('H5312', 'English', 'User actions: Here users known to the system (see <a href="/help/settings/users">User settings</a>) or searched from an Ldap can be assigned to resp. removed from the user groups.');
INSERT INTO txt VALUES ('H5313', 'German',  'Name: Name der Nutzergruppe.');
INSERT INTO txt VALUES ('H5313', 'English', 'Name: Name of the user group.');
INSERT INTO txt VALUES ('H5314', 'German',  'Nutzer: Liste der der Gruppe zugeordneten Nutzer.');
INSERT INTO txt VALUES ('H5314', 'English', 'Users: List of assigned users to the group.');
INSERT INTO txt VALUES ('H5315', 'German',  'Eigent&uuml;mergruppe: Kann f&uuml;r die Eigent&uuml;merverwaltung beim Rezertifizieren oder Modellieren verwendet werden.');
INSERT INTO txt VALUES ('H5315', 'English', 'Owner Group: Can be used for owner administration in recertification or modelling modules.');
INSERT INTO txt VALUES ('H5331', 'German',  'Alle definierten Rollen werden mit einer kurzen Erkl&auml;rung dargestellt.<br>
    Der Admin kann Nutzer oder Nutzergruppen den Rollen zuweisen bzw. von diesen entfernen.
');
INSERT INTO txt VALUES ('H5331', 'English', 'All defined roles in the system are displayed with a short explanation.<br>
    The admin can assign or remove users or user groups to/from the roles.
');
INSERT INTO txt VALUES ('H5332', 'German',  'Die verf&uuml;gbaren Rollen k&ouml;nnen in mehrere Kategorien eingeteilt werden:');
INSERT INTO txt VALUES ('H5332', 'English', 'The provided roles can be divided into several categories:');
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
INSERT INTO txt VALUES ('H5361', 'German',  'Reporting, Modellierung und Rezertifizierung (regelbasiert): reporter, reporter-viewall, modeller, recertifier');
INSERT INTO txt VALUES ('H5361', 'English', 'Reporting, modelling and recertification (rule based): reporter, reporter-viewall, modeller, recertifier');
INSERT INTO txt VALUES ('H5362', 'German',  'Workflow: requester, approver, planner, implementer, reviewer');
INSERT INTO txt VALUES ('H5362', 'English', 'Workflow: requester, approver, planner, implementer, reviewer');
INSERT INTO txt VALUES ('H5363', 'German',  '&Uuml;bergeordnete Rollen: admin, fw-admin, auditor, (anonymous)');
INSERT INTO txt VALUES ('H5363', 'English', 'Superordinate roles: admin, fw-admin, auditor, (anonymous)');
INSERT INTO txt VALUES ('H5364', 'German',  'Technische Rollen: importer, dbbackup, middleware-server');
INSERT INTO txt VALUES ('H5364', 'English', 'Technical roles: importer, dbbackup, middleware-server');
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
INSERT INTO txt VALUES ('H5412', 'German',  'UI - Pro Abruf geholte Elemente: Definiert die (maximale) Anzahl der Objekte, die bei der Reporterzeugung und beim Aufbau der rechten Randleiste in einem Schritt geholt werden.
    Dies kann genutzt werden, um die Performanz zu optimieren, wenn n&ouml;tig.
');
INSERT INTO txt VALUES ('H5412', 'English', 'UI - Elements per fetch: Defines the (maximum) number of objects which are fetched in one step for the report creation and the build up of the right sidebar.
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
INSERT INTO txt VALUES ('H5416', 'German',  '&Auml;nderungsbenachrichtigung via Email:');
INSERT INTO txt VALUES ('H5416', 'English', 'Change notification via email:');
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
    Die Nutzer-Meldungen k&ouml;nnen auch danach noch im Monitoring unter <a href="/help/monitoring/ui_messages">UI-Nachrichten</a> eingesehen werden.
');
INSERT INTO txt VALUES ('H5423', 'English', 'Message view time (in seconds): defines how long success messages are displayed, until they fade out automatically.
    Error messages are displayed 3 times as long. Value 0 means that the messages do not fade out.
    All user messages can still be reviewed in the monitoring tab at <a href="/help/monitoring/ui_messages">UI Messages</a>.
');
INSERT INTO txt VALUES ('H5424', 'German',  'Startzeit t&auml;glicher Check: legt die Zeit fest, wann der t&auml;gliche Check durchgef&uuml;hrt werden soll.');
INSERT INTO txt VALUES ('H5424', 'English', 'Daily check start at: defines the time when the daily check should happen.');
INSERT INTO txt VALUES ('H5426', 'German',  'Autodiscover-Intervall (in Stunden): legt das Intervall fest, in dem die Autodiscovery durchgef&uuml;hrt werden soll.');
INSERT INTO txt VALUES ('H5426', 'English', 'Auto-discovery sleep time (in hours): defines the interval in which the autodiscovery should be performed.');
INSERT INTO txt VALUES ('H5427', 'German',  'Autodiscover-Start: legt eine Bezugszeit fest, ab dem die Intervalle f&uuml;r die Autodiscovery gerechnet werden.');
INSERT INTO txt VALUES ('H5427', 'English', 'Auto-discovery start at: defines a referential time from which the autodiscovery intervals are calculated.');
INSERT INTO txt VALUES ('H5428', 'German',  'Rezert Check - aktiv: aktviere bzw. deaktiviere regelm&auml;&szlig;ige Pr&uuml;fungen zur Versendung von Benachrichtigungs- oder Eskalations-Emails an die Eigent&uuml;mer.');
INSERT INTO txt VALUES ('H5428', 'English', 'Recert Check - active: enable or disable recurring recertification checks to send out notification or escalation emails to owners.');
INSERT INTO txt VALUES ('H5429', 'German',  'Rezert Check alle: Abstand der Pr&uuml;fungen f&uuml;r den Versand von Benachrichtigungs- oder Eskalations-Emails an die Eigent&uuml;mer.');
INSERT INTO txt VALUES ('H5429', 'English', 'Recert Check every: Interval between checks for recertification notifications.');
INSERT INTO txt VALUES ('H5430', 'German',  'Rezert Check - Email Titel: Titel der Benachrichtigungs-Email.');
INSERT INTO txt VALUES ('H5430', 'English', 'Recert Check - Email subject: Subject line of the notification email.');
INSERT INTO txt VALUES ('H5431', 'German',  'Der Administrator kann Vorgaben f&uuml;r Passw&ouml;rter definieren, gegen die alle neuen Passw&ouml;rter aller (internen) Nutzer gepr&uuml;ft werden.');
INSERT INTO txt VALUES ('H5431', 'English', 'The admin user can define a password policy, against which all new passwords of all (internal) users are checked.');
INSERT INTO txt VALUES ('H5432', 'German',  'Rezert Check - Text &uuml;berf&auml;llig: Textinhalt der Benachrichtigungsmail bei &uuml;berf&auml;lligen Rezertifizierungen (Eskalation).');
INSERT INTO txt VALUES ('H5432', 'English', 'Recert Check - text overdue: Email body of the notification email for overdue recertifications (escalation).');

INSERT INTO txt VALUES ('H5433', 'German',  'Autom. Anlegen L&ouml;schantrag: Soll automatisch ein L&ouml;sch-Ticket erzeugt werden, wenn eine Regel vollst&auml;ndig dezertifiziert wurde?');
INSERT INTO txt VALUES ('H5433', 'English', 'Autocreate delete rule ticket: When a rule has been fully de-certified, should a delete ticket be automatically generated?');
INSERT INTO txt VALUES ('H5434', 'German',  'Titel f&uuml;r L&ouml;schantrag: Titel des zu erzeugenden L&ouml;sch-Tickets.');
INSERT INTO txt VALUES ('H5434', 'English', 'Title delete ticket: Subject line of the delete ticket to be generated.');
INSERT INTO txt VALUES ('H5435', 'German',  'Grund f&uuml;r L&ouml;schantrag: Text f&uuml;r den Grund des zu erzeugenden L&ouml;sch-Tickets.');
INSERT INTO txt VALUES ('H5435', 'English', 'Reason delete ticket: Text for the reason of the delete ticket to be generated.');
INSERT INTO txt VALUES ('H5436', 'German',  'Titel f&uuml;r L&ouml;schauftrag: Titel der zu erzeugenden L&ouml;schaufgabe.');
INSERT INTO txt VALUES ('H5436', 'English', 'Title delete rule task: Title of the delete task to be generated.');
INSERT INTO txt VALUES ('H5437', 'German',  'Grund f&uuml;r L&ouml;schauftrag: Begr&uuml;ndungstext f&uuml;r die zu erzeugende L&ouml;schaufgabe.');
INSERT INTO txt VALUES ('H5437', 'English', 'Reason for delete rule task: Text for the reason of the delete task to be generated.');
INSERT INTO txt VALUES ('H5438', 'German',  'Priorit&auml;t f&uuml;r L&ouml;schantrag: Auswahl zwischen den Prio-Bezeichnungen wie in den <a href="/help/settings/workflowcustomizing">Workflow-Einstellungen</a> definiert (Vorgabe: Lowest, Low, Medium, High, Highest).');
INSERT INTO txt VALUES ('H5438', 'English', 'Priority for delete rule ticket: Choose between existing priority labels as defined in <a href="/help/settings/workflowcustomizing">workflow customizing</a>(default: Lowest, Low, Medium, High, Highest).');
INSERT INTO txt VALUES ('H5439', 'German',  'Initialer Status f&uuml;r L&ouml;schantrag: Standard="Draft"');
INSERT INTO txt VALUES ('H5439', 'English', 'Initial state for delete rule ticket: default="Draft"');
INSERT INTO txt VALUES ('H5440', 'German',  'Neuberechnen offene Rezertifizierungen: Auswahl, wann die Neuberechnung durchgef&uuml;hrt werden soll - beim Hochfahren, t&auml;glich via Scheduler oder jetzt (kann mehrere Minuten dauern).');
INSERT INTO txt VALUES ('H5440', 'English', 'Recalculate open recertifications: Choose, when to do this: at startup, daily via scheduler or now (this may take several minutes).');

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
INSERT INTO txt VALUES ('H5446', 'German',  'Rezert Check - Text anstehend: Textinhalt der Benachrichtigungsmail bei demn&auml;chst anstehenden Rezertifizierungen.');
INSERT INTO txt VALUES ('H5446', 'English', 'Recert Check - text upcoming: Email body of the notification email for upcoming recertifications.');
INSERT INTO txt VALUES ('H5447', 'German',  'Als unbenutzt gewertet nach (in Tagen): Gibt den Zeitpunkt an, vor dem die letzte Nutzung der Regel f&uuml;r den Unbenutzte-Regel-Report in der Vergangenheit liegen muss.');
INSERT INTO txt VALUES ('H5447', 'English', 'Regarded as unused from (in days): Defines the point in time, before which the last usage has to be in the past for the Unused Rules Report.');
INSERT INTO txt VALUES ('H5448', 'German',  'Toleranz ab Erzeugungsdatum (in Tagen): Noch niemals benutzte Regeln werden im Unbenutzte-Regel-Report nur ber&uuml;cksichtigt, wenn sie vor dem durch den hier definierten Toleranzwert festgelegten Zeitpunkt erzeugt wurden.');
INSERT INTO txt VALUES ('H5448', 'English', 'Tolerance from creation date (in days): Never used rules are only regarded in the Unused Rules Report, if they have been created before the point in time defined by this tolerance value.');
INSERT INTO txt VALUES ('H5449', 'German',  'Sitzungs-Timeout (in Minuten): Zeit, nach der ein Nutzer automatisch aus der Sitzung ausgeloggt wird.');
INSERT INTO txt VALUES ('H5449', 'English', 'Session timeout (in minutes): Time after which a user is logged out automatically.');
INSERT INTO txt VALUES ('H5450', 'German',  'Benachrichtigung vor Sitzungs-Timeout (in Minuten): Intervall vor dem automatischen Logout, in dem eine Warnung ausgegeben wird.');
INSERT INTO txt VALUES ('H5450', 'English', 'Warning before session timeout (in minutes): Interval before automatic logout when a warning message is displayed.');
INSERT INTO txt VALUES ('H5451', 'German',  'Jeder Nutzer (ausser Demo-Nutzer) kann sein eigenes Passwort &auml;ndern.<br>
    Bitte das alte Passwort einmal und das neue Passwort zweimal eingeben, um Eingabefehler zu vermeiden.
    Das neue Passwort muss sich vom alten unterscheiden und wird gegen die <a href="/help/settings/passwordpolicy">Passworteinstellungen</a> gepr&uuml;ft.
');
INSERT INTO txt VALUES ('H5451', 'English', 'Every user (except demo user) can change his own password.<br>
    Please insert the old password once and the new password twice to avoid input mistakes.
    The new password has to be different from the old one and is checked against the <a href="/help/settings/passwordpolicy">Password Policy</a>.
');
INSERT INTO txt VALUES ('H5452', 'German',  'Max erlaubte Importdauer (in Stunden): Obergrenze, welche Importdauer im t&auml;glichen Check noch als akzeptabel gewertet wird.');
INSERT INTO txt VALUES ('H5452', 'English', 'Max allowed import duration (in hours): Upper limit for the accepted import duration in the daily check.');
INSERT INTO txt VALUES ('H5453', 'German',  'Max erlaubtes Importintervall (in Stunden): Obergrenze, welcher Abstand zwischen zwei Imports im t&auml;glichen Check noch akzeptiert wird.');
INSERT INTO txt VALUES ('H5453', 'English', 'Max import interval (in hours): Upper limit for the accepted interval between two imports in the daily check.');
INSERT INTO txt VALUES ('H5454', 'German',  'Regel-Eigent&uuml;merschaftsmodus: (Gemischt/Exklusiv) Wird z. Zt. nicht genutzt.');
INSERT INTO txt VALUES ('H5454', 'English', 'Rule Ownership Mode: (Mixed/Exclusive) Currently not in use.');

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
INSERT INTO txt VALUES ('H5483', 'German',  '&Auml;nderungsbenachrichtigung aktiv: Sollen Emails bei festgestellten &Auml;nderungen versendet werden, ist diese 
    Einstellung zu aktivieren. Default-Wert = "inaktiv".
');
INSERT INTO txt VALUES ('H5483', 'English', 'Change notification active?: When an import finds security relevant changes, should an email be sent out?
    Default value = "inactive".
');
INSERT INTO txt VALUES ('H5484', 'German',  '&Auml;nderungsbenachrichtigungstyp: Art und Umfang, in dem die &Auml;nderungsbenachrichtigung gesendet werden soll:
    <ul>
        <li>Einfacher Text (kein &Auml;nderungsreport): Es wird nur der hier definierte Text der &Auml;nderungsbenachrichtigung gesendet.</li>
        <li>Html in Email: Ein Changes Report wird zu den im Import gefundenen &Auml;nderungen erstellt und in der email als Html versendet.</li>
        <li>Pdf als Anhang: Ein Changes Report wird erstellt und der email als Pdf-Datei angeh&auml;ngt.</li>
        <li>Html als Anhang: Ein Changes Report wird erstellt und der email als Html-Datei angeh&auml;ngt.</li>
        <li>Json als Anhang: Ein Changes Report wird erstellt und der email als Json-Datei angeh&auml;ngt.</li>
    </ul>
');
INSERT INTO txt VALUES ('H5484', 'English', 'Change notification type: Defines how and with which content the notification should be sent:
    <ul>
        <li>Simple Text (no Change Report): Only the body of change notification emails as defined below is sent.</li>
        <li>Html in email body: A Changes Report is created and sent as Html in the email body</li>
        <li>Pdf as Attachment: A Changes Report is created and attached to the email as Pdf file.</li>
        <li>Html as Attachment: A Changes Report is created and attached to the email as Html file.</li>
        <li>Json as Attachment: A Changes Report is created and attached to the email as Json file.</li>
    </ul>
');
INSERT INTO txt VALUES ('H5485', 'German',  '&Auml;nderungsbenachrichtigungs-Intervall (in Sekunden): Zeit zwischen den Checks auf importierte &Auml;nderungen.');
INSERT INTO txt VALUES ('H5485', 'English', 'Change notification sleep time (in seconds): Time between the checks for imported changes.');
INSERT INTO txt VALUES ('H5486', 'German',  '&Auml;nderungsbenachrichtigungs-Start: Startzeit f&uuml;r die Checks auf importierte &Auml;nderungen.');
INSERT INTO txt VALUES ('H5486', 'English', 'Change notification start at: Start time for the import change checks.');
INSERT INTO txt VALUES ('H5487', 'German',  'Empf&auml;nger-Email-Adressen f&uuml;r &Auml;nderungen: Komma-separierte Liste von Email-Adressen, die bei festgestellter
    sicherheitsrelevanter &Auml;nderung auf einem importierten Management benachrichtigt werden. Default-Wert = "leer".
');
INSERT INTO txt VALUES ('H5487', 'English', 'Recipient email addresses for change notifications: A comma-separated list of email addresses, which will get information in the case of 
    security relevant changes found during import of a firewall management. Default value = "empty".
');
INSERT INTO txt VALUES ('H5488', 'German',  'Titel der &Auml;nderungsbenachrichtigung: Betreffzeile der Benachrichtigungs-Email. Default-Wert = "leer".');
INSERT INTO txt VALUES ('H5488', 'English', 'Subject of change notification emails: Subject line for notification emails. Default value = "empty".');
INSERT INTO txt VALUES ('H5489', 'German',  'Text der &Auml;nderungsbenachrichtigung: Start des Email-Textes f&uuml;r alle &Auml;nderungsbenachrichtigungstypen. Die Email enth&auml;lt danach stets
    eine Liste der Namen und IDs der ge&auml;nderten Managements sowie die Anzahl der festgestellten &Auml;nderungen. Default-Wert = "leer".
');
INSERT INTO txt VALUES ('H5489', 'English', 'Body of change notification emails: Start of the email text for all change notification types. The email will subsequently always contain
    a list of names and IDs of the changed firewall management as well as the number of changes. Default value = "empty".
');
INSERT INTO txt VALUES ('H5491', 'German',  'Firewall Orchestrator kann Benachrichtigungen versenden, z.B. f&uuml;r anstehende Rezertifizierungen oder wenn beim Import &Auml;nderungen festgestellt wurden.');
INSERT INTO txt VALUES ('H5491', 'English', 'Firewall Orchestrator is able to send out notifications, e.g. for upcoming recertifications or when an import found changes in the firewall configuration.<br>');
INSERT INTO txt VALUES ('H5491a','German',  'Der Name oder die IP-Adresse des SMTP-Servers f&uuml;r ausgehende Emails wird im Feld "Adresse" eingetragen.');
INSERT INTO txt VALUES ('H5491a','English', 'Enter the name of IP address of your outgoing SMTP server in the field Feld "Adress".');
INSERT INTO txt VALUES ('H5491b','German',  'Der TCP-Port des SMTP-Servers (meist 25, 587 oder 465, abh&auml;ngig von der verwendeten Verschl&uumlsselung) wird im "Port"-Feld eingetragen.');
INSERT INTO txt VALUES ('H5491b','English', 'The TCP port of the SMTP server (usually 25, 587 or 465, depending on the encryption method used) is entered in the "Port" field.');
INSERT INTO txt VALUES ('H5491c','German',  'Anschlie&szlig;end wird die gew&uuml;nschte Art der Verschl&uuml;sselung eingestellt (None=unverschl&uuml;sselt / StartTls / Tls)');
INSERT INTO txt VALUES ('H5491c','English', 'Choose the desired encryption type (None=clear-text / StartTls / Tls)');
INSERT INTO txt VALUES ('H5491d','German',  'Verlangt der SMTP-Server eine Authentisierung, so sind Email-Nutzer und Email-Nutzer-Passwort in den beiden Feldern einzutragen. Anderfalls k&ouml;nnen diese Felder leer gelassen werden.');
INSERT INTO txt VALUES ('H5491d','English', 'If the SMTP server requires authentication, enter Email User name and password in the two fields. Otherwise leave empty.');
INSERT INTO txt VALUES ('H5491e','German',  'Schlie&szlig;lich kann auch eine individuelle Absendeadresse im Feld "Email-Absendeadresse" konfiguriert werden.');
INSERT INTO txt VALUES ('H5491e','English', 'Finally an individual sender address can be configured using the field "Email sender address".');
INSERT INTO txt VALUES ('H5492','German',   'Verbindung testen: Es wird eine Test-email an die oben eingerichtete email-Adresse versandt.');
INSERT INTO txt VALUES ('H5492','English',  'Test connection: A test email is sent to the above defined email address.');
INSERT INTO txt VALUES ('H5495', 'German',  'Die folgenden Einstellungen wirken sich auf das Import-Modul (python) aus.');
INSERT INTO txt VALUES ('H5495', 'English', 'The following settings apply to the Import Module (python)');
INSERT INTO txt VALUES ('H5496', 'German',  'Importintervall (in Sekunden): Zeitintervall zwischen zwei Import-L&auml;ufen. Default-Wert = 40.');
INSERT INTO txt VALUES ('H5496', 'English', 'Import sleep time (in seconds): Time between import loops; default value=40.');
INSERT INTO txt VALUES ('H5497', 'German',  'Zertifikate beim Import pr&uuml;fen: Sollen bei den API-Calls in Richtung der Firewalls nur g&uuml;ltige Zertifikate akzeptiert werden?. 
    Sollte nur auf "aktiv" gesetzt werden, wenn alle Firewalls offiziell signierte Zertifikate besitzen, 
    andernfalls ist ein Import nicht m&ouml;glich. Default-Wert = "inaktiv".
');
INSERT INTO txt VALUES ('H5497', 'English', 'Check certificates during import: During API calls towards Firewalls shall only valid certificates be accepted?. 
    This should only be set to "active" if all firewall API certificates are valid, otherwise an import will not be possible.
    Default value = "inactive".
');
INSERT INTO txt VALUES ('H5498', 'German',  'Zertifikatswarnungen unterdr&uuml;cken: Sollen im Log Warnungen bei selbstsignierten oder ung&uuml;ltigen Zertifkaten auf zu importierenden
    Firewalls ausgegeben werden? Default-Wert = "inaktiv".
');
INSERT INTO txt VALUES ('H5498', 'English', 'Suppress certificate warnings: Shall warnings about invalid certificates be written to import log? Default value = "inactive".');
INSERT INTO txt VALUES ('H5499', 'German',  'FW API - Pro Abruf geholte Elemente: Wie viele Objekte sollen beim Import per Firewall-API Call auf einmal geholt werden? Default-Wert = 150.');
INSERT INTO txt VALUES ('H5499', 'English', 'FW API - Elements per fetch: How many objects/rules shall be fetched per API call from a firewall management? Default value = 150.');
INSERT INTO txt VALUES ('H5501', 'German',  'Aktionen m&uuml;ssen zuerst in den Einstellungen definiert werden und k&ouml;nnen dann den jeweiligen Stati zugeordnet werden.
    Die Aktion wird dann bei Eintreffen der hier definierten Bedingungen angeboten bzw. ausgef&uuml;hrt.
');
INSERT INTO txt VALUES ('H5501', 'English', 'Actions have to be defined first in the customizing settings before they can be assigned to the desired states.
    The action is offered resp. performed when the defined conditions are met.
');
INSERT INTO txt VALUES ('H5511', 'German',  'Allgemeine Parameter f&uuml;r alle Aktionstypen: Hier wird definiert, unter welchen Bedingungen eine Aktion ausgel&ouml;st werden soll.');
INSERT INTO txt VALUES ('H5511', 'English', 'General parameters for all action types: Here it can be defined, under which conditions an action should be performed.');
INSERT INTO txt VALUES ('H5512', 'German',  'Name: Der Name, unter dem die Aktion den Stati zugeordnet wird (da intern eine Id verarbeitet wird, sind auch doppelt vergebene Namen m&ouml;glich).');
INSERT INTO txt VALUES ('H5512', 'English', 'Name: The name to be found in the state assignment (as internally the Id is processed, duplicate names are possible).');
INSERT INTO txt VALUES ('H5513', 'German',  'Ereignis: Es wird zwischen drei Ereignistypen unterschieden: Bei "Beim Erreichen" wird die Aktion beim Erreichen, bei "Beim Verlassen" beim Verlassen des zugeordneten Status ausgel&ouml;st.
    Bei "Schaltfl&auml;che anbieten" wird eine Schaltfl&auml;che zur manuellen Ausf&uuml;hrung in dem ausgew&auml;hlten Objekttyp eingeblendet, solange der zugeordnete Status besteht. In diesem Fall ist auch der auf der Schaltfl&auml;che erscheinende Text auszuf&uuml;llen.
    F&uuml;r den Objekttyp Genehmigung ist die Einblendung von Schaltfl&auml;chen (noch) nicht vorgesehen.
');
INSERT INTO txt VALUES ('H5513', 'English', 'Event: It has to be distinguished between three event types:  For "On set" the action is performed, if the target state is reached, for "On leave", if this state is left.
    With "Offer button" a button for manual execution is displayed in the selected object type as long as it is in the assigned state. In this case also the text for the button has to be filled.
    For approval objects the display of buttons is not provided (yet).
');
INSERT INTO txt VALUES ('H5514', 'German',  'Phase: Hier kann die Aktion f&uuml;r alle Phasen zugelassen oder auf eine auszuw&auml;hlende Phase beschr&auml;nkt werden.');
INSERT INTO txt VALUES ('H5514', 'English', 'Phase: Here the action can be permitted for all phases or restricted on a selected phase.');
INSERT INTO txt VALUES ('H5515', 'German',  'Geltungsbereich: Hier wird festgelegt, auf welchen Objekttyp (Ticket, fachlicher Auftrag, Implementierungs-Auftrag, Genehmigung) sich die Aktion bezieht. 
    F&uuml;r Request Task oder Implementation Task kann in einer weiteren Auswahl der zu ber&uuml;cksichtigende Tasktyp eingeschr&auml;nkt werden.
');
INSERT INTO txt VALUES ('H5515', 'English', 'Scope: Here it can be defined, to which object type (Ticket, Request Task, Implementation Task, Approval) the action should reference.
    For Request Task or Implementation Task the considered task type can be restricted in a further selection field.
');
INSERT INTO txt VALUES ('H5521', 'German',  'Spezifische Parameter je nach ausgew&auml;hltem Aktionstyp: Hier wird definiert, was bei der Aktion passieren soll.');
INSERT INTO txt VALUES ('H5521', 'English', 'Specific parameters depending on selected action type: Here can be defined, what should happen in the action.');
INSERT INTO txt VALUES ('H5522', 'German',  'Autom. Weiterleitung: Hier ist der Zielstatus festzulegen, der dem ausgew&auml;hlten Objekt durch die Aktion zugewiesen werden soll (der Ausgangsstatus ergibt sich dann durch die Zuordnung in der Statustabelle).
    Wird der Wert "Automatisch" ausgew&auml;hlt, so wird der Status aus der Status-Matrix ermittelt.
');
INSERT INTO txt VALUES ('H5522', 'English', 'Auto-forward: Here the target state is to be set, which should be assigned to the selected object in the action (the source state is the defined by the assignment of the action in the state table).
    If the value "Automatic" is selected, the state is computed by the state matrix.
');
INSERT INTO txt VALUES ('H5523', 'German',  'Genehmigung hinzuf&uuml;gen: Hier muss der Status angegeben werden, mit dem die neue Genehmigung angelegt werden soll. Des weiteren kann hier bereits eine Gruppenzuordnung und eine Deadline gesetzt werden.');
INSERT INTO txt VALUES ('H5523', 'English', 'Add approval: Here the desired state of the approval to be created has to be filled. Additionally a group assignment and a deadline can be set.');
INSERT INTO txt VALUES ('H5524', 'German',  'Alarm ausl&ouml;sen: Hier wird die im Alarm verwendete Meldung eingetragen.');
INSERT INTO txt VALUES ('H5524', 'English', 'Set alert: Here the message for the alert is filled.');
INSERT INTO txt VALUES ('H5525', 'German',  'Externer Aufruf: Hier werden in zuk&uuml;nftigen Entwicklungen die f&uuml;r externe Aufrufe anzugebenden Parameter erfasst.');
INSERT INTO txt VALUES ('H5525', 'English', 'External call: Here in later releases the parameters for external calls will be recorded.');
INSERT INTO txt VALUES ('H5526', 'German',  'Pfadanalyse: Hier kann zwischen den Optionen "In Ger&auml;teliste eintragen" und "Gefundene Ger&auml;te darstellen" gew&auml;hlt werden.
    Bei Ersterer wird die Liste der betroffenen Ger&auml;te des Auftrags durch die in der Pfadanalyse gefundenen ersetzt, bei Letzterer wird das Ergebnis der Pfadanalyse lediglich in einem separaten Fenster eingeblendet.
');
INSERT INTO txt VALUES ('H5526', 'English', 'Path analysis: Here the options "Write to device list" or "Display found devices" can be selected.
    In the first case the list of devices in the request task is replaced by the devices found in the path analysis, in the second the result of the path analysis is only displayed in a separate window.
');
INSERT INTO txt VALUES ('H5531', 'German',  'Es k&ouml;nne beliebig viele neue Stati angelegt bzw. vorhandene Stati umbenannt, ggf. auch gel&ouml;scht werden. Die Namen und Nummern der Stati sind weitgehend frei w&auml;hlbar. 
    Zu beachten ist dabei, dass die Nummern zu den in den <a href="/help/settings/statematrix">Status-Matrizen</a> definierten Bereichen (Eingang, Bearbeitung, Ausgang) der jeweiligen Phasen passen.
    Da intern ausschliesslich die Nummern verarbeitet werden, sind auch doppelt vergebene Status-Namen (technisch) m&ouml;glich.
    Es werden nur Stati zum L&ouml;schen angeboten, die in keiner Status-Matrix verwendet werden (auch nicht in deaktivierten Phasen oder Aktionen).
');
INSERT INTO txt VALUES ('H5531', 'English', 'An arbitrary number of states can be created, renamed or deleted where appropriate. Names and numbers of the states can be selected freely.
    But it has to be considered, that the numbers fit into the ranges (Input, Started, Exit) of the phases defined in the <a href="/help/settings/statematrix">state matrices</a>.
    As internally solely the numbers are processed, duplicates in state names are (technically) possible.
    Only states are offered for deletion, who are not used in any state matrix (even in deactivated phases or in actions).
');
INSERT INTO txt VALUES ('H5541', 'German',  'In der Status-Matrix werden die verarbeitbaren Stati pro Phase und Tasktyp festgelegt. 
    Es gibt eine Master-Matrix, welche die Eigenschaften auf Ticket-Ebene beschreibt, sowie und f&uuml;r jeden Tasktyp separate Matrizen.
    In der Installation sind diese Matrizen bereits vorbelegt, sie k&ouml;nnen aber nahezu beliebig &uuml;berschrieben werden.
    Zu beachten ist, dass das Speichern der ge&auml;nderten Matrizen jeweils als Ganzes durch t&auml;tigen der "Speichern"-Schaltfl&auml;che erfolgt, einzelne &Auml;nderungen der Stati also bei Abbruch verloren gehen.
    Jede einzelne Matrix kann auch als Ganzes durch Bet&auml;tigen der entsprechenden Schaltfl&auml;che auf die Initialeinstellungen zur&uuml;ckgesetzt werden. 
    W&auml;hrend die bereits vorhandenen Matrizen bei Software-Upgrades nicht ber&uuml;hrt werden, kann es vorkommen, dass die Initialeinstellungen aktualisiert werden.
');
INSERT INTO txt VALUES ('H5541', 'English', 'In the state matrix the usable states per phase and task type are defined.
    There is a master matrix, which characterizes the ticket properties, as well as separate matrices for each task type.
    During installation these matrices are already initialized, but they can be overwritten almost arbitrarily.
    Be aware that saving of each changed matrix is always done as a whole by using the "Save" button, single changes on states are lost with cancellation inbetween.
    Each matrix can also be reset to the default settings as a whole by using the respective button.
    As already existing matrices are not touched with software upgrades, it may happen, that the default settings are updated.
');
INSERT INTO txt VALUES ('H5542', 'German',  'Phasen: Die f&uuml;r die Tickets bzw. den jeweiligen Tasktyp vorgesehenen Bearbeitungsphasen k&ouml;nnen durch setzen der entsprechenden H&auml;kchen in der Status-Matrix festgelegt werden (die Tabelle der Stati klappt dann automatisch ein oder aus).
    Die Phasen Verifizieren und Rezertifizieren sind noch nicht implementiert, so dass eine Aktivierung hier folgenlos bleibt.
');
INSERT INTO txt VALUES ('H5542', 'English', 'Phases: The workflow phases provided for the tickets resp. each task type can be defined by setting the check mark in the respective state matrix (the table of used states then appears or disappears automatically).
    The phases Verify and Recertify are not implemented yet, an activation would have no effect.
');
INSERT INTO txt VALUES ('H5543', 'German',  'Status&uuml;berg&auml;nge: F&uuml;r jeden in einer Phase vorkommenden Status muss hier festgelegt werden, in welche Stati von dort beim Speichern gewechselt werden kann. 
    Diese werden dann bei den jeweiligen Aktionen in einer Liste angeboten. Ist nur der &Uuml;bergang zu genau einem Status m&ouml;glich, so wird dieser &Uuml;bergang automatisch ohne R&uuml;ckfrage ausgef&uuml;hrt.
    (z.B. ist in der Standardkonfiguration nur der &Uuml;bergang "Requested" -&amp;gt; "In Approval" eingetragen, so dass beim bet&auml;tigen von "Genehmigung beginnen" automatisch letzterer Status gesetzt wird.)
    Soll eine Aktion, die ein Speichern bewirkt, auch ohne Statuswechsel stattfinden k&ouml;nnen, so ist der Ausgangszustand auch in der Liste der Zielzust&auml;nde aufzunehmen.
    Es ist darauf zu achten, dass alle vorkommenden Zielstati der &Uuml;bergangsmatrizen auch in den Ausgangsstati zu finden sind.
');
INSERT INTO txt VALUES ('H5543', 'English', 'State transitions: For each state appearing in a phase it has to be defined, to which states transitions are possible on saving.
    These states are displayed in a list in the particular actions. If there is only the transition to exactly one state possible, this transition is performed automatically without further dialogue.
    (E.g. in the default configuration the transition "Requested" -&amp;gt; "In Approval" is listed, so that on pushing the button "Start approval" the latter state is set automatically.)
    If an action leading to a storage should also have the possibility to be performed without state change, the source state has to be added also to the target state list.
    Make sure that all used target states in all transition matrices also appear in the source states.
');
INSERT INTO txt VALUES ('H5544', 'German',  'Abgeleitete Stati: Bei der Behandlung der abgeleiteten Stati wird unterschieden zwischen der Antragstellung und den anschliessenden Phasen:
    Bei der Antragstellung wird zuerst der Status des Tickets gesetzt.
    Die Stati der zugeordneten Auftr&auml;gen bekommen zun&auml;chst denselben Status wie das Ticket, sofern sie nicht schon in einem h&ouml;heren Status waren (m&ouml;glich durch R&uuml;ckzuweisungen des Tickets z.B. vom Genehmiger). 
    Die Tasktyp-spezifischen Status-Matrizen legen nun anschliessend aus den abgeleiteten Stati fest, welcher Status dem jeweiligen spezifischen Auftrag zugewiesen wird. Dabei k&ouml;nnen z.B. auch Phasen &uuml;bersprungen werden.
    In den weiteren Phasen werden die abgeleiteten Stati dann umgekehrt interpretiert: Aus den Stati der Genehmigungen und Implementierungs-Auftr&auml;ge wird mittels der Tasktyp-spezifischen Status-Matrizen der Status des fachlichen Auftrags ermittelt.
    Aus den Stati der fachlichen Auftr&auml;ge wird dann mittels der Master-Matrix der Status f&uuml;r das Ticket abgeleitet.
');
INSERT INTO txt VALUES ('H5544', 'English', 'Derived states: Regarding the handling of the derived states, it has to be distinguished between ticket creation and the subsequent phases:
    On ticket creation, first the state of the ticket is set.
    In the next step the associated tasks get the same state as the ticket, if they do not already have a higher state (possible by back assignments of the ticket, e.g. by the approver).
    Task specific state matrices now determine the state of the single request tasks from the derived states. At this point e.g. phases can be skipped for this specific task type.
    In further phases, the derived states are interpreted the other way round: From the states of approval and implementation tasks the state of the request task is computed via the task type specific state matrix.
    From the states of the request tasks now the state of the ticket is derived via the master state matrix.
');
INSERT INTO txt VALUES ('H5545', 'German',  'Spezielle Stati: F&uuml;r jede Phase werden drei Bereiche unterschieden: Eingang, Bearbeitung, Ausgang. Sie werden durch die speziellen Stati markiert:');
INSERT INTO txt VALUES ('H5545', 'English', 'Special states: For each phase there are three different ranges to be distinguished: Input, started, exit. They are indicated by special states:');
INSERT INTO txt VALUES ('H5551', 'German',  '"Niedrigster Eingangsstatus": Ab diesem Status wird der Auftrag f&uuml;r den Bearbeiter dieser Phase sichtbar.');
INSERT INTO txt VALUES ('H5551', 'English', '"Lowest input state": From this state on the ticket is visible for the actor in the current phase');
INSERT INTO txt VALUES ('H5552', 'German',  '"Niedrigster Bearbeitungsstatus": Ab diesem Status gilt der Auftrag als in dieser Phase in Bearbeitung. Phasenspezifische &Auml;nderungen k&ouml;nnen ausgef&uuml;hrt werden.');
INSERT INTO txt VALUES ('H5552', 'English', '"Lowest started state": From this state the ticket counts as in work. Phase specific changes can be done.');
INSERT INTO txt VALUES ('H5553', 'German',  '"Niedrigster Ausgangsstatus": Ab diesem Status k&ouml;nnen vom Bearbeiter dieser Phase keine &Auml;nderungen mehr vorgenommen werden. Ein Antrag in diesem Status ist nicht mehr sichtbar.');
INSERT INTO txt VALUES ('H5553', 'English', '"Lowest exit state": From this state the handler of the current phase can not do any changes anymore. A ticket in this state is not visible anymore.');
INSERT INTO txt VALUES ('H5561', 'German',  'In diesem Abschnitt k&ouml;nnen allgemeine Einstellungen zur Konfiguration der Workflows vorgenommen werden.');
INSERT INTO txt VALUES ('H5561', 'English', 'In this chapter general settings for workflow configuration can be done.');
INSERT INTO txt VALUES ('H5562', 'German',  'Verf&uuml;gbare Auftragstypen: Es kann ausgew&auml;hlt werden, welche der technisch vorhandenen Auftragstypen zur Verwendung in den Workflows angeboten werden sollen.');
INSERT INTO txt VALUES ('H5562', 'English', 'Available Task Types: It can be selected, which of the technically available task types should be offered for use in the workflows.');
INSERT INTO txt VALUES ('H5563', 'German',  'Priorit&auml;ten und Deadlines: Die vorbelegten 5 Priorit&auml;tsstufen f&uuml;r Tickets k&ouml;nnen hier entsprechend den eigenen Konventionen (um-)benannt werden.
	Zu jeder Priorit&auml;t kann ein eigenes Intervall (in Tagen) f&uuml;r die Ticket- bzw. Genehmigungs-Deadline gesetzt werden, welches dann bei der automatischen Deadline-Erzeugung genutzt wird.
	Der Wert 0 bedeutet hierbei, dass keine Deadline gesetzt wird.
');
INSERT INTO txt VALUES ('H5563', 'English', 'Priorities and Deadlines: The 5 initialized priority levels for tickets can be (re)named according to the own conventions.
    For each priority an own interval (in days) for ticket and approval deadlines can be set, which is used by the automatic computation of the deadlines.
    The value 0 is interpreted as setting no deadline.
');
INSERT INTO txt VALUES ('H5564', 'German',  'Objektsuche erlauben: Beim Definieren der Ip-Adressen oder Dienste wird das Durchsuchen und Ausw&auml;hlen bereits vorhandener Objekte unterst&uuml;tzt (noch nicht implementiert).');
INSERT INTO txt VALUES ('H5564', 'English', 'Allow object search: During definition of IP addresses or services the search of already existing objects is supported (not implemented yet).');
INSERT INTO txt VALUES ('H5565', 'German',  'Manuelle Eigent&uuml;merverwaltung erlauben: Es wird das manuelle Anlegen und Verwalten von Eigent&uuml;mern durch den Administrator gestattet.');
INSERT INTO txt VALUES ('H5565', 'English', 'Allow manual owner administration: The manual creation and administration of owners can be permitted.');
INSERT INTO txt VALUES ('H5566', 'German',  'Autom. Erzeugen von Implementierungs-Auftr&auml;gen: Ist die Planungs-Phase nicht aktiviert, so m&uuml;ssen aus den vorhandenen fachlichen Auftr&auml;gen automatisch jeweils ein oder mehrere Implementierungs-Auftr&auml;ge erzeugt werden.
    Daf&uuml;r kann zwischen folgenden Optionen gew&auml;hlt werden:
');
INSERT INTO txt VALUES ('H5566', 'English', 'Auto-create implementation tasks: If the planning phase is not activated, one or more implementation tasks have to be created automatically from the request task.
    Therefore the following options can be selected:
');
INSERT INTO txt VALUES ('H5567', 'German',  'Pfadanalyse aktivieren: Dem Planer werden Werkzeuge zur automatischen Pfadanalyse (Pr&uuml;fung, Erzeugen von Implementierungsauftr&auml;gen, Bereinigung) zur Verf&uuml;gung gestellt.');
INSERT INTO txt VALUES ('H5567', 'English', 'Activate Path Analysis: The planner gets access to tools for automatic path analysis (check, creation of implementation tasks, cleanup).');
INSERT INTO txt VALUES ('H5571', 'German',  'Niemals: Es wird kein Implementierungs-Auftrag erzeugt (nur sinnvoll, falls Implementierung und folgende Phasen nicht ben&ouml;tigt werden).');
INSERT INTO txt VALUES ('H5571', 'English', 'Never: No implementation task is created (only reasonable, if implementation and following phases are not needed).');
INSERT INTO txt VALUES ('H5572', 'German',  'Nur eines wenn Ger&auml;t vorhanden: Bei mindestens einem vorhandenen Ger&auml;t wird das erste der Liste eingetragen
	(kann z.B. verwendet werden, wenn es nicht auf das Ger&auml;t ankommt, bzw. wenn dies erst sp&auml;ter festgelegt werden soll).
');
INSERT INTO txt VALUES ('H5572', 'English', 'Only one if device available: The first device from the list is taken, if there is any at all
    (can e.g. be used, if the device choice is not important at this stage or can only be determined later).
');
INSERT INTO txt VALUES ('H5573', 'German',  'F&uuml;r jedes Ger&auml;t: F&uuml;r jedes der bekannten Ger&auml;te wird ein eigener Implementierungs-Auftrag angelegt (Vorsicht bei grosser Anzahl angeschlossener Ger&auml;te).');
INSERT INTO txt VALUES ('H5573', 'English', 'For each device: For each of the known devices an own implementation task is created (Take care in case of a big number of connected devices).');
INSERT INTO txt VALUES ('H5574', 'German',  'Ger&auml;t im Antrag eingeben: Standardm&auml;ssig eingestellt: Bereits bei Antragstellung wird ein Pflichtfeld zur Auswahl der betroffenen Ger&auml;te eingeblendet,
    falls vom Tasktypen ben&ouml;tigt (hier wird also schon dem Antragsteller technisches Wissen abverlangt).
');
INSERT INTO txt VALUES ('H5574', 'English', 'Enter device in request: Default value: A mandatory field to select devices is already displayed during request task creation,
    if needed in the task type (in this case some technical know-how is presumed from the requester).
');
INSERT INTO txt VALUES ('H5575', 'German',  'Nach Pfadanalyse: F&uuml;r jedes bei der automatischen Pfadanalyse gefundene Ger&auml;t wird ein eigener Implementierungs-Auftrag angelegt.');
INSERT INTO txt VALUES ('H5575', 'English', 'After path analysis: For each device found in the automatic path analysis an own implementation task is created.');
INSERT INTO txt VALUES ('H5581', 'German',  'In diesem Abschnitt k&ouml;nnen die vorhandenen Eigent&uuml;mer eingesehen und administriert (falls in den <a href="/help/settings/workflowcustomizing">Einstellungen</a> aktiviert) werden.
    Die Eigent&uuml;merschaft muss Nutzern entweder direkt oder &uuml;ber Eigent&uuml;mergruppen zugeordnet werden.
    Es ist geplant, die Eigent&uuml;merschaft mit der Zust&auml;ndigkeit bei der Antragsstellung zu verkn&uuml;pfen.
');
INSERT INTO txt VALUES ('H5581', 'English', 'In this chapter the existing owners can be displayed and administrated (if activated in the <a href="/help/settings/workflowcustomizing">Customizing Settings</a>).
    Ownership has to be assigned to users directly or via owner groups.
    It is planned to connect the ownership with responsiblity on request creation.
');
INSERT INTO txt VALUES ('H5582', 'German',  'Name: Name der Eigent&uuml;mers');
INSERT INTO txt VALUES ('H5582', 'English', 'Name: Owner name');
INSERT INTO txt VALUES ('H5583', 'German',  'Hauptverantwortlicher (DN): Name und Ldap-Pfad des zugeordneten Nutzers.
    Mindestens eines der Felder "Hauptverantwortlicher (DN)" oder "Gruppe" muss gef&uuml;llt sein.
');
INSERT INTO txt VALUES ('H5583', 'English', 'Main responsible person (DN): Name and Ldap path of the associated user.
    At least one of the fields "Main responsible person (DN)" or "Group" has to be filled.
');
INSERT INTO txt VALUES ('H5584', 'German',  'Gruppe: Name und Ldap-Pfad der zugeordneten Nutzergruppe.
    Die referenzierte Gruppe muss in den <a href="/help/settings/groups">Gruppen-Einstellungen</a> als Eigent&uuml;mergruppe markiert sein.
    Mindestens eines der Felder "Hauptverantwortlicher (DN)" oder "Gruppe" muss gef&uuml;llt sein.
');
INSERT INTO txt VALUES ('H5584', 'English', 'Group: Name and Ldap path of the associated user group.
    The referenced group has to be marked as owner group in the <a href="/help/settings/groups">Group settings</a>.
    At least one of the fields "Main responsible person (DN)" or "Group" has to be filled.
');
INSERT INTO txt VALUES ('H5585', 'German',  'Mandant: Der Mandant, dem der Eigent&uuml;mer zugeordnet ist.');
INSERT INTO txt VALUES ('H5585', 'English', 'Tenant: Tenant to which the owner is assigned to.');
INSERT INTO txt VALUES ('H5586', 'German',  'Externe Anwendungs-Id: Id eines externen Eigent&uuml;mers, vorgesehen f&uuml;r importierte Eigent&uuml;merschaften.');
INSERT INTO txt VALUES ('H5586', 'English', 'External Application Id: Id of an external owner, which may be used for imported ownerships.');
INSERT INTO txt VALUES ('H5587', 'German',  'Rezertintervall (in Tagen): Hier kann das in den <a href="/help/settings/recertificationgeneral">Allgemeinen Rezertifizierungseinstellungen</a> gesetzte Rezertifizierungsintervall f&uuml;r den aktuellen Eigent&uuml;mer &uuml;berschrieben werden.');
INSERT INTO txt VALUES ('H5587', 'English', 'Recert Interval (in days): Here the recert interval set in the <a href="/help/settings/recertificationgeneral">global recertification settings</a> can be overwritten for the specific owner.');
INSERT INTO txt VALUES ('H5588', 'German',  'Rezert Check alle: Hier kann das in den <a href="/help/settings/recertificationgeneral">Allgemeinen Rezertifizierungseinstellungen</a> gesetzte Rezert-Check-Intervall f&uuml;r den aktuellen Eigent&uuml;mer &uuml;berschrieben werden.');
INSERT INTO txt VALUES ('H5588', 'English', 'Recert Check every: Here the recert check interval set in the <a href="/help/settings/recertificationgeneral">global recertification settings</a> can be overwritten for the specific owner.');
INSERT INTO txt VALUES ('H5589', 'German',  'Regeln: Dem Eigent&uuml;mer k&ouml;nnen hier einzelne Regeln, definiert durch Gateway und Regel-Uid, zugeordnet werden.');
INSERT INTO txt VALUES ('H5589', 'English', 'Rules: specific rules, defined by gateway and rule Uid, can be assigned to the owner.');
INSERT INTO txt VALUES ('H5590', 'German',  'IP-Adressen: Dem Eigent&uuml;mer k&ouml;nnen hier einzelne IP-Adressen zugeordnet werden.');
INSERT INTO txt VALUES ('H5590', 'English', 'IP Addresses: IP addresses can be assigned to the owner.');
INSERT INTO txt VALUES ('H5591', 'German',  'Common Service zugelassen: Modellierern wird erlaubt, hier Common Services anzulegen.');
INSERT INTO txt VALUES ('H5591', 'English', 'Common Service Possible: Allows modellers to create common services inside.');
INSERT INTO txt VALUES ('H5592', 'German',  'Importquelle: Falls importiert das dort vergebene Label (sh. <a href="/help/settings/modelling">Modellierungseinstellungen</a>).');
INSERT INTO txt VALUES ('H5592', 'English', 'Import Source: If imported the label given there (see <a href="/help/settings/modelling">Modelling Settings</a>).');

INSERT INTO txt VALUES ('H5601', 'German',  'Hier werden die Einstellungen f&uuml;r die Netzwerk-Modellierung verwaltet.
    Dies betrifft Vordefinierte Dienste, Darstellung verschiedener Elemente, Definition von Namenskonventionen sowie Scheduling-Einstellungen f&uuml;r die zu importierenden Objekte:
');
INSERT INTO txt VALUES ('H5601', 'English', 'On this page all types of modelling settings are administrated.
    This includes Predefined Services, Display options of different elements, definition of naming conventions as well as scheduling settings for the objects to be imported:
');
INSERT INTO txt VALUES ('H5602', 'German',  'Vordefinierte Dienste: Hier wird dem Administrator ein Men&uuml; angeboten, um Dienste und Gruppierungen von Diensten vorzudefinieren,
    zu bearbeiten oder zu l&ouml;schen. Diese stehen dann allen Applikationen zur Verf&uuml;gung.
');
INSERT INTO txt VALUES ('H5602', 'English', 'Predefined Services: Offers a menu to the administrator to define, change or delete predefined services or service groups.
    These services are available for all applications.
');
INSERT INTO txt VALUES ('H5603', 'German',  'Server in Verbindung erlauben: Steuert, ob in der Bibliothek neben den App Rollen auch App Server zur direkten Verwendung in den Verbindungen angeboten werden.');
INSERT INTO txt VALUES ('H5603', 'English', 'Allow Servers in Connection: Controls, if App Servers are offered in the Library besides the App Roles for direct use in the connections.');
INSERT INTO txt VALUES ('H5604', 'German',  'Einfache Dienste in Verbindung erlauben: Steuert, ob in der Bibliothek neben den Servicegruppen auch einfache Services zur direkten Verwendung in den Verbindungen angeboten werden.');
INSERT INTO txt VALUES ('H5604', 'English', 'Allow Simple Services in Connection: Controls, if simple Services are offered in the Library besides the Service Groups for direct use in the connections.');
INSERT INTO txt VALUES ('H5605', 'German',  'Max. Anzahl Zeilen in &Uuml;bersicht: Definiert die Zeilenzahl innerhalb eines Eintrags in der &Uuml;bersichtstabelle der Verbindungen, ab der die Elemente eingeklappt dargestellt werden.');
INSERT INTO txt VALUES ('H5605', 'English', 'Max. Number of Rows in Overview: Defines the number of rows inside an entry of the connections overview table, from which the elements are displayed retracted.');
INSERT INTO txt VALUES ('H5606', 'German',  'Netzwerkarea vorgeschrieben: Wenn dieses Flag gesetzt ist, m&uuml;ssen die auszuw&auml;hlenden App Server einer festen Area zugeordnet sein.
    Es werden dann beim Zusammenstellen einer App Rolle in der Bibliothek nur die der aktuell ausgew&auml;hlten Area zugeh&ouml;rigen App Server angeboten.
    F&uuml;r die Namensgebung der App Rolle wird dann die in den folgenden Punkten definierte Namenskonvention angewendet.
');
INSERT INTO txt VALUES ('H5606', 'English', 'Network Area Required: If this flag is set, the App Servers used have to be associated to a fixed area.
    When defining an App Role, only the App Servers belonging to the selected area are displayed in the library.
    Naming of the App Role is then restricted to the naming convention defined in the following settings.
');
INSERT INTO txt VALUES ('H5607', 'German',  'L&auml;nge fixer Teil: L&auml;nge des vorgebenen Teils des Namensmusters einer App Rolle.');
INSERT INTO txt VALUES ('H5607', 'English', 'Fixed Part Length: Length of the predefined part of the name pattern of an App Role.');
INSERT INTO txt VALUES ('H5608', 'German',  'L&auml;nge freier Teil: L&auml;nge des frei zu vergebenden Teils des Namens einer App Rolle.');
INSERT INTO txt VALUES ('H5608', 'English', 'Free Part Length: Length of the free part of the name pattern of an App Role.');
INSERT INTO txt VALUES ('H5609', 'German',  'Muster Netzwerkarea: Definiert, wie der Name einer Netzwerkarea beginnt (z.B "NA").');
INSERT INTO txt VALUES ('H5609', 'English', 'Network Area Pattern: Defines the beginning of a network area name (e.g. "NA").');
INSERT INTO txt VALUES ('H5610', 'German',  'Muster App Rolle: Definiert, wie der Name einer App Rolle beginnt (z.B. "AR").
    Zu einer Netzwerkarea (z.B. "NAxx") wird dann ein Name der App Rolle (z.B. "ARxx") mit der oben definierten L&auml;nge des fixen Teils vorgegeben.
');
INSERT INTO txt VALUES ('H5610', 'English', 'App Role Pattern: Defines the beginning of an App Role name (e.g. "AR").
    According to an network area name (e.g. "NAxx"), an App Role name (e.g. "ARxx") is preset in the length of the fixed part defined above.
');
INSERT INTO txt VALUES ('H5611', 'German',  'Pfad und Name von Appdaten-Import (ohne Endung): Hier werden die vollst&auml;ndigen Pfade f&uuml;r eventuell vorhandene Importskripte und -dateien eingegeben.
    Der Importprozess pr&uuml;ft f&uuml;r jede der eingegebenen Datenquellen zun&auml;chst, ob ein Skript dieses Namens mit der Endung .py vorhanden ist, und f&uuml;hrt dieses ggf. aus. 
    Anschliessend wird eine Datei desselben Namens mit der Endung .json gesucht und ggf. importiert. 
    Es gibt f&uuml;r den Import pro Datenquelle also sowohl die M&ouml;glichkeit, eine direkt zu importierende Datei zur Verf&uuml;gung zu stellen, als auch ein Skript zur Datenabholung,
    welches die ben&ouml;tigte Import-Datei erst erzeugt. Die Struktur der Importdatei wird unter <a href="/help/API/appdataimport">Import-Schnittstellen</a> beschrieben.
');
INSERT INTO txt VALUES ('H5611', 'English', 'Path and Name of App data import (without ending): Here the full paths of provided import scripts and files are inserted.
    The import process checks for each data source, if a script of this name with ending .py exists and executes it.
    Then a file of this name with ending .json is searched and imported if found.
    Thus there is the possibility for each data source to provide a file for direct import or a script to catch the import data and create the app data import file.
    The structure of the import file is described at <a href="/help/API/appdataimport">Import Interfaces</a>.
');
INSERT INTO txt VALUES ('H5612', 'German',  'Import Appdaten-Intervall (in Stunden): Zeitintervall zwischen zwei Appdaten-Import-L&auml;ufen.
    Ein Wert 0 bedeutet, dass der Appdaten-Import deaktiviert ist. Default-Wert = 0. 
');
INSERT INTO txt VALUES ('H5612', 'English', 'Import App data sleep time (in hours): Time between App data import loops.
    A value 0 means, that the App data import is deactivated. Default value = 0.
');
INSERT INTO txt VALUES ('H5613', 'German',  'Import Appdaten-Start: Legt eine Bezugszeit fest, ab dem die Intervalle f&uuml;r die Appdaten-Importe gerechnet werden.');
INSERT INTO txt VALUES ('H5613', 'English', 'Import App data start at: Defines a referential time from which the App data import intervals are calculated.');
INSERT INTO txt VALUES ('H5614', 'German',  'Pfad und Name von Subnetzdaten-Import (ohne Endung): Hier wird der vollst&auml;ndige Pfad f&uuml;r ein eventuell vorhandenes Importskript oder einer Import-Datei eingegeben.
    Der Importprozess pr&uuml;ft zun&auml;chst, ob ein Skript dieses Namens mit der Endung .py vorhanden ist, und f&uuml;hrt dieses ggf. aus. Anschliessend wird eine Datei desselben Namens mit der Endung .json
    gesucht und ggf. importiert. Es gibt f&uuml;r den Import also sowohl die M&ouml;glichkeit, eine direkt zu importierende Datei zur Verf&uuml;gung zu stellen, als auch ein Skript zur Datenabholung,
    welches die ben&ouml;tigte Import-Datei erst erzeugt. Die Struktur der Importdatei wird unter <a href="/help/API/subnetdataimport">Import-Schnittstellen</a> beschrieben.
');
INSERT INTO txt VALUES ('H5614', 'English', 'Path and Name of subnet data import (without ending): Here the full path of a provided import script or file is inserted.
    The import process checks, if a script of this name with ending .py exists and executes it.
    Then a file of this name with ending .json is searched and imported if found.
    Thus there is the possibility to provide a file for direct import or a script to catch the import data and create the subnet data import file.
    The structure of the import file is described at <a href="/help/API/subnetdataimport">Import Interfaces</a>.
');
INSERT INTO txt VALUES ('H5615', 'German',  'Import Subnetzdaten-Intervall (in Stunden): Zeitintervall zwischen zwei Subnetzdaten-Import-L&auml;ufen.
    Ein Wert 0 bedeutet, dass der Subnetzdaten-Import deaktiviert ist. Default-Wert = 0.
');
INSERT INTO txt VALUES ('H5615', 'English', 'Import Subnet data sleep time (in hours): Time between Subnet data import loops.
    A value 0 means, that the Subnet data import is deactivated. Default value = 0.
');
INSERT INTO txt VALUES ('H5616', 'German',  'Import Subnetzdaten-Start: Legt eine Bezugszeit fest, ab dem die Intervalle f&uuml;r die Subnetzdaten-Importe gerechnet werden.');
INSERT INTO txt VALUES ('H5616', 'English', 'Import Subnet data start at: Import App data start at: Defines a referential time from which the Subnte data import intervals are calculated.');
INSERT INTO txt VALUES ('H5617', 'German',  'Reduzierten Protokollset darstellen: Nur eine begrenzte Zahl von Protokollen wird zur Auswahl angeboten (TCP, UDP, ICMP).');
INSERT INTO txt VALUES ('H5617', 'English', 'Display reduced Protocol set: Offer only a reduced number of protocols for selection (TCP, UDP, ICMP).');
INSERT INTO txt VALUES ('H5701', 'German',  'Die in der Datenbank hinterlegten sprachabh&auml;ngigen Texte k&ouml;nnen individuell &uuml;berschrieben werden.
    Dabei werden die vom System vorgegebenen Texte nicht ge&auml;ndert, sondern nur durch die hier definierten Texte - falls vorhanden - &uuml;berblendet.
    Die hier gemachten &Auml;nderungen werden in der UI beim n&auml;chsten Login sichtbar, bei Hilfetexten erst nach dem n&auml;chsten Restart.
');
INSERT INTO txt VALUES ('H5701', 'English', 'The language dependent texts stored in the database can be overwritten individually.
    In doing so, system texts are not changed but crossfaded by the texts defined here. The changes made here become visible in the UI with the next login,
    help texts only after the next restart.
');
INSERT INTO txt VALUES ('H5702', 'German',  'Im ersten Schritt muss die betroffene Sprache ausgew&auml;hlt werden. Dann erscheint die Zeile zur eigentlichen Textsuche.');
INSERT INTO txt VALUES ('H5702', 'English', 'In the first step the language to be handled has to be selected. Then the row for the text search appears.');
INSERT INTO txt VALUES ('H5703', 'German',  'Bei Eingabe eines Suchstrings erscheinen alle hinterlegten Texte, welche diesen beinhalten.
    Wird das Feld leer gelassen, erscheinen alle verf&uuml;gbaren Texte der gew&auml;hlten Sprache.
    Durch das Setzen des "Hilfetexte ignorieren"-Flags werden die Hilfetexte (Schl&uuml;ssel beginnend mit "H"), bei der Suche nicht ber&uuml;cksichtigt.
');
INSERT INTO txt VALUES ('H5703', 'English', 'When entering a search string, all texts containing this are displayed. If left empty, all texts of the selected language are displayed.
    By setting the "Ignore Help Texts" flag all Help texts (Key beginning with "H") are disregarded.
');
INSERT INTO txt VALUES ('H5704', 'German',  'In der Tabelle der Suchergebnisse k&ouml;nnen pro Schl&uuml;ssel neue Texte definiert,
    bereits vorhandene durch Setzen des "L&ouml;schen"-Flags zum L&ouml;schen vorgemerkt werden. Wird nur der Text entfernt, wird der Systemtext mit Leertext &uuml;berschrieben!
    Die &Auml;nderungen werden erst durch das Bet&auml;tigen des "Speichern"-Knopfes wirksam.
');
INSERT INTO txt VALUES ('H5704', 'English', 'In the table of search results new texts can be defined per key, existing texts can be marked for deletion by setting the "Delete" flag.
    If only a text is removed, the system text will be overwritten by an empty text! All changes get effective only by pressing the "Save" button.
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
INSERT INTO txt VALUES ('H6921', 'German',  'Der Import von Applikationsdaten wird aus einer oder mehreren .json-Dateien mit den in den <a href="/help/settings/modelling">Modellierungs-Einstellungen</a> definierten Pfaden und Namen gespeist.
    Dort kann auch jeweils ein gleichnamiges Python-Skript (mit der Endung .py) zur Erzeugung eben dieser Dateien hinterlegt werden. Die .json-Datei hat die folgende Struktur:
');
INSERT INTO txt VALUES ('H6921', 'English', 'The import of application data is fed from one or several .json files with paths and names defined in the <a href="/help/settings/modelling">Modelling Settings</a>.
    There also python scripts with the same names can be provided to create these files. The structure of the .json file is as following:
');
INSERT INTO txt VALUES ('H6922', 'German',  'Die einzelnen Felder haben folgende Bedeutung:
    <ul>
        <li>app_id_external: Eindeutige Kennzeichnung der Applikation. Dies ist ggf. &uuml;ber verschiedene Importquellen hinweg sicherzustellen.</li>
        <li>name: Dargestellter Name der Applikation.</li>
        <li>main_user: Die DN der hauptverantwortlichen Person. Mindestens eines der Felder main_user, modellers oder modeller_groups sollte gef&uuml;llt sein, damit ein Zugriff 
            auf diese Applikation m&ouml;glich ist. (Ansonsten muss dieses in den <a href="/help/settings/owners">Eigent&uuml;mer-Einstellungen</a> manuell nachgeholt werden.)</li>
        <li>modellers: Hier werden die DNs von allen zur Bearbeitung dieser Applikation Berechtigten angegeben.</li>
        <li>modeller_groups: Hier k&ouml;nnen die DNs von Gruppen angegeben werden. Zur Zeit werden diese genauso wie die Eintr&auml;ge unter "modellers" behandelt.</li>
        <li>criticality: Hier kann optional ein String zur Kennzeichnung der Kritikalit&auml;t der Applikation definiert werden.</li>
        <li>import_source: String zur Kennzeichnung der Importquelle. Dient zur Unterscheidung bei mehreren Quellen.</li>
        <li>app_servers: Liste aller zur Applikation zugeordneten Host-Adressen.
            <ul>
                <li>name: Optionaler Name zur Darstellung</li>
                <li>ip: IP-Adresse</li>
            </ul>
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H6922', 'English', 'These fields have the following meaning:
    <ul>
        <li>app_id_external: Unique identification string of the application. This has to be ensured over several import sources.</li>
        <li>name: Displayed name of the application.</li>
        <li>main_user: DN of the main responsible person. At least one of the fields main_user, modellers or modeller_groups should be filled to ensure access
            to the application. (Else this has to be ensured manually in the <a href="/help/settings/owners">Owner Settings</a> afterwards.)</li>
        <li>modellers: Here the DNs of all persons authorized to work on this application have to be delivered.</li>
        <li>modeller_groups: Here DNs of user groups can be delivered. Currently they are handled the same way as the entries in "modellers".</li>
        <li>criticality: An optional string to mark the criticality of the application can be defined here.</li>
        <li>import_source: String to identify the import source. Necessary to distinguish between several sources.</li>
        <li>app_servers: List of all host addresses assigned to the application:
            <ul>
                <li>name: Optional name for display</li>
                <li>ip: IP address</li>
            </ul>
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H6931', 'German',  'Der Import von Subnetzdaten wird aus einer .json-Datei mit dem in den <a href="/help/settings/modelling">Modellierungs-Einstellungen</a> definierten Pfad und Namen gespeist.
    Dort kann auch ein gleichnamiges Python-Skript (mit der Endung .py) zur Erzeugung eben dieser Datei hinterlegt werden. Die .json-Datei hat die folgende Struktur:
');
INSERT INTO txt VALUES ('H6931', 'English', 'The import of subnet data is fed from a .json file with path and name defined in the <a href="/help/settings/modelling">Modelling Settings</a>.
    There also a python script with the same name can be provided to create this file. the structure of the .json file is as following:
');
INSERT INTO txt VALUES ('H6932', 'German',  'Die einzelnen Felder haben folgende Bedeutung:
    <ul>
        <li>name: Dargestellter Name der Area.</li>
        <li>id_string: Eindeutige Kennzeichnung der Area.</li>
        <li>subnets: Liste alle Subnetze der Area:
            <li>name: Optionaler Name zur Darstellung.</li>
            <li>ip: IP-Adresse oder Start-IP-Addresse, falls Bereiche definiert werden sollen.</li>
            <li>ip_end: Ende-IP-Addresse, falls Bereiche definiert werden sollen. Sonst leer lassen oder denselben Wert wie in "ip" liefern.</li>
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H6932', 'English', 'These fields have the following meaning:
    <ul>
        <li>name: Displayed name of the area</li>
        <li>id_string: Unique identification string of the area.</li>
        <li>subnets: List of all subnets of the area:
            <li>name: Optional name for display.</li>
            <li>ip: IP address or start IP address, if ranges are to be defined.</li>
            <li>ip_end: end IP address, if ranges are to be defined. Else leave empty or fill with the same value as "ip".</li>
        </li>
    </ul>
');

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

INSERT INTO txt VALUES ('H8001', 'German',  'Das Workflow-Modul soll die Zusammenarbeit mehrerer beteiligter Akteure bei Arbeitsabl&auml;ufen im Umfeld der Netzwerkadministration unterst&uuml;tzen.
    Um eine m&ouml;glichst grosse Vielzahl von Workflows abbilden zu k&ouml;nnen, werden diverse Konfigurationsm&ouml;glichkeiten angeboten.
');
INSERT INTO txt VALUES ('H8001', 'English', 'The Workflow module is intended to support the collaboration of several actors in the network administration environment.
    To map as many workflows as possible, several configuration settings are offered.
');
INSERT INTO txt VALUES ('H8011', 'German',  '<a href="/help/workflow/objects">Objekte</a>: Den Rahmen f&uuml;r Antr&auml;ge bilden Tickets. Hier werden die eigentlichen fachlichen Auftr&auml;ge (Request Tasks) gesammelt und verwaltet.
    Diese k&ouml;nnen verschiedene Auspr&auml;gungen (<a href="/help/workflow/tasktypes">Tasktypen</a>) haben, je nach Art des Auftrags. An den fachlichen Auftr&auml;ge h&auml;ngen die Genehmigungen.
    Aus den fachlichen Auftr&auml;gen k&ouml;nnen automatisch oder manuell ein oder mehrere Implementierungs-Auftr&auml;ge (Implementation Tasks) erzeugt werden, welche die technische Umsetzung des Auftrags reflektieren.
    Diesen 4 Objekttypen ist jeweils ein Status zugewiesen.
');
INSERT INTO txt VALUES ('H8011', 'English', '<a href="/help/workflow/objects">Objects</a>: The tickets are building the framework for requests. Here the functional tasks (request tasks) are collected and administrated.
    These can have different flavors (<a href="/help/workflow/tasktypes">Task Types</a>), depending on the type of order. The approvals are associated to the request tasks.
    Based on the request tasks, one or more implementation tasks can be generated automatically or manually, which reflect the technical realization of the request.
    To each of these 4 object types there is a state assigned.
');
INSERT INTO txt VALUES ('H8012', 'German',  '<a href="/help/workflow/phases">Phasen und Rollen</a>: Die Bearbeitung der Auftr&auml;ge ist in Phasen unterteilt, welche an Rollen gebunden sind. 
    Bei der Konfiguration des Workflows wird festgelegt, welche Phasen f&uuml;r die jeweiligen Tasktypen &uuml;berhaupt verwendet werden.
');
INSERT INTO txt VALUES ('H8012', 'English', '<a href="/help/workflow/phases">Phases and Roles</a>: Processing of the requests is divided into several phases, which are bound to roles.
    During configuration it has to be defined, which of the phases are used for the respective task types.
');
INSERT INTO txt VALUES ('H8013', 'German',  '<a href="/help/workflow/states">Stati</a>: Bei der Konfiguration des Workflows k&ouml;nnen Stati frei definiert und benannt werden.
    Durch geeignete Wahl der Nummernkreise werden diese in den verschiedenen Phasen sichtbar bzw. bearbeitbar.    
');
INSERT INTO txt VALUES ('H8013', 'English', '<a href="/help/workflow/states">States</a>: During configuration of the workflow, states can be defined and named freely.
    They become visible and processable in the different phases by choosing the appropriate number ranges.
');
INSERT INTO txt VALUES ('H8014', 'German',  '<a href="/help/workflow/actions">Aktionen</a>: Um die Bearbeitung der Auftr&auml;ge zu unterst&uuml;tzen, k&ouml;nnen Aktionen verschiedener Typen definiert werden. 
    Dazu geh&ouml;ren automatische Status-Weiterleitungen oder das Anfordern weiterer Genehmigungen. Auch die Konfiguration f&uuml;r Aufrufe externer Komponenten ist vorgesehen.
');
INSERT INTO txt VALUES ('H8014', 'English', '<a href="/help/workflow/actions">Actions</a>: To support processing of the requests, different kinds of actions can be defined.
    This includes automatic state forwarding or the request of further approvals. Also configuration of calls to external components is in preparation.
');
INSERT INTO txt VALUES ('H8101', 'German',  'Das Workflow-Modul operiert mit 4 verschiedenen Objekttypen, welche der Statusbehandlung unterliegen.
    Entsprechend der Objekthierarchie sind die Stati voneinander abh&auml;ngig.
');
INSERT INTO txt VALUES ('H8101', 'English', 'The workflow module operates on 4 different object types which are subject to state handling.
    According to the object hierarchy their states are interdependent.
');
INSERT INTO txt VALUES ('H8111', 'German',  'Ticket: Bildet die Klammer f&uuml;r einen oder mehrere fachliche Auftr&auml;ge. Als Felder werden angeboten:');
INSERT INTO txt VALUES ('H8111', 'English', 'Ticket: Serves as a clamp around one or more functional (request) tasks. Fields are:');
INSERT INTO txt VALUES ('H8112', 'German',  'Titel: Pflichtfeld zur Kennzeichnung des Antrags.');
INSERT INTO txt VALUES ('H8112', 'English', 'Title: Mandatory field to identify the request');
INSERT INTO txt VALUES ('H8113', 'German',  'Status: Der Ticket-Status wird in der ersten Phase vom Antragssteller gesetzt, in sp&auml;teren Phasen aus den Stati der fachlicher Auftr&auml;ge ermittelt.');
INSERT INTO txt VALUES ('H8113', 'English', 'State: The ticket state is set in the first phase by the requester, in later phases it is computed from the states of the request tasks.');
INSERT INTO txt VALUES ('H8114', 'German',  'Antragsteller: Wird automatisch beim Anlegen des Tickets auf den erzeugenden Nutzer gesetzt.');
INSERT INTO txt VALUES ('H8114', 'English', 'Requester: Is automatically set to the requesting user at ticket creation time.');
INSERT INTO txt VALUES ('H8115', 'German',  'Priorit&auml;t: Es steht eine Liste von 5 <a href="/help/settings/workflowcustomizing">konfigurierbaren</a> Priorit&auml;tsstufen zur Auswahl.');
INSERT INTO txt VALUES ('H8115', 'English', 'Priority: Can be selected from a list of 5 <a href="/help/settings/workflowcustomizing">configurable</a> priority levels.');
INSERT INTO txt VALUES ('H8116', 'German',  'Deadline: Kann hier manuell gesetzt oder aus der Priorit&auml;t (soweit gesetzt) automatisch berechnet werden.');
INSERT INTO txt VALUES ('H8116', 'English', 'Deadline: Can be set manually or computed automatically from the priority (if set).');
INSERT INTO txt VALUES ('H8117', 'German',  'Grund: Dient vor allem der &uuml;bergreifenden Begr&uuml;ndung und Dokumentation.');
INSERT INTO txt VALUES ('H8117', 'English', 'Reason: Primarily serves for overall motivation and documentation.');
INSERT INTO txt VALUES ('H8118', 'German',  'Liste der fachlichen Auftr&auml;ge (bis Planungsphase) bzw. Liste der technischen Auftr&auml;ge (ab Implementierungsphase).');
INSERT INTO txt VALUES ('H8118', 'English', 'List of functional tasks (up to planning phase) resp. list of implementation tasks (starting from implementation phase).');
INSERT INTO txt VALUES ('H8131', 'German',  'Fachlicher Auftrag (Request task): Stellt die fachliche Sicht eines einzelnen Auftrags dar.
    Einem fachlichen Auftrag k&ouml;nnen mehrere Implementierungs-Auftr&auml;ge (in der Regel je betroffenem Ger&auml;t einer) und mehrere Genehmigungen zugeordnet sein. Er enth&auml;lt folgende Felder:
');
INSERT INTO txt VALUES ('H8131', 'English', 'Functional (Request) task: Represents the functional view of a task.
    Several implementation tasks (usually one per involved gateway) and approvals can be associated. It contains following fields:
');
INSERT INTO txt VALUES ('H8132', 'German',  'Titel: Pflichtfeld zur schnellen Kennzeichnung der Aufgabe. (In sp&auml;teren Ausbauphasen ist hier eine Unterst&uuml;tzung zur Einhaltung von Namenskonventionen m&ouml;glich).');
INSERT INTO txt VALUES ('H8132', 'English', 'Title: Mandatory field for quick identification of the task. (In later releases a support for obeying name conventions is possible).');
INSERT INTO txt VALUES ('H8133', 'German',  'Status: Der Aufrags-Status wird zun&auml;chst vom Ticket-Status, sp&auml;ter dann vom Status der zugeh&ouml;rigen Genehmigungen bzw. Implementierungsauftr&auml;gen abgeleitet. ');
INSERT INTO txt VALUES ('H8133', 'English', 'State: The state of the request task is determined from the ticket state in the beginning, later from the associated approvals resp. implementation tasks.');
INSERT INTO txt VALUES ('H8134', 'German',  'Bearbeiter: Hier wird automatisch der Nutzer, welcher den Auftrag als letztes bearbeitet hat, gesetzt.');
INSERT INTO txt VALUES ('H8134', 'English', 'Handler: Automatically the last editing user of the request task is set.');
INSERT INTO txt VALUES ('H8135', 'German',  'Zugewiesen: Ein fachlicher Auftrag kann einem Nutzer explizit <a href="/help/workflow/phases">zugewiesen</a> sein.');
INSERT INTO txt VALUES ('H8135', 'English', 'Assigned: A request task can be <a href="/help/workflow/phases">assigned</a> to a user explicitly.');
INSERT INTO txt VALUES ('H8136', 'German',  'Tasktypspezifische Felder: Je nach ausgew&auml;hltem Auftragstyp werden unterschiedliche Felder eingeblendet (<a href="/help/workflow/tasktypes">Tasktypen</a>).');
INSERT INTO txt VALUES ('H8136', 'English', 'Task type specific fields: Depending on the task type different fields are displayed (<a href="/help/workflow/tasktypes">Task types</a>)');
INSERT INTO txt VALUES ('H8137', 'German',  'Grund: Dient vor allem der detaillierteren Dokumentation auf fachlicher Ebene und Erkl&auml;rung f&uuml;r Genehmiger und ggf. Planer. Wird in Phasen ab Implementierung nicht mehr dargestellt.');
INSERT INTO txt VALUES ('H8137', 'English', 'Reason: Serves for a more detailled documentation on functional level and for explanations to approver and planner if activated. Not displayed in phases starting from implementation.');
INSERT INTO txt VALUES ('H8138', 'German',  'Kommentare: In den Phasen Genehmigung und Planung k&ouml;nnen Kommentare zu dem fachlichen Auftrag hinzugef&uuml;gt werden. Sie werden mit Datum und Autor aufgelistet und k&ouml;nnen nicht gel&ouml;scht werden.');
INSERT INTO txt VALUES ('H8138', 'English', 'Comments: In the approval and planning phase comments can be added to the request task. They are listed with date and author an can not be deleted.');
INSERT INTO txt VALUES ('H8139', 'German',  'Start: Hier wird automatisch ein Zeitstempel eingetragen, sobald der Auftrag das erste mal nach der Genehmigung angefasst wird.
	Falls aktiviert, kann dies den Beginn der Planungsphase markieren, ansonsten wird hier der Beginn der ersten Implementierung widergespiegelt.
');
INSERT INTO txt VALUES ('H8139', 'English', 'Start: Here the timestamp of the first change after approval is set.
    If activated, this can mark the beginning of the planning phase, else the start of the first implementation is indicated.
');
INSERT INTO txt VALUES ('H8140', 'German',  'Stop: Wird der letzte Implementierungs-Auftrag des fachlichen Auftrags durch setzen des Stop-Datums als fertiggestellt markiert, wird auch hier der aktuelle Zeitstempel gesetzt.');
INSERT INTO txt VALUES ('H8140', 'English', 'Stop: When the last implementation task of the request task is marked as finished by setting the stop date, the actual date is set here too.');
INSERT INTO txt VALUES ('H8141', 'German',  'Liste der Implementierungs-Auftr&auml;ge falls schon vorhanden.');
INSERT INTO txt VALUES ('H8141', 'English', 'List of implementation tasks if already existing.');
INSERT INTO txt VALUES ('H8151', 'German',  'Implementierungs-Auftrag (Implementation task): Stellt die technische Sicht f&uuml;r die konkrete Implementierung eines einzelnen Auftrags dar. Er enth&auml;lt folgende weitgehend dem fachlichen Auftrag entsprechenden Felder:');
INSERT INTO txt VALUES ('H8151', 'English', 'Inplementation Task: Represents the technical view for the implementation of the request task. It contains following fields, mostly corresponding to the request task:');
INSERT INTO txt VALUES ('H8152', 'German',  'Titel (abgeleitet): Wird automatisch aus dem Titel des fachlichen Auftrags und dem Ger&auml;tenamen zusammengesetzt.');
INSERT INTO txt VALUES ('H8152', 'English', 'Title (derived): Automatically composed from the title of the request task and the device name.');
INSERT INTO txt VALUES ('H8153', 'German',  'Status: Status des Implementierungs-Auftrags.');
INSERT INTO txt VALUES ('H8153', 'English', 'State: State of the implementation task.');
INSERT INTO txt VALUES ('H8154', 'German',  'Implementierer: Hier wird automatisch der Nutzer, welcher den Implementierungs-Auftrag als letztes bearbeitet hat, gesetzt.');
INSERT INTO txt VALUES ('H8154', 'English', 'Implementer: Automatically the last editing user of the implementation task is set.');
INSERT INTO txt VALUES ('H8155', 'German',  'Zugewiesen: Ein Implementierungs-Auftrag kann einem Nutzer explizit <a href="/help/workflow/phases">zugewiesen</a> sein.');
INSERT INTO txt VALUES ('H8155', 'English', 'Assigned: An implementation task can be <a href="/help/workflow/phases">assigned</a> to a user explicitly.');
INSERT INTO txt VALUES ('H8156', 'German',  'Tasktypspezifische Felder: werden aus den entsprechenden Feldern des fachlichen Auftrags weitgehend vorbelegt. Der Planer kann dann daran &Auml;nderungen vornehmen. Der Auftragstyp selbst kann nicht mehr ge&auml;ndert werden.');
INSERT INTO txt VALUES ('H8156', 'English', 'Task type specific fields: are mostly prefilled from the corresponding fields of the request task. The planner can make changes on them. The task type itself can not be changed anymore.');
INSERT INTO txt VALUES ('H8157', 'German',  'Kommentare: In den Phasen Implementierung und Review k&ouml;nnen Kommentare zu dem Implementierungs-Auftrag hinzugef&uuml;gt werden. Sie werden mit Datum und Autor aufgelistet und k&ouml;nnen nicht gel&ouml;scht werden.');
INSERT INTO txt VALUES ('H8157', 'English', 'Comments: In the implementation and review phase comments can be added to the implementation task. They are listed with date and author an can not be deleted.');
INSERT INTO txt VALUES ('H8158', 'German',  'Start: Hier wird automatisch ein Zeitstempel eingetragen, sobald der Implementierungs-Auftrag das erste mal nach der Planung angefasst wird.');
INSERT INTO txt VALUES ('H8158', 'English', 'Start: Here the timestamp of the first change after planning is set automatically.');
INSERT INTO txt VALUES ('H8159', 'German',  'Stop: Wird bei Bearbeitung in der Implementierungsphase ein Status im Ausgangsbereich erreicht, wird hier der aktuelle Zeitstempel gesetzt. ');
INSERT INTO txt VALUES ('H8159', 'English', 'Stop: When reaching a state in the exit range, the actual timestamp is set.');
INSERT INTO txt VALUES ('H8171', 'German',  'Genehmigungen werden als eigenst&auml;ndige Objekte dem fachlichen Auftrag zugeordnet. Ein Auftrag gilt dann als genehmigt, wenn alle zugeordneten Einzelgenehmigungen den entsprechenden Status aufweisen.
	Eine Genehmigung enth&auml;lt folgende Felder:
');
INSERT INTO txt VALUES ('H8171', 'English', 'Approvals are associated to the functional (request) task as separate objects. A request task is regarded as approved, if all related single approvals have the approprioate state.
    An approval contains the following fields:
');
INSERT INTO txt VALUES ('H8172', 'German',  'Ge&ouml;ffnet: Zeitstempel des Anlegens der Genehmigung.');
INSERT INTO txt VALUES ('H8172', 'English', 'Opened: Timestamp of the approval creation.');
INSERT INTO txt VALUES ('H8173', 'German',  'Deadline: Beim Anlegen der Genehmigung wird automatisch eine Deadline gesetzt. 
    Diese wird beim Anlegen des Auftrags aus der Priorit&auml;t des Tickets (<a href="/help/settings/workflowcustomizing">Einstellungen</a>) ermittelt.
	Beim Anfordern weiterer Genehmigungen &uuml;ber <a href="/help/settings/stateactions">Aktionen</a> kann in dessen Parametern ebenfalls eine Deadline gesetzt werden.
	Der Wert 0 hat dabei zur Folge, dass keine Deadline gesetzt wird.
');
INSERT INTO txt VALUES ('H8173', 'English', 'Deadline: During approval creation a deadline is set automatically. 
    It is computed from the ticket priority at task creation time (<a href="/help/settings/workflowcustomizing">Customizing</a>).
    The requesting of new approvals via <a href="/help/settings/stateactions">Actions</a> also allows setting of a deadline in the parameters.
    The value 0 results in setting no deadline.
');
INSERT INTO txt VALUES ('H8174', 'German',  'Zugewiesen: Eine Genehmigung kann einem anderen Nutzer explizit zugewiesen werden.');
INSERT INTO txt VALUES ('H8174', 'English', 'Assigned: An approval can be assigned to another user explicitly.');
INSERT INTO txt VALUES ('H8175', 'German',  'Genehmigt: Erreicht die Genehmigung einen Status im Ausgangsbereich, wird hier der aktuelle Zeitstempel gesetzt.');
INSERT INTO txt VALUES ('H8175', 'English', 'Approved: When the state of the approval reaches the exit range, the actual timestamp is set here.');
INSERT INTO txt VALUES ('H8176', 'German',  'Genehmiger: Hier wird automatisch der Nutzer, der den Ausgangsstatus gesetzt hat, gesetzt.');
INSERT INTO txt VALUES ('H8176', 'English', 'Approver: The user setting the state in the exit range is set here automatically.');
INSERT INTO txt VALUES ('H8177', 'German',  'Status: Status der Genehmigung.');
INSERT INTO txt VALUES ('H8177', 'English', 'State: State of the approval.');
INSERT INTO txt VALUES ('H8178', 'German',  'Kommentare: Nach dem Anlegen einer Genehmigung bis zur erfolgten Genehmigung k&ouml;nnen Kommentare hinzugef&uuml;gt werden.
	Dies ist &uuml;ber eine Schaltfl&auml;che in der Genehmigungs&uuml;bersicht oder beim Status&uuml;bergang (z.B. Ablehnung) selbst m&ouml;glich.
	Die Kommentare werden mit Datum und Autor aufgelistet und k&ouml;nnen nicht gel&ouml;scht werden.
');
INSERT INTO txt VALUES ('H8178', 'English', 'Comments: After creation of an approval until it is committed comments can be added.
    This can be done via a button in the approval overview or during state transition (e.g. reject).
    Comments are listed with date and author an can not be deleted.
');
INSERT INTO txt VALUES ('H8201', 'German',  'Je nach Art der beantragten Aufgaben k&ouml;nnen verschiedene Workflow-Varianten erforderlich sein. 
    Dies wird in den Tasktypen abgebildet, welche separat die jeweils n&ouml;tigen Felder anbieten und in verschiedenen Workflows konfiguriert werden k&ouml;nnen.
    Die f&uuml;r die Nutzer verf&uuml;gbaren Tasktypen werden in den <a href="/help/settings/workflowcustomizing">Einstellungen</a> freigeschaltet.
');
INSERT INTO txt VALUES ('H8201', 'English', 'Depending on the kind of the requested duties, different workflow variants may be necessary.
    They are represented in task types, which separately offer the needed fields and can be configured in different workflows.
    The available task types for the users are activiated in the <a href="/help/settings/workflowcustomizing">Customizing</a> settings.
');
INSERT INTO txt VALUES ('H8211', 'German',  'Generisch: Eine einfache Variante, in der die beauftragte T&auml;tigkeit in einem Freitextfeld beschrieben wird.');
INSERT INTO txt VALUES ('H8211', 'English', 'Generic: A basic variant, where the requested activity is described in a free text field.');
INSERT INTO txt VALUES ('H8212', 'German',  'Zugriff: Es wird eine Reihe von Feldern angeboten, die f&uuml;r einen Antrag auf Netzwerkzugriff n&ouml;tig sind. 
    Dazu geh&ouml;ren zwingend Angaben zu Quelle, Ziel und Dienst, Aktion, Regel-Aktion, Logging.
    Bei entsprechender Konfiguration (keine Planungsphase, "Ger&auml;t im Antrag eingeben" in <a href="/help/settings/workflowcustomizing">Einstellungen</a>) m&uuml;ssen auch die betroffenen Gateways selektiert werden.
    Hinzu kommen optionale Angaben wie G&uuml;ltigkeitszeitraum und Grund.
');
INSERT INTO txt VALUES ('H8212', 'English', 'Access: Several fields are offered, which are necessary to handle a request on network access.
    That includes mandatory specifications of source, destination and service, as well as action, rule action and logging.
    In case of the respective configuration (no planning phase, "Enter device in request" in <a href="/help/settings/workflowcustomizing">Customizing</a>) also the affected devices have to be selected.
    Additionally there are optional specifications like validity range and reason.
');
INSERT INTO txt VALUES ('H8213', 'German',  'Die weiteren vorgesehenen Tasktypen "Gruppe anlegen", "Gruppe &auml;ndern" und "Gruppe l&ouml;schen" k&ouml;nnen zwar aktiviert und genutzt werden, sind aber noch nicht mit spezifischen Feldern versehen.');
INSERT INTO txt VALUES ('H8213', 'English', 'Further task types "create group", "modify group" and "delete group" can be activated and used, but are not equipped with specific fields yet.');
INSERT INTO txt VALUES ('H8214', 'German',  'Regel l&ouml;schen: Hier muss zwingend das Gateway und die Uid der zu l&ouml;schenden Regel eingegeben werden.
    Diese wird gegen die tats&auml;chlich auf dem Gateway vorhandenen Uids gepr&uuml;ft.<br>
    Dieser Auftragstyp wird auch bei der automatischen Erzeugung aus dem Unbenutzte-Regel-Report bzw. bei der Dezertifizierung verwendet.
');
INSERT INTO txt VALUES ('H8214', 'English', 'Delete Rule: Mandatory input fields are the gateway and the Uid of the rule to be deleted.
    The Uid is checked against the rules actually existing on the gateway.<br>
    This Task Type is also used for the automatic creation of delete requests in the Unused Rules Report resp. in the decertification workflow. 
');
INSERT INTO txt VALUES ('H8301', 'German',  'Jeder Verarbeitungsschritt kann nur von Nutzern mit entsprechenden <a href="/help/settings/roles">Rollen</a> get&auml;tigt werden.
    Dabei k&ouml;nnen einzelnen Nutzern auch mehrere Rollen zufallen. Die Rollen k&ouml;nnen individuell oder &uuml;ber <a href="/help/settings/groups">Gruppenzugeh&ouml;rigkeit</a> zugewiesen werden.
    Hinzu kommt die Rolle des admin, welche einen Komplettzugriff erlaubt. Je nach Rolle des Bearbeiters sind nur die f&uuml;r ihn relevanten Teile der folgenden Rubriken sichtbar.
');
INSERT INTO txt VALUES ('H8301', 'English', 'Each processing step can only be done by users with adequate <a href="/help/settings/roles">Roles</a>.
    Although, single users can be in possession of several roles. Roles can be assigned individually or via <a href="/help/settings/groups">group membership</a>.
    Additionally there is the role of the admin, who has always full access. Depending on the roles of the user, only the relevant parts of the following chapters are visible.
');
INSERT INTO txt VALUES ('H8311', 'German',  'Ticket-Liste (Rolle: requester, fw-admin): 
    Dem Antragsteller steht eine &Uuml;bersicht &uuml;ber alle von ihm selbst angelegten Tickets aller Bearbeitungsstufen zur Verf&uuml;gung. Der fw-admin kann hier alle Tickets sehen.
    &Auml;nderungen an den Tickets sind in dieser Ansicht nicht m&ouml;glich.
');
INSERT INTO txt VALUES ('H8311', 'English', 'Ticket List (Role: requester, fw-admin):
    The requester gets an overview of all tickets in all processing states created by himself. The fw-admin has view on all tickets.
    Changes on the tickets are not possible in this view. 
');
INSERT INTO txt VALUES ('H8312', 'German',  'Antrag stellen (Rolle: requester), voreingestellt: 
    Antr&auml;ge k&ouml;nnen nur von Nutzern mit entsprechenden Rechten gestellt werden, definiert durch die Rolle (weitere Einschr&auml;nkungen auf die Eigent&uuml;merschaft ist in sp&auml;teren Versionen vorgesehen).
    Solange noch kein Status des Ausgangsbereichs erreicht wurde, k&ouml;nnen Tickets beliebig ge&auml;ndert und fachliche Auftr&auml;ge angeh&auml;ngt, ge&auml;ndert oder gel&ouml;scht werden.
    Um Inkonsistenzen zu vermeiden, werden angelegte Auftr&auml;ge erst beim ersten Speichern des Tickets mit erzeugt. Vorher sind sie nur lokal vorhanden und gehen beim Abbruch der Antragstellung verloren.
    In sp&auml;teren Phasen sind keine inhaltlichen &Auml;nderungen an Ticket und fachlichen Auftr&auml;gen mehr m&ouml;glich, lediglich an Metadaten wie Status, Start und Stop sowie Kommentierungen.
');
INSERT INTO txt VALUES ('H8312', 'English', 'Create ticket (Role: requester), preselected:
    Requests can only be created by users with according rights, defined by the roles (further restrictions on ownership are envisaged for later releases).
    As long as no state in the exit range is reached, tickets can be changed arbitrarily, and request task can be added, changed or deleted.
    To avoid inconsistencies, already built request tasks are created with the first saving of the ticket. Before, they exist only locally and get lost on cancellation of ticket creation.
    In later phases no changes of the ticket and request tasks contents are possible, only changes on metadata like state, start and stop, as well as adding comments can be done.
');
INSERT INTO txt VALUES ('H8313', 'German',  'Genehmigungen (Rolle: approver), voreingestellt: 
    Der Workflow kann einen verpflichtenden Genehmigungsschritt vorsehen, bevor der Antrag weiter bearbeitet werden kann (<a href="/help/settings/statematrix">Status-Matrix</a>).
    Beim erstmaligen Speichern des Antrags wird automatisch pro Auftrag ein Genehmigungsobjekt angelegt. 
    Konfigurierbar &uuml;ber <a href="/help/workflow/actions">Aktionen</a> k&ouml;nnen in beliebigen Phasen weitere Genehmigungen angefordert werden.
    Der Genehmiger kann das Ticket und die Auftr&auml;ge nicht mehr inhaltlich ver&auml;ndern, lediglich einen neuen Status setzen und Kommentare hinzuf&uuml;gen.
    Hinzu kommen gegebenenfalls weitere vorkonfigurierte eingeblendete Aktionen.
    Ein Auftrag gilt dann als genehmigt, wenn alle einzelnen Genehmigungen den entsprechenden Status erreicht haben. Danach kann der Genehmiger keine &Auml;nderungen mehr vornehmen.
');
INSERT INTO txt VALUES ('H8313', 'English', 'Approvals (Role: approver), preselected:
    The workflow can be designed to contain a mandatory approval step before further work on the request ticket can be done (<a href="/help/settings/statematrix">State matrix</a>).
    In the course of first saving the request ticket, an approval object is created automatically for each request task.
    Configurable in <a href="/help/workflow/actions">Actions</a>, further approvals can be requested in arbitrary phases.
    The approver is not allowed to make changes on the content of ticket and tasks, only set a new state or add comments.
    Additionally there may be further preconfigured shown actions.
    A request task counts as approved, if all single approvals have reached the appropriate state. Henceforward the approver can not perform changes anymore.
');
INSERT INTO txt VALUES ('H8314', 'German',  'Planungen (Rolle: planner, fw-admin), optional: 
    Im Workflow kann vorgesehen werden, dass die Implementierungs-Auftr&auml;ge aus den fachlichen Auftr&auml;gen manuell von einem Planer erzeugt werden.
    Ist diese Phase aktiviert, greift die automatische Erzeugung der Implementierungs-Auftr&auml;ge nicht (<a href="/help/settings/workflowcustomizing">Einstellungen</a>).
    Stattdessen kann der Planer beliebige Implementierungs-Auftr&auml;ge erzeugen, editieren und l&ouml;schen.
    Dabei werden die Felder aus den analogen Feldern des fachlichen Auftrags zwar weitgehend vorbelegt, k&ouml;nnen aber beliebig den Erfordernissen entsprechend abge&auml;ndert werden.
    F&uuml;r die Bearbeitung von Zugriffsauftr&auml;gen stehen ihm zus&auml;tzliche Funktionen zur Pfadanalyse zur Verf&uuml;gung (soweit in den <a href="/help/settings/workflowcustomizing">Einstellungen</a> aktiviert):
    Zum &Uuml;berpr&uuml;fen, f&uuml;r welche der bei der Pfadanalyse gefundenen Ger&auml;te bereits Implementierungs-Auftr&auml;ge angelegt sind,
    zum automatischen Anlegen von Implementierungs-Auftr&auml;gen f&uuml;r alle der bei der Pfadanalyse gefundenen Ger&auml;te (soweit noch nicht vorhanden),
    sowie zum L&ouml;schen aller vorhandenen Implementierungs-Auftr&auml;ge.
    Die fachlichen Auftr&auml;ge k&ouml;nnen in dieser Phase auch anderen Nutzern oder Gruppen zugewiesen werden. 
    Bei Bet&auml;tigen der entsprechenden Schaltfl&auml;che erscheint eine Auswahlliste aller Nutzer und internen Gruppen, welche den notwendigen Rollen f&uuml;r diese Planungsphase besitzen.
    Wurde einem selbst auf diese Weise der Auftrag zugewiesen, wird auch eine Option zum direkten Zur&uuml;ckzuweisen angeboten.
');
INSERT INTO txt VALUES ('H8314', 'English', 'Plannings (Role: planner, fw-admin), optional:
    The workflow can be designed to create implementation tasks from the request tasks manually by a planner.
    In case this phase is active, the automatic creation of implementation tasks is deactivated (<a href="/help/settings/workflowcustomizing">Customizing</a>).
    Instead, the planner can create, edit or delete arbitrarily implementation tasks.
    When creating, the fields are largely prefilled by the corresponding fields in the request task, but can be changed according to the needs.
    For the handling of access requests further path analysis functions are offered (if activated in <a href="/help/settings/workflowcustomizing">Customizing</a>):
    To check, for which of the found devices of the path analysis there are already implementation tasks existing,
    to create automatically implemntation tasks for all devices found in path analysis (if not already existing),
    as well as to delete all existing implementation tasks. 
    The request tasks can also be assigned to other users or groups in this phase.
    After pushing the respective button a selection list appears with all users and groups, which own the necessary roles for the planning phase.
    If the task had been assigned to oneself this way, an option for direct assigning back is shown.
');
INSERT INTO txt VALUES ('H8315', 'German',  'Implementierungen (Rolle: implementer, fw-admin), voreingestellt: 
    Hier wird die technische Umsetzung der einzelnen Auftr&auml;ge unterst&uuml;tzt und dokumentiert. Die fachlichen Auftr&auml;ge sind im Ticket nicht sichtbar, lediglich die Implementierungs-Auftr&auml;ge.
    In der &Uuml;bersicht k&ouml;nnen f&uuml;r den Nutzer auch statt der Tickets direkt alle Implementierungs-Auftr&auml;ge oder nur die Implementierungs-Auftr&auml;ge f&uuml;r ein Ger&auml;t dargestellt werden.
    Die Implementierungs-Auftr&auml;ge k&ouml;nnen in dieser Phase auch anderen Nutzern oder Gruppen zugewiesen werden. 
    Bei Bet&auml;tigen der entsprechenden Schaltfl&auml;che erscheint eine Auswahlliste aller Nutzer und internen Gruppen, welche den notwendigen Rollen f&uuml;r Implementierungsphase besitzen.
    Wurde einem selbst auf diese Weise der Auftrag zugewiesen, wird auch eine Option zum direkten Zur&uuml;ckzuweisen angeboten.
');
INSERT INTO txt VALUES ('H8315', 'English', 'Implementations (Role: implementer, fw-admin), preselected:
    Here the technical realization of the single tasks is supported and documented. Functional (request) tasks are not visible, only the implementation tasks.
    In the overview, instead of the tickets, also a list of all implementation task or the implementation tasks for a special device can be displayed.
    The implementation tasks can also be assigned to other users or groups in this phase.
    After pushing the respective button a selection list appears with all users and groups, which own the necessary roles for the implementation phase.
    If the task had been assigned to oneself this way, an option for direct assigning back is shown.
');
INSERT INTO txt VALUES ('H8316', 'German',  'Reviews (Rolle: reviewer), optional: 
    Abschliessend kann der Workflow einen Review-Schritt vorsehen, um die Umsetzung des Antrags zu &uuml;berpr&uuml;fen. Dazu werden die Tickets mitsamt der Implementierungs-Auftr&auml;ge dargestellt.
');
INSERT INTO txt VALUES ('H8316', 'English', 'Reviews (Role: reviewer), optional: 
    Finally the workflow can contain a review phase to check the implementation of the request. Therefore the tickets are displayed with their implementation tasks.
');
INSERT INTO txt VALUES ('H8317', 'German',  'Weitere Phasen zum Verifizieren und Rezertifizieren sind vorbereitet, aber noch nicht implementiert.');
INSERT INTO txt VALUES ('H8317', 'English', 'Further phases for verification and recertification are prepared but not implemented yet.');
INSERT INTO txt VALUES ('H8401', 'German',  'Stati und deren &Uuml;berg&auml;nge bilden die Basis der Workflows. Bei der Konfiguration k&ouml;nnen sie frei definiert und benannt werden (<a href="/help/settings/statedefinitions">Statusdefinitionen</a>).
    Durch geeignete Wahl der Nummernkreise werden die Stati dann in den verschiedenen Phasen sichtbar bzw. benutzbar, was in den <a href="/help/settings/statematrix">Status-Matrizen</a> definiert wird.
    In einer Status-Matrix werden pro Phase alle vorkommenden Stati mitsamt den m&ouml;glichen Status&uuml;berg&auml;ngen festgelegt.
    Ausserdem werden drei Bereiche bestimmt: Eingangs-, Bearbeitungs- und Ausgangsbereich, welche f&uuml;r die Bearbeitbarkeit in der jeweiligen Phase entscheidend sind.
    F&uuml;r das Ticket und jeden einzelnen Tasktypen werden separate Status-Matrizen angelegt, so dass sich deren Workflows unterscheiden k&ouml;nnen.
    Auch m&uuml;ssen hier die Beziehungen der Stati der verschiedenen Objekttypen zueinander festgelegt werden.
');
INSERT INTO txt VALUES ('H8401', 'English', 'States and their transitions are the basis of the workflows. During configuration they can be defined and named freely (<a href="/help/settings/statedefinitions">State Definitions</a>).
    Appropriate selection of number ranges make the states visible and usable in the different phases, which is defined in <a href="/help/settings/statematrix">State Matrices</a>.
    In a state matrix all occurring states together with their transitions are defined per phase.
    Additionally three ranges are set: Input, started and exit range, which decide about the possibility of making changes within the phase.
    Ticket and the different task types get separate state matrices, so their workflows can vary.
    Furtheron the relations between the states of the different object types have to be defined here.
');
INSERT INTO txt VALUES ('H8501', 'German',  'Die Aktionen der verschiedenen Typen dienen der Unterst&uuml;tzung und Automatisierung der Bearbeitung der Auftr&auml;ge.
    Dazu geh&ouml;ren automatische Status-Weiterleitungen, das Anfordern weiterer Genehmigungen oder das Ausl&ouml;sen eines Alarms. Auch die Konfiguration f&uuml;r Aufrufe externer Komponenten ist vorgesehen.
    Aktionen sind an Bedingungen gebunden und werden bestimmten Stati zugewiesen (<a href="/help/settings/stateactions">Aktionen anlegen</a>).
    Sie bewirken bei Eintreffen der Bedingungen entweder eine automatische Ausf&uuml;hrung oder das Aufblenden einer Schaltfl&auml;che zur manuellen Ausf&uuml;hrung.
    Bislang stehen folgende Aktionen zur Auswahl:
');
INSERT INTO txt VALUES ('H8501', 'English', 'Actions of different types provide a basis to support and automate the request handling.
    That includes automatic state forwarding, adding new approvals or raising an alert. The configuration of calls to external components is planned.
    Actions are bound to conditions and have to be assigned to certain states (<a href="/help/settings/stateactions">Configure Actions</a>).
    If the conditions are met, they lead to automatic execution or the display of a button for manual execution of the action.
    Currently following actions can be selected:
');
INSERT INTO txt VALUES ('H8511', 'German',  'Autom. Weiterleitung: Obwohl die Statusweiterleitung mit dem Mechanismus der Status-Matrix weitgehend abgebildet werden kann, erweitert diese Aktion die M&ouml;glichkeiten.
	So kann die Weiterleitung st&auml;rker auf bestimmte Objekttypen eingeschr&auml;nkt werden (die Status-Matrix gilt f&uuml;r alle Objekte eines Tasktyps). 
	Auch ein Aufblenden einer speziellen Weiterleitung als "Shortcut" kann erw&uuml;nscht sein.
');
INSERT INTO txt VALUES ('H8511', 'English', 'Auto-forward: Although state forwarding can widely be realized by the state matrix mechanism, this action enlarges the options.
    The forwarding can be more restricted to dedicated object types (the state matrix is valid for all object types within a task type).
    Additionally the display of a special state transition as "Shortcut" may be desired.
');
INSERT INTO txt VALUES ('H8512', 'German',  'Genehmigung hinzuf&uuml;gen: Wenn im Verlauf des Workflows (z.B. vom Planer) festgestellt wird, dass weitere Genehmigungen eingeholt werden m&uuml;ssen, 
    kann man mit dieser Aktion weitere Genehmigungsobjekte erzeugen und zuweisen.
');
INSERT INTO txt VALUES ('H8512', 'English', 'Add approval: If in the course of the workflow it is realized (e.g. by the planner) that further approvals are necessary,
    additional approval objects can be created and assigned with this action.
');
INSERT INTO txt VALUES ('H8513', 'German',  'Alarm ausl&ouml;sen: Unter Umst&auml;nden kann eine gezielte Alarmierung in einem Workflow n&uuml;tzlich sein (z.B. durch den Reviewer nach einer festgestellten Fehlimplementierung).');
INSERT INTO txt VALUES ('H8513', 'English', 'Set alert: Possibly a specific alerting within a workflow may be useful (e.g. by the reviewer in case of a wrong or dangerous implementaion).');
INSERT INTO txt VALUES ('H8514', 'German',  'Externer Aufruf: Aufrufe externer Komponenenten bieten ein weites Spektrum von Erweiterungs- und Integrationsm&ouml;glichkeiten, die stark vom Systemumfeld abh&auml;ngen.
	Hier sind f&uuml;r kommende Releases die Ankn&uuml;pfungspunkte f&uuml;r Erweiterungen vorgesehen.
');
INSERT INTO txt VALUES ('H8514', 'English', 'External call: Calls to external components provide a wide range of extension or integration possibilities, which strongly depend on the system environment.
    Here connecting factors for extensions future releases are planned.
');
INSERT INTO txt VALUES ('H8515', 'German',  'Pfadanalyse: Die in der automatischen Pfadanalyse gefundenen Ger&auml;te werden als Liste der Ger&auml;te eines Zugriffs-Auftrags &uuml;bernommen oder in einem eigenen Fenster dargestellt.');
INSERT INTO txt VALUES ('H8515', 'English', 'Path Analysis: The devices found in the automatic path analysis are transferred to the list of devices of a request task or displayed in an own window.');
INSERT INTO txt VALUES ('H8601', 'German',  'Zum Aufsetzen eines Workflows empfiehlt es sich, in folgenden Schritten vorzugehen:
    <ul>
        <li><a href="/help/settings/workflowcustomizing">Einstellungen</a>: Auswahl der zu verwendenden Tasktypen</li>
        <li><a href="/help/settings/statematrix">Status-Matrizen</a>: Festlegen der Phasen pro Tasktyp</li>
        <li><a href="/help/settings/statedefinitions">Status-Definitionen</a>: Definition der benutzten Stati</li>
        <li><a href="/help/settings/statematrix">Status-Matrizen</a>: Erstellen der Status-Matrizen pro Tasktyp</li>
        <li><a href="/help/settings/stateactions">Aktionen</a>: Definition der Aktionen falls n&ouml;tig</li>
        <li><a href="/help/settings/statedefinitions">Status-Definitionen</a>: Zuordnung Aktionen zu Stati</li>
        <li><a href="/help/settings/workflowcustomizing">Einstellungen</a>: Festlegen der Priorit&auml;ten und Deadlines</li>
        <li><a href="/help/settings/workflowcustomizing">Einstellungen</a>: Ausw&auml;hlen der Option zum "Autom. Erzeugen von Implementierungs-Auftr&auml;gen", falls Planungsphase deaktiviert</li>
        <li><a href="/help/settings/groups">Gruppen</a>: Nutzergruppen einrichten und Nutzer zuweisen</li>
        <li><a href="/help/settings/roles">Rollen</a>: Rollenzuweisungen zu den Nutzern/Gruppen</li>
    </ul>
');
INSERT INTO txt VALUES ('H8601', 'English', 'For the setup of a workflow it is suggested to proceed in following steps:
    <ul>
        <li><a href="/help/settings/workflowcustomizing">Customizing</a>: Selection of the task types to be applied</li>
        <li><a href="/help/settings/statematrix">State Matrices</a>: Definition of task types per phase</li>
        <li><a href="/help/settings/statedefinitions">State Definitions</a>: Definition of the used states</li>
        <li><a href="/help/settings/statematrix">State Matrices</a>: Construct the state matrices per task type</li>
        <li><a href="/help/settings/stateactions">Actions</a>: Definition of the actions if needed</li>
        <li><a href="/help/settings/statedefinitions">State Definitions</a>: Assignment of actions to states</li>
        <li><a href="/help/settings/workflowcustomizing">Customizing</a>: Define the priority levels and deadlines</li>
        <li><a href="/help/settings/workflowcustomizing">Customizing</a>: Select option for "Auto-create implementation tasks", if planning phase deactivated</li>
        <li><a href="/help/settings/groups">Groups</a>: Set up user groups and assign users</li>
        <li><a href="/help/settings/roles">Roles</a>: Assign roles to users/groups</li>
    </ul>
');
INSERT INTO txt VALUES ('H8701', 'German',  'Die folgenden Beispiele sollen ein Schlaglicht auf die verschiedenen Konfigurationsm&ouml;glichkeiten des Workflowmoduls werfen.
    Sie k&ouml;nnen gleichzeitig oder voneinander unabh&auml;ngig ausprobiert werden (mit Ausnahme von Beispiel 5, welches auf den in Beispiel 4 definierten Stati aufsetzt). 
    Es wurden hier englische Namen f&uuml;r Stati oder Aktionen gew&auml;hlt (wie sie ja auch vorinstalliert sind),
    eine beliebige Umbenennung (z. B. &Uuml;bersetzung ins Deutsche) ist nat&uuml;rlich jederzeit und einfach m&ouml;glich.
');
INSERT INTO txt VALUES ('H8701', 'English', 'Following examples are intended to give an idea about the different configuration options of the workflow module.
    They can be used all together or independently (with exeption of example 5, which uses the states defined in example 4).
    Names of states or actions are given arbitrarily and can be changed (e.g. translated) easily at any time.
');
INSERT INTO txt VALUES ('H8711', 'German',  '<H4>1) Einf&uuml;gen eines neuen Status in die Genehmigungsphase</H4>
    Dient zum Markieren von "geparkten" offenen Genehmigungen (nur zu erreichen von "In Approval" und zur&uuml;ck)
    <ul>
        <li>Einstellungen -&amp;gt; Statusdefinitionen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; Eingabe Id: 61 
            (Nummer im Bereich zwischen niedrigstem Bearbeitungs- und niedrigstem Ausgangsstatus der Genehmigungsphase), Name: "Approval on Hold" -&amp;gt; Speichern</li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Master
            <ul>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Approval on Hold" ausw&auml;hlen</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: "Approval on Hold" bearbeiten -&amp;gt; Status hinzuf&uuml;gen 
                    -&amp;gt; "Approval on Hold" ausw&auml;hlen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "In Approval" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: "In Approval" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Approval on Hold" ausw&auml;hlen -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Zugriff
            <ul>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Approval on Hold" ausw&auml;hlen</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: "Approval on Hold" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Approval on Hold" ausw&auml;hlen 
                    -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "In Approval" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: "In Approval" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Approval on Hold" ausw&auml;hlen -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8711', 'English', '<H4>1) Insert new state to approval phase</H4>
    Serves for marking of "parked" open approvals (only reachable from "In Approval" and back)
    <ul>
        <li>Settings -&amp;gt; State Definitions -&amp;gt; Add State -&amp;gt; Insert Id: 61 
            (Id has to be in the range between lowest started state and lowest exit state of approval phase), Name: "Approval on Hold" -&amp;gt; Save</li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Master
            <ul>
                <li>Approval: Allowed transitions: Add State -&amp;gt; Select "Approval on Hold"</li>
                <li>Approval: Allowed transitions: Edit "Approval on Hold" -&amp;gt; Add State -&amp;gt; Select "Approval on Hold" -&amp;gt; Add State -&amp;gt; Select "In Approval" -&amp;gt; Ok</li>
                <li>Approval: Allowed transitions: Edit "In Approval" -&amp;gt; Add State -&amp;gt; Select "Approval on Hold" -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Access
            <ul>
                <li>Approval: Allowed transitions: Add State -&amp;gt; Select "Approval on Hold"</li>
                <li>Approval: Allowed transitions: Edit "Approval on Hold" -&amp;gt; Add State -&amp;gt; Select "Approval on Hold" -&amp;gt; Add State -&amp;gt; Select "In Approval" -&amp;gt; Ok</li>
                <li>Approval: Allowed transitions: Edit "In Approval" -&amp;gt; Add State -&amp;gt; Select "Approval on Hold" -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8712', 'German',  '<H4>2) R&uuml;cksprung in vorherige Phase</H4>
    Soll beispielsweise dem Genehmiger erlaubt werden, den Antrag an den Antragsteller zur&uuml;ckzuschicken, 
    kann einfach in der Status-Matrix ein &Uuml;bergang zu einem Status im Eingangsbeerich der Antragsphase eingetragen werden.
    Dann wird dieser Status beim Genehmigen automatisch als m&ouml;glicher Zielstatus angezeigt.
    Zur leichteren Erkennung f&uuml;r den Antragsteller wird in diesem Beispiel in der anzuspringenden Phase ein weiterer Status definiert und in der Status-Matrix mit den erw&uuml;nschten Status&uuml;berg&auml;ngen versehen:
    <ul>
        <li>Einstellungen -&amp;gt; Statusdefinitionen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; Eingabe Id: 1, Name: "Back To Requester" -&amp;gt; Speichern 
            (Nummer im Bereich zwischen niedrigstem Eingangs- und niedrigstem Bearbeitungsstatus der Request-Phase)</li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Master
            <ul>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Back To Requester" ausw&auml;hlen</li>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: "Back To Requester" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Back To Requester" ausw&auml;hlen 
                    -&amp;gt; Status hinzuf&uuml;gene -&amp;gt; "Requested" ausw&auml;hlen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Discarded" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Back To Requester" ausw&auml;hlen</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Zugriff 
            <ul>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Back To Requester" ausw&auml;hlen</li>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: "Back To Requester" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Back To Requester" ausw&auml;hlen 
                    -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Requested" ausw&auml;hlen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Discarded" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: "In Approval" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Back To Requester" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Back To Requester" ausw&auml;hlen</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: "Back To Requester" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "In Approval" ausw&auml;hlen -&amp;gt; Status hinzuf&uuml;gen 
                    -&amp;gt; "Approved" ausw&auml;hlen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Rejected" ausw&auml;hlen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Back To Requester" ausw&auml;hlen -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8712', 'English', '<H4>2) Jump back to previous phase</H4>
    If an approver should be able to send back a ticket to the requester, a new transition to a state in the input range of the request phase can be inserted easily into the state matrix.
    This state is then offered automatically as target state for the approver.
    For easier identification for the requester, in this example a new state is defined and equipped with the necessary transitions in the state matrix:
    <ul>
        <li>Settings -&amp;gt; State Definitions -&amp;gt; Add State -&amp;gt; Insert Id: 1, Name: "Back To Requester" -&amp;gt; Save 
            (Id in the range between lowest imput and lowest started state of Request phase)</li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Master
            <ul>
                <li>Request: Allowed transitions: Add State -&amp;gt; Select "Back To Requester"</li>
                <li>Request: Allowed transitions: Edit "Back To Requester" -&amp;gt; Add State -&amp;gt; Select "Back To Requester" 
                    -&amp;gt; Add State -&amp;gt; Select "Requested" -&amp;gt; Add State -&amp;gt; Select "Discarded" -&amp;gt; Ok</li>
                <li>Approval: Allowed transitions: Add State -&amp;gt; Select "Back To Requester"</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Access 
            <ul>
                <li>Request: Allowed transitions: Add State -&amp;gt; Select "Back To Requester"</li>
                <li>Request: Allowed transitions: Edit "Back To Requester" -&amp;gt; Add State -&amp;gt; Select "Back To Requester" 
                    -&amp;gt; Add State -&amp;gt; Select "Requested" -&amp;gt; Add State -&amp;gt; Select "Discarded" -&amp;gt; Ok</li>
                <li>Approval: Allowed transitions: Edit "In Approval" -&amp;gt; Add State -&amp;gt; Select "Back To Requester" -&amp;gt; Ok</li>
                <li>Approval: Allowed transitions: Add State -&amp;gt; Select "Back To Requester"</li>
                <li>Approval: Allowed transitions: Edit "Back To Requester" -&amp;gt; Add State -&amp;gt; Select "In Approval" -&amp;gt; Add State -&amp;gt; Select "Approved" 
                    -&amp;gt; Add State -&amp;gt; Select "Rejected" -&amp;gt; Add State -&amp;gt; Select "Back To Requester" -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8713', 'German',  '<H4>3) Auslassen von Phasen f&uuml;r bestimmten Tasktyp</H4>
    In diesem Beispiel soll die Genehmigungsphase f&uuml;r Generische Aufgaben &uuml;bersprungen werden:
    <ul>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Generisch
            <ul>
                <li>Genehmigung: Phase deselektieren (die &Uuml;bergangsmatrix wird ausgeblendet)
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: "Requested" bearbeiten -&amp;gt; Abgeleiteter Status: "To Implement" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "To Implement" ausw&auml;hlen</li>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: "To Implement" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "In Implementation" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: "Approved" l&ouml;schen (wird nicht mehr ben&ouml;tigt)</li>
                <li>Implementierung: Spezielle Stati: Niedrigster Eingangsstatus: "To Implement" ausw&auml;hlen</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8713', 'English', '<H4>3) Skip phase for specific Task Type</H4>
    In this example the approval phase for generic task is skipped: 
    <ul>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Generic
            <ul>
                <li>Approval: unselect phase (the transition matrix disappears)
                <li>Request: Allowed transitions: Edit "Requested" -&amp;gt; Derived State: Select "To Implement" -&amp;gt; Ok</li>
                <li>Implementation: Allowed transitions: Add State -&amp;gt; Select "To Implement"</li>
                <li>Implementation: Allowed transitions: Edit "To Implement" -&amp;gt; Add State -&amp;gt; Select "In Implementation" -&amp;gt; Ok</li>
                <li>Implementation: Allowed transitions: Remove "Approved" (not necessary any more)</li>
                <li>Implementation: Special States: Lowest input state: Select "To Implement"</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8714', 'German',  '<H4>4) Aktion Autom. Weiterleitung</H4>
    Als weitere Option ist es auch m&ouml;glich, eine Aktion vom Typ Autom. Weiterleitung zu nutzen.
    In diesem Beispiel wird beim Reject durch den Implementer (nur zu erreichen nach vorherigem Status "Implementation Trouble") das Ticket automatisch wieder dem Requester zur Best&auml;tigung vorgelegt
    (der Einfachheit halber werden die vorhandenen Stati soweit m&ouml;glich weiterverwendet, eine Definition weiterer Stati wie "Acknowledge Reject" und "Try again" w&uuml;rde sich aber anbieten):
    <ul>
        <li>Einstellungen -&amp;gt; Statusdefinitionen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; Eingabe Id: 2, Name "Rejected By Implementer" -&amp;gt; Speichern</li>
        <li>Einstellungen -&amp;gt; Statusaktionen -&amp;gt; Aktion hinzuf&uuml;gen -&amp;gt; Name: "Acknowledge Reject", Aktionstyp: "Autom. Weiterleitung", Ereignis: "Beim Erreichen", Phase: "Implementierung", 
            Geltungsbereich: "Implementierungs-Auftrag", Tasktyp: "All", Zielstatus: "Rejected By Implementer" -&amp;gt; Speichern</li>
        <li>Einstellungen -&amp;gt; Statusdefinitionen -&amp;gt; "Rejected" bearbeiten -&amp;gt; Aktion hinzuf&uuml;gen -&amp;gt; "Acknowledge Reject" ausw&auml;hlen -&amp;gt; Speichern</li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Master
            <ul>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Rejected By Implementer" ausw&auml;hlen</li>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: "Rejected By Implementer" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Discarded" ausw&auml;hlen 
                    -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Requested" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Rejected By Implementer" ausw&auml;hlen</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Generisch
            <ul>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Rejected By Implementer" ausw&auml;hlen</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Zugriff
            <ul>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Rejected By Implementer" ausw&auml;hlen</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8714', 'English', '<H4>4) Action Auto-forward</H4>
    As further option it is possible to use an action of type Auto-forward.
    In this example in case of a reject by the implementer (only reachable from the preceeding state Implementation Trouble") the ticket is assigned back to the requester for confirmation
    (for simplicity the existing states are reused if possible, a definition of further states as "Acknowledge Reject" and "Try again" would be appropriate):
    <ul>
        <li>Settings -&amp;gt; State Definitions -&amp;gt; Add State -&amp;gt; Insert Id: 2, Name "Rejected By Implementer" -&amp;gt; Save</li>
        <li>Settings -&amp;gt; State Actions -&amp;gt; Add Action -&amp;gt; Name: "Acknowledge Reject", Action Type: "Auto-forward", Event: "On Set", Phase: "Implementation", 
            Scope: "Implementation Task", Task Type: "All", To State: "Rejected By Implementer" -&amp;gt; Save</li>
        <li>Settings -&amp;gt; State Definitions -&amp;gt; Edit "Rejected" -&amp;gt; Add Action -&amp;gt; Select "Acknowledge Reject" -&amp;gt; Save</li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Master
            <ul>
                <li>Request: Allowed transitions: Add State -&amp;gt; Select "Rejected By Implementer"</li>
                <li>Request: Allowed transitions: Edit "Rejected By Implementer" -&amp;gt; Add State -&amp;gt; Select "Discarded" -&amp;gt; Add State -&amp;gt; Select "Requested" -&amp;gt; Ok</li>
                <li>Implementation: Allowed transitions: Add State -&amp;gt; Select "Rejected By Implementer"</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Generic
            <ul>
                <li>Implementation: Allowed transitions: Add State -&amp;gt; Select "Rejected By Implementer"</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Access
            <ul>
                <li>Implementation: Allowed transitions: Add State -&amp;gt; Select "Rejected By Implementer"</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8715', 'German',  '<H4>5) Automatische Aktion Genehmigung hinzuf&uuml;gen</H4>
    Nun soll f&uuml;r den erneuten Versuch nach "Rejected By Implementer" (Beispiel 4) statt die alte zu &uuml;berschreiben eine neue Genemigung erzeugt werden.
    Daf&uuml;r wird der noch unbenutzte Status "To approve" nach "Requested again" umbenannt (nat&uuml;rlich k&ouml;nnte stattdessen auch ein neuer Status definiert werden):
    <ul>
        <li>Einstellungen -&amp;gt; Statusdefinitionen -&amp;gt; Status "50: To Approve" bearbeiten -&amp;gt; Name &auml;ndern in "Requested again" -&amp;gt; Speichern</li>
        <li>Einstellungen -&amp;gt; Statusaktionen -&amp;gt; Aktion hinzuf&uuml;gen -&amp;gt; Name: "Reapprove", Aktionstyp: "Genehmigung hinzuf&uuml;gen", Ereignis: "Beim Erreichen", 
            Phase: "Antrag", Geltungsbereich: "fachlicher Auftrag", Tasktyp: "Zugriff", Zielstatus: "Requested again" -&amp;gt; Speichern</li>
        <li>Einstellungen -&amp;gt; Statusdefinitionen -&amp;gt; "Requested again" bearbeiten -&amp;gt; Aktion hinzuf&uuml;gen -&amp;gt; "Reapprove" ausw&auml;hlen -&amp;gt; Speichern</li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Master
            <ul>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Requested again" ausw&auml;hlen</li>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: "Rejected By Implementer" bearbeiten -&amp;gt; "Requested" l&ouml;schen -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "Requested again" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Requested again" ausw&auml;hlen</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: "Requested again" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "In Approval" ausw&auml;hlen -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Zugriff
            <ul>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Requested again" ausw&auml;hlen</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Requested again" ausw&auml;hlen</li>
                <li>Genehmigung: Erlaubte &Uuml;berg&auml;nge: "Requested again" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "In Approval" ausw&auml;hlen -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlene: Generisch
            <ul>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Requested again" ausw&auml;hlen</li>
                <li>Antrag: Erlaubte &Uuml;berg&auml;nge: "Requested again" bearbeiten -&amp;gt; Abgeleiteter Status: "To Implement" ausw&auml;hlen -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8715', 'English', '<H4>5) Automatic action Add Approval</H4>
    Now, when resending the request after reaching the state "Rejected By Implementer" (example 4), instead of overwriting the old approval a new approval should be created.
    Therefore the currently unused state "To approve" is renamed to "Requested again" (of course instead a new state could be defined):
    <ul>
        <li>Settings -&amp;gt; State Definitions -&amp;gt; Edit State "50: To Approve" -&amp;gt; Change Name to "Requested again" -&amp;gt; Save</li>
        <li>Settings -&amp;gt; State Actions -&amp;gt; Add Action -&amp;gt; Name: "Reapprove", Action Type: "Add approval", Event: "On Set", 
            Phase: "Request", Scope: "Request Task", Task Type: "Access", To State: "Requested again" -&amp;gt; Save</li>
        <li>Settings -&amp;gt; State Definitions -&amp;gt; Edit "Requested again" -&amp;gt; Add Action -&amp;gt; Select "Reapprove" -&amp;gt; Save</li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Master
            <ul>
                <li>Request: Allowed transitions: Add State -&amp;gt; Select "Requested again"</li>
                <li>Request: Allowed transitions: Edit "Rejected By Implementer" -&amp;gt; Remove "Requested" -&amp;gt; Add State -&amp;gt; Select "Requested again" -&amp;gt; Ok</li>
                <li>Approval: Allowed transitions: Add State -&amp;gt; Select "Requested again"</li>
                <li>Approval: Allowed transitions: Edit "Requested again" -&amp;gt; Add State -&amp;gt; Select "In Approval" -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Access
            <ul>
                <li>Request: Allowed transitions: Add State -&amp;gt; Select "Requested again"</li>
                <li>Approval: Allowed transitions: Add State -&amp;gt; Select "Requested again"</li>
                <li>Approval: Allowed transitions: Edit "Requested again" -&amp;gt; Add State -&amp;gt; Select "In Approval" -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Generic
            <ul>
                <li>Request: Allowed transitions: Add State -&amp;gt; Select "Requested again"</li>
                <li>Request: Allowed transitions: Edit "Requested again" -&amp;gt; Derived State: Select "To Implement" -&amp;gt; Ok</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8716', 'German',  '<H4>6) Aktion Genehmigung hinzuf&uuml;gen als Schaltfl&auml;che</H4>
    Der Genehmiger soll die M&ouml;glichkeit bekommen, bei Bedarf ein weiteres Approval zu erzeugen (um es z.B. jemand anderem zuzuweisen).
    Daf&uuml;r soll eine Schaltfl&auml;che mit dem Text "weitere Genehmigung erforderlich" angeboten werden, die beim Bearbeiten des Auftrags im Status "In approval" erscheint:
    <ul>
        <li>Einstellungen -&amp;gt; Statusaktionen -&amp;gt; Aktion hinzuf&uuml;gen -&amp;gt; Name: "FurtherApproval", Aktionstyp: "Genehmigung hinzuf&uuml;gen", Ereignis: "Schaltfl&auml;che anbieten", 
            Schaltertext: "weitere Genehmigung erforderlich", Phase: "Genehmigung", Geltungsbereich: "fachlicher Auftrag", Tasktyp: "Zugriff", Zielstatus: "Requested" -&amp;gt; Speichern</li>
        <li>Einstellungen -&amp;gt; Statusdefinitionen -&amp;gt; "In approval" bearbeiten -&amp;gt; Aktion hinzuf&uuml;gen -&amp;gt; "FurtherApproval" ausw&auml;hlen -&amp;gt; Speichern</li>
    </ul>
');
INSERT INTO txt VALUES ('H8716', 'English', '<H4>6) Action Add Approval as button</H4>
    The approver should get the possibility to create a further approval (e.g. to assign it to someone else).
    To achieve this, a button with the text "Further approval needed" is offered, which appears when working on the task with the state "In approval":
    <ul>
        <li>Settings -&amp;gt; State Actions -&amp;gt; Add Action -&amp;gt; Name: "FurtherApproval", Action Type: "Add approval", Event: "Offer Button", 
            Button Text: "Further approval needed", Phase: "Approval", Scope: "Request Task", Task Type: "Access", To State: "Requested" -&amp;gt; Save</li>
        <li>Settings -&amp;gt; State Definitions -&amp;gt; Edit "In approval" -&amp;gt; Add Action -&amp;gt; Select "FurtherApproval" -&amp;gt; Save</li>
    </ul>
');
INSERT INTO txt VALUES ('H8717', 'German',  '<H4>7) Aktivieren Planungsphase</H4>
    F&uuml;r Zugriffsauftr&auml;ge soll die Planungsphase wie vorinstalliert aktiviert werden 
    (Implementierungsauftr&auml;ge werden dann nicht mehr automatisch erzeugt, sondern m&uuml;ssen vom Planer erstellt werden):
    <ul>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Master
            <ul>
                <li>Planung: Phase ausw&auml;hlen (die &Uuml;bergangsmatrix wird eingeblendet)</li>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Planned" ausw&auml;hlen</li>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: "Planned" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "In Implementation" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Implementierung: Spezielle Stati: Niedrigster Eingangsstatus: "Planned" ausw&auml;hlen</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
        <li>Einstellungen -&amp;gt; Statusmatrix -&amp;gt; Typ ausw&auml;hlen: Zugriff
            <ul>
                <li>Planung: Phase ausw&auml;hlen (die &Uuml;bergangsmatrix wird eingeblendet)</li>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: Status hinzuf&uuml;gen -&amp;gt; "Planned" ausw&auml;hlen</li>
                <li>Implementierung: Erlaubte &Uuml;berg&auml;nge: "Planned" bearbeiten -&amp;gt; Status hinzuf&uuml;gen -&amp;gt; "In Implementation" ausw&auml;hlen -&amp;gt; Ok</li>
                <li>Implementierung: Spezielle Stati: Niedrigster Eingangsstatus: "Planned" ausw&auml;hlen</li>
            </ul>
            -&amp;gt; Statusmatrix: Speichern
        </li>
    </ul>
');
INSERT INTO txt VALUES ('H8717', 'English', '<H4>7) Activate Planning phase</H4>
    For access tasks the Planning phase will be activated as preinstalled 
    (Implementation tasks will not be created automatically but have to be defined by the planner):
    <ul>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Master
            <ul>
                <li>Planning: select phase (the transition matrix is displayed)</li>
                <li>Implementation: Allowed transitions: Add State -&amp;gt; Select "Planned"</li>
                <li>Implementation: Allowed transitions: Edit "Planned" -&amp;gt; Add State -&amp;gt; Select "In Implementation" -&amp;gt; Ok</li>
                <li>Implementation: Special States: Lowest input state: Select "Planned"</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
        <li>Settings -&amp;gt; State Matrix -&amp;gt; Select Type: Access
            <ul>
                <li>Planning: select phase (the transition matrix is displayed)</li>
                <li>Implementation: Allowed transitions: Add State -&amp;gt; Select "Planned"</li>
                <li>Implementation: Allowed transitions: Edit "Planned" -&amp;gt; Add State -&amp;gt; Select "In Implementation" -&amp;gt; Ok</li>
                <li>Implementation: Special States: Lowest input state: Select "Planned"</li>
            </ul>
            -&amp;gt; State Matrix: Save
        </li>
    </ul>
');

INSERT INTO txt VALUES ('H9001', 'German',  'Insbesondere in gr&ouml;sseren Netzwerken besteht der Bedarf, die vielf&auml;ltigen Verbindungen zwischen den Teilnehmern zu modellieren,
    um sie so einer weitergehenden Verwaltung zug&auml;nglich zu machen. Dieses Modul stellt die Hilfsmittel, bereits vorhandene <a href="/help/modelling/applications">Applikationen</a> von anderen Systemen zu importieren
    und ihre Elemente nach vorgegebenen Kriterien zu verkn&uuml;pfen. Dadurch wird ein Kommunikationsprofil erzeugt, bestehend aus einem Satz von <a href="/help/modelling/connections">Verbindungen und Schnittstellen</a>.<br>
    Zur Definition der Schnittstellen und Verbindungen wird auf der linken Seite eine Bibliothek bereitgestellt, in der zun&auml;chst die zur Applikation zugeordneten 
    (in der Regel aus Fremdsystemen <a href="/help/settings/modelling">importierten</a>) Host-Adressen (App-Server) angeboten werden. Diese k&ouml;nnen im ersten Schritt zu App-Rollen geb&uuml;ndelt werden (sh. <a href="/help/modelling/networkobjects">Netzwerkobjekte</a>).
    Die App-Rollen (und je nach <a href="/help/settings/modelling">Modellierungs-Einstellungen</a> auch die App-Server selbst) k&ouml;nnen dann als Quelle oder Ziel in die zu erstellende Verbindung &uuml;bertragen werden.
    Hinzu k&ouml;nnen noch weitere Objekte (z. B. Netzwerke) kommen, und es k&ouml;nnen (interne und externe) Schnittstellen eingebunden werden.<br>
    Desweiteren werden in der Bibliothek vordefinierte (vom Administrator eingestellte) <a href="/help/modelling/services">Dienste</a> angeboten. Diese k&ouml;nnen durch selbst definierte Dienste erg&auml;nzt, 
    als Dienstgruppen geb&uuml;ndelt und dann in den zu definierenden Verbindungen verwendet werden.
');
INSERT INTO txt VALUES ('H9001', 'English', 'Especially in greater networks there is the demand to model the connections between the participants,
    with the aim of further administration. This module provides tools to import already existing <a href="/help/modelling/applications">applications</a> from other systems
    and to connect their elements by predefined criteria. By doing this a communication profile is created, composed by a set of <a href="/help/modelling/connections">connections and interfaces</a>.<br>
    To define interfaces and connections a library is provided on the left side, where at the beginning the host addresses (App Server) associated to the application 
    (which usually are <a href="/help/settings/modelling">imported</a> from external systems) are offered. They can in a first step be bundled to App Roles (see <a href="/help/modelling/networkobjects">Network Objects</a>).
    These App Roles (and depending on the <a href="/help/settings/modelling">Modelling Settings</a> also the App Servers themselves) can be used as source or destination in the connections to be created.
    Additionally further objects (e.g. networks) and (internal or external) interfaces can be integrated.<br>
    Furthermore the library offers predefined <a href="/help/modelling/services">Services</a> (inserted by the administrator). They can be complemented by self defined services, bundled as Service Groups,
    and used in the connections.
');
INSERT INTO txt VALUES ('H9011', 'German',  'Eine Applikation ist aus Sicht des Firewall Orchestrators ein Beh&auml;lter, in dem aus zugeordneten Host-Adressen ein Kommunikationsprofil erstellt wird.
    Sie wird in der Regel extern aus den Anforderungen und Gegebenheiten der jeweiligen Unternehmung definiert und kann &uuml;ber eine Importschnittstelle in den Firewall Orchestrator importiert
    (oder auch manuell angelegt) werden.
    Das Kommunikationsprofil besteht aus einem Satz von Schnittstellen und Verbindungen welche die Kommunikation sowohl intern als auch mit anderen Applikationen definieren.<br>
    Jeder Modellierer bekommt die ihm zug&auml;nglichen Applikationen dargestellt. D.h.
    <ul>
        <li>Der Nutzer muss die Rolle "Modellierer" besitzen (Voraussetzung, dass diese Seite &uuml;berhaupt dargestellt wird).
            Die Rollen k&ouml;nnen vom Administrator in den <a href="/help/settings/roles">Rollen-Einstellungen</a> gesetzt werden.</li>
        <li>Die Applikationen wurden mit den entsprechenden Skripten importiert (<a href="/help/settings/modelling">Modelling-Einstellungen</a>)
            oder vom Administrator manuell angelegt (<a href="/help/settings/owners">Eigent&uuml;mer-Einstellungen</a>).</li>
        <li>Der Nutzer ist entweder in den <a href="/help/settings/groups">Gruppen-Einstellungen</a> der entsprechenden "ModellerGroup" der Applikation
            (wird beim Import automatisch angelegt) oder in den <a href="/help/settings/owners">Eigent&uuml;mer-Einstellungen</a> direkt zugeordnet.</li>
    </ul>
    Eine Applikation kann durch den Administrator in den <a href="/help/settings/owners">Eigent&uuml;mer-Einstellungen</a> als "Common Service zugelassen" markiert werden.
    Nur dann k&ouml;nnen auch Common Services angelegt werden.
');
INSERT INTO txt VALUES ('H9011', 'English', 'An application is - from the perspective of the Firewall Orchestrator - a container, where a communication profile is defined on basis of associated host addresses.
    Generally the application is defined externally by the requests and conditions of the enterprise and can be imported to the Firewall Orchestrator via import interface (or created manually).
    The communication profile consists of a set of interfaces and connections, which define the communication both internally and to other applications.<br>
    For each modeller his accessible applications are displayed. That means
    <ul>
        <li>The user has to have the role "modeller" (precondition that this page is displayed at all).
            Roles are set by the administrator in <a href="/help/settings/roles">Role Settings</a>.</li>
        <li>Applications have been imported by respective scripts (<a href="/help/settings/modelling">Modelling Settings</a>)
            or manually created by the administrator (<a href="/help/settings/owners">Owner Settings</a>).</li>
        <li>The user is assigned to the application via the appropriate "ModellerGroup" (automatically created by the import)
            in the <a href="/help/settings/groups">Group Settings</a> or directly in the <a href="/help/settings/owners">Owner Settings</a>.</li>
    </ul>
    An application can be marked by the administrator as "Common Service Possible" in the <a href="/help/settings/owners">Owner Settings</a>.
    Only in this case Common Services can be created in this application.
');
INSERT INTO txt VALUES ('H9021', 'German',  'Verbindungen sind die Hauptbestandteile des Kommunikationsprofils. Es wird zwischen verschiedenen Arten von Verbindungen unterschieden:');
INSERT INTO txt VALUES ('H9021', 'English', 'Connections are the main components of the communication profile. There are different types of connections:');
INSERT INTO txt VALUES ('H9022', 'German',  'Schnittstellen: Sie dienen in erster Linie der Modellierung von (aus Sicht der Applikation) externen Verbindungen oder der B&uuml;ndelung interner Objekte.
    Es m&uuml;ssen in der Applikation neben dem Dienst entweder Quelle oder Ziel definiert werden. Die Schnittstellen werden in den anderen Applikationen
    zur Auswahl angeboten und k&ouml;nnen dort in der Definition von eigenen Verbindungen verwendet werden.
');
INSERT INTO txt VALUES ('H9022', 'English', 'Interfaces: They serve primarily the modelling of (relative to the application) external connections or the bundling of internal objects.
    Besides the service either source or destination have to be defined in the application. The interfaces are offered to other applications to use
    them in the definition of own connections.
');
INSERT INTO txt VALUES ('H9023', 'German',  'Standard-Verbindungen: Zentrale Objekte zur Modellierung der Kommunikationsverbindungen. Dabei m&uuml;ssen Quelle, Dienst und Ziel aus den in der Bibliothek 
    angebotenen Ntzwerkobjekten bzw. Services gew&auml;hlt werden. Es k&ouml;nnen auch eigene oder externe Schnittstellen eingebunden werden. Dann m&uuml;ssen nur noch die "offenen Enden"
    (je nach Schnittstelle Quelle oder Ziel) aus der Bibliothek hinzugef&uuml;gt werden.
');
INSERT INTO txt VALUES ('H9023', 'English', 'Regular Connections: Essential objects for modelling the communication. Source, Service and Destination have to be selected from the network resp. service objects 
    offered in the library. Additionally own or external interfaces can be integrated. In this case only the "open ends" (source or destination, depending on the inetrface type)
    have to be added from the library.
');
INSERT INTO txt VALUES ('H9024', 'German',  'Common Services: K&ouml;nnen nur definiert werden, wenn die Applikation durch den Administrator in den <a href="/help/settings/owners">Eigent&uuml;mer-Einstellungen</a> 
    daf&uuml;r freigegeben wurde. Sie sind formal wie Standard-Verbindungen aufgebaut. 
');
INSERT INTO txt VALUES ('H9024', 'English', 'Common Services: Can only be defined, if the application is marked as permitted in the <a href="/help/settings/owners">Owner Settings</a> by the administrator.
    Formally they are structured as regular connections.
');
INSERT INTO txt VALUES ('H9031', 'German',  'Netzwerkobjekte werden zur Definition von Quelle und Ziel der Verbindungen ben&ouml;tigt. Es wird zwischen verschiedenen Arten von Netzwerkobjekten unterschieden:');
INSERT INTO txt VALUES ('H9031', 'English', 'Network objects are used to define source and destination of the connections. There are different types of network objects:');
INSERT INTO txt VALUES ('H9032', 'German',  'App-Server: Die elementaren Bausteine (Host-Adressen), die der Applikation zugeordnet sind. Sie werden in der Regel mit den Applikationen importiert
    (<a href="/help/settings/modelling">Import-Einstellungen</a>, <a href="/help/API/appdataimport">Import-Schnittstelle</a>), k&ouml;nnen aber auch manuell vom Administrator angelegt werden.
    Je nach Einstellung (abh&auml;ngig von den jeweiligen Vorgaben des Unternehmens) k&ouml;nnen die App-Server direkt in die Verbindungen &uuml;bernommen werden oder m&uuml;ssen zuerst in App-Rollen geb&uuml;ndelt werden.
');
INSERT INTO txt VALUES ('H9032', 'English', 'App Server: Elementary components (host addresses) associated to the application. Usually they are imported with the applications
    (<a href="/help/settings/modelling">Import Settings</a>, <a href="/help/API/appdataimport">Import Interface</a>), but can also be created manually by the administrator.
    Depending on the settings (according to company requirements) App Servers can be used directly in the connections or have to be bundled in App Roles first.
');
INSERT INTO txt VALUES ('H9033', 'German',  'App-Rollen: Dienen der B&uuml;ndelung von App-Servern. Falls in den <a href="/help/settings/modelling">Modellierungs-Einstellungen</a> so vorgesehen,
    m&uuml;ssen sie einer Netzwerkarea zugeh&ouml;ren. Beim Erstellen der App-Rolle muss dann zun&auml;chst eine Area ausgew&auml;hlt werden, nur von dieser werden dann die App-Server in der Bibliothek angeboten.
    Die Namen der App-Rollen m&uuml;ssen dann einer ebenfalls in den Einstellungen vorgegebenen Namenskonvention folgen.
');
INSERT INTO txt VALUES ('H9033', 'English', 'App Roles: Used for bundling of App Servers. If required in the <a href="/help/settings/modelling">Modelling Settings</a>,
    they have to belong to a network area. When creating an App Role, first a network area has to be selected, only App Servers belonging to this are are displayed then in the library.
    Names of the App Roles have to comply to a naming convention, defined in the Modelling Settings.
');
INSERT INTO txt VALUES ('H9034', 'German',  'Netzwerkareas: Werden &uuml;ber die Subnetzdaten-Importschnittstelle (<a href="/help/settings/modelling">Import-Einstellungen</a>, <a href="/help/API/subnetdataimport">Import-Schnittstelle</a>) importiert.
    Sie k&ouml;nnen aus der Bibliothek heraus gesucht, selektiert und anschliessend in Quelle oder Ziel der Verbindungen &uuml;bernommen werden.
');
INSERT INTO txt VALUES ('H9034', 'English', 'Network Areas: Are imported via the Subnet Data Import Interface (<a href="/help/settings/modelling">Import Settings</a>, <a href="/help/API/subnetdataimport">Import Interface</a>).
    They can be searched an selected from the library and then used for source and destination of the connections.
');
INSERT INTO txt VALUES ('H9041', 'German',  'Es wird zwischen einfachen Diensten und Dienstgruppen unterschieden. In den <a href="/help/settings/modelling">Modellierungs-Einstellungen</a>
    kann festgelegt werden, dass nur Dienstgruppen in der Definition von Verbindungen genutzt werden k&ouml;nnen, ansonsten sind auch einfache Dienste zugelassen.
');
INSERT INTO txt VALUES ('H9041', 'English', 'There is a differentiation between simple services and Service Groups. It can be defined in the <a href="/help/settings/modelling">Modelling Settings</a>,
    that only Service Groups are allowed to be used in the definition of connections, else also simple services are permitted.
');
INSERT INTO txt VALUES ('H9042', 'German',  'Dienste: Einfache Dienste werden durch Port (einfach oder Intervall) und Protokoll definiert und k&ouml;nnen einen Namen zugewiesen bekommen.
    Die Definition kann durch den Modellierer selbst vorgenommen werden, es k&ouml;nnen aber auch - falls vorhanden - vom Administrator vordefinierte Dienste verwendet werden.
');
INSERT INTO txt VALUES ('H9042', 'English', 'Services: Simple services are defined by port (single or range) and protocol and can have a name assigned.
    Definition can be done by the modeller, but also - if available - predefined services by the administrator can be used.
');
INSERT INTO txt VALUES ('H9043', 'German',  'Dienstgruppen: In Dienstgruppen k&ouml;nnen die einfachen Dienste zusammengefasst werden. Hier muss ein Name vergeben werden, es k&ouml;nnen auch Kommentare hinzugef&uuml;gt werden.
    Auch hier kann die Definition durch den Modellierer selbst erfolgen oder auch vom Administrator vordefinierte Dienstgruppen verwendet werden.
');
INSERT INTO txt VALUES ('H9043', 'English', 'Service Groups: Simple services can be bundled in Service Groups. A name has to be given to them, comments can be added.
    Again definition can be done by the modeller, but also Service Groups predefined by the administrator can be used.
');
