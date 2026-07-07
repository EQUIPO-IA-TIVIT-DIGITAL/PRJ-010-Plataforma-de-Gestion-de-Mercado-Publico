-- usp_SyncLog_Iniciar inserta el literal 'EN_PROGRESO' (11 caracteres), que no cabe en
-- VARCHAR(10). Este bug nunca se detecto antes porque el procedimiento usp_SyncLog_Iniciar
-- nunca se ejecuto (ver V080, que corrige la migracion V009 duplicada que lo dejaba huerfano).
ALTER TABLE sync_log ALTER COLUMN estado TYPE VARCHAR(20);
