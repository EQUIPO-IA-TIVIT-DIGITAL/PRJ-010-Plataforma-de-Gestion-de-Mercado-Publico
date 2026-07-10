-- Cuentas para los gerentes que revisaran el sistema (Pablo Prieto, Jesus Lopez).
-- Los hashes fueron pre-calculados (bcrypt via crypt()/gen_salt) -- la contrasena en texto
-- plano nunca se escribe en este archivo ni queda en el historial de git. Se comunican fuera
-- del repositorio; se recomienda que las cambien via "Olvide mi contrasena" en su primer ingreso.
INSERT INTO usuarios (email, nombre, password_hash, roles, tenant_id, tenant_nombre)
VALUES
    ('pablo.prieto@tivit.com', 'Pablo Prieto', '$2a$11$30Qu.Sq1zCFWuxSRbH2PWOD96.JLtdpWz1a2jCXYnvRR9fGzQ1JXq', ARRAY['SuperAdmin'], 'tenant-001', 'TIVIT Chile'),
    ('jesus.lopez@tivit.com', 'Jesus Lopez', '$2a$11$rLgnOjvpsmOiYPuYthY96.5VMkED8vzi/OcvwXr8gsBbV7BOC6Y6u', ARRAY['SuperAdmin'], 'tenant-001', 'TIVIT Chile')
ON CONFLICT (email) DO NOTHING;
