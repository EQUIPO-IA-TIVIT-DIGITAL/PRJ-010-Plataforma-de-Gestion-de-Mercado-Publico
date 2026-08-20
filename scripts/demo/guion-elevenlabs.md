# Guion para ElevenLabs — Demo [PRJ-010] Plataforma de Gestión de Mercado Público

Mismo contenido que `guion-demo-mpm.md`, pero reformateado para generar el
audio directamente en ElevenLabs. Cada bloque `[n]` corresponde al paso con
el mismo número en `demo-mpm.js` — generar un archivo de audio por bloque
facilita alinear cada clip con su parte del video en la edición final.

> Nota: el código de proyecto **"[PRJ-010]"** solo aparece como texto en las
> placas de apertura/cierre del video — ningún bloque de acá lo pronuncia,
> para no forzar a la voz TTS a leer una sigla+número. Se dice directamente
> "Plataforma de Gestión de Mercado Público".

> **Alcance del demo:** el Módulo de Análisis con IA se muestra sobre un workspace
> **ya cargado** (no se sube ni descarga una licitación nueva). La **Sala de Oferta**
> queda fuera porque su primera etapa depende de la descarga bajo demanda de
> documentos desde Mercado Público, actualmente bloqueada desde IPs de datacenter.

## Cómo usarlo

**Opción A — TTS estándar (Speech Synthesis / API), la más simple:**
Pegar cada bloque de abajo **uno a la vez** en un audio separado. No hace
falta ninguna etiqueta especial: los saltos de línea y los puntos ya generan
pausas naturales. Exportar cada uno como `01-intro.mp3`, `02-login.mp3`, etc.
(numerados en orden) para después montarlos en la línea de tiempo del video
en el mismo orden que los pasos del script.

**Opción B — ElevenLabs Studio/Projects (si tenés acceso), un solo archivo:**
Studio sí soporta la etiqueta `<break time="Xs" />` para controlar pausas
dentro de un mismo audio continuo. Al final de este documento hay una
versión de todo el guion en un solo bloque con `<break>` insertados en los
puntos donde el script cambia de pantalla — útil si preferís un único
archivo de audio en vez de 15 clips sueltos.

**Configuración de voz recomendada** (Voice Settings):
- **Stability**: 40–50% (más bajo = más expresivo/natural; más alto = más
  plano y estable). Para este tono explicativo, 45% suele sonar bien.
- **Similarity Boost**: 75–85%.
- **Style** (si el modelo lo permite, v2/v3): 20–30%, para que no suene
  robótico pero tampoco sobreactuado.
- Modelo: `eleven_multilingual_v2` (o `v3` si ya tenés acceso) — mejor
  pronunciación de español chileno que el modelo solo-inglés.
- Voz: probar con una voz en español neutro/latino; si necesitás acento
  chileno específico, ElevenLabs no tiene muchas nativas — considerar
  Voice Cloning con una muestra de audio propia, o aceptar el acento
  neutro para esta demo.

---

## Bloques (Opción A — un archivo por bloque)

**01-intro**
```
Esta es la Plataforma de Gestión de Mercado Público de TIVIT: sincroniza
automáticamente las licitaciones publicadas, clasifica las oportunidades
según nuestras áreas de negocio, entiende búsquedas complejas con
inteligencia artificial, y avisa proactivamente sobre nuevas oportunidades
y movimientos de la competencia. Vamos a recorrerla en vivo.
```

**02-login**
```
Empezamos iniciando sesión con una cuenta de administrador.
```

**03-licitaciones-listado**
```
Esta es la pantalla principal: el listado de licitaciones. Hoy el sistema
tiene sincronizadas más de ciento ochenta mil licitaciones desde la API
oficial de Mercado Público, con su estado, tipo, fecha de publicación y
cierre.
```

**04-detalle-licitacion**
```
Al hacer clic en cualquier licitación se abre su ficha completa: código,
organismo, montos, fechas y el link directo al portal oficial.
```

**05-seguir-licitacion**
```
Con un clic en la estrella podemos empezar a seguir una licitación
puntual, el sistema detecta automáticamente si aparecen aclaraciones
nuevas.
```

**06-busqueda-inteligente**
```
Además del filtro tradicional, hay una búsqueda semántica en lenguaje
natural. Por ejemplo, buscamos "adquisición de software y equipamiento
tecnológico", y el sistema, usando inteligencia artificial, entiende la
intención y trae licitaciones relevantes aunque el texto no coincida
exactamente.
```

**07-dashboard-ejecutivo**
```
Este es el dashboard ejecutivo: compara el desempeño histórico de TIVIT
contra la competencia. Licitaciones analizadas, tasa de éxito, montos
ganados y perdidos, y los factores de pérdida más frecuentes.
```

**08-ranking-competidores**
```
Más abajo está el ranking de competidores. Al expandir cualquiera de
ellos vemos el historial completo: en qué licitaciones compitió contra
TIVIT, cuáles ganó y cuáles perdió, y por qué monto se adjudicó cada una.
```

**09-todas-licitaciones-analizadas**
```
Y en esta otra pestaña está el listado completo de licitaciones
analizadas, con su resultado y quién se la adjudicó.
```

**10-analisis-workspace**
```
Acá está el módulo de análisis. Abrimos un workspace previamente cargado
con la validación documental de los documentos de evaluación, listos para
ser consultados.
```

**11-dashboard-resultados-ia**
```
Y este es el resultado del análisis con Gemini: puntaje de TIVIT versus
el ganador, brecha de puntos, diferencia de monto ofertado, y el ranking
final entre todos los oferentes.
```

**12-chat-ia**
```
También se puede conversar directamente con los resultados. Le
preguntamos cuál fue el factor más importante de la pérdida, y la
inteligencia artificial responde con el detalle exacto: en qué criterio
se perdió puntaje y por qué.
```

**13-mensajeria**
```
La plataforma incluye mensajería interna en tiempo real, para que el
equipo comente licitaciones específicas sin salir de ella.
```

**14-notificaciones**
```
El centro de notificaciones agrupa todo lo que el sistema detecta solo:
nuevas licitaciones que calzan con las alertas configuradas, resultados
de scraping y aclaraciones.
```

**15-alertas**
```
Y esto es lo más particular del sistema: las alertas por palabra clave
no son búsquedas literales. Creamos una alerta con la palabra
"ciberseguridad", y la inteligencia artificial la expande automáticamente
a sinónimos y conceptos relacionados: seguridad informática, protección
de datos, defensa digital. Así no se escapa ninguna licitación relevante
aunque use otro término.
```

**16-catalogos**
```
Por último, el módulo de catálogos centraliza los datos de referencia
del sistema: estados, tipos de licitación y monedas.
```

**17-cierre**
```
Eso es la Plataforma de Gestión de Mercado Público: sincronización
automática, búsqueda inteligente con inteligencia artificial, alertas
inteligentes y mensajería, todo en un solo lugar para no perder ninguna
oportunidad en Mercado Público.
```

---

## Versión continua con `<break>` (Opción B — solo Studio/Projects)

```
Esta es la Plataforma de Gestión de Mercado Público de TIVIT: sincroniza
automáticamente las licitaciones publicadas, clasifica las oportunidades
según nuestras áreas de negocio, entiende búsquedas complejas con
inteligencia artificial, y avisa proactivamente sobre nuevas oportunidades
y movimientos de la competencia. Vamos a recorrerla en vivo.
<break time="4.0s" />
Empezamos iniciando sesión con una cuenta de administrador.
<break time="4.0s" />
Esta es la pantalla principal: el listado de licitaciones. Hoy el sistema
tiene sincronizadas más de ciento ochenta mil licitaciones desde la API
oficial de Mercado Público, con su estado, tipo, fecha de publicación y
cierre.
<break time="9.0s" />
Al hacer clic en cualquier licitación se abre su ficha completa: código,
organismo, montos, fechas y el link directo al portal oficial.
<break time="9.0s" />
Con un clic en la estrella podemos empezar a seguir una licitación
puntual, el sistema detecta automáticamente si aparecen aclaraciones
nuevas.
<break time="6.0s" />
Además del filtro tradicional, hay una búsqueda semántica en lenguaje
natural. Por ejemplo, buscamos "adquisición de software y equipamiento
tecnológico", y el sistema, usando inteligencia artificial, entiende la
intención y trae licitaciones relevantes aunque el texto no coincida
exactamente.
<break time="10.0s" />
Este es el dashboard ejecutivo: compara el desempeño histórico de TIVIT
contra la competencia. Licitaciones analizadas, tasa de éxito, montos
ganados y perdidos, y los factores de pérdida más frecuentes.
<break time="9.0s" />
Más abajo está el ranking de competidores. Al expandir cualquiera de
ellos vemos el historial completo: en qué licitaciones compitió contra
TIVIT, cuáles ganó y cuáles perdió, y por qué monto se adjudicó cada una.
<break time="8.0s" />
Y en esta otra pestaña está el listado completo de licitaciones
analizadas, con su resultado y quién se la adjudicó.
<break time="6.0s" />
Acá está el módulo de análisis. Abrimos un workspace previamente cargado
con la validación documental de los documentos de evaluación, listos para
ser consultados.
<break time="5.0s" />
Y este es el resultado del análisis con Gemini: puntaje de TIVIT versus
el ganador, brecha de puntos, diferencia de monto ofertado, y el ranking
final entre todos los oferentes.
<break time="6.0s" />
También se puede conversar directamente con los resultados. Le
preguntamos cuál fue el factor más importante de la pérdida, y la
inteligencia artificial responde con el detalle exacto: en qué criterio
se perdió puntaje y por qué.
<break time="10.0s" />
La plataforma incluye mensajería interna en tiempo real, para que el
equipo comente licitaciones específicas sin salir de ella.
<break time="6.0s" />
El centro de notificaciones agrupa todo lo que el sistema detecta solo:
nuevas licitaciones que calzan con las alertas configuradas, resultados
de scraping y aclaraciones.
<break time="8.0s" />
Y esto es lo más particular del sistema: las alertas por palabra clave
no son búsquedas literales. Creamos una alerta con la palabra
"ciberseguridad", y la inteligencia artificial la expande automáticamente
a sinónimos y conceptos relacionados: seguridad informática, protección
de datos, defensa digital. Así no se escapa ninguna licitación relevante
aunque use otro término.
<break time="10.0s" />
Por último, el módulo de catálogos centraliza los datos de referencia
del sistema: estados, tipos de licitación y monedas.
<break time="5.0s" />
Eso es la Plataforma de Gestión de Mercado Público: sincronización
automática, búsqueda inteligente con inteligencia artificial, alertas
inteligentes y mensajería, todo en un solo lugar para no perder ninguna
oportunidad en Mercado Público.
```

> Nota: los tiempos de `<break>` de esta versión son aproximados y más
> cortos que las pausas del video (`NARRATION_MS` en `demo-mpm.js`) porque
> acá solo cubren el silencio *entre* frases, no la duración de la propia
> narración. Si armás un solo archivo de audio con este bloque, igual vas a
> necesitar recortarlo/estirarlo en el editor de video para que cada frase
> caiga sobre su pantalla correspondiente — la Opción A (un clip por bloque)
> es más fácil de sincronizar a mano.
