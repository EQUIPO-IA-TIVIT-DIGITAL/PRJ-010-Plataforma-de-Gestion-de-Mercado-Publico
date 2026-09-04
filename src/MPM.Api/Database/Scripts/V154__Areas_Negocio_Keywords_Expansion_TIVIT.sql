-- V154: Validación y expansión palabras_clave áreas de negocio (Workshop TIVIT 14/08/2026)
-- Feedback capacitación: "verificar que ciberseguridad amplía bien, cubrir todo TIVIT"
-- Expansión basada en servicios reales TIVIT y terminología Mercado Público

-- Actualizar Cloud (código 1)
UPDATE areas_negocio SET
    palabras_clave = ARRAY[
        -- Cloud genérico
        'cloud', 'nube', 'computación en la nube', 'servicios en la nube',
        'aws', 'amazon web services', 'google cloud', 'gcp', 'azure', 'microsoft azure',
        'infraestructura como servicio', 'iaas', 'paas', 'saas',
        'centro de datos', 'data center', 'servidores', 'hosting', 'colocation',
        'almacenamiento en la nube', 'migración a la nube', 'cloud migration',
        'kubernetes', 'k8s', 'contenedores', 'docker', 'orchestration',
        'serverless', 'faas', 'lambda', 'cloud functions', 'cloud run',
        'virtualización', 'vmware', 'hyper-v', 'proxmox', 'openstack',
        'disaster recovery cloud', 'backup cloud', 'alta disponibilidad cloud',
        'cloud público', 'cloud privado', 'cloud híbrido', 'multi-cloud',
        'cloud management', 'cloud governance', 'finops', 'cloud cost optimization'
    ],
    updated_at = CURRENT_TIMESTAMP
WHERE codigo = 1;

-- Actualizar Ciberseguridad (código 2) - EXPANSIÓN MAYOR según workshop TIVIT
UPDATE areas_negocio SET
    palabras_clave = ARRAY[
        -- Ciberseguridad genérico
        'ciberseguridad', 'seguridad informática', 'seguridad de la información',
        'seguridad TI', 'infosec', 'cybersecurity',
        -- SOC y monitoreo
        'soc', 'centro de operaciones de seguridad', 'security operations center',
        'monitoreo de seguridad', 'monitoreo 24x7', 'monitoreo continuo',
        'siem', 'security information and event management', 'correlación de eventos',
        'soar', 'security orchestration automation response', 'automatización respuesta',
        -- Gestión vulnerabilidades
        'vulnerabilidades', 'gestión de vulnerabilidades', 'escaneo de vulnerabilidades',
        'pentesting', 'test de penetración', 'pruebas de penetración', 'ethical hacking',
        'red team', 'blue team', 'purple team', 'adversary simulation',
        -- Amenazas y respuesta
        'ciberataque', 'ransomware', 'phishing', 'spear phishing', 'whaling',
        'malware', 'antimalware', 'edr', 'endpoint detection response', 'xdr',
        'threat hunting', 'caza de amenazas', 'inteligencia de amenazas', 'threat intelligence',
        'incident response', 'respuesta a incidentes', 'forense digital', 'digital forensics',
        -- Red y perímetro
        'firewall', 'next-gen firewall', 'waf', 'web application firewall',
        'ids', 'ips', 'detección de intrusión', 'prevención de intrusión',
        'ddos', 'protección ddos', 'mitigación ddos', 'bot management',
        'zero trust', 'confianza cero', 'sase', 'secure access service edge',
        'zmta', 'zero trust network access', 'ztna',
        -- Identidad y acceso
        'autenticación', 'mfa', 'multifactor', '2fa', 'single sign on', 'sso',
        'gestión de identidades', 'iam', 'identity access management', 'pam',
        'privileged access management', 'credenciales', 'certificados digitales',
        'pki', 'infraestructura clave pública', 'firma electrónica',
        -- Cifrado y protección datos
        'cifrado', 'encriptación', 'encryption', 'tls', 'ssl', 'ipsec', 'vpn',
        'respaldo de datos', 'backup', 'recuperación de datos', 'data protection',
        'dlp', 'data loss prevention', 'prevención pérdida datos', 'clasificación datos',
        -- Cumplimiento y gobernanza
        'iso 27001', 'iso 27002', 'nist', 'cis controls', 'pci dss', 'gdpr', 'ley de datos',
        'auditoría de seguridad', 'compliance', 'gobernanza seguridad', 'políticas seguridad',
        'concienciación seguridad', 'security awareness', 'entrenamiento phishing',
        -- Especializado
        'seguridad industrial', 'ot security', 'scada security', 'ics security',
        'seguridad iot', 'iot security', 'device security',
        'cloud security', 'seguridad cloud', 'cspm', 'cloud security posture management',
        'cwpp', 'cloud workload protection platform', 'container security',
        'devsecops', 'security as code', 'shift left security',
        'bug bounty', 'programa recompensas', 'coordinated vulnerability disclosure'
    ],
    updated_at = CURRENT_TIMESTAMP
WHERE codigo = 2;

-- Actualizar Digital (código 3)
UPDATE areas_negocio SET
    palabras_clave = ARRAY[
        -- Transformación digital
        'transformación digital', 'digitalización', 'digitalizacion', 'modernización',
        'modernizacion', 'agilidad digital', 'cultura digital',
        -- Desarrollo software
        'desarrollo de software', 'fábrica de software', 'factory software',
        'aplicación web', 'aplicacion web', 'web app', 'progressive web app', 'pwa',
        'aplicación móvil', 'aplicacion movil', 'app móvil', 'app nativa', 'flutter', 'react native', 'kotlin', 'swift',
        'sistema web', 'plataforma web', 'portal web', 'intranet', 'extranet',
        'sistema de gestión', 'erp', 'crm', 'hris', 'ats', 'lms', 'bpm',
        'integración de sistemas', 'integracion sistemas', 'api', 'rest', 'graphql', 'soap', 'middleware', 'eaI', 'iPaaS',
        'microservicios', 'arquitectura microservicios', 'event driven', 'kafka', 'rabbitmq',
        -- Datos e IA
        'inteligencia artificial', 'ia', 'ai', 'machine learning', 'ml', 'deep learning',
        'analítica de datos', 'analitica de datos', 'data analytics', 'business intelligence', 'bi',
        'big data', 'data lake', 'data warehouse', 'etl', 'elt', 'data pipeline',
        'visualización datos', 'dashboard', 'reporting', 'kpi', 'self-service bi',
        'procesamiento lenguaje natural', 'nlp', 'computer vision', 'generative ai', 'genai', 'llm',
        'chatbot', 'asistente virtual', 'rpa', 'automatización robótica', 'automatizacion robotica',
        'low code', 'no code', 'citizen development',
        -- Experiencia usuario
        'experiencia de usuario', 'ux', 'user experience', 'diseño centrado usuario', 'usabilidad',
        'accesibilidad', 'wcag', 'diseño inclusivo',
        -- E-commerce y marketplace
        'e-commerce', 'ecommerce', 'marketplace', 'tienda online', 'pasarela pagos', 'gateway pago'
    ],
    updated_at = CURRENT_TIMESTAMP
WHERE codigo = 3;

-- Opcional: Nueva área 4 - Infraestructura y Comunicaciones (si TIVIT la necesita)
-- INSERT INTO areas_negocio (codigo, nombre, palabras_clave) VALUES
--     (4, 'Infraestructura y Comunicaciones', ARRAY[...])
-- ON CONFLICT (codigo) DO NOTHING;

-- Verificación
SELECT codigo, nombre, array_length(palabras_clave, 1) as total_keywords
FROM areas_negocio
ORDER BY codigo;