# Contrato: Endpoint de análisis de competidor (fix FR-003)

Endpoint existente en `CompetidoresController` (no se cambia su firma pública ni su ruta/verbo — solo el manejo de un caso de error que hoy no está cubierto).

## Antes del fix

Cuando Gemini retorna una respuesta sin `candidates` (contenido bloqueado por el filtro de seguridad), la excepción no controlada llega hasta el middleware global (`ErrorHandlingMiddleware`) y el cliente recibe:

```http
HTTP/1.1 500 Internal Server Error
Content-Type: application/json

{ "error": "Ocurrió un error inesperado." }
```

Sin distinción de causa, sin poder mostrar al usuario un mensaje accionable.

## Después del fix

El mismo caso (Gemini bloqueó el contenido) se captura explícitamente y se responde con un código y mensaje distinguibles:

```http
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/json

{
  "error": "gemini_contenido_bloqueado",
  "message": "No fue posible generar el análisis para este competidor: el contenido fue bloqueado por el filtro de seguridad de Gemini. Puedes reintentar o contactar soporte si persiste."
}
```

Cualquier otro error no relacionado con `candidates` vacío (fallo de red, timeout, error de autenticación ADC, etc.) sigue propagándose como 500 — este fix no cambia el manejo de esos casos, solo cubre el caso específico no controlado hoy.

## Contrato de éxito (sin cambios)

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "competidorId": "...",
  "periodo": { "desde": "...", "hasta": "..." },
  "analisis": { "patrones": "...", "organismosFrecuentes": [...], "montoPromedioOfertado": 0, "tasaExito": 0, "recomendaciones": "..." }
}
```

## Consumidores a actualizar

- `src/mpm-web/src/hooks/useCompetidores.ts`: debe distinguir el código `gemini_contenido_bloqueado` (422) de un error genérico para mostrar el mensaje correcto en `CompetidoresPage.tsx`, en vez de un toast de error genérico.
