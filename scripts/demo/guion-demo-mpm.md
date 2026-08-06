# Guion narrado — Demo [PRJ-010] Plataforma de Gestión de Mercado Público

Sincronizado con los pasos numerados `[n]` de `demo-mpm.js`. Cada bloque es lo que
la voz automatizada debería decir mientras el script ejecuta ese paso. Los tiempos
son orientativos (ajustar según la velocidad real de la voz).

---

**[Intro, antes de [1]]**
> "Esta es la Plataforma de Gestión de Mercado Público de TIVIT: sincroniza
> automáticamente las licitaciones publicadas, las analiza con inteligencia
> artificial, y avisa proactivamente sobre nuevas oportunidades y movimientos
> de la competencia. Vamos a recorrerla en vivo."

**[1] Login**
> "Empezamos iniciando sesión con una cuenta de administrador."

**[2] Licitaciones — listado**
> "Esta es la pantalla principal: el listado de licitaciones. Hoy el sistema
> tiene sincronizadas más de ciento ochenta mil licitaciones desde la API
> oficial de Mercado Público, con su estado, tipo, fecha de publicación y
> cierre."

**[3] Detalle de una licitación**
> "Al hacer clic en cualquier licitación se abre su ficha completa: código,
> organismo, montos, fechas y el link directo al portal oficial."

**[4] Seguir una licitación**
> "Con un clic en la estrella podemos empezar a seguir una licitación puntual
> — el sistema detecta automáticamente si aparecen aclaraciones nuevas."

**[5] Búsqueda inteligente**
> "Además del filtro tradicional, hay una búsqueda semántica en lenguaje
> natural. Por ejemplo, buscamos 'adquisición de software y equipamiento
> tecnológico' y el sistema, usando IA, entiende la intención y trae
> licitaciones relevantes aunque el texto no coincida exactamente."

**[6] Dashboard Ejecutivo**
> "Este es el dashboard ejecutivo: compara el desempeño histórico de TIVIT
> contra la competencia — licitaciones analizadas, tasa de éxito, montos
> ganados y perdidos, y los factores de pérdida más frecuentes."

**[6b] Ranking de competidores — detalle por empresa**
> "Más abajo está el ranking de competidores. Al expandir cualquiera de
> ellos vemos el historial completo: en qué licitaciones compitió contra
> TIVIT, cuáles ganó y cuáles perdió, y por qué monto se adjudicó cada una."

**[6c] Todas las licitaciones analizadas**
> "Y en esta otra pestaña está el listado completo de licitaciones
> analizadas, con su resultado y quién se la adjudicó."

**[7] Análisis — listado de workspaces**
> "Acá está el módulo de análisis: cada licitación adjudicada se convierte en
> un 'workspace' donde se suben los documentos de evaluación en PDF."

**[8] Abrir un workspace**
> "Dentro de cada workspace vemos los documentos cargados — actas de
> adjudicación, informes técnicos — listos para ser analizados."

**[9] Dashboard de resultados IA**
> "Y este es el resultado del análisis con Gemini: puntaje de TIVIT versus el
> ganador, brecha de puntos, diferencia de monto ofertado, y el ranking final
> entre todos los oferentes."

**[10] Chat contextual con IA**
> "También se puede conversar directamente con los resultados. Le preguntamos
> cuál fue el factor más importante de la pérdida, y la IA responde con el
> detalle exacto: en qué criterio se perdió puntaje y por qué."

**[11] Mensajería**
> "La plataforma incluye mensajería interna en tiempo real, para que el
> equipo comente licitaciones específicas sin salir de ella."

**[12] Notificaciones**
> "El centro de notificaciones agrupa todo lo que el sistema detecta solo:
> nuevas licitaciones que calzan con las alertas configuradas, resultados de
> scraping y aclaraciones."

**[13] Alertas inteligentes**
> "Y esto es lo más particular del sistema: las alertas por palabra clave no
> son búsquedas literales. Creamos una alerta con la palabra 'ciberseguridad'
> y la IA la expande automáticamente a sinónimos y conceptos relacionados —
> seguridad informática, protección de datos, defensa digital — así no se
> escapa ninguna licitación relevante aunque use otro término."

**[14] Catálogos**
> "Por último, el módulo de catálogos centraliza los datos de referencia del
> sistema: estados, tipos de licitación y monedas."

**[Cierre]**
> "Eso es la Plataforma de Gestión de Mercado Público: sincronización
> automática, análisis con inteligencia artificial, alertas inteligentes y
> mensajería, todo en un solo lugar para no perder ninguna oportunidad en
> Mercado Público."

---

## Notas de producción

- El código de proyecto **"[PRJ-010]"** aparece solo como texto en las placas
  de apertura y cierre (`intro-card.png` / `outro-card.png`) — la narración
  nunca lo pronuncia, dice directamente "Plataforma de Gestión de Mercado
  Público". Evita que la voz TTS tenga que leer una sigla+número rara.
- Las pausas de `demo-mpm.js` (constante `NARRATION_MS`) están calculadas a
  ~150 palabras/minuto sobre el texto de cada bloque de este guion, para que
  la voz TTS alcance a terminar de hablar antes de que la UI pase al
  siguiente paso. Si editás una frase del guion, recalculá su duración
  (palabras ÷ 2.5 × 1000 ms) y actualizá la entrada correspondiente en
  `NARRATION_MS`.
- Duración total estimada del recorrido con las pausas ya sincronizadas:
  ~3.5–4.5 minutos (varía según la latencia real de red y de las llamadas a
  Gemini en los pasos [5], [10] y [13]).
- Puntos donde el script espera más tiempo por una llamada real a Gemini
  además de la narración: pasos **[5]**, **[10]** y **[13]** — el margen ya
  incluido en `NARRATION_MS` cubre tanto la narración como esa espera.
- El paso **[4]** (seguir licitación) puede fallar silenciosamente si el
  ícono cambió de posición en un futuro rediseño; si eso pasa, el script
  sigue sin romperse pero conviene recortar esa frase del audio en edición.
- Grabar en una resolución de al menos 1440×900 para que el texto de las
  tablas se lea bien.
