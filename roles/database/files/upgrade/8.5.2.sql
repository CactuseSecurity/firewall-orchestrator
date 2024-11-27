DO $do$ BEGIN
    IF EXISTS (SELECT color_name FROM stm_color WHERE color_name='crete blue') THEN
        INSERT INTO stm_color (color_name, color_rgb) VALUES ('crete blue', '485cd4')
    END IF;
    IF EXISTS (SELECT color_name FROM stm_color WHERE color_name='state blue') THEN
        INSERT INTO stm_color (color_name, color_rgb) VALUES ('state blue', 'a186ed')
    END IF;
    IF EXISTS (SELECT color_name FROM stm_color WHERE color_name='olive') THEN
        INSERT INTO stm_color (color_name, color_rgb) VALUES ('olive', '617d28')
    END IF;
    IF EXISTS (SELECT color_name FROM stm_color WHERE color_name='dark gold') THEN
        INSERT INTO stm_color (color_name, color_rgb) VALUES ('dark gold', 'cdad00')
    END IF;
END $do$;