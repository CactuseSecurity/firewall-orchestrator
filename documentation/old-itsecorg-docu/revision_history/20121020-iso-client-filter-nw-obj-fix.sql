/*


zusätzlich: Source or Destination Filter einbauen!
zusätzlich: show_any_rules auch ohne ip filter deaktivierbar?

wenn Mandantenfilterung aktiv:
für alle NW-Objekte
   prüfe für alle angezeigten Regeln (nach Auflösung aller Gruppen), ob 
     a) dieses Objekt Zugriff auf das Mandantennetzwerk hat (i.e. auf der anderen Seite er Regel steht):
        Bilde Vereinigungsmenge aller Objekte auf der anderen Seite derjenigen Regeln, in denen dieses Objekt vorkommt (auch als Teil einer Gruppe) 
        oder
     b) selbst Teil des Mandantennetzwerks ist --> trivial, sollte eigentlich bereits kodiert sein
     Prüfe ob Vereinigungsmenge von a) und b) mit Mandantennetzwerk überlappt, falls nein --> filtere Objekt aus

  gibt es bereits im rulefiltering einen korrekt arbeitenden Algorithmus? Dann hier eventuell die Liste aller relevanten nw-objekte speichern? 

  debug logging fuer 
  class NetworkObjectList::NetworkObjectList funktioniert nur bei sehr kurzen Regelwerken?!
 	es funktioniert alles bis auf das syslog-Debugging - diese Zeile kommt nicht mehr:
      SELECT dev_name,mgm_name FROM device LEFT JOIN management USING (mgm_id) WHERE dev_id=13
	in dieser Zeile verschwindet das debugging:
	display_rule_config.php::displayRules::
	$filtered_rules = $this->ruleList->getRules($filter->getReportTime(), $filter->getReportId()); 

	Problem mit Beispiel:
	
	Mandantenfilter = 1.0.0.0/8
	
	Gruppe1 = (2.1.1.1, 1.1.1.1)
	Regel 1 = Gruppe1 --> 3.3.3.3
	Regel 2 = Gruppe1 --> 1.2.3.4
	
	Inhalt von Gruppe1? In Regel 1 ohne 2.1.1.1, in Regel 2 mit 2.1.1.1

*/