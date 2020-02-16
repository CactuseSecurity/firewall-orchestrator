/*
	Comment:
	This script creates the Standard client (id=0) with IP 0.0.0.0/0
	This client is used for restricting a user to a single client (making all other clients invisible) and not restricting access with ip filtering
	This also still displays rule/section header in html reports.
	corresponding gui.conf settings - note: user groups that need this feature need to be able to view only "Standard" user  
	
	usergroup group1 members:                        user1
	usergroup group1 privileges:                     view-reports
	usergroup group1 visible-clients:               'Standard'

	Additional changes:
		- display_rule_config.php changed so that when filtered for "Standard" (id=0) client rule headers are displayed
		- when Standard client is selected in config, no edit button is displayed - making the Standard client unchangeable

*/

insert into client (client_id, client_name, client_comment) VALUES (0, 'Standard', 'Default client with network 0.0.0.0/0');
insert into client_network (client_net_id, client_id, client_net_ip) VALUES (0, 0, '0.0.0.0/0');

