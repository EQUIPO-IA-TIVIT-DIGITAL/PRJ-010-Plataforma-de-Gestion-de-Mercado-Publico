-- Seed de usuarios demo con contraseñas hasheadas usando pgcrypto
-- Requiere extensión pgcrypto para crypt() y gen_salt()
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Usuarios demo (contraseña: test123)
INSERT INTO usuarios (email, nombre, password_hash, roles, tenant_id, tenant_nombre)
VALUES 
    ('admin@tivit.cl', 'Admin TIVIT', crypt('test123', gen_salt('bf', 11)), ARRAY['SuperAdmin'], 'tenant-001', 'TIVIT Chile'),
    ('analista@tivit.cl', 'Analista TIVIT', crypt('test123', gen_salt('bf', 11)), ARRAY['Analista'], 'tenant-001', 'TIVIT Chile')
ON CONFLICT (email) DO NOTHING;
