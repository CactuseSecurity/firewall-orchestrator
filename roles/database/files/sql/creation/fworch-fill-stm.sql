
INSERT INTO language ("name", "culture_info") VALUES('German', 'de-DE');
INSERT INTO language ("name", "culture_info") VALUES('English', 'en-US');

insert into uiuser (uiuser_id, uiuser_username, uuid) VALUES (0,'default', 'default');

insert into config (config_key, config_value, config_user) VALUES ('DefaultLanguage', 'English', 0);
insert into config (config_key, config_value, config_user) VALUES ('sessionTimeout', '720', 0);
insert into config (config_key, config_value, config_user) VALUES ('sessionTimeoutNoticePeriod', '60', 0); -- in minutes before expiry
insert into config (config_key, config_value, config_user) VALUES ('uiHostName', 'http://localhost:5000', 0);
-- insert into config (config_key, config_value, config_user) VALUES ('maxMessages', '3', 0);
insert into config (config_key, config_value, config_user) VALUES ('elementsPerFetch', '100', 0);
insert into config (config_key, config_value, config_user) VALUES ('maxInitialFetchesRightSidebar', '10', 0);
insert into config (config_key, config_value, config_user) VALUES ('autoFillRightSidebar', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('dataRetentionTime', '731', 0);
insert into config (config_key, config_value, config_user) VALUES ('importSleepTime', '40', 0);
insert into config (config_key, config_value, config_user) VALUES ('importCheckCertificates', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('importSuppressCertificateWarnings', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('fwApiElementsPerFetch', '150', 0);
insert into config (config_key, config_value, config_user) VALUES ('recertificationPeriod', '365', 0);
insert into config (config_key, config_value, config_user) VALUES ('recertificationNoticePeriod', '30', 0);
insert into config (config_key, config_value, config_user) VALUES ('recertificationDisplayPeriod', '30', 0);
insert into config (config_key, config_value, config_user) VALUES ('ruleRemovalGracePeriod', '60', 0);
insert into config (config_key, config_value, config_user) VALUES ('commentRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('recAutocreateDeleteTicket', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketTitle', 'Ticket Title', 0);
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketReason', 'Ticket Reason', 0);
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleReqTaskTitle', 'Task Title', 0);
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleReqTaskReason', 'Task Reason', 0);
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleTicketPriority', '3', 0);
insert into config (config_key, config_value, config_user) VALUES ('recDeleteRuleInitState', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('recCheckActive', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('recCheckEmailSubject', 'Upcoming rule recertifications', 0);
insert into config (config_key, config_value, config_user) VALUES ('recCheckEmailUpcomingText', 'The following rules are upcoming to be recertified:', 0);
insert into config (config_key, config_value, config_user) VALUES ('recCheckEmailOverdueText', 'The following rules are overdue to be recertified:', 0);
insert into config (config_key, config_value, config_user) VALUES ('recCheckParams', '{"check_interval":2,"check_offset":1,"check_weekday":null,"check_dayofmonth":null}', 0);
insert into config (config_key, config_value, config_user) VALUES ('recRefreshStartup', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('recRefreshDaily', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('messageViewTime', '7', 0);
insert into config (config_key, config_value, config_user) VALUES ('dailyCheckStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('autoDiscoverStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('autoDiscoverSleepTime', '24', 0);
insert into config (config_key, config_value, config_user) VALUES ('minCollapseAllDevices', '15', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwMinLength', '10', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwUpperCaseRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwLowerCaseRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwNumberRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('pwSpecialCharactersRequired', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('maxImportDuration', '4', 0);
insert into config (config_key, config_value, config_user) VALUES ('maxImportInterval', '12', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqMasterStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGenStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAccStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqRulDelStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqRulModStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGrpCreStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGrpModStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGrpDelStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqNewIntStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":49,"active":true},"approval":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"planning":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"verification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"implementation":{"matrix":{"205":[205,249],"49":[210],"210":[610,210,249]},"derived_states":{"205":205,"49":49,"210":210},"lowest_input_state":49,"lowest_start_state":205,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[249,205,299]},"derived_states":{"249":249},"lowest_input_state":249,"lowest_start_state":249,"lowest_end_state":299,"active":true},"recertification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqMasterStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGenStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAccStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqRulDelStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqRulModStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGrpCreStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGrpModStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqGrpDelStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqNewIntStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":49,"active":true},"approval":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"planning":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"verification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"implementation":{"matrix":{"205":[205,249],"49":[210],"210":[610,210,249]},"derived_states":{"205":205,"49":49,"210":210},"lowest_input_state":49,"lowest_start_state":205,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[249,205,299]},"derived_states":{"249":249},"lowest_input_state":249,"lowest_start_state":249,"lowest_end_state":299,"active":true},"recertification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false}}}', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAvailableTaskTypes', '[0,1,2,3]', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqPriorities', '[{"numeric_prio":1,"name":"Highest","ticket_deadline":1,"approval_deadline":1},{"numeric_prio":2,"name":"High","ticket_deadline":3,"approval_deadline":2},{"numeric_prio":3,"name":"Medium","ticket_deadline":7,"approval_deadline":3},{"numeric_prio":4,"name":"Low","ticket_deadline":14,"approval_deadline":7},{"numeric_prio":5,"name":"Lowest","ticket_deadline":30,"approval_deadline":14}]', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAutoCreateImplTasks', 'enterInReqTask', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqOwnerBased', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAllowObjectSearch', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqAllowManualOwnerAdmin', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqActivatePathAnalysis', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('reqShowCompliance', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('unusedTolerance', '400', 0);
insert into config (config_key, config_value, config_user) VALUES ('creationTolerance', '90', 0);
insert into config (config_key, config_value, config_user) VALUES ('ruleOwnershipMode', 'mixed', 0);
insert into config (config_key, config_value, config_user) VALUES ('allowServerInConn', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('allowServiceInConn', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('importAppDataStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('importAppDataSleepTime', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataSleepTime', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('importAppDataPath', '[]', 0);
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataPath', '[]', 0);
insert into config (config_key, config_value, config_user) VALUES ('modNamingConvention', '{"networkAreaRequired":false,"useAppPart":false,"fixedPartLength":0,"freePartLength":0,"networkAreaPattern":"","appRolePattern":""}', 0);
insert into config (config_key, config_value, config_user) VALUES ('modCommonAreas', '[]', 0);
insert into config (config_key, config_value, config_user) VALUES ('modAppServerTypes', '[{"Id":0,"Name":"Default"}]', 0);
insert into config (config_key, config_value, config_user) VALUES ('modReqInterfaceName', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('modReqEmailReceiver', 'OwnerGroupOnly', 0);
insert into config (config_key, config_value, config_user) VALUES ('modReqEmailRequesterInCc', 'true', 0);
insert into config (config_key, config_value, config_user) VALUES ('modReqEmailSubject', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('modReqEmailBody', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('modReqTicketTitle', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('modReqTaskTitle', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('modRolloutActive', 'true', 0);
insert into config (config_key, config_value, config_user) VALUES ('modRolloutResolveServiceGroups', 'true', 0);
insert into config (config_key, config_value, config_user) VALUES ('modRolloutBundleTasks', 'false', 0);
insert into config (config_key, config_value, config_user) VALUES ('modRolloutErrorText', 'Error during external request', 0);
insert into config (config_key, config_value, config_user) VALUES ('modIconify', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('reducedProtocolSet', 'True', 0);
insert into config (config_key, config_value, config_user) VALUES ('overviewDisplayLines', '3', 0);
insert into config (config_key, config_value, config_user) VALUES ('emailServerAddress', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('emailPort', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('emailTls', 'None', 0);
insert into config (config_key, config_value, config_user) VALUES ('emailUser', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('emailPassword', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('emailSenderAddress', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('impChangeNotifyRecipients', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('impChangeNotifySubject', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('impChangeNotifyBody', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('impChangeNotifyActive', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('impChangeNotifyType', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('impChangeNotifySleepTime', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('impChangeNotifyStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('externalRequestSleepTime', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('externalRequestStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('externalRequestWaitCycles', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('modExtraConfigs', '[]', 0);
insert into config (config_key, config_value, config_user) VALUES ('extTicketSystems', '[{"Url":"","TicketTemplate":"{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"domain_name\":\"\",\"workflow\":{\"name\":\"@@WORKFLOW_NAME@@\"},\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[@@TASKS@@]}}}}]}}}","TasksTemplate":"{\"@xsi.type\":\"multi_access_request\",\"name\":\"GewünschterZugang\",\"read_only\":false,\"access_request\":{\"order\":\"AR1\",\"verifier_result\":{\"status\":\"notrun\"},\"use_topology\":true,\"targets\":{\"target\":{\"@type\":\"ANY\"}},\"users\":{\"user\":@@USERS@@},\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"action\":\"@@ACTION@@\",\"labels\":\"\"}},{\"@xsi.type\":\"text_area\",\"name\":\"Grund für den Antrag\",\"read_only\":false,\"text\":\"@@REASON@@\"},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Regel Log aktivieren?\",\"selection\":\"@@LOGGING@@\"},{\"@xsi.type\":\"date\",\"name\":\"Regel befristen bis:\"},{\"@xsi.type\":\"text_field\",\"name\":\"Anwendungs-ID\",\"text\":\"@@APPID@@\"},{\"@xsi.type\":\"checkbox\",\"name\":\"Die benötigte Kommunikationsverbindung ist im Kommunikationsprofil nach IT-Sicherheitsstandard hinterlegt\",\"value\":@@COM_DOCUMENTED@@},{\"@xsi.type\":\"drop_down_list\",\"name\":\"Expertenmodus: Exakt wie beantragt implementieren (Designervorschlag ignorieren)\",\"selection\":\"Nein\"}"}]', 0);
insert into config (config_key, config_value, config_user) VALUES ('welcomeMessage', '', 0);
insert into config (config_key, config_value, config_user) VALUES ('dnsLookup', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('overwriteExistingNames', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('autoReplaceAppServer', 'False', 0);
insert into config (config_key, config_value, config_user) VALUES ('ownerLdapId', '1', 0);
insert into config (config_key, config_value, config_user) VALUES ('ownerLdapGroupNames', 'ModellerGroup_@@ExternalAppId@@', 0);
insert into config (config_key, config_value, config_user) VALUES ('manageOwnerLdapGroups', 'true', 0);
insert into config (config_key, config_value, config_user) VALUES ('modModelledMarker', 'FWOC', 0);
insert into config (config_key, config_value, config_user) VALUES ('modModelledMarkerLocation', 'rulename', 0);
insert into config (config_key, config_value, config_user) VALUES ('ruleRecognitionOption', '{"nwRegardIp":true,"nwRegardName":false,"nwRegardGroupName":false,"nwResolveGroup":false,"svcRegardPortAndProt":true,"svcRegardName":false,"svcRegardGroupName":false,"svcResolveGroup":true,"svcSplitPortRanges":false}', 0);
insert into config (config_key, config_value, config_user) VALUES ('availableReportTypes', '[1,2,3,4,5,6,7,8,9,10,21,22]', 0);
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisSleepTime', '0', 0);
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisStartAt', '00:00:00', 0);
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisSync', 'false', 0);
insert into config (config_key, config_value, config_user) VALUES ('varianceAnalysisRefresh', 'false', 0);
insert into config (config_key, config_value, config_user) VALUES ('resolveNetworkAreas', 'False', 0);

INSERT INTO "report_format" ("report_format_name") VALUES ('json');
INSERT INTO "report_format" ("report_format_name") VALUES ('pdf');
INSERT INTO "report_format" ("report_format_name") VALUES ('csv');
INSERT INTO "report_format" ("report_format_name") VALUES ('html');

-- default report templates belong to user 0 
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','Current Rules','T0101', 0,
        '{"report_type":1,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','This year''s Rule Changes','T0102', 0, 
        '{"report_type":2,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','Basic Statistics','T0103', 0,
        '{"report_type":3,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('(src=any or dst=any or svc=any or src=all or dst=all or svc=all) and not(action=drop or action=reject or action=deny) ',
        'Compliance: Pass rules with ANY','T0104', 0, 
        '{"report_type":1,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','Current NAT Rules','T0105', 0,
        '{"report_type":4,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','Last year''s Unused Rules','T0106', 0,
        '{"report_type":10,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false},
            "unused_filter": {
                "creationTolerance": 0,
                "unusedForDays": 365}}');
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner", "report_parameters") 
    VALUES ('','Next Month''s Recertifications','T0107', 0,
        '{"report_type":7,"device_filter":{"management":[]},
            "time_filter": {
                "is_shortcut": true,
                "shortcut": "now",
                "report_time": "2022-01-01T00:00:00.0000000+01:00",
                "timerange_type": "SHORTCUT",
                "shortcut_range": "this year",
                "offset": 0,
                "interval": "DAYS",
                "start_time": "2022-01-01T00:00:00.0000000+01:00",
                "end_time": "2022-01-01T00:00:00.0000000+01:00",
                "open_start": false,
                "open_end": false},
            "recert_filter": {
                "recertOwnerList": [],
                "recertShowAnyMatch": true,
                "recertificationDisplayPeriod": 30}}');

insert into parent_rule_type (id, name) VALUES (1, 'section');          -- do not restart numbering
insert into parent_rule_type (id, name) VALUES (2, 'guarded-layer');    -- restart numbering, rule restrictions are ANDed to all rules below it, layer is not entered if guard does not apply
insert into parent_rule_type (id, name) VALUES (3, 'unguarded-layer');  -- restart numbering, no further restrictions

insert into stm_change_type (change_type_id,change_type_name) VALUES (1,'factory settings');
insert into stm_change_type (change_type_id,change_type_name) VALUES (2,'initial import');
insert into stm_change_type (change_type_id,change_type_name) VALUES (3,'in operation');

insert into stm_usr_typ (usr_typ_id,usr_typ_name) VALUES (1,'group');
insert into stm_usr_typ (usr_typ_id,usr_typ_name) VALUES (2,'simple');

insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (1,'simple','standard services');
insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (2,'group','groups of services');
insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (3,'rpc','special services, here: RPC');

insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (1,'network');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (2,'group');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (3,'host');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (4,'machines_range');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (5,'dynamic_net_obj');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (6,'sofaware_profiles_security_level');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (7,'gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (8,'cluster_member');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (9,'gateway_cluster');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (10,'domain');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (11,'group_with_exclusion');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (12,'ip_range');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (13,'uas_collection');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (14,'sofaware_gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (15,'voip_gk');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (16,'gsn_handover_group');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (17,'voip_sip');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (18,'simple-gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (19,'external-gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (20,'voip');   -- general voip object replacing old specific ones and including CpmiVoipSipDomain
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (21,'access-role'); 

insert into stm_action (action_id,action_name) VALUES (1,'accept'); -- cp, fortinet
insert into stm_action (action_id,action_name, allowed) VALUES (2,'drop', FALSE); -- cp
insert into stm_action (action_id,action_name, allowed) VALUES (3,'deny', FALSE); -- netscreen, fortinet
insert into stm_action (action_id,action_name) VALUES (4,'access'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (5,'client encrypt'); -- cp
insert into stm_action (action_id,action_name) VALUES (6,'client auth'); -- cp
insert into stm_action (action_id,action_name, allowed) VALUES (7,'reject', FALSE); -- cp
insert into stm_action (action_id,action_name) VALUES (8,'encrypt'); -- cp
insert into stm_action (action_id,action_name) VALUES (9,'user auth'); -- cp
insert into stm_action (action_id,action_name) VALUES (10,'session auth'); -- cp
insert into stm_action (action_id,action_name) VALUES (11,'permit'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (12,'permit webauth'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (13,'redirect'); -- phion
insert into stm_action (action_id,action_name) VALUES (14,'map'); -- phion
insert into stm_action (action_id,action_name) VALUES (15,'permit auth'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (16,'tunnel l2tp'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (17,'tunnel vpn-group'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (18,'tunnel vpn'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (19,'actionlocalredirect'); -- phion
insert into stm_action (action_id,action_name) VALUES (20,'inner layer'); -- check point r8x
-- adding new nat actions for nat rules (xlate_rule only)
insert into stm_action (action_id,action_name) VALUES (21,'NAT src') ON CONFLICT DO NOTHING; -- source ip nat
insert into stm_action (action_id,action_name) VALUES (22,'NAT src, dst') ON CONFLICT DO NOTHING; -- source and destination ip nat
insert into stm_action (action_id,action_name) VALUES (23,'NAT src, dst, svc') ON CONFLICT DO NOTHING; -- source and destination ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (24,'NAT dst') ON CONFLICT DO NOTHING; -- destination ip nat
insert into stm_action (action_id,action_name) VALUES (25,'NAT dst, svc') ON CONFLICT DO NOTHING; -- destination ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (26,'NAT svc') ON CONFLICT DO NOTHING; -- port nat
insert into stm_action (action_id,action_name) VALUES (27,'NAT src, svc') ON CONFLICT DO NOTHING; -- source ip nat plus port nat
insert into stm_action (action_id,action_name) VALUES (28,'NAT') ON CONFLICT DO NOTHING; -- generic NAT
insert into stm_action (action_id,action_name) VALUES (29,'inform'); -- cp

insert into stm_track (track_id,track_name) VALUES (1,'log');
insert into stm_track (track_id,track_name) VALUES (2,'none');
insert into stm_track (track_id,track_name) VALUES (3,'alert');
insert into stm_track (track_id,track_name) VALUES (4,'userdefined');
insert into stm_track (track_id,track_name) VALUES (5,'mail');
insert into stm_track (track_id,track_name) VALUES (6,'account');
insert into stm_track (track_id,track_name) VALUES (7,'userdefined 1');
insert into stm_track (track_id,track_name) VALUES (8,'userdefined 2');
insert into stm_track (track_id,track_name) VALUES (9,'userdefined 3');
insert into stm_track (track_id,track_name) VALUES (10,'snmptrap');
-- junos
insert into stm_track (track_id,track_name) VALUES (11,'log count');
insert into stm_track (track_id,track_name) VALUES (12,'count');
insert into stm_track (track_id,track_name) VALUES (13,'log alert');
insert into stm_track (track_id,track_name) VALUES (14,'log alert count');
insert into stm_track (track_id,track_name) VALUES (15,'log alert count alarm');
insert into stm_track (track_id,track_name) VALUES (16,'log count alarm');
insert into stm_track (track_id,track_name) VALUES (17,'count alarm');
-- fortinet:
insert into stm_track (track_id,track_name) VALUES (18,'all');
insert into stm_track (track_id,track_name) VALUES (19,'all start');
insert into stm_track (track_id,track_name) VALUES (20,'utm');
insert into stm_track (track_id,track_name) VALUES (21,'network log'); -- check point R8x:
insert into stm_track (track_id,track_name) VALUES (22,'utm start'); -- fortinet
insert into stm_track (track_id,track_name) VALUES (23,'detailed log'); -- check point R8x:

-- insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
--     VALUES (2,'Netscreen','5.x-6.x','Netscreen', '', true,false);
-- insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
--     VALUES (4,'FortiGateStandalone','5ff','Fortinet','', true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (5,'Barracuda Firewall Control Center','Vx','phion','',true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (6,'phion netfence','3.x','phion','', false,false);
-- insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
--     VALUES (7,'Check Point','R5x-R7x','Check Point','', true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (8,'JUNOS','10-21','Juniper','any;0;0;65535;;junos-predefined-service;simple;', true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (9,'Check Point','R8x','Check Point','', true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (10,'FortiGate','5ff','Fortinet','', false,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (11,'FortiADOM','5ff','Fortinet','', true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (12,'FortiManager','5ff','Fortinet','',true,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (13,'Check Point','MDS R8x','Check Point','',true,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (14,'Cisco Firepower Management Center','7ff','Cisco','',true,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (15,'Cisco Firepower Domain','7ff','Cisco','',false,true,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (16,'Cisco Firepower Gateway','7ff','Cisco','',false,false,false);
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (17,'DummyRouter Management','1','DummyRouter','',false,true,true) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (18,'DummyRouter Gateway','1','DummyRouter','',false,false,true) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (19,'Azure','2022ff','Microsoft','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (20,'Azure Firewall','2022ff','Microsoft','',false,false,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
     VALUES (21,'Palo Alto Firewall','2023ff','Palo Alto','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (22,'Palo Alto Panorama','2023ff','Palo Alto','',true,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (23,'Palo Alto Management','2023ff','Palo Alto','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (24,'FortiOS Management','REST','Fortinet','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (25,'Fortinet FortiOS Gateway','REST','Fortinet','',false,false,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (26,'NSX','REST','VMWare','',false,true,false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt,dev_typ_is_mgmt,is_pure_routing_device)
    VALUES (27,'NSX DFW Gateway','REST','VMWare','',false,false,false) ON CONFLICT DO NOTHING;

-- SET statement_timeout = 0;
-- SET client_encoding = 'UTF8';
-- SET standard_conforming_strings = on;
-- SET check_function_bodies = false;
-- SET client_min_messages = warning;
-- SET search_path = public, pg_catalog;

insert into request.state (id,name) VALUES (0,'Draft');
insert into request.state (id,name) VALUES (49,'Requested');

insert into request.state (id,name) VALUES (50,'To Approve');
insert into request.state (id,name) VALUES (60,'In Approval');
insert into request.state (id,name) VALUES (99,'Approved');

insert into request.state (id,name) VALUES (100,'To Plan');
insert into request.state (id,name) VALUES (110,'In Planning');
insert into request.state (id,name) VALUES (120,'Wait For Approval');
insert into request.state (id,name) VALUES (130,'Compliance Violation');
insert into request.state (id,name) VALUES (149,'Planned');

insert into request.state (id,name) VALUES (150,'To Verify Plan');
insert into request.state (id,name) VALUES (160,'Plan In Verification');
insert into request.state (id,name) VALUES (199,'Plan Verified');

insert into request.state (id,name) VALUES (200,'To Implement');
insert into request.state (id,name) VALUES (205,'Rework');
insert into request.state (id,name) VALUES (210,'In Implementation');
insert into request.state (id,name) VALUES (220,'Implementation Trouble');
insert into request.state (id,name) VALUES (249,'Implemented');

insert into request.state (id,name) VALUES (250,'To Review');
insert into request.state (id,name) VALUES (260,'In Review');
insert into request.state (id,name) VALUES (270,'Further Work Requested');
insert into request.state (id,name) VALUES (299,'Verified');

insert into request.state (id,name) VALUES (300,'To Recertify');
insert into request.state (id,name) VALUES (310,'In Recertification');
insert into request.state (id,name) VALUES (349,'Recertified');
insert into request.state (id,name) VALUES (400,'Decertified');

insert into request.state (id,name) VALUES (500,'InProgress');

insert into request.state (id,name) VALUES (600,'Done');
insert into request.state (id,name) VALUES (610,'Rejected');
insert into request.state (id,name) VALUES (620,'Discarded');

INSERT INTO owner (id, name, dn, group_dn, is_default, recert_interval, app_id_external) 
VALUES    (0, 'super-owner', 'uid=admin,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal', 'group-dn-for-super-owner', true, 365, 'NONE')
ON CONFLICT DO NOTHING; 

insert into stm_link_type (id, name) VALUES (2, 'ordered');
insert into stm_link_type (id, name) VALUES (3, 'inline');
insert into stm_link_type (id, name) VALUES (4, 'concatenated');
