import jsPDF from 'jspdf'
import autoTable from 'jspdf-autotable'
import type { ValidacionDocumental } from '../components/ComparativaDocumentos'

/**
 * Generación de PDF estructurado del análisis (texto real y seleccionable,
 * tablas con paginación correcta) a partir del objeto de datos del análisis
 * — no del DOM, para ser independiente del layout de pantalla.
 */

interface PdfAnalisis {
  licitacion?: {
    id?: string | null
    nombre?: string | null
    estado?: string | null
    organismo?: { nombre?: string | null } | null
    tipo_licitacion?: string | null
    fechas?: { adjudicacion?: string | null } | null
    monto_estimado?: number | null
    moneda?: string | null
    duracion_contrato?: string | null
  } | null
  adjudicacion?: {
    adjudicatario?: { nombre?: string | null; rut?: string | null; monto_adjudicado?: number | null } | null
    ofertantes?: { nombre?: string | null; rut?: string | null; monto_ofertado?: number | null; puntaje_total?: number | null; resultado?: string | null }[]
  } | null
  evaluacion?: {
    criterios?: { nombre?: string | null; ponderacion?: number | null; puntaje_tivit_total?: number | null; puntaje_ganador_total?: number | null; puntaje_maximo_total?: number | null; brecha?: number | null }[]
  } | null
  analisis_tivit?: {
    monto_ofertado?: number | null
    puntaje_obtenido?: number | null
    puntaje_maximo_posible?: number | null
    resultado?: string | null
    fortalezas?: string[]
    debilidades?: string[]
    brechas_identificadas?: { area?: string | null; descripcion?: string | null; impacto?: string | null }[]
  } | null
  validacion_documental?: ValidacionDocumental
  recomendaciones_estrategicas?: string[]
  riesgos_identificados?: { riesgo?: string | null; nivel?: string | null; mitigacion?: string | null }[]
}

interface PdfMeta {
  documentoNombre?: string
  modeloUsado?: string
  fechaAnalisis?: string
}

const MARGIN = 14
const BRAND = '#E30613'

function formatMoney(value?: number | null, moneda?: string | null): string {
  if (value == null) return 'No especificado'
  return new Intl.NumberFormat('es-CL', { maximumFractionDigits: 0 }).format(value) + ' ' + (moneda ?? 'CLP').toUpperCase()
}

export function generarPdfAnalisis(analisis: PdfAnalisis, meta: PdfMeta = {}): void {
  const doc = new jsPDF('p', 'mm', 'a4')
  const pageWidth = doc.internal.pageSize.getWidth()
  const contentWidth = pageWidth - MARGIN * 2
  let y = 0

  const lic = analisis.licitacion
  const adj = analisis.adjudicacion
  const ev = analisis.evaluacion
  const at = analisis.analisis_tivit
  const vd = analisis.validacion_documental
  const moneda = lic?.moneda

  // ---- Encabezado ----
  doc.setFillColor(15, 23, 42) // #0f172a
  doc.rect(0, 0, pageWidth, 26, 'F')
  doc.setTextColor(255, 255, 255)
  doc.setFontSize(15)
  doc.setFont('helvetica', 'bold')
  doc.text('Análisis de Licitación — TIVIT', MARGIN, 11)
  doc.setFontSize(9)
  doc.setFont('helvetica', 'normal')
  doc.text(lic?.nombre ?? 'Sin nombre', MARGIN, 18, { maxWidth: contentWidth })
  if (lic?.id) doc.text(`Código: ${lic.id}`, MARGIN, 23)
  y = 34

  const seccion = (titulo: string) => {
    if (y > 260) { doc.addPage(); y = 20 }
    doc.setTextColor(BRAND)
    doc.setFontSize(12)
    doc.setFont('helvetica', 'bold')
    doc.text(titulo, MARGIN, y)
    y += 6
    doc.setTextColor(30, 41, 59)
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(10)
  }

  const parrafo = (texto: string) => {
    const lines = doc.splitTextToSize(texto, contentWidth)
    for (const line of lines) {
      if (y > 280) { doc.addPage(); y = 20 }
      doc.text(line, MARGIN, y)
      y += 5
    }
    y += 3
  }

  const lista = (items: string[]) => {
    for (const item of items) {
      const lines = doc.splitTextToSize(`• ${item}`, contentWidth - 2)
      for (const line of lines) {
        if (y > 280) { doc.addPage(); y = 20 }
        doc.text(line, MARGIN + 2, y)
        y += 5
      }
    }
    y += 3
  }

  const tabla = (head: string[], body: (string | number)[][]) => {
    autoTable(doc, {
      startY: y,
      head: [head],
      body,
      margin: { left: MARGIN, right: MARGIN },
      styles: { fontSize: 8.5, cellPadding: 2.5 },
      headStyles: { fillColor: [15, 23, 42], textColor: 255, fontStyle: 'bold' },
      alternateRowStyles: { fillColor: [248, 250, 252] },
    })
    y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 8
  }

  // ---- Información de la licitación ----
  seccion('Información de la licitación')
  tabla(['Campo', 'Valor'], [
    ['Estado', lic?.estado ?? '—'],
    ['Organismo', lic?.organismo?.nombre ?? '—'],
    ['Tipo de licitación', lic?.tipo_licitacion ?? '—'],
    ['Fecha adjudicación', lic?.fechas?.adjudicacion ?? '—'],
    ['Monto estimado', formatMoney(lic?.monto_estimado, moneda)],
    ['Duración contrato', lic?.duracion_contrato ?? '—'],
    ['Adjudicatario', adj?.adjudicatario?.nombre ?? '—'],
    ['RUT adjudicatario', adj?.adjudicatario?.rut ?? '—'],
    ['Monto adjudicado', formatMoney(adj?.adjudicatario?.monto_adjudicado, moneda)],
    ['Monto ofertado TIVIT', formatMoney(at?.monto_ofertado, moneda)],
    ['Puntaje TIVIT', `${at?.puntaje_obtenido ?? '—'} / ${at?.puntaje_maximo_posible ?? '—'}`],
    ['Resultado TIVIT', at?.resultado ?? '—'],
  ])

  // ---- Ofertantes ----
  if (adj?.ofertantes?.length) {
    seccion('Ofertantes')
    tabla(
      ['Nombre', 'RUT', 'Monto ofertado', 'Puntaje', 'Resultado'],
      adj.ofertantes.map((o) => [
        o.nombre ?? '—',
        o.rut ?? '—',
        formatMoney(o.monto_ofertado, moneda),
        o.puntaje_total ?? '—',
        o.resultado ?? '—',
      ]),
    )
  }

  // ---- Comparativa de puntajes por criterio ----
  if (ev?.criterios?.length) {
    seccion('Comparativa de puntajes por criterio')
    tabla(
      ['Criterio', 'Ponderación', 'TIVIT', 'Ganador', 'Máximo', 'Brecha'],
      ev.criterios.map((c) => [
        c.nombre ?? '—',
        c.ponderacion != null ? `${c.ponderacion}%` : '—',
        c.puntaje_tivit_total ?? '—',
        c.puntaje_ganador_total ?? '—',
        c.puntaje_maximo_total ?? '—',
        (c.brecha ?? ((c.puntaje_tivit_total ?? 0) - (c.puntaje_ganador_total ?? 0))).toFixed(2),
      ]),
    )
  }

  // ---- Comparativa de documentos (validación documental) ----
  if (vd) {
    seccion('Comparativa de documentos')
    if (vd.resumen) parrafo(vd.resumen)
    if (vd.documentos?.length) {
      tabla(
        ['Documento', 'Requerido', 'Enviado', 'Según el acta', 'Estado'],
        vd.documentos.map((d) => [
          d.nombre ?? '—',
          d.requerido == null ? '—' : d.requerido ? 'Sí' : 'No',
          d.enviado == null ? '—' : d.enviado ? 'Sí' : 'No',
          d.observado_en_acta ?? '—',
          d.estado ?? '—',
        ]),
      )
    }
    if (vd.inconsistencias?.length) {
      tabla(
        ['Documento', 'Dice el acta', 'Evidencia', 'Severidad'],
        vd.inconsistencias.map((i) => [
          i.documento ?? '—',
          i.dice_acta ?? '—',
          i.evidencia ?? '—',
          i.severidad ?? '—',
        ]),
      )
    }
  }

  // ---- Brechas identificadas ----
  if (at?.brechas_identificadas?.length) {
    seccion('Brechas identificadas')
    tabla(
      ['#', 'Área', 'Descripción', 'Impacto'],
      at.brechas_identificadas.map((f, i) => [i + 1, f.area ?? '—', f.descripcion ?? '—', f.impacto ?? '—']),
    )
  }

  // ---- Fortalezas / Debilidades ----
  if (at?.fortalezas?.length) {
    seccion('Fortalezas TIVIT')
    lista(at.fortalezas)
  }
  if (at?.debilidades?.length) {
    seccion('Debilidades TIVIT')
    lista(at.debilidades)
  }

  // ---- Riesgos identificados ----
  if (analisis.riesgos_identificados?.length) {
    seccion('Riesgos identificados')
    tabla(
      ['Riesgo', 'Nivel', 'Mitigación'],
      analisis.riesgos_identificados.map((r) => [r.riesgo ?? '—', r.nivel ?? '—', r.mitigacion ?? '—']),
    )
  }

  // ---- Recomendaciones estratégicas ----
  if (analisis.recomendaciones_estrategicas?.length) {
    seccion('Recomendaciones estratégicas')
    lista(analisis.recomendaciones_estrategicas)
  }

  // ---- Pie: metadata + numeración ----
  const totalPages = doc.getNumberOfPages()
  for (let p = 1; p <= totalPages; p++) {
    doc.setPage(p)
    doc.setFontSize(8)
    doc.setTextColor(148, 163, 184)
    const pie = [
      meta.documentoNombre ? `Documento: ${meta.documentoNombre}` : null,
      meta.modeloUsado ? `Modelo: ${meta.modeloUsado}` : null,
      meta.fechaAnalisis ? `Análisis: ${meta.fechaAnalisis}` : null,
    ].filter(Boolean).join('  ·  ')
    doc.text(pie, MARGIN, 290)
    doc.text(`Página ${p} de ${totalPages}`, pageWidth - MARGIN, 290, { align: 'right' })
  }

  doc.save(`analisis-${lic?.id ?? 'licitacion'}.pdf`)
}
