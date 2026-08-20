# Guion Narrado para LLM de Voz (TTS) — Demo MPM

Guion narrado profesional y calibrado para generar locuciones con **ElevenLabs**, **OpenAI TTS**, **Azure Speech**, **Google Cloud TTS**, etc.

> **Alcance del demo:** el Módulo de Análisis con IA se muestra sobre un workspace
> **ya cargado** (no se sube ni descarga una licitación nueva). Se excluyen la
> **Sala de Oferta** y el **Centro de Administración**.

---

## 🎙️ Recomendaciones de Generación de Audio

- **Tono**: Profesional, ejecutivo, dinámico y corporativo.
- **Velocidad**: ~140–150 palabras por minuto.
- **Modelo ElevenLabs recomendado**: `eleven_multilingual_v2` o `eleven_turbo_v2_5`.
- **Modelo OpenAI TTS recomendado**: `tts-1-hd` con voz `alloy` u `onyx`.

---

## 📋 Opción 1: Bloques Individuales (Copiar y Pegar por Sección)

Genera cada archivo de audio por separado con el nombre sugerido para montarlos fácilmente en tu editor de video:

### `01-intro.mp3`
```text
Esta es la Plataforma de Gestión e Inteligencia de Mercado Público de TIVIT. Una solución tecnológica integral que sincroniza miles de licitaciones oficiales, clasifica las oportunidades según nuestras áreas de negocio, entiende búsquedas complejas con inteligencia artificial y avisa proactivamente sobre movimientos de la competencia. Vamos a recorrer sus capacidades en vivo.
```

---

### `02-login.mp3`
```text
Comenzamos iniciando sesión con una cuenta de administrador. La plataforma cuenta con autenticación segura por tokens y un esquema de control de acceso por roles organizacionales.
```

---

### `03-licitaciones-listado.mp3`
```text
Esta es la consola de licitaciones. El sistema sincroniza diariamente la API oficial de Mercado Público con más de ciento ochenta mil procesos. Cuenta con clasificación por áreas de negocio como Cloud, Ciberseguridad o Digital, filtros por estado con conteo en vivo, y vistas rápidas para licitaciones seguidas y marcadas de interés comercial.
```

---

### `04-busqueda-semantica-ia.mp3`
```text
Disponemos de un buscador semántico en lenguaje natural. Escribimos requerimientos complejos como "adquisición de software y equipamiento tecnológico" y el motor de inteligencia artificial interpreta la intención técnica, encontrando oportunidades relevantes aunque las palabras no coincidan de forma literal.
```

---

### `05-analisis-ia.mp3`
```text
Para las licitaciones ya adjudicadas y analizadas, el módulo de análisis mantiene los resultados listos para consulta. Abrimos un workspace previamente cargado — en este caso, una licitación de créditos de nube pública AWS ganada por TIVIT: vemos la validación documental y el tablero comparativo que desglosa los puntajes frente al competidor, analizando la brecha técnica y el diferencial económico en cada criterio. También podemos conversar directamente con los resultados: le preguntamos cuáles fueron los factores de éxito que permitieron ganar y la inteligencia artificial responde con el detalle exacto.
```

---

### `06-dashboard-ejecutivo.mp3`
```text
El Dashboard Ejecutivo entrega métricas estratégicas para la gerencia: tasa de adjudicación, montos ganados y perdidos, factores de pérdida más frecuentes y el ranking detallado de competidores con su historial de enfrentamientos directos.
```

---

### `07-inteligencia-competidores.mp3`
```text
El módulo de competidores permite buscar cualquier empresa del mercado, consultar su historial de ofertas públicas y analizar su actividad total en el portal, identificando patrones de precio y oportunidades donde no participamos.
```

---

### `08-alertas-expansion-semantica.mp3`
```text
El motor de alertas inteligentes va más allá de filtros exactos. Al definir una regla con una palabra como "ciberseguridad", la inteligencia artificial expande automáticamente el término a conceptos relacionados como SOC, firewall o seguridad perimetral, garantizando una cobertura total.
```

---

### `09-mensajeria-notificaciones.mp3`
```text
Para la colaboración del equipo, la plataforma incorpora mensajería en tiempo real vinculada a cada licitación y un centro unificado de notificaciones que avisa sobre nuevas oportunidades y aclaraciones detectadas.
```

---

### `10-catalogos-corporativos.mp3`
```text
El módulo de catálogos centraliza el repositorio corporativo de TIVIT: casos de éxito comercial con montos y clientes, acreditaciones oficiales de empresa con visor de certificados PDF, y la configuración de capítulos de propuestas.
```

---

### `11-perfil-cierre.mp3`
```text
Por último, cada usuario puede personalizar sus preferencias y vincular el canal de correo para recibir alertas. En conclusión: gestión integral del ciclo comercial, inteligencia artificial aplicada y automatización completa para liderar las compras públicas.
```

---

## 📜 Opción 2: Guion Continuo con Pausas `<break>` (Un Solo Archivo)

```text
Esta es la Plataforma de Gestión e Inteligencia de Mercado Público de TIVIT. Una solución tecnológica integral que sincroniza miles de licitaciones oficiales, clasifica las oportunidades según nuestras áreas de negocio, entiende búsquedas complejas con inteligencia artificial y avisa proactivamente sobre movimientos de la competencia. Vamos a recorrer sus capacidades en vivo.
<break time="5.0s" />
Comenzamos iniciando sesión con una cuenta de administrador. La plataforma cuenta con autenticación segura por tokens y un esquema de control de acceso por roles organizacionales.
<break time="5.0s" />
Esta es la consola de licitaciones. El sistema sincroniza diariamente la API oficial de Mercado Público con más de ciento ochenta mil procesos. Cuenta con clasificación por áreas de negocio como Cloud, Ciberseguridad o Digital, filtros por estado con conteo en vivo, y vistas rápidas para licitaciones seguidas y marcadas de interés comercial.
<break time="10.0s" />
Disponemos de un buscador semántico en lenguaje natural. Escribimos requerimientos complejos como "adquisición de software y equipamiento tecnológico" y el motor de inteligencia artificial interpreta la intención técnica, encontrando oportunidades relevantes aunque las palabras no coincidan de forma literal.
<break time="9.0s" />
Para las licitaciones ya adjudicadas y analizadas, el módulo de análisis mantiene los resultados listos para consulta. Abrimos un workspace previamente cargado — en este caso, una licitación de créditos de nube pública AWS ganada por TIVIT: vemos la validación documental y el tablero comparativo que desglosa los puntajes frente al competidor, analizando la brecha técnica y el diferencial económico en cada criterio. También podemos conversar directamente con los resultados: le preguntamos cuáles fueron los factores de éxito que permitieron ganar y la inteligencia artificial responde con el detalle exacto.
<break time="9.0s" />
El Dashboard Ejecutivo entrega métricas estratégicas para la gerencia: tasa de adjudicación, montos ganados y perdidos, factores de pérdida más frecuentes y el ranking detallado de competidores con su historial de enfrentamientos directos.
<break time="9.0s" />
El módulo de competidores permite buscar cualquier empresa del mercado, consultar su historial de ofertas públicas y analizar su actividad total en el portal, identificando patrones de precio y oportunidades donde no participamos.
<break time="9.0s" />
El motor de alertas inteligentes va más allá de filtros exactos. Al definir una regla con una palabra como "ciberseguridad", la inteligencia artificial expande automáticamente el término a conceptos relacionados como SOC, firewall o seguridad perimetral, garantizando una cobertura total.
<break time="9.0s" />
Para la colaboración del equipo, la plataforma incorpora mensajería en tiempo real vinculada a cada licitación y un centro unificado de notificaciones que avisa sobre nuevas oportunidades y aclaraciones detectadas.
<break time="8.0s" />
El módulo de catálogos centraliza el repositorio corporativo de TIVIT: casos de éxito comercial con montos y clientes, acreditaciones oficiales de empresa con visor de certificados PDF, y la configuración de capítulos de propuestas.
<break time="9.0s" />
Por último, cada usuario puede personalizar sus preferencias y vincular el canal de correo para recibir alertas. En conclusión: gestión integral del ciclo comercial, inteligencia artificial aplicada y automatización completa para liderar las compras públicas.
```
