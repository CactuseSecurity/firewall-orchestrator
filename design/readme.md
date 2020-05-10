
# Designziele

- Einfache Entwicklung im (erweiterbaren) Team
- Open Source
- Clean Code (Doku, Tests, coding best practices ...)
- Klare Modularisierung (mittels API), keine Logik im Client
- Sicherer Code, mandantenfähige Nutzung, RBAC
- Moderne & "zeitlose" GUI
- Wartbarkeit (einfaches Einspielen, Bereitstellen von Updates, updater?)

# Design-, Methoden- und Toolentscheidungen

- GitHub.com
- Rapid Prototyping (Trial & Error, MuP)
- PostgreSQL
- GraphQL statt REST API
- Verwendung von hasura

# Funktionale Anforderungen (High-Level)

- Kostengünstige Alternative zu den Kernfunktionalitäten der Konkurrenz (Tufin, Algosec, Skybox)
- Abdecken der regulatorischen Richtlinien (Dokumentation, Rezertifizierung)
- "CMDB Netzwerk"
- kritische Funktionen nicht im Kernprodukt (Config-Änderungen)
- Bereitstellung offener Schnittstellen zur Automatisierung
