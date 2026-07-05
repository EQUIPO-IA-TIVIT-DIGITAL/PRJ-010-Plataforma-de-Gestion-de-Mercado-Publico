export const PROMPT_ANALISIS_LICITACION = `
Eres un experto en análisis de licitaciones públicas chilenas. Analiza el siguiente documento y extrae TODOS los parámetros relevantes en formato JSON estructurado.

EXTRAE ESTOS PARÁMETROS:

1. IDENTIFICACIÓN:
   - nombre_licitacion
   - codigo_licitacion
   - tipo_licitacion (privada, pública, etc.)
   - organismo_demandante
   - rut_organismo

2. CRITERIOS DE EVALUACIÓN:
   - criterios: [{nombre, ponderacion_porcentaje, descripcion}]
   - suma_total_porcentajes

3. MONTO Y DURACIÓN:
   - monto_estimado
   - moneda (UF, CLP, USD)
   - duracion_meses
   - renovacion (true/false)
   - tipo_duracion

4. FECHAS CLAVE:
   - fecha_publicacion
   - fecha_cierre
   - fecha_adjudicacion
   - fecha_apertura_tecnica
   - fecha_apertura_economica

5. REQUISITOS TÉCNICOS:
   - certificaciones_requeridas: [lista]
   - experiencia_minima
   - personal_requerido
   - infraestructura_requerida

6. REQUISITOS ADMINISTRATIVOS:
   - documentos_legales: [lista]
   - garantias_requeridas
   - seguros_requeridos

7. ESPECIFICACIONES DEL SERVICIO:
   - descripcion_servicio
   - alcance
   - nivel_servicio_sla
   - entregables

8. CONDICIONES ESPECIALES:
   - subcontratacion_permitida
   - condiciones_pago
   - penalizaciones
   - clausulas_especiales

RESPONDE SOLO CON JSON VÁLIDO. Si un campo no está disponible, usa null.
`;

export const PROMPT_ANALISIS_OFERTA = `
Eres un experto en análisis de ofertas técnicas para licitaciones públicas. Analiza esta oferta y extrae:

1. OFERENTE:
   - nombre_empresa
   - rut_empresa
   - representante_legal

2. PROPUESTA TÉCNICA:
   - solucion_propuesta
   - metodologia
   - tecnologia_utilizada
   - certificaciones_presentadas: [lista]

3. EQUIPO DE TRABAJO:
   - personal_propuesto: [{cargo, nombre, experiencia}]

4. EXPERIENCIA:
   - proyectos_similares: [{nombre, cliente, año, monto}]
   - años_experiencia_sector

5. CUMPLIMIENTO DE REQUISITOS:
   - requisitos_cumplidos: [lista]
   - requisitos_parciales: [lista]
   - requisitos_no_cumplidos: [lista]

RESPONDE SOLO CON JSON VÁLIDO.
`;

export const PROMPT_COMPARATIVA = `
Eres un analista de licitaciones. Tienes los datos extraídos de múltiples documentos de una licitación.
Genera un análisis comparativo que incluya:

1. RESUMEN EJECUTIVO:
   - fortalezas_oferta
   - debilidades_oferta
   - riesgos_identificados

2. ANÁLISIS DE COMPETITIVIDAD:
   - ventaja_competitiva
   - diferenciadores
   - areas_mejora

3. RECOMENDACIONES:
   - acciones_inmediatas
   - mejoras_para_futuro

RESPONDE SOLO CON JSON VÁLIDO.
`;
