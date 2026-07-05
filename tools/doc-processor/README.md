# Procesador de Documentos de Licitaciones con IA

Script que analiza documentos de licitaciones usando OpenAI GPT-4o-mini y extrae parámetros estructurados.

## Requisitos

- Node.js 18+
- API Key de OpenAI

## Instalación

```bash
cd tools/doc-processor
npm install
```

## Configuración

1. Copia el archivo de ejemplo:
```bash
cp .env.example .env
```

2. Edita `.env` y agrega tu API Key de OpenAI:
```
OPENAI_API_KEY=sk-tu-api-key-real
OPENAI_MODEL=gpt-4o-mini
DOCUMENTS_PATH=/home/maliaga/Documentos/Licitaciones/SERVICIO DE HOUSING - CÓDIGO: E1O2RT9
OUTPUT_PATH=./resultados
```

## Uso

```bash
node procesar.js
```

## Qué hace

1. **Lista todos los documentos** en la carpeta (PDF, DOCX, XLS)
2. **Extrae el texto** de cada documento
3. **Clasifica los documentos** por tipo (bases, oferta, certificación, etc.)
4. **Analiza con IA** usando prompts específicos:
   - Bases → Extrae criterios, montos, requisitos, fechas
   - Ofertas → Extrae propuesta técnica, equipo, experiencia
   - Comparativa → Genera análisis de fortalezas/debilidades
5. **Guarda resultados** en JSON estructurado

## Archivos generados

```
resultados/
├── textos-extraidos.json      ← Texto extraído de cada documento
├── analisis-completo.json     ← Análisis completo de IA
└── resumen-dashboard.json     ← Resumen para visualización
```

## Estructura del resumen

```json
{
  "licitacion": {
    "nombre": "SERVICIO DE HOUSING",
    "codigo": "E1O2RT9",
    "organismo": "Instituto Nacional de Estadísticas",
    "monto": 5833,
    "moneda": "UF",
    "duracion": 36
  },
  "criterios": [
    {"nombre": "Técnico", "ponderacion_porcentaje": 45},
    {"nombre": "Económico", "ponderacion_porcentaje": 50},
    {"nombre": "Administrativo", "ponderacion_porcentaje": 5}
  ],
  "requisitos": [...],
  "analisis": {
    "oferta": {...},
    "comparativa": {...}
  }
}
```

## Costo estimado

Con **gpt-4o-mini**:
- ~$0.15 por millón de tokens de entrada
- ~$0.60 por millón de tokens de salida
- Para esta licitación: **~$0.05-0.10 USD** (muy barato)

## Próximos pasos

Una vez generado el `resumen-dashboard.json`, se puede:
1. Importar a PostgreSQL (JSONB)
2. Crear endpoints en MPM.Api
3. Visualizar en dashboard React con gráficos
