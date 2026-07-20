# Contrato: Campo de moneda en el dashboard de Análisis (fix FR-013)

No es un endpoint nuevo — es un campo adicional en el JSON ya devuelto por el dashboard de un workspace de Análisis (`GET` del detalle/dashboard, `MPM.Modules.Analisis`), para que el frontend pueda mostrar la moneda real en vez de asumir siempre `US$`.

## Antes del fix

Cada monto en el dashboard se muestra con el símbolo `US$` fijo en el frontend, sin importar la moneda real del documento fuente.

```json
{
  "montoAdjudicado": 209529081,
  "montoEstimado": 526431
}
```

## Después del fix

Cada monto relevante viene acompañado de su moneda real, identificada por el prompt de extracción a partir del texto fuente:

```json
{
  "montoAdjudicado": 209529081,
  "montoAdjudicadoMoneda": "CLP",
  "montoEstimado": 526431,
  "montoEstimadoMoneda": "CLP"
}
```

Si el documento no indica moneda explícita para una cifra, el campo de moneda correspondiente viene como `"NO_DETERMINADA"` (nunca se asume `"USD"` por defecto) — el frontend debe mostrar esa cifra sin símbolo de moneda o con una indicación explícita de "moneda no determinada", nunca con `US$` implícito.

## Consumidores a actualizar

- Los componentes del dashboard de Análisis en `src/mpm-web/src/components/` que hoy renderizan `US$` fijo deben leer el campo de moneda real y formatear en consecuencia (backend ya normaliza el formato de moneda — ver FR-017 — así que el frontend no debería tener que decidir el símbolo por texto libre).
