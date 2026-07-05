# Investigación: ¿Podemos explicar/predecir por qué se ganan licitaciones?

**Fecha**: 2026-07-01
**Alcance**: SOLO investigación — este documento no implica ningún cambio de comportamiento en el sistema (US6, spec 017).
**Pregunta**: ¿Es factible determinar por qué ciertas entidades (ej. "entity data" de organismos compradores) adjudican a ciertos proveedores, y usar eso para mejorar la tasa de victoria de TIVIT?

## 1. Fuentes de datos disponibles

### 1.1 Datos internos (ya en MPM)

Los análisis históricos generados por Gemini sobre actas de evaluación ya contienen señales estructuradas por licitación:

| Señal | Campo del análisis | Utilidad |
|---|---|---|
| Puntajes por criterio TIVIT vs. ganador | `evaluacion.criterios[]`, `comparativa_puntajes` | Identificar en qué criterios se pierde sistemáticamente (precio, experiencia, técnico) |
| Adjudicatario y monto | `adjudicacion.adjudicatario` | Ranking de rivales por organismo y rubro |
| Factores de pérdida | `analisis_tivit.debilidades`, `brechas_identificadas` | Patrones repetidos de derrota |
| Ponderaciones de criterios | `evaluacion.criterios[].ponderacion` | Detectar qué organismos ponderan más el precio vs. lo técnico |
| Organismo comprador | `licitacion.organismo` | Cruce comportamiento por entidad |

**Limitación**: la muestra interna es pequeña (solo licitaciones donde TIVIT participó y se subió el acta), con sesgo de supervivencia — no vemos las licitaciones en las que no participamos.

### 1.2 API pública de Mercado Público

`api.mercadopublico.cl/servicios/v1/publico/licitaciones.json` permite consultar licitaciones adjudicadas por fecha y por código; con el detalle de una licitación adjudicada se obtiene proveedor ganador, montos y organismo. Esto habilita construir, sin scraping:

- Historial de adjudicaciones **por organismo** (a quién le compra cada entidad y con qué frecuencia repite proveedor).
- Historial **por proveedor** (dónde gana la competencia, en qué rubros, con qué tamaño de contrato).
- Tasa de incumbencia: cuántas veces el ganador anterior vuelve a ganar la renovación del mismo servicio (señal fuerte y medible).

**Limitación**: la API pública no expone los puntajes de evaluación ni las ofertas perdedoras — solo el resultado. El "por qué" fino sigue viniendo de las actas.

### 1.3 Datos abiertos de ChileCompra

ChileCompra publica datasets descargables (datos abiertos: órdenes de compra, licitaciones adjudicadas, montos por proveedor y organismo, por año). Sirven para análisis masivo histórico (volúmenes 2019-2026) sin límites de rate de la API.

### 1.4 Señales extraíbles de las actas (ya se extraen)

Las actas contienen criterios y ponderaciones. Con suficiente volumen, se puede estimar por organismo: peso promedio del precio, sensibilidad a experiencia previa con ese organismo, y si el ganador suele ser el más barato o el mejor evaluado técnicamente.

## 2. Viabilidad

**Sí es factible un análisis explicativo (descriptivo) con valor inmediato**, combinando 1.1 + 1.2:

1. **Perfil por organismo**: "este organismo adjudicó 8 de sus últimas 10 licitaciones TI al incumbente; pondera precio 60%" — accionable para decidir dónde competir y con qué estrategia de precio.
2. **Perfil por competidor**: dónde gana Entel/Sonda/etc., con qué márgenes vs. nuestras ofertas (ya hay base en el dashboard ejecutivo).
3. **Factores de pérdida recurrentes de TIVIT** cuantificados por criterio (ya parcialmente disponible).

**Un modelo predictivo (probabilidad de ganar una licitación antes de ofertar) es prematuro**: la muestra interna con puntajes es demasiado chica para entrenar algo confiable, y los datos públicos no traen puntajes. Sería especulación con apariencia de ciencia.

## 3. Limitaciones y riesgos

- **Sesgo de muestra**: solo tenemos actas de licitaciones donde participamos; el perfil de organismos se construye con datos públicos que no explican el porqué.
- **Datos faltantes**: actas escaneadas de mala calidad, licitaciones sin acta publicada, criterios descritos de forma no comparable entre organismos.
- **Causalidad vs. correlación**: que un organismo repita proveedor puede reflejar buen servicio, lock-in técnico o simple inercia — el dato no distingue.
- **Cambio normativo**: la nueva ley de compras públicas (modificaciones a la 19.886) puede alterar patrones históricos de adjudicación.

## 4. Recomendación

**Siguiente paso sugerido (no implementar aún)**: una fase acotada de "Perfil de Organismo" que:
1. Ingiera adjudicaciones históricas del organismo vía API pública/datos abiertos (sin scraping).
2. Calcule 4-5 indicadores por organismo: tasa de incumbencia, peso promedio del precio, proveedores dominantes, monto promedio, frecuencia de licitaciones del rubro TI.
3. Muestre ese perfil en la ficha de la licitación para decidir go/no-go antes de ofertar.

Esto entrega el 80% del valor ("¿vale la pena competir aquí y con qué estrategia?") con datos verificables, y deja el modelo predictivo para cuando exista una muestra interna de 100+ análisis con puntajes.

---
*Documento de investigación — sin cambios de código asociados.*
