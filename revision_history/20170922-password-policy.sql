/*
 * 

Austausch web Zweig komplett 

TODO: 
 - password history
 	- pwd hashes mitschreiben
 	- password differs from last x passwords
 - last pwd change mitschreiben: OK
	- password max age
 	- password min age
 - last login mitschreiben: OK

*/

-- Database changes:

ALTER TABLE isoadmin ADD "isoadmin_password_must_be_changed" Boolean NOT NULL Default TRUE;
ALTER TABLE isoadmin ADD "isoadmin_last_login" Timestamp with time zone;
ALTER TABLE isoadmin ADD "isoadmin_last_password_change" Timestamp with time zone;
ALTER TABLE isoadmin ADD "isoadmin_pwd_history" Text;

INSERT INTO text_msg VALUES ('password_policy', 'Mind. 9 Zeichen, 1 Sonderzeichen, 1 Ziffer, 1 Buchstabe', 'Min. 9 characters, 1 special char, 1 digit, 1 letter');
UPDATE text_msg SET text_msg_ger='Neues Passwort', text_msg_eng='New password' WHERE text_msg_id='new_password';

GRANT SELECT, UPDATE ON TABLE public.isoadmin TO reporters;
GRANT SELECT, UPDATE ON TABLE public.isoadmin TO secuadmins;

-- force pwd change for all users:
UPDATE isoadmin SET isoadmin_password_must_be_changed=TRUE;
