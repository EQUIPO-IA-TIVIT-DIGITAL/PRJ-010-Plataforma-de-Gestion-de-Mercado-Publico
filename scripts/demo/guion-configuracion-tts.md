# Guion Narrado para LLM de Voz (TTS) — Configuración de Usuario MPM

Guion del segmento "Mi Perfil y Configuración" de la demo de MPM, sincronizado con
`record-configuracion-usuario.js`. Genera un audio por bloque (recomendado) o el
guion continuo con `<break>` al final.

**Flujo del video:** login → Catálogos > Parámetros Mercado Público → Mi Perfil →
Configuración Alertas (Canal de Correo) → Conclusión.

---

## 🎙️ Configuración de voz

- **Tono**: Profesional, ejecutivo, corporativo.
- **Velocidad**: ~140-150 palabras por minuto.
- **Modelos recomendados**: `eleven_multilingual_v2` / `eleven_turbo_v2_5` (ElevenLabs), `tts-1-hd` (OpenAI).

---

## 📋 Opción 1: Bloques individuales

### `01-intro.mp3`
```text
Además de todas las capacidades de análisis y gestión que ya recorrimos, la plataforma permite a cada usuario configurar su perfil y sus canales de notificación. Veamos cómo se hace.
```

### `02-login.mp3`
```text
Ingresamos con una cuenta de administrador para acceder a la configuración personal.
```

### `03-catalogos-portal.mp3`
```text
El módulo de catálogos centraliza también los parámetros oficiales de Mercado Público: estados de licitación, tipos de proceso y monedas reconocidas. Todo el catálogo de referencia del sistema queda unificado en un solo lugar.
```

### `04-perfil.mp3`
```text
Desde el menú de usuario, en la esquina superior derecha, abrimos nuestro perfil. Aquí vemos los datos de la cuenta y podemos modificar nuestro nombre.
```

### `05-configuracion-alertas.mp3`
```text
En la pestaña de configuración de alertas podemos vincular un canal de correo adicional, para recibir directamente las notificaciones del sistema sobre licitaciones y actividades relevantes. Así, ningún aviso importante se pierde.
```

### `06-conclusion.mp3`
```text
Con esto cerramos el recorrido por la Plataforma de Gestión de Mercado Público de TIVIT. Una solución integral que sincroniza licitaciones, entiende búsquedas complejas con inteligencia artificial, analiza resultados, detecta movimientos de la competencia y mantiene informado a todo el equipo mediante alertas configurables y mensajería en tiempo real. Todo en un solo lugar, para que TIVIT nunca pierda una oportunidad en las compras públicas.
```

---

## 📜 Opción 2: Guion continuo con pausas `<break>`

```text
Además de todas las capacidades de análisis y gestión que ya recorrimos, la plataforma permite a cada usuario configurar su perfil y sus canales de notificación. Veamos cómo se hace.
<break time="4.0s" />
Ingresamos con una cuenta de administrador para acceder a la configuración personal.
<break time="4.0s" />
El módulo de catálogos centraliza también los parámetros oficiales de Mercado Público: estados de licitación, tipos de proceso y monedas reconocidas. Todo el catálogo de referencia del sistema queda unificado en un solo lugar.
<break time="8.0s" />
Desde el menú de usuario, en la esquina superior derecha, abrimos nuestro perfil. Aquí vemos los datos de la cuenta y podemos modificar nuestro nombre.
<break time="6.0s" />
En la pestaña de configuración de alertas podemos vincular un canal de correo adicional, para recibir directamente las notificaciones del sistema sobre licitaciones y actividades relevantes. Así, ningún aviso importante se pierde.
<break time="8.0s" />
Con esto cerramos el recorrido por la Plataforma de Gestión de Mercado Público de TIVIT. Una solución integral que sincroniza licitaciones, entiende búsquedas complejas con inteligencia artificial, analiza resultados, detecta movimientos de la competencia y mantiene informado a todo el equipo mediante alertas configurables y mensajería en tiempo real. Todo en un solo lugar, para que TIVIT nunca pierda una oportunidad en las compras públicas.
```
