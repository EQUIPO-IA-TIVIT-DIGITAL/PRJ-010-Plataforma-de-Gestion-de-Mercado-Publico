-- Cuenta personal de Matias Mendez Cabrejos (responsable CU010)
INSERT INTO usuarios (email, nombre, password_hash, roles, tenant_id, tenant_nombre)
VALUES (
    'mencabrejos@gmail.com',
    'Matias Mendez',
    crypt('Tivit2025!', gen_salt('bf', 11)),
    ARRAY['SuperAdmin'],
    'tenant-001',
    'TIVIT Chile'
)
ON CONFLICT (email) DO UPDATE
    SET nombre        = EXCLUDED.nombre,
        password_hash = EXCLUDED.password_hash,
        roles         = EXCLUDED.roles;
