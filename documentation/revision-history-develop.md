# Firewall Orchestrator Revision History for DEVELOP branch only

pre-5, a product called IT Security Organizer and was closed source. It was developed starting in 2005.
In 2020 we decided to re-launch a new 

### 6.1.0 - 16.11.2022 DEVELOP
- interactive network analysis prototype in UI
- integrate path analysis to workflow

### 6.1.1 - 15.12.2022 DEVELOP
- recertification on owner base
- preparation of new task types

### 6.1.2 - 20.12.2022 DEVELOP
- start of Palo Alto import module

### 6.1.3 - xx.01.2023 DEVELOP
- enhance recertification

### 6.1.4 - 27.01.2023 DEVELOP
- prepare delete rule requests

### 6.2.2 22.03.2023 DEVELOP
- adding last hit of each rule for check point and FortiManager to recertification (report)

### 6.3.3 09.05.2023 DEVELOP
- new importer module for importing FortiGate directly via FortiOS REST API

### 6.4.4 19.06.2023 DEVELOP
- CPR8x importer: basic support for inline layers

### 6.4.5 22.06.2023 DEVELOP
- Fortigate API importer: hotfix NAT rules
- upgrade to hasura API 2.28.0

### 6.4.6 23.06.2023 DEVELOP
- new email notification on import changes

### 6.4.7 26.06.2023 DEVELOP
- hotfix fortiOS importer NAT IP addresses
- fixing issue during ubuntu OS upgrade with ldap 
- unifying all buttons in UI

### 6.4.8 29.06.2023 DEVELOP
- hotfix fortiOS importer: replacing ambiguous import statement

### 6.4.9 03.07.2023 DEVELOP
- fix sample group role path

### 6.4.10 07.07.2023 DEVELOP
- fixes in importer change mail notification for encrypted mails
- fixes for report links to objects
- fix template name display issue
- fix UI visibility for fw-admin role (multiple pages)
- UI login page: allow enter as submit
- UI reporting: filter objects in rule report
- adding demo video in github README.MD

### 6.4.11 10.07.2023 DEVELOP
- bugfix in importer change mail notification for missing mail server config

### 6.4.12 14.07.2023 DEVELOP
- hotfix email port (default 25) was not written to config before
