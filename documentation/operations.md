# Tips on Firewall Orchestrator Operations


## Backup / Restore

We recommend to backup the following data:

- PostgreSQL database fworchdb using a tool like pg_dump
- directory /etc/fworch (which is a link to /usr/local/fworch/etc)
- optionally any customized settings like /etc/syslog-ng.conf etc. which were not automatically created during the installation process

## Monitoring and alerting

Use /var/log/fworch/alert.log to generate alerts with the SIEM tool of your choice.

## Logging

All logs are written into the /var/log/fworch directory on the central backend system.
We recommend using a separate partition for logs.

View multiple logs at once with the following command:

```console
tim@ubu2004test:~$ cd /var/log/fworch
tim@ubu2004test:/var/log/fworch$ tail -f ui.log auth.log ldap.log api.log 

==> ui.log <==
Aug 25 16:48:19 ubu2004test fworch-ui[407866]: Sending GetJWT Request...

==> auth.log <==
Aug 25 16:48:19 ubu2004test fworch.middleware-server[403234]: Try to validate as admin...
Aug 25 16:48:19 ubu2004test fworch.middleware-server[403234]: FWO_Auth_Server::Ldap.cs: ValidateUser called for user uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal
Aug 25 16:48:19 ubu2004test fworch.middleware-server[403234]: FWO_Auth_Server::Ldap.cs: LdapServerPort=636

==> ldap.log <==
Aug 25 16:48:19 ubu2004test slapd[403175]: conn=1002 fd=12 ACCEPT from IP=127.0.0.1:35558 (IP=0.0.0.0:636)
Aug 25 16:48:19 ubu2004test slapd[403175]: conn=1002 fd=12 TLS established tls_ssf=256 ssf=256
Aug 25 16:48:19 ubu2004test slapd[403175]: conn=1002 op=0 BIND dn="uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal" method=128
Aug 25 16:48:19 ubu2004test slapd[403175]: conn=1002 op=0 BIND dn="uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal" mech=SIMPLE ssf=0
Aug 25 16:48:19 ubu2004test slapd[403175]: conn=1002 op=0 RESULT tag=97 err=0 text=
Aug 25 16:48:19 ubu2004test slapd[403175]: conn=1002 op=1 UNBIND
Aug 25 16:48:19 ubu2004test slapd[403175]: conn=1002 fd=12 closed

==> auth.log <==
Aug 25 16:48:19 ubu2004test fworch.middleware-server[403234]: Successfully validated as FWO_Auth_Server.User!
Aug 25 16:48:19 ubu2004test fworch.middleware-server[403234]: Generating JWT for user admin ...
Aug 25 16:48:19 ubu2004test fworch.middleware-server[403234]: Generated JWT eyJhbGciOiJIUzM4NCIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImFkbWluIiwieC1oYXN1cmEtdmlzaWJsZS1tYW5hZ2VtZW50cyI6InsxLDcsMTd9IiwieC1oYXN1cmEtdmlzaWJsZS1kZXZpY2VzIjoiezEsNH0iLCJyb2xlIjpbInJlcG9ydGVyLXZpZXdhbGwiLCJyZXBvcnRlciJdLCJ4LWhhc3VyYS1hbGxvd2VkLXJvbGVzIjpbInJlcG9ydGVyLXZpZXdhbGwiLCJyZXBvcnRlciJdLCJ4LWhhc3VyYS1kZWZhdWx0LXJvbGUiOiJyZXBvcnRlci12aWV3YWxsIiwibmJmIjoxNTk4Mzc0MDk5LCJleHAiOjE1OTg5Nzg4OTksImlhdCI6MTU5ODM3NDA5OSwiaXNzIjoiRldPIEF1dGggTW9kdWxlIiwiYXVkIjoiRldPIn0.APtkX_fAMICID9pAzASKrlBGERWvlxbGg01up1CAYD8QKLGtmW1URnO2hvJbkVli for User FWO_Auth_Server.User
Aug 25 16:48:19 ubu2004test fworch.middleware-server[403234]: User admin was assigned the following roles: eyJhbGciOiJIUzM4NCIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImFkbWluIiwieC1oYXN1cmEtdmlzaWJsZS1tYW5hZ2VtZW50cyI6InsxLDcsMTd9IiwieC1oYXN1cmEtdmlzaWJsZS1kZXZpY2VzIjoiezEsNH0iLCJyb2xlIjpbInJlcG9ydGVyLXZpZXdhbGwiLCJyZXBvcnRlciJdLCJ4LWhhc3VyYS1hbGxvd2VkLXJvbGVzIjpbInJlcG9ydGVyLXZpZXdhbGwiLCJyZXBvcnRlciJdLCJ4LWhhc3VyYS1kZWZhdWx0LXJvbGUiOiJyZXBvcnRlci12aWV3YWxsIiwibmJmIjoxNTk4Mzc0MDk5LCJleHAiOjE1OTg5Nzg4OTksImlhdCI6MTU5ODM3NDA5OSwiaXNzIjoiRldPIEF1dGggTW9kdWxlIiwiYXVkIjoiRldPIn0.APtkX_fAMICID9pAzASKrlBGERWvlxbGg01up1CAYD8QKLGtmW1URnO2hvJbkVli

==> ui.log <==
Aug 25 16:48:19 ubu2004test fworch-ui[407866]: APIConnection::ChangeAuthHeader Jwt=eyJhbGciOiJIUzM4NCIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImFkbWluIiwieC1oYXN1cmEtdmlzaWJsZS1tYW5hZ2VtZW50cyI6InsxLDcsMTd9IiwieC1oYXN1cmEtdmlzaWJsZS1kZXZpY2VzIjoiezEsNH0iLCJyb2xlIjpbInJlcG9ydGVyLXZpZXdhbGwiLCJyZXBvcnRlciJdLCJ4LWhhc3VyYS1hbGxvd2VkLXJvbGVzIjpbInJlcG9ydGVyLXZpZXdhbGwiLCJyZXBvcnRlciJdLCJ4LWhhc3VyYS1kZWZhdWx0LXJvbGUiOiJyZXBvcnRlci12aWV3YWxsIiwibmJmIjoxNTk4Mzc0MDk5LCJleHAiOjE1OTg5Nzg4OTksImlhdCI6MTU5ODM3NDA5OSwiaXNzIjoiRldPIEF1dGggTW9kdWxlIiwiYXVkIjoiRldPIn0.APtkX_fAMICID9pAzASKrlBGERWvlxbGg01up1CAYD8QKLGtmW1URnO2hvJbkVli

==> api.log <==
Aug 25 16:48:30 localhost fworch-api[404041]: {"type":"http-log","timestamp":"2020-08-25T16:48:30.779+0000","level":"info","detail":{"operation":{"query_execution_time":1.7827645e-2,"user_vars":{"x-hasura-visible-devices":"{1,4}","x-hasura-role":"reporter-viewall","x-hasura-visible-managements":"{1,7,17}"},"request_id":"218ba919-27e8-42a1-9094-9921b37bab26","response_size":68218,"request_read_time":6.038e-6},"http_info":{"status":200,"http_version":"HTTP/1.1","url":"//v1/graphql","ip":"127.0.0.1","method":"POST","content_encoding":null}}}

==> ui.log <==
Aug 25 16:48:30 ubu2004test fworch-ui[407866]: #033[40m#033[1m#033[33mwarn#033[39m#033[22m#033[49m: Microsoft.AspNetCore.Components.Server.Circuits.RemoteRenderer[100]
Aug 25 16:48:30 ubu2004test fworch-ui[407866]:       Unhandled exception rendering component: The JSON value could not be converted to FWO.Backend.Data.API.Management[]. Path: $.data | LineNumber: 0 | BytePositionInLine: 9.
Aug 25 16:48:30 ubu2004test fworch-ui[407866]: System.Text.Json.JsonException: The JSON value could not be converted to FWO.Backend.Data.API.Management[]. Path: $.data | LineNumber: 0 | BytePositionInLine: 9.
Aug 25 16:48:30 ubu2004test fworch-ui[407866]:    at System.Text.Json.ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type propertyType)
Aug 

```

