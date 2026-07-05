-- Seed users for testing chat
INSERT INTO usuarios (email, nombre, password_hash, roles, tenant_id, tenant_nombre)
VALUES
    ('usuario2@mpm.cl', 'Usuario Demo 2', crypt('test123', gen_salt('bf', 11)), ARRAY['Usuario'], 'tenant-001', 'TIVIT Chile'),
    ('usuario3@mpm.cl', 'Usuario Demo 3', crypt('test123', gen_salt('bf', 11)), ARRAY['Usuario'], 'tenant-001', 'TIVIT Chile')
ON CONFLICT (email) DO NOTHING;
