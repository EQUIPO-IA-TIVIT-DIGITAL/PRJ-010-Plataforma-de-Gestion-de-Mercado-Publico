-- V127: Amplía el catálogo de áreas de negocio (spec 019 — feedback del usuario: "3 áreas
-- me parece muy poco"). Palabras clave definidas directamente por el equipo de desarrollo
-- (equipo comercial no disponible en el momento) -- a revisar/ajustar con negocio más
-- adelante si alguna clasifica mal. Mismo mecanismo que V118: sin columna nueva en
-- licitaciones, se clasifica en consulta vía fn_licitacion_area_codigos (V118).

INSERT INTO areas_negocio (codigo, nombre, palabras_clave) VALUES
    (4, 'Infraestructura y Data Center', ARRAY[
        'data center', 'centro de datos', 'colocation', 'colocación',
        'sala de servidores', 'rack', 'servidores físicos', 'hardware',
        'cableado estructurado', 'ups', 'climatización de precisión',
        'arriendo de servidores', 'infraestructura tecnológica',
        -- 'almacenamiento' (a secas) se cambió por la frase completa: la palabra sola es
        -- genérica en español (bodegas, insulina, documentos físicos) y devolvía sobre todo
        -- falsos positivos ajenos a TI (confirmado en vivo 2026-08-05).
        'almacenamiento de datos', 'storage', 'virtualización', 'respaldo físico'
    ]),
    (5, 'Redes y Telecomunicaciones', ARRAY[
        'red de datos', 'redes de comunicaciones', 'conectividad',
        'enlace de datos', 'fibra óptica', 'wan', 'lan', 'wifi',
        'telecomunicaciones', 'ancho de banda', 'proveedor de internet',
        -- 'isp' se saco a proposito: colisiona con "Instituto de Salud Publica" (entidad
        -- chilena muy comun), generaba falsos positivos de licitaciones de salud/laboratorio
        -- (confirmado en vivo 2026-08-05).
        'enlace dedicado', 'switch', 'router', 'firewall perimetral',
        'telefonía ip', 'videoconferencia'
    ]),
    (6, 'Soporte y Outsourcing TI', ARRAY[
        'mesa de ayuda', 'service desk', 'help desk',
        'soporte técnico', 'soporte informático', 'outsourcing de ti',
        'externalización de servicios ti',
        -- 'mesa de servicio', 'mesa técnica' y 'gestión de servicios ti' se sacaron a
        -- proposito: el stemmer en español reduce "mesa" y "mes"/"meses" a la misma raíz
        -- ('mes'), y "gestión de servicios" (sin "ti", que se descarta por muy corto) es
        -- demasiado generico -- las tres devolvian decenas de licitaciones de salud/aseo/
        -- transporte sin relacion a TI (confirmado en vivo 2026-08-05: 78, 7 y 52 falsos
        -- positivos respectivamente).
        'sla', 'acuerdo de nivel de servicio', 'administración de sistemas',
        'operación de plataformas', 'monitoreo de plataformas'
    ])
ON CONFLICT (codigo) DO NOTHING;
