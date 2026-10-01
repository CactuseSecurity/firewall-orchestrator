# Entwicklungsanforderung: Platzierung neuer Firewall-Regeln im Regelwerk

> **Dokumentzweck:** Anforderung an die Produktentwicklung. Dieses Dokument beschreibt eine noch nicht implementierte Funktion und dient als Grundlage für deren Umsetzung.
> Zielgruppe: Produktentwicklung sowie Administratoren des Zielsystems.

## 1. Zweck der Funktion

Nach Genehmigung eines Firewall-Antrags muss die neue Regel im Regelwerk der jeweiligen Firewall an einer definierten Stelle eingefügt werden. Die Position entscheidet über Wirksamkeit (Auswertungsreihenfolge, Inline-Layer) und über die Pflegbarkeit des Regelwerks. Die geforderte Funktion ermittelt je Firewall-Typ deterministisch die Zielposition und führt die Platzierung über die jeweilige Hersteller-API aus.

Ergänzt die Pfadanalyse: Diese ermittelt, **auf welchen** Firewalls Regeln entstehen; die Platzierung ermittelt, **wo** in deren Regelwerk.

Das Dokument umfasst drei Themenblöcke:

1. **Technische Positionierung** der FW-Regeln im Regelwerk (Kapitel 4 bis 10)
2. **Anzeige in der Implementation Phase** — Darstellung der neu zu positionierenden FW-Regel im aktuellen Regelwerk (Kapitel 11)
3. **Übergeordnete Entscheidungen zum Schreiben** — nur angerissen, gesonderte Ausarbeitung (Kapitel 12)

## 2. Prämissen

1. Es werden ausschließlich **neue Regeln** geschrieben. Bestehende Regeln werden nicht angepasst. Ein späteres Ändern bestehender Regeln ist denkbar und ggf. konfigurierbar, aber **nicht Teil dieses Scopes**.
2. Ein **Default-Verhalten** (Platzierung, wenn für einen Firewall-Typ nichts konfiguriert ist) wird nur als Idee beschrieben und ist für den aktuellen Umfang **nicht relevant**.
3. **Sections werden nicht durch diese Funktion angelegt.** Das Anlegen erfolgt im ersten Schritt über einen externen Prozess.
4. **Zonen werden nicht durch diese Funktion ermittelt.** Die Ermittlung erfolgt über eine separate Funktion, die sicherstellt, dass jede Regel in Quelle und Ziel **genau eine** Security-Zone besitzt.
5. Im Umfeld existieren zusätzlich **Azure Firewalls**. Diese werden derzeit **außerhalb von FWO** verwaltet und sind **nicht Teil dieses Scopes**. Eine Einbindung ist zukünftig möglich; das Positionierungsverfahren für Azure ist dann zu ergänzen (siehe Abschnitt 5).

## 3. Anforderungen

1. Je **Firewall-Typ** (CheckPoint, Fortinet, …) muss die Positionierung im Regelwerk **unterschiedlich konfigurierbar** sein.
2. Ist für einen Firewall-Typ nichts definiert, kann ein Default-Verhalten greifen.
3. Die Positionierung besteht je Firewall-Typ aus **bis zu zwei Teilen**:

| Teil | Bezeichnung | Charakter | Beispiel |
|---|---|---|---|
| 1 | **Funktionaler Teil** | Pflicht — ohne korrekte Positionierung greift die Regel technisch nicht | CheckPoint Inline-Layer: falsch positionierte Regel wird nie ausgewertet |
| 2 | **Kunden-Positionierung** | Organisatorisch — Struktur und Pflegbarkeit des Regelwerks | Zusammenführung nach Security-Zonen |

4. Die Umsetzung muss um weitere Firewall-Typen erweiterbar sein, ohne den Kern des Algorithmus zu ändern.

## 4. Zwei-Schichten-Modell

Der funktionale Teil bestimmt den **zulässigen Bereich** im Regelwerk (z. B. Layer oder Inline-Layer). Die Kunden-Positionierung bestimmt die **exakte Stelle innerhalb** dieses Bereichs.

**Der funktionale Teil ist nicht bei allen Firewalls vorhanden.** Er wird nur berücksichtigt, wenn er für den jeweiligen Firewall-Typ tatsächlich erforderlich ist. Ist er nicht erforderlich, erfolgt eine einfache klassische Bestimmung der Policy, und es greift ausschließlich die Kunden-Positionierung.

Verbindliche Rangfolge, sofern ein funktionaler Teil vorhanden ist:

1. Der funktionale Teil hat **immer Vorrang**.
2. Ergibt die Kunden-Positionierung eine Stelle **außerhalb** des funktional zulässigen Bereichs, wird die Platzierung **abgebrochen** und der Antrag zur manuellen Bearbeitung markiert. Es wird **nicht** stillschweigend auf den funktionalen Bereich zurückgefallen — eine Regel an unerwarteter Stelle ist schwerer zu finden als ein sauber gemeldeter Fehler.

## 5. Positionierungsverfahren je Firewall-Typ

Die Hersteller unterscheiden sich nicht nur in der API, sondern im **Grundkonzept der Gruppierung**. Die Verzweigung erfolgt deshalb früh im Algorithmus (Abschnitt 7, Schritt 3) über ein je Firewall-Typ konfiguriertes **Positionierungsverfahren**:

| Verfahren | Firewall-Typ | Gruppierungsobjekt | Ordnung innerhalb der Gruppe | Platzierung |
|---|---|---|---|---|
| `SECTION_NATIVE` | CheckPoint | Section als eigenständige Entität im Layer | vorhanden, relevant | Regel ans **Ende** der Section |
| `SECTION_LABEL` | Fortinet | Label als Eigenschaft der FW-Regel (`global-label`) | **keine eigene Ordnung** — die Reihenfolge ergibt sich allein aus der Regelnummer | FW-Regel **unter** die letzte FW-Regel des **ersten Blocks** der Section, Label setzen |

Bei `SECTION_LABEL` existiert die Section nicht als eigenes Objekt: Sie ist nur ein Label an der FW-Regel, und die Ordnung des Regelwerks ergibt sich allein aus der Regelnummer. **Eine Section kann dadurch in n Blöcke aufgeteilt sein**, wenn zwischen gleich gelabelten FW-Regeln andersartig gelabelte FW-Regeln stehen.

Ist die Section in mehrere Blöcke aufgeteilt, wird die neue FW-Regel im **ersten Block** platziert. Begründung: Eine Platzierung im letzten Block würde die FW-Regel weit unten im Regelwerk einordnen, wo die Wahrscheinlichkeit einer Verschattung durch vorgelagerte FW-Regeln höher ist.

Neue Firewall-Typen werden über ein zusätzliches Verfahren angebunden; die Schritte 1, 2 und 5 des Algorithmus bleiben unverändert. Für eine spätere Einbindung von **Azure Firewalls** ist ein eigenes Verfahren zu ergänzen, da Azure kein Section-Konzept besitzt und die Gruppierung über Rule Collections mit Prioritäten erfolgt.

## 6. Namenskonvention und Zonen

### 6.1 Security-Zonen

Quell- und Ziel-Zone werden von einer separaten Funktion geliefert. Für diesen Algorithmus gilt als Zusicherung: Jede Regel besitzt in Quelle und Ziel **genau eine** Zone. Ein Zonenpaar ergibt damit **genau eine** Section.

Quell- und Ziel-Zone können identisch sein; dieser Fall ist zulässig und wird wie jeder andere behandelt.

### 6.2 Namenskonvention für Sections (CheckPoint, Fortinet)

Sections sind nach Ziel- und Quell-Security-Zone benannt. Aufbau des Strings:

```
"[DEST-ZONE] < [SOURCE-ZONE]"
```

Beispiel eines Section-Namens: `"[ZONE-A] < [ZONE-B]"`

| Regel | Festlegung |
|---|---|
| Eckige Klammern | **Bestandteil des Namens**, umschließen jeden Zonennamen |
| Trennzeichen | `<`, mit **genau einem Leerzeichen** davor und dahinter |
| Reihenfolge | Ziel-Zone zuerst, danach Quell-Zone |
| Schreibweise | **Großschreibung** |
| Führende/nachfolgende Leerzeichen | Beim Vergleich **ignorieren** (Trim) |

Der Name ist der **Matching-Schlüssel** für die Zuordnung. Er wird zentral aus einer einzigen Funktion erzeugt und nie an mehreren Stellen zusammengesetzt. Vor dem Vergleich werden beide Seiten getrimmt und in Großschreibung normalisiert.

## 7. Algorithmus

Eingabe: genehmigte Regel + Zielfirewall (aus Pfadanalyse) + Firewall-Typ + Zonenpaar.

### Schritt 1 — Positionierungs-Konfiguration laden
Konfiguration für den Firewall-Typ ermitteln; daraus ergeben sich der funktionale Teil und das **Positionierungsverfahren** (Abschnitt 5). Keine Konfiguration vorhanden → Default-Verhalten (Abschnitt 9) bzw. Abbruch mit Meldung. Verfahren unbekannt → **Abbruch**.

### Schritt 2 — Funktionalen Zielbereich bestimmen
Layer / Inline-Layer / Policy-Package festlegen. Nicht auflösbar → **Abbruch**, keine Platzierung.

### Schritt 3 — Verzweigung nach Positionierungsverfahren
Ab hier läuft die Verarbeitung getrennt. Alle Zweige liefern dasselbe Ergebnis: eine platzierte Regel oder einen definierten Abbruch.

#### Zweig A — `SECTION_NATIVE` (CheckPoint)
1. Section-Namen aus dem Zonenpaar bilden (Abschnitt 6.2).
2. Sections des Zielbereichs lesen, normalisiert matchen.
3. Nicht gefunden → **Behandlung fehlender Section** (Schritt 4).
4. FW-Regel über den API-Call mit `position.bottom` und dem Section-Namen anlegen.

#### Zweig B — `SECTION_LABEL` (Fortinet)
1. Section-Namen aus dem Zonenpaar bilden (Abschnitt 6.2).
2. Alle FW-Regeln des Zielbereichs mit passendem `global-label` ermitteln, normalisiert matchen.
3. Keine FW-Regel gefunden → **Behandlung fehlender Section** (Schritt 4).
4. In der Policy **letzte** FW-Regel der Section bestimmen.
5. Neue FW-Regel mit korrektem `global-label` anlegen.
6. Neue FW-Regel per Move-Operation **hinter** die ermittelte letzte FW-Regel verschieben.

Ist die Section in mehrere Blöcke aufgeteilt (Abschnitt 5), bezieht sich Punkt 4 auf die letzte FW-Regel des **ersten Blocks**, also auf die letzte FW-Regel der ersten zusammenhängenden Folge von FW-Regeln mit diesem Label.

### Schritt 4 — Behandlung fehlender Sections
Existiert die Section nicht, wird sie **nicht angelegt**. Die Platzierung wird **abgebrochen** und der Antrag mit dem fehlenden Namen zur manuellen Bearbeitung markiert. Das Anlegen erfolgt über den externen Prozess.

Dieses Verhalten kann in einem **zweiten Schritt** angepasst werden, sodass fehlende Sections durch die Funktion selbst behandelt werden. Der dafür nötige Aufwand unterscheidet sich je Positionierungsverfahren erheblich:

| Verfahren | Bewertung |
|---|---|
| `SECTION_NATIVE` (CheckPoint) | Die Section ist ein **eigenständiges Objekt** und muss angelegt werden. Erforderlich sind die Berechtigung zum Schreiben von Sections sowie eine Festlegung der Einfügeposition der Section im Layer. Höherer Aufwand |
| `SECTION_LABEL` (Fortinet) | Die Section ist **nur eine Eigenschaft der FW-Regel**. Es ist kein Objekt anzulegen — die Section entsteht implizit dadurch, dass die neue FW-Regel als erste das Label trägt. Zu bestimmen ist ausschließlich die Zielposition der FW-Regel. **Geringer Aufwand** |

#### Behandlung fehlender Sections bei `SECTION_LABEL` (Fortinet)

Da die Section kein eigenes Objekt ist, lässt sich eine FW-Regel hier vergleichsweise einfach an der richtigen Stelle einfügen. Die Zielposition wird über die Sortierregel der Sections (siehe unten) bestimmt:

1. Alle im Zielbereich vorhandenen Section-Labels ermitteln.
2. Diese Labels zusammen mit dem neuen Section-Namen nach der Sortierregel sortieren.
3. **Vorgänger-Section** bestimmen: die in dieser Sortierung unmittelbar vor der neuen Section stehende, im Regelwerk tatsächlich vorhandene Section.
4. Neue FW-Regel mit korrektem `global-label` anlegen und per Move-Operation **hinter** die letzte FW-Regel des ersten Blocks der Vorgänger-Section verschieben.
5. Existiert keine Vorgänger-Section — die neue Section wäre die erste —, wird stattdessen die **Nachfolger-Section** bestimmt und die neue FW-Regel **vor** deren erste FW-Regel positioniert.
6. Ist im Zielbereich keine Section vorhanden, greift das Default-Verhalten (Abschnitt 9).

Der funktionale Teil (Abschnitt 4) und eine vorhandene Cleanup-Regel begrenzen die Zielposition auch in diesem Fall.

#### Sortierregel der Sections (zur allgemeinen Bewertung)

Die nachfolgende Regel beschreibt die vorgegebene Reihenfolge der Sections im Regelwerk. Sie dient der **allgemeinen Bewertung** — etwa der Prüfung, ob ein Regelwerk der Konvention entspricht, oder als Grundlage für ein späteres automatisches Anlegen von Sections. **Die in diesem Dokument beschriebene Platzierung neuer FW-Regeln bleibt davon unberührt.**

Die Sections folgen einer vorgegebenen Reihenfolge und werden zweistufig sortiert:

1. **Erstes Kriterium:** Ziel-Zone
2. **Zweites Kriterium:** Quell-Zone

Beispiel für die resultierende Reihenfolge:

```
[A] < [Z]
[A] < [Y]
[A] < [X]
[B] < [Z]
[B] < [Y]
...
```

Die Reihenfolge der Zonen selbst ergibt sich aus der **Reihenfolge der Zonen in der Security-Matrix**.

**Achtung:** Die Zonen sind in der Security-Matrix **logisch sortiert**, nicht alphabetisch. Die Sortierung darf deshalb nicht über einen Stringvergleich der Zonennamen erfolgen, sondern ausschließlich über den **Index der Zone in der Security-Matrix**. Ein alphabetischer Vergleich liefert eine andere und damit falsche Reihenfolge.

### Schritt 5 — Verifikation
Nach der Platzierung das Ergebnis zurücklesen und gegen das Ziel prüfen. Der Prüfumfang ist verfahrensabhängig:

| Verfahren | Zu prüfen |
|---|---|
| `SECTION_NATIVE` | Zugehörigkeit zur Section und Position innerhalb der Section |
| `SECTION_LABEL` | `global-label` **und** Position relativ zur zuvor ermittelten letzten FW-Regel |

Abweichung → Antrag als fehlerhaft markieren, nicht automatisch nachkorrigieren.

## 8. Herstellerspezifische Umsetzung

### 8.1 CheckPoint (`SECTION_NATIVE`)

Sections sind **eigenständige Entitäten** im Layer. Es existiert ein dedizierter API-Call, um eine Regel relativ zu einer Section zu platzieren:

```
add access-rule layer "xyz" position.bottom "[DEST-ZONE] < [SOURCE-ZONE]" ...
```

Als relative Positionsangaben stehen `top`, `above`, `below` und `bottom` zur Verfügung; als Bezugsobjekt kann der Name einer Section oder einer Regel angegeben werden. Eine Positionierung an einer **Nummer innerhalb** einer Section wird nicht unterstützt — für „ans Ende der Section" ist `position.bottom` die passende Angabe.

Der Layer-Name ist üblicherweise dem Policy-Package vorangestellt (z. B. `"<Package> Network"`). Bei mehreren Packages muss der Layer eindeutig adressiert werden, sonst landen Regeln im falschen Package.

### 8.2 Fortinet (`SECTION_LABEL`)

Die Section ist **keine eigenständige Entität**, sondern eine **Eigenschaft der jeweiligen Firewall-Regel**. Konsequenz: Die **letzte Regel der Section** muss ermittelt und die neue Regel **darunter** positioniert werden.

Das Label wird über das Policy-Feld `global-label` geführt (Sequence Grouping). Die Gruppierung arbeitet strikt **top-to-bottom**: FW-Regeln **ohne** eigenes Label, die nach einer gelabelten FW-Regel stehen, werden der **vorangehenden** Gruppe zugerechnet. Die neue FW-Regel muss deshalb **beides** erhalten: korrektes `global-label` **und** korrekte Position. Nur eines von beiden führt zu einer inkonsistenten Ansicht.

Neue FW-Regeln werden bei der Erstellung ans Ende angehängt; das Verschieben erfolgt als **eigener Schritt** (`action=move` mit `after`/`before` auf die Policy-ID). Die Operation ist damit **nicht atomar** — schlägt der Move fehl, liegt eine korrekte, aber falsch positionierte Regel vor. Dieser Zustand muss über die Verifikation (Abschnitt 7, Schritt 5) erkannt und explizit gemeldet werden.

Da die Section kein eigenes Objekt ist, kann sie über das Regelwerk verteilt in mehreren Blöcken auftreten. Die Ermittlung der Zielposition muss deshalb den **ersten** zusammenhängenden Block mit dem passenden Label bestimmen, nicht nur nach dem letzten Vorkommen des Labels suchen.

`global-label` erscheint nicht in der Ausgabe von `show` bzw. `show full`, wohl aber in der über die GUI heruntergeladenen Backup-Konfiguration. Für Import und Verifikation ist deshalb die API-Abfrage des FW-Regel-Objekts zu verwenden, kein CLI-`show`-Parsing.

### 8.3 Azure Firewalls (nicht im Scope)

Im Umfeld existieren zusätzlich **Azure Firewalls**. Sie werden derzeit **außerhalb von FWO** verwaltet; Regelanträge für diese Firewalls laufen über einen eigenen Prozess. Diese Funktion liest und beschreibt keine Azure-Regelwerke.

Eine **zukünftige Einbindung ist vorgesehen und im Aufbau des Algorithmus berücksichtigt**: Sie erfordert lediglich ein zusätzliches Positionierungsverfahren (Abschnitt 5); die Schritte 1, 2 und 5 bleiben unverändert. Da Azure kein Section-Konzept kennt, sondern nach Rule Collections mit Prioritäten gruppiert, sind dafür eine eigene Namenskonvention sowie die Zuordnung Zonenpaar → Rule Collection festzulegen.

## 9. Default-Verhalten (nur als Idee, laut Prämisse nicht im Scope)

Ist für einen Firewall-Typ keine Positionierung konfiguriert, wäre denkbar: Platzierung am Ende des funktional zulässigen Bereichs, jedoch **oberhalb** einer als Cleanup/Deny-Any erkannten Abschlussregel, mit Protokolleintrag „Default-Positionierung angewendet". Bewusst konservativ, weil eine Regel unterhalb der Cleanup-Rule wirkungslos ist.

## 10. Beispiele

### Fall 1 — CheckPoint, Section vorhanden
Flow von `ZONE-B` nach `ZONE-A`, Layer `Standard Network`.
- Section-Name: `"[ZONE-A] < [ZONE-B]"` → im Layer vorhanden
- Platzierung: `add access-rule layer "Standard Network" position.bottom "[ZONE-A] < [ZONE-B]" ...`
- Section-Zugehörigkeit und Position zurücklesen

### Fall 2 — Section fehlt
- Kein Anlegen durch diese Funktion
- Abbruch; Antrag wird mit dem fehlenden Namen zur manuellen Bearbeitung markiert

### Fall 3 — Fortinet, Section vorhanden
- Alle FW-Regeln mit `global-label = "[ZONE-A] < [ZONE-B]"` ermitteln, den ersten zusammenhängenden Block bestimmen und dessen letzte FW-Regel ermitteln
- Neue FW-Regel anlegen mit `global-label = "[ZONE-A] < [ZONE-B]"`
- Move hinter die ermittelte letzte FW-Regel
- Label und Position zurücklesen

### Fall 4 — Funktionaler Konflikt
Die ermittelte Section liegt außerhalb des funktional erforderlichen Bereichs (z. B. CheckPoint Inline-Layer).
- Keine Platzierung, Antrag wird als „manuelle Bearbeitung erforderlich" markiert (Abschnitt 4)

### Fall 5 — Intra-Zone-Flow
Quell- und Ziel-Zone identisch.
- Section-Name: `"[ZONE-A] < [ZONE-A]"`
- Behandlung identisch zu Fall 1 bzw. Fall 3

## 11. Anzeige in der Implementation Phase

Neben der technischen Positionierung ist die **Darstellung der neu zu positionierenden FW-Regel im aktuellen Regelwerk** Bestandteil dieser Anforderung. Ziel ist, dass der Bearbeiter die geplante Platzierung im Kontext des bestehenden Regelwerks bewerten kann, bevor geschrieben wird.

### 11.1 Verortung der neuen FW-Regel in der Infrastruktur

Die neue FW-Regel wird im Verhältnis zur Infrastruktur **analog einer Ordnerstruktur** angezeigt:

```
Management
  Policy (Firewall)
    [Optional] Layer
      Section
        [Optional] Nachbar-FW-Regel oberhalb der neuen FW-Regel
                   - nur wenn in der Section bereits eine FW-Regel vorhanden ist
                   - UID der Nachbar-FW-Regel muss angezeigt werden
          Neue FW-Regel
```

Erläuterung der optionalen Ebenen:

- **Layer** entfällt bei Firewall-Typen ohne Layer-Konzept.
- **Nachbar-FW-Regel** ist die FW-Regel, hinter der die neue FW-Regel platziert wird. Ist die Section leer, entfällt diese Ebene und die neue FW-Regel wird direkt unterhalb der Section angezeigt.

### 11.2 Anzeige der neuen und der benachbarten FW-Regel

Die neue FW-Regel und die Nachbar-FW-Regel werden gemeinsam in einer **menschenlesbaren, normalisierten Tabelle** dargestellt. Die Normalisierung erlaubt den direkten Vergleich unabhängig vom Firewall-Typ.

| Spalte | Inhalt / Hinweis |
|---|---|
| Status | Enabled |
| UID | bei der neuen FW-Regel Label `NEU` |
| No | relative Positionsnummer |
| Name | |
| Quelle | |
| Ziel | |
| Service | |
| Action | |
| Time | |
| Track | |
| Install On | |
| Comment | |
| From | Interface/Zone |
| To | Interface/Zone |
| Security Profile | |
| Type | derzeit nicht benötigt; zukünftig z. B. IPS bei CheckPoint |
| AdoIT | |
| Change ID | |
| Datum Regelprüfung | aktuelles Datum |

❌ **Zu entscheiden:** Wird die normalisierte Tabelle immer vollständig angezeigt, oder werden je Firewall-Typ nur die dort relevanten Spalten eingeblendet?

### 11.3 Anzeige neuer Adressobjekte

Anzuzeigen ist, **wo die Objekte angelegt werden** (Manager). Die Darstellung erfolgt als normalisierte Tabelle:

| Spalte |
|---|
| Name |
| Type |
| IPv4 |
| Mask |
| Comment |
| Interface/Zone |

### 11.4 Anzeige neuer Serviceobjekte

Darstellung als normalisierte Tabelle:

| Spalte |
|---|
| Name |
| Protocol |
| Port first |
| Port last |
| Comment |

## 12. Übergeordnete Entscheidungen zum Schreiben

> Dieses Kapitel reißt die Entscheidungen nur an. Die Ausarbeitung erfolgt **gesondert** und ist nicht Bestandteil dieser Anforderung.

### 12.1 Allgemeine Optionen

- **Automatisiertes Schreiben ist je Management bzw. je Firewall konfigurierbar.** Ist für eine Firewall nichts konfiguriert, wird per Default **nur ein Vorschlag** erzeugt und nicht geschrieben.
- Folgende Parameter werden **je Firewall-Typ und dort je Firewall** konfiguriert:
  - Log
  - Security Profiles
  - Type (z. B. Fortinet `Standard`)
  - From
  - To

### 12.2 CheckPoint

- FW-Regeln
  - `Log (Track)` = Log + Accounting, Wert über die jeweilige FW-Konfiguration

### 12.3 Fortinet

- Adressobjekte
  - Schreiben in die **Global Database**
  - **Achtung:** Bei Verwendung der Global Database muss der API-Call *Assignment* (Assign ALL Objects) auf **alle ADOMs** erfolgen.
  - `Interface/Zone` = Any
- Serviceobjekte
  - Schreiben in die **Global Database**
  - **Achtung:** Bei Verwendung der Global Database muss der API-Call *Assignment* (Assign ALL Objects) auf **alle ADOMs** erfolgen.
- FW-Regeln
  - `From` = Any, Wert über die jeweilige FW-Konfiguration
  - `To` = Any, Wert über die jeweilige FW-Konfiguration
  - `Log` = Log All Sessions, Wert über die jeweilige FW-Konfiguration
  - `Type` = Standard, Wert über die jeweilige FW-Konfiguration
  - `Security Profiles` = Wert über die jeweilige FW-Konfiguration

## 13. Fehlerbehebung

| Symptom | Wahrscheinliche Ursache | Lösung |
|---|---|---|
| Abbruch „Section nicht gefunden" | Section noch nicht durch den externen Prozess angelegt | Über den externen Prozess anlegen, Antrag erneut verarbeiten |
| Section vorhanden, wird aber nicht gematcht | Schreibweise weicht von der Namenskonvention ab (Klammern, Leerzeichen um `<`, Großschreibung) | Namen gemäß Abschnitt 6.2 korrigieren |
| Fortinet: FW-Regel korrekt gelabelt, aber falsch einsortiert | Move nach dem Anlegen fehlgeschlagen (nicht atomar) | Position manuell korrigieren, Verifikation auswerten |
| Fortinet: fremde Regeln erscheinen plötzlich in der Section | Nachfolgende FW-Regeln ohne eigenes Label erben die vorangehende Gruppe | Label der betroffenen FW-Regeln explizit setzen |
| Fortinet: neue FW-Regel steht weit unten im Regelwerk | Zielposition wurde im letzten statt im ersten Block der Section ermittelt | Ermittlung des ersten zusammenhängenden Blocks prüfen |
| Regel greift nicht, obwohl vorhanden | Funktionaler Teil verletzt oder Position unterhalb einer Cleanup-Rule | Zielbereich prüfen, Lage der Section im Regelwerk korrigieren |
| CheckPoint: Regel im falschen Policy-Package | Layer-Name nicht eindeutig adressiert | Layer inkl. Package-Präfix angeben |

## 14. Offene Punkte

| Nr. | Punkt | Abschnitt |
|---|---|---|
| 1 | Speicherort und Format der Positionierungs-Konfiguration je Firewall-Typ | 7 / Schritt 1 |
| 2 | Verhalten bei Teilfehlern, wenn ein Antrag Regeln auf mehreren Firewalls erzeugt | 7 / Schritt 5 |
| 3 | Positionierungsverfahren für Azure — erst bei Einbindung in FWO relevant | 8.3 |
| 4 | Umfang der normalisierten Tabelle: alle Spalten oder je Firewall-Typ nur die relevanten | 11.2 |
| 5 | Ausarbeitung der übergeordneten Entscheidungen zum Schreiben | 12 |

## 15. Grenzen des Verfahrens

- Nur **neue** Regeln; das Zusammenführen mit bestehenden gleichartigen Regeln findet nicht statt (laut Prämisse) — Regelwerke wachsen dadurch monoton.
- Der Algorithmus prüft die **Position**, nicht die **Wirksamkeit**: Eine korrekt in ihrer Section platzierte Regel kann von einer weiter oben stehenden Deny-Regel verschattet werden. Eine Shadowing-Prüfung ist nicht Bestandteil und sollte separat betrachtet werden.
- Bei Fortinet ist die Platzierung prinzipbedingt zweistufig und damit nicht transaktionssicher.
- Azure Firewalls sind nicht Bestandteil; sie werden außerhalb von FWO verwaltet.
- Die Verfügbarkeit der Sections ist Voraussetzung; die Funktion ist damit vom externen Anlege-Prozess abhängig. Bei `SECTION_LABEL` (Fortinet) ließe sich diese Abhängigkeit mit geringem Aufwand auflösen (Abschnitt 7, Schritt 4).
- Die Anzeige in der Implementation Phase (Kapitel 11) stellt die geplante Platzierung dar; sie ersetzt keine inhaltliche Prüfung der Regel.
- Kapitel 12 ist bewusst nur angerissen und für die Umsetzung nicht ausreichend detailliert.

## 16. Verwandte Themen

- Pfadanalyse zur Ermittlung Flow-relevanter Firewalls (liefert die Zielfirewalls)
- Zonenermittlung (liefert Quell- und Ziel-Zone, genau eine je Seite)
- Regelbeantragungs-Workflow (liefert Quell-, Ziel- und Service-Objekte)
- Externer Prozess zum Anlegen von Sections
- Verwaltung der Azure Firewalls außerhalb von FWO (zukünftige Einbindung)
- Shadowing- und Compliance-Prüfung nach der Platzierung
- Gesonderte Ausarbeitung der übergeordneten Entscheidungen zum Schreiben (Kapitel 12)
