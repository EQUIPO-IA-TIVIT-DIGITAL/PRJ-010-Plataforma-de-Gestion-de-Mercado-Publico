-- Migración del módulo Catálogo
-- Seed de monedas (idempotente)

INSERT INTO monedas (codigo, nombre, simbolo, codigo_iso) VALUES
    (1, 'Peso Chileno', '$', 'CLP'),
    (2, 'Dólar Estadounidense', 'US$', 'USD'),
    (3, 'Euro', '€', 'EUR')
ON CONFLICT (codigo) DO NOTHING;