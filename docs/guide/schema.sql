-- Reads the schema and the Turkish descriptions stored in the database (SchemaComments) as one JSON document.
-- The user guide's database chapter is generated from this output.
SELECT json_agg(t ORDER BY t.ord, t.name) FROM (
  SELECT
    c.relname AS name,
    CASE WHEN c.relname LIKE 'AspNet%' THEN 2 WHEN c.relname LIKE '\_\_%' THEN 3 ELSE 1 END AS ord,
    obj_description(c.oid, 'pg_class') AS comment,
    (SELECT json_agg(json_build_object(
        'name', a.attname,
        'type', format_type(a.atttypid, a.atttypmod),
        'nullable', NOT a.attnotnull,
        'pk', EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = c.oid AND i.indisprimary AND a.attnum = ANY (i.indkey)),
        'comment', col_description(c.oid, a.attnum)) ORDER BY a.attnum)
     FROM pg_attribute a WHERE a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped) AS columns,
    (SELECT json_agg(json_build_object(
        'name', ic.relname,
        'unique', i.indisunique,
        'definition', regexp_replace(pg_get_indexdef(i.indexrelid), '^.* USING ', '')) ORDER BY ic.relname)
     FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid
     WHERE i.indrelid = c.oid AND NOT i.indisprimary) AS indexes,
    (SELECT json_agg(json_build_object(
        'columns', (SELECT string_agg(att.attname, ', ') FROM unnest(fk.conkey) k JOIN pg_attribute att ON att.attrelid = fk.conrelid AND att.attnum = k),
        'references', fk.confrelid::regclass::text,
        'onDelete', CASE fk.confdeltype WHEN 'c' THEN 'cascade' WHEN 'r' THEN 'restrict' WHEN 'n' THEN 'set null' ELSE 'no action' END))
     FROM pg_constraint fk WHERE fk.conrelid = c.oid AND fk.contype = 'f') AS foreign_keys
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
  WHERE n.nspname = 'public' AND c.relkind = 'r'
) t;
