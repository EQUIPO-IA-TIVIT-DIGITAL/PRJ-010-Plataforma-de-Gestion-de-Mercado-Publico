/**
 * Explicaciones en lenguaje simple de los conceptos de catálogo de Mercado Público,
 * basadas en las definiciones oficiales de ChileCompra (Ley 19.886 y su reglamento).
 * Se muestran al hacer click sobre un estado o tipo en la pantalla de Catálogos.
 */

export interface CatalogoDescripcion {
  titulo: string;
  explicacion: string;
  ejemplo?: string;
}

/** Estados de licitación, por código de Mercado Público. */
export const ESTADOS_DESC: Record<number, CatalogoDescripcion> = {
  1: {
    titulo: 'Publicada',
    explicacion:
      'La licitación está abierta y visible en Mercado Público: los proveedores pueden revisar las bases, hacer preguntas y presentar sus ofertas hasta la fecha de cierre.',
    ejemplo: 'Un organismo publica hoy una licitación de servicios TI; desde ese momento y hasta el cierre, cualquier proveedor inscrito puede ofertar.',
  },
  2: {
    titulo: 'Cerrada',
    explicacion:
      'Terminó el plazo para presentar ofertas. Ya no se aceptan nuevas ofertas y el organismo comienza la etapa de apertura y evaluación de las recibidas.',
  },
  3: {
    titulo: 'Desierta',
    explicacion:
      'La licitación terminó sin adjudicarse: no se presentaron ofertas, o ninguna cumplió los requisitos de las bases, o el organismo las declaró inconvenientes para sus intereses.',
  },
  4: {
    titulo: 'Adjudicada',
    explicacion:
      'El organismo evaluó las ofertas y eligió formalmente a un ganador mediante una resolución de adjudicación. Con el adjudicatario se firma el contrato u orden de compra.',
    ejemplo: 'De 5 ofertas recibidas, el organismo adjudica a la empresa con mejor puntaje técnico-económico según los criterios de las bases.',
  },
  5: {
    titulo: 'Revocada',
    explicacion:
      'El organismo dejó sin efecto la licitación antes de la adjudicación, mediante un acto administrativo fundado. Las ofertas presentadas dejan de tener efecto.',
  },
  6: {
    titulo: 'Suspendida',
    explicacion:
      'El proceso está detenido temporalmente (por ejemplo, por una medida del Tribunal de Contratación Pública o una revisión interna) y puede reanudarse o revocarse después.',
  },
};

/** Tipos de licitación / procedimientos de compra, por slug o nombre. */
export const TIPOS_DESC: Record<string, CatalogoDescripcion> = {
  'licitacion-publica': {
    titulo: 'Licitación Pública',
    explicacion:
      'Procedimiento competitivo y abierto: cualquier proveedor puede ofertar. Es la regla general de las compras del Estado. Según el monto, se clasifica en L1, LE, LP, LQ o LR (de menor a mayor).',
    ejemplo: 'Una municipalidad necesita renovar su plataforma web y publica una licitación LE (entre 100 y 1.000 UTM) abierta a todos los proveedores del rubro.',
  },
  'licitacion-privada': {
    titulo: 'Licitación Privada',
    explicacion:
      'Procedimiento donde el organismo invita directamente a un mínimo de tres proveedores a ofertar, en vez de abrir a todo el mercado. Requiere resolución fundada y solo procede en los casos que la ley permite (por ejemplo, tras una licitación pública desierta).',
  },
  'trato-directo': {
    titulo: 'Trato Directo',
    explicacion:
      'Contratación excepcional sin competencia: el organismo contrata directamente con un proveedor. Solo procede en causales específicas de la ley (emergencia, proveedor único, montos menores, etc.) y debe justificarse con resolución fundada.',
    ejemplo: 'Tras un temporal, un hospital contrata por trato directo la reparación urgente de su techumbre invocando la causal de emergencia.',
  },
  'convenio-marco': {
    titulo: 'Convenio Marco',
    explicacion:
      'Catálogo de productos y servicios ya licitados por ChileCompra: los organismos compran directamente desde la "tienda" (catálogo electrónico) sin hacer una licitación propia.',
  },
  'compra-agil': {
    titulo: 'Compra Ágil',
    explicacion:
      'Procedimiento simplificado para compras de bajo monto (hasta 100 UTM aprox.): el organismo solicita cotizaciones y elige la más conveniente, con menos formalidades y plazos más cortos.',
  },
};

/**
 * Resuelve la descripción de un tipo por slug, con fallback por nombre normalizado.
 */
export function descripcionTipo(slugONombre: string): CatalogoDescripcion | undefined {
  const clave = slugONombre
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/\s+/g, '-');
  return TIPOS_DESC[clave];
}

export function descripcionEstado(codigo: number): CatalogoDescripcion | undefined {
  return ESTADOS_DESC[codigo];
}
