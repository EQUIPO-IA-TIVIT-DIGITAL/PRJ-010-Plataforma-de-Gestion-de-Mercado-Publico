# Contrato: Formato del correo de alerta

No es una API expuesta a terceros — este documento fija el contrato de contenido del correo HTML que `EmailNotificationService.EnviarAsync` envía al usuario, para que la implementación y las pruebas manuales tengan una referencia única de qué debe (y no debe) aparecer.

## Campos y su condición de aparición

| Campo | Aparece cuando | Texto/formato |
|---|---|---|
| Keyword/término | Siempre | Título del correo: "Nueva alerta: {keyword}" |
| Nombre de la licitación | Siempre | En negrita |
| Código externo | Siempre | Entre paréntesis junto al nombre |
| Presupuesto | `presupuesto != null` | "Presupuesto: {monto}" (formato ya existente, sin cambios) |
| Organismo | `organismo` no vacío | "Organismo: {organismo}" |
| Fecha de cierre | `fechaCierre.HasValue` | "Cierra: {fecha en formato dd-MM-yyyy}" |
| Descripción | `descripcion` no vacío | Texto tal cual, sin etiqueta, en un párrafo propio |
| Enlace directo | `link` no vacío | Texto ancla "Ver ficha en Mercado Público" apuntando a `link` |

## Reglas

1. Ningún campo opcional ausente genera texto vacío, `null` literal, o un enlace roto — se omite la línea/bloque completo (FR-006).
2. Todo texto proveniente de datos de la licitación (nombre, organismo) se sanitiza con `WebUtility.HtmlEncode` antes de insertarse en el HTML, igual que ya hace el código actual con `nombreLicitacion`/`keyword`.
3. El enlace, si está presente, se renderiza como `<a href="...">` — se valida que `link` sea un valor no vacío antes de emitirlo; no se agrega validación de formato de URL adicional (se confía en el dato ya almacenado desde el sync).
4. El asunto del correo (`subject`) no cambia — sigue siendo "Nueva alerta: {keyword}".

## Ejemplo — todos los campos presentes

```html
<h2>🔔 Nueva alerta: TI</h2>
<p><strong>Servicio de soporte TI para oficinas regionales</strong> (1234-56-LE26)</p>
<p>Organismo: Servicio de Impuestos Internos</p>
<p>Cierra: 15-08-2026</p>
<p>Presupuesto: 45.000.000</p>
<p><a href="https://www.mercadopublico.cl/...">Ver ficha en Mercado Público</a></p>
```

## Ejemplo — solo campos obligatorios (resto sin dato disponible)

```html
<h2>🔔 Nueva alerta: TI</h2>
<p><strong>Servicio de soporte TI para oficinas regionales</strong> (1234-56-LE26)</p>
```
