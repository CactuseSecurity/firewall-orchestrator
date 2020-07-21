-- $Id: 20070728-iso-request-type.sql,v 1.1.2.2 2007-12-13 10:47:32 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20070728-iso-request-type.sql,v $

alter table "request" add column "request_type_id" Integer;

Create table "request_type"
(
	"request_type_id" Integer NOT NULL UNIQUE,
	"request_type_name" Varchar NOT NULL UNIQUE,
	"request_type_comment" Varchar,
 primary key ("request_type_id")
) With Oids;

-- now creating the link between request and request_type
Create index "IX_Relationship181" on "request" ("request_type_id");
Alter table "request" add  foreign key ("request_type_id") references "request_type" ("request_type_id") on update restrict on delete restrict;
alter table "request" alter column "request_type_id" DROP NOT NULL;
-- this can surely be solved more elegantly (but how?)

-- set table permissions
Grant select on "request_type" to group "secuadmins";
Grant select on "request_type" to group "dbbackupusers";
Grant select on "request_type" to group "reporters";
Grant select on "request_type" to group "isoadmins";
Grant update on "request_type" to group "isoadmins";
Grant insert on "request_type" to group "isoadmins";

-- now neither client nor type has to be specified (become optional)
alter table "request" alter column "client_id" DROP  NOT NULL;

-- adding default request-type (optional)
-- insert into request_type (request_type_id, request_type_name, request_type_comment) VALUES (1, 'ARS', 'Remedy ARS Ticket');

-- the following is not necessary any more (since request_type_id can be null)
-- update request set request_type_id = 1; 

-- the following function has to be redefined:
-- CREATE OR REPLACE FUNCTION get_request_str(VARCHAR,INTEGER) RETURNS VARCHAR AS $$
CREATE OR REPLACE FUNCTION get_request_str(VARCHAR,BIGINT) RETURNS VARCHAR AS $$
DECLARE
	v_table	ALIAS FOR $1;
	i_id	ALIAS FOR $2;
	r_request RECORD;
	v_tbl	VARCHAR;
	v_result VARCHAR;
	v_id_name VARCHAR;
	v_sql_statement VARCHAR;
BEGIN
	v_result := '';
	IF v_table='object' THEN v_tbl := 'obj'; END IF;
	IF v_table='service' THEN v_tbl := 'svc'; END IF;
	IF v_table='user' THEN v_tbl := 'usr'; END IF;
	IF v_table='rule' THEN v_tbl := 'rule'; END IF;
	v_id_name := 'log_' || v_tbl || '_id';
	v_sql_statement := 'SELECT request_number, client_name, request_type_name FROM request_' ||
		v_table || '_change LEFT JOIN request USING (request_id) LEFT JOIN client USING (client_id) ' ||
		 ' LEFT JOIN request_type using (request_type_id) ' ||
		' WHERE ' || v_id_name || '=' || CAST(i_id AS VARCHAR);
	FOR r_request IN EXECUTE v_sql_statement
	LOOP
		IF v_result<>'' THEN v_result := v_result || '<br>'; END IF;
		IF NOT r_request.client_name IS NULL THEN
			v_result := v_result || r_request.client_name || ': ';
		END IF;
		IF NOT r_request.request_type_name IS NULL THEN
			v_result := v_result || r_request.request_type_name || '-';
		END IF;
		v_result := v_result || r_request.request_number;
		
	END LOOP;
	RETURN v_result;
END;
$$ LANGUAGE plpgsql;
