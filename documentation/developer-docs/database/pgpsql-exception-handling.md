# Exception Handling in PGPSQL stored procedures

## documentation
- official docs: https://www.postgresql.org/docs/14/plpgsql-control-structures.html#PLPGSQL-ERROR-TRAPPING
- error codes: https://www.postgresql.org/docs/current/errcodes-appendix.html
- short tutorial: https://www.postgresqltutorial.com/plpgsql-exception/

## log error

    GET DIAGNOSTICS stack = PG_CONTEXT;
    RAISE NOTICE E'--- Call Stack ---\n%', stack;

## exception handling strategy

1) need to rollback all database changes when error occurs
2) need to report errors 
   - print on command line when calling import manually
   - write to import_control.import_errors
   - content: 
     - position of error (stack: GET DIAGNOSTICS stack = PG_CONTEXT;)
     - error string containing the object causing the error (obj_name and obj uid)
     - in case of rule, also include rule UID
3) decide where to catch the error and how to go on with the function
4) db changes within exception block remain intact, changes in statement block get rolled back
5) do not pass error strings up in call stack but simply add to end of string import_control.import_errors

### Example call hierarchy

    import_all_main
	    import_global_refhandler_main
		    import_rule_refhandler_main
			    resolve_rule_list
				    f_add_single_rule_from_element
				    f_add_single_rule_to_element
				    f_add_single_rule_svc_element

### Exception handling architecture

    import_all_main
        --> exception_handling top level
            - catch exceptions from detail level and append them to import_control.import_errors
            - make sure everything is rolled back!
	    import_global_refhandler_main
		    import_rule_refhandler_main
			    resolve_rule_list
				    f_add_single_rule_from_element
                        --> exception_handling detail level
                            - throw exception with details on stack + object name
                            - can we use a function for this?
				    f_add_single_rule_to_element
                        ...
				    f_add_single_rule_svc_element
                        ...


### error handling function
```plpgsql
CREATE OR REPLACE FUNCTION error_handling (varchar, varchar)
    RETURNS varchar
    AS $$
DECLARE
    errid ALIAS FOR $1;
    var_output_string ALIAS FOR $2;
    err RECORD;
    lang RECORD;
    err_txt text;
    err_prefix varchar;
BEGIN
    err_txt := '';
    SELECT
        INTO err *
    FROM
        error
    WHERE
        error_id = errid;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'errorid not found %', errid;
    END IF;
    SELECT
        INTO lang config_value
    FROM
        config
    WHERE
        config_key = 'DefaultLanguage';
    IF NOT FOUND THEN
        RAISE EXCEPTION 'config not found, %', errid;
    END IF;
    IF lang.config_value = 'German' THEN
        err_txt := err.error_txt_ger;
        IF err.error_lvl = 1 THEN
            err_prefix := 'FEHLER: ';
        ELSIF err.error_lvl = 2 THEN
            err_prefix := 'WARNUNG: ';
        ELSIF err.error_lvl = 3 THEN
            err_prefix := 'WARNUNG: ';
        ELSIF err.error_lvl = 4 THEN
            err_prefix := 'INFO: ';
        ELSE
            RAISE EXCEPTION 'Unbekannte Fehlerstufe %', err.error_lvl;
        END IF;
    ELSE
        err_txt := err.error_txt_eng;
        IF err.error_lvl = 1 THEN
            err_prefix := 'ERROR: ';
        ELSIF err.error_lvl = 2 THEN
            err_prefix := 'WARNING: ';
        ELSIF err.error_lvl = 3 THEN
            err_prefix := 'WARNING: ';
        ELSIF err.error_lvl = 4 THEN
            err_prefix := 'INFO: ';
        ELSE
            RAISE EXCEPTION 'Unbekannte Fehlerstufe %', err.error_lvl;
        END IF;
    END IF;
    err_prefix := err_prefix || errid || ': ';
    IF var_output_string <> '' THEN
        err_txt := err_txt || ': ' || var_output_string;
    END IF;
    err_txt := err_prefix || err_txt;
    INSERT INTO error_log (error_id, error_txt)
        VALUES (errid, err_txt);
    IF err.error_lvl = 1 THEN
        RAISE DEBUG 'sorry, encountered fatal error: %', err_txt;
        RAISE EXCEPTION '%', err_txt;
    ELSIF err.error_lvl = 2 THEN
        RAISE NOTICE '%', err_txt;
    ELSIF err.error_lvl = 3 THEN
        RAISE NOTICE '%', err_txt;
    ELSIF err.error_lvl = 4 THEN
        RAISE DEBUG '%', err_txt;
        --		NULL;
    ELSE
        RAISE EXCEPTION 'unknown errorlevel %', err.error_lvl;
    END IF;
    RETURN err_txt;
END;
$$
LANGUAGE plpgsql;
```