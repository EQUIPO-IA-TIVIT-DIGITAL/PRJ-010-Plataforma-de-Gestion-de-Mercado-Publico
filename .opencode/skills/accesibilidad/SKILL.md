---
name: accesibilidad
description: 'Web accessibility (a11y): WCAG 2.2 AA, ARIA, keyboard navigation, screen
  readers, color contrast, focus management, axe-core automated testing. Trigger: When
  implementing, auditing, or fixing accessible UI components and patterns.'
metadata:
  phase:
  - quality
  layer:
  - frontend
  enforcement: recommended
  depends_on:
  - react
  - design-system
  consumed_by:
  - agent-frontend
  - agent-fullstack
  - agent-qa
  agent_roles:
  - control-agent
  - delivery-agent
  validation_profile: documentation
  mcp_usage: optional
---

## Propósito

Garantizar que todas las interfaces cumplan WCAG 2.2 AA, sean operables por teclado, compatibles con lectores de pantalla y verificables con herramientas automatizadas.

## Objetivo

1. ¿Qué componentes deben ser accesibles desde el diseño?
2. ¿Cómo se implementan atributos ARIA correctamente?
3. ¿Cómo se maneja el foco en modales, menús y navegación?
4. ¿Cómo se audita accesibilidad con axe-core en Playwright?
5. ¿Qué contraste de color mínimo se requiere?

## Relación con otras skills

- `react` / `design-system` reciben los patrones de componentes accesibles.
- `playwright` ejecuta las auditorías automatizadas con `@axe-core/playwright`.
- `agent-qa` consume esta skill para validar accesibilidad en el pipeline.
- `performance` complementa con patrones de carga accesibles (ARIA live regions).

## Qué debe hacer el agente

1. Implementar componentes con Roles ARIA nativos primero, explícitos solo cuando no existan.
2. Asegurar navegación por teclado completa (Tab, Enter, Escape, Arrow keys).
3. Manejar foco: `focus()` en apertura, `focus-trap` en modales, retorno al cerrar.
4. Agregar `aria-label`, `aria-describedby`, `aria-expanded` en elementos interactivos sin texto visible.
5. Garantizar contraste 4.5:1 (texto normal) o 3:1 (texto grande, 18px+ bold o 24px+ regular).
6. Escribir tests de accesibilidad con `@axe-core/playwright`.
7. Probar con lector de pantalla (VoiceOver, NVDA, JAWS) en flujos críticos.

## Alcance

Incluye: HTML semántico, ARIA, teclado, foco, contraste, screen readers, formularios, imágenes, tablas de datos, notificaciones, animaciones (prefers-reduced-motion).  
No incluye: accesibilidad nativa mobile (iOS/Android), testing cognitivo avanzado, certificación formal.

## Principios

- La accesibilidad no es una capa separada: es parte del diseño del componente.
- Priorizar semántica HTML nativa sobre ARIA explícito. `ARIA no añade funcionalidad, solo describe`.
- El teclado debe poder alcanzar y operar todo.
- Las auditorías automatizadas detectan ~30% de issues; el resto requiere revisión manual.
- No sacrificar accesibilidad por estética: el contraste y el foco visible no son opcionales.

## Technical Design

### ARIA patterns for React

```tsx
// Button with loading state — ARIA live region
<button
  aria-busy={isLoading}
  aria-disabled={isLoading}
  onClick={handleSubmit}
>
  {isLoading ? 'Saving...' : 'Save'}
  <span aria-live="polite" className="sr-only">
    {isLoading ? 'Submitting form' : ''}
  </span>
</button>

// Modal with focus trap
<dialog
  ref={dialogRef}
  role="dialog"
  aria-modal="true"
  aria-labelledby="modal-title"
  aria-describedby="modal-desc"
>
  <h2 id="modal-title">Confirm</h2>
  <p id="modal-desc">Are you sure?</p>
  <button ref={closeRef} onClick={onClose}>Cancel</button>
  <button onClick={onConfirm}>Confirm</button>
</dialog>
```

### Focus management hook

```typescript
// useTrapFocus — returns refs for first/last focusable elements
function useTrapFocus() {
  const containerRef = useRef<HTMLDivElement>(null);
  const previousFocus = useRef<HTMLElement | null>(null);

  useEffect(() => {
    previousFocus.current = document.activeElement as HTMLElement;
    containerRef.current?.focus();
    return () => previousFocus.current?.focus();
  }, []);

  return containerRef;
}
```

### Automated testing with axe-core

```typescript
// playwright spec
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('page has no WCAG violations @a11y', async ({ page }) => {
  await page.goto('/entity/create');
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag22aa'])
    .analyze();
  expect(results.violations).toEqual([]);
});
```

## Preguntas guía

- ¿Este componente tiene un rol semántico nativo o necesita ARIA explícito?
- ¿Se puede alcanzar y operar completamente con teclado?
- ¿El contraste cumple 4.5:1 en todos los estados (hover, focus, disabled, error)?
- ¿Qué anuncia un screen reader al interactuar con este componente?
- ¿El foco tiene un orden lógico y visible?

## Salidas esperadas

- Componentes con atributos ARIA correctos.
- Hook de manejo de foco (`useTrapFocus`, `useSkipLink`).
- Test de accesibilidad por página o componente con axe-core.
- Evidencia de contraste aprobado y navegación por teclado.

## Criterios de calidad

- 0 violaciones WCAG 2.2 AA en auditoría automatizada.
- Navegación completa con Tab/Enter/Escape sin perder el foco.
- Skip link visible al primer Tab.
- Estados de foco visibles (outline 2px, no `outline: none` sin alternativa).

## Comportamiento esperado del agente

Cuando una propuesta use `div` sin ARIA para un botón, el agente debe reemplazarlo por `<button>`.  
Cuando falte manejo de foco en modales, debe implementar focus trap.  
Cuando axe-core reporte violaciones, debe corregirlas antes de dar el componente por terminado.

## Plantilla de respuesta

```
1. WCAG target level (AA).
2. Keyboard navigation map (Tab order, shortcuts).
3. ARIA attributes per component.
4. Focus management strategy.
5. Color contrast compliance.
6. Automated test snippet.
```

## Ejemplos

### Ejemplo 1 — Modal accesible
```tsx
<dialog open aria-modal="true" aria-labelledby="dialog-title">
  <h2 id="dialog-title">Delete entity</h2>
  <p>This action cannot be undone.</p>
  <button onClick={close}>Cancel</button>
  <button onClick={confirm} autoFocus>Delete</button>
</dialog>
```

### Ejemplo 2 — Error summary (after form validation)
```tsx
<div role="alert" aria-live="assertive" tabIndex={-1} ref={errorRef}>
  <h2>There are {errors.length} errors</h2>
  <ul>{errors.map(e => <li key={e.field}>{e.message}</li>)}</ul>
</div>
```

## Checklist

- [ ] Roles ARIA nativos preferidos sobre ARIA explícito.
- [ ] Todos los elementos interactivos accesibles por teclado.
- [ ] Focus management en modales, menús, tabs, toasts.
- [ ] Skip link presente.
- [ ] Contraste 4.5:1 (normal), 3:1 (large text).
- [ ] `prefers-reduced-motion` respetado.
- [ ] Tests axe-core por página con 0 violaciones.
- [ ] Formularios con `aria-describedby` para errores.
- [ ] Imágenes con `alt` descriptivo o `role="presentation"`.
