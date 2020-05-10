
# Designziele

- Einfache & angenehme Entwicklung im (erweiterbaren) Team
- Open Source
- Clean Code (Doku, Tests, coding best practices ...)
- Klare Modularisierung (mittels API), keine Logik im Client
- Sicherer Code, mandantenfähige Nutzung, RBAC
- Moderne & "zeitlose" GUI
- Wartbarkeit (einfaches Einspielen, Bereitstellen von Updates, updater?)

# Design-, Methoden- und Toolentscheidungen

- Sprache:
  - Entwicklersprache Deutsch/Englisch
  - GUI Sprache parallel Englisch & Deutsch
- GitHub.com
- Rapid Prototyping (Trial & Error, MuP)
- Tools
  - PostgreSQL
  - API GraphQL statt REST API
  - Verwendung von hasura
  - Client: Apollo (https://www.apollographql.com/)
  
# Funktionale Anforderungen (High-Level)

- Kostengünstige Alternative zu den Kernfunktionalitäten der Konkurrenz (Tufin, Algosec, Skybox)
- Abdecken der regulatorischen Richtlinien (Dokumentation Config-Änderungen, Rezertifizierung Config)
- "CMDB Netzwerk"
- kritische Funktionen nicht im Kernprodukt (Config-Änderungen)
- Bereitstellung offener Schnittstellen zur Automatisierung

# Architektur-Dokumentation: https://demo.itsecorg.de
