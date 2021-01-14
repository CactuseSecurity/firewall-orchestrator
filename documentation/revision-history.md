# Firewall Orchestrator Revision History

pre-5, a product called IT Security Organizer and was closed source. It was developed starting in 2005.
In 2020 we decided to re-launch a new 

## 5.0

### 5.0.0
rename isoadmin --> uiuser

### 5.0.1
create table report_schedule

### 5.0.2
Add column changes_found to import_control table - signifying if an import produced any changes

### 5.0.3
adding config_user column to config table for user specific config settings

### 5.0.4
add report_owner_id column to report table (but do not allow for sharing of generated reports yet)

### 5.0.5 - 13.12.2020
adjust all relevant tables to allow for growth beyond integer (turning into BIGINT)

### 5.0.6 - 18.12.2020
adding tenant_id to ldap_connection table (optional) to allow dedicated ldap_connections per tenant

### 5.0.7 - 22.12.2020
removing stm_report_typ table and references, adding report_schedule.report_schedule_name.

### 5.0.8 - 23.12.2020
adding report_name and report_filetype to report table

### 5.0.9 - 28.12.2020
remove unique constraint from uiuser_username in uiuser

### 5.1.01 - 30.12.2020
drop old functions to enable re-creation with bigint

### 5.1.02 - 30.12.2020
drop default value for uiuser_language in uiuser

### 5.1.03 - 31.12.2020
adding some reporting columns

### 5.1.04 - 01.01.2021
adding default report_templates

### 5.1.05 - 06.01.2021
adding first compliance report template (any rules)

### 5.1.06 - 10.01.2021
adding report template format fk and permissions

### 5.1.07 - 14.01.2021
- adding https reverse proxy in front of middleware server
- removing column report.report_filetype which has been replaced with relation report_schedule_format and extra fields report_json, report_pdf, report_csv, report_html
