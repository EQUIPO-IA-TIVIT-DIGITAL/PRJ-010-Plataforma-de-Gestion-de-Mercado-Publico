import { useState, useCallback, useRef, useEffect } from 'react'
import { Spin, App } from 'antd'
import { UserOutlined } from '@ant-design/icons'
import { Bubble, Sender } from '@ant-design/x'
import ReactMarkdown from 'react-markdown'
import { useEnviarChat, useChatHistorial } from '../hooks/useAnalisis'
import type { ChatMensaje } from '../types/analisis'

// ── Icono Sparkles (SVG personalizado — reemplaza RobotOutlined) ──────────────

export function SparklesIcon({ size = 14, color = 'currentColor' }: { size?: number; color?: string }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={color}
      xmlns="http://www.w3.org/2000/svg"
    >
      {/* Estrella principal */}
      <path d="M11.48 3.499a.562.562 0 011.04 0l2.125 5.111a.563.563 0 00.475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 00-.182.557l1.285 5.385a.562.562 0 01-.84.61l-4.725-2.885a.563.563 0 00-.586 0L6.982 20.54a.562.562 0 01-.84-.61l1.285-5.386a.562.562 0 00-.182-.557l-4.204-3.602a.563.563 0 01.321-.988l5.518-.442a.563.563 0 00.475-.345L11.48 3.5z" />
      {/* Destellos pequeños */}
      <path d="M5.25 5.25c0 .414-.336.75-.75.75A.75.75 0 013.75 5.25V4.5h-.75a.75.75 0 010-1.5h.75V2.25a.75.75 0 011.5 0V3h.75a.75.75 0 010 1.5H5.25v.75z" />
      <path d="M18.25 17.25c0 .414-.336.75-.75.75a.75.75 0 01-.75-.75V16.5h-.75a.75.75 0 010-1.5h.75V14.25a.75.75 0 011.5 0V15h.75a.75.75 0 010 1.5h-.75v.75z" />
    </svg>
  )
}

/**
 * Normaliza la respuesta del asistente antes de renderizar Markdown:
 * quita fences envolventes (``` / ```json / ```markdown) y colapsa
 * saltos de línea excesivos.
 */
export function normalizarMarkdown(texto: string): string {
  let t = texto.trim()
  if (t.startsWith('```')) {
    const primeraLinea = t.indexOf('\n')
    const ultimoFence = t.lastIndexOf('```')
    if (primeraLinea >= 0 && ultimoFence > primeraLinea) {
      t = t.slice(primeraLinea + 1, ultimoFence).trim()
    }
  }
  return t.replace(/\n{3,}/g, '\n\n')
}

// ── Estilos de burbuja por rol ────────────────────────────────────────────────

const BUBBLE_USER = {
  placement: 'end' as const,
  avatar: {
    icon: <UserOutlined style={{ color: 'white', fontSize: 13 }} />,
    style: {
      background: 'linear-gradient(135deg, #E30613, #ff3a46)',
      boxShadow: '0 2px 8px rgba(227,6,19,0.35)',
      width: 30,
      height: 30,
    },
  },
  styles: {
    content: {
      background: 'linear-gradient(135deg, #E30613, #ff3a46)',
      color: 'white',
      borderRadius: '16px 16px 4px 16px',
      fontSize: 13,
      lineHeight: 1.55,
      boxShadow: '0 2px 10px rgba(227,6,19,0.25)',
      padding: '10px 14px',
    },
  },
}

const BUBBLE_AI = {
  placement: 'start' as const,
  avatar: {
    icon: <SparklesIcon size={14} color="white" />,
    style: {
      background: 'linear-gradient(135deg, #8b5cf6, #a78bfa)',
      boxShadow: '0 2px 8px rgba(139,92,246,0.35)',
      width: 30,
      height: 30,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
    },
  },
  styles: {
    content: {
      background: 'linear-gradient(135deg, #faf5ff, #f3e8ff)',
      color: '#2e1065',
      borderRadius: '16px 16px 16px 4px',
      fontSize: 13,
      lineHeight: 1.55,
      border: '1px solid #ddd6fe',
      boxShadow: '0 2px 10px rgba(139,92,246,0.12)',
      padding: '10px 14px',
    },
  },
}

// ── Componente principal ──────────────────────────────────────────────────────

interface AnalisisChatProps {
  workspaceId: number | null
  /** Alto máximo del historial de mensajes (px o CSS). */
  maxHeight?: number | string
}

export function AnalisisChat({ workspaceId, maxHeight = 420 }: AnalisisChatProps) {
  const { message } = App.useApp()
  const [chatInput, setChatInput] = useState('')
  const [pendingUserMsg, setPendingUserMsg] = useState<string | null>(null)
  const chatEndRef = useRef<HTMLDivElement>(null)
  const sentCountRef = useRef(0)

  const { data: chatData, isLoading: chatLoading } = useChatHistorial(workspaceId)
  const chatMutation = useEnviarChat()
  const mensajes: ChatMensaje[] = chatData?.data?.mensajes ?? []

  // Auto-scroll al fondo cuando cambian los mensajes
  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [mensajes, pendingUserMsg, chatMutation.isPending])

  // Limpia el mensaje optimista una vez que el historial real llegó
  useEffect(() => {
    if (pendingUserMsg && mensajes.length > sentCountRef.current) {
      setPendingUserMsg(null)
    }
  }, [mensajes.length, pendingUserMsg])

  const handleEnviarChat = useCallback(async (texto?: string) => {
    const valor = (texto ?? chatInput).trim()
    if (!workspaceId || !valor) return
    setChatInput('')
    setPendingUserMsg(valor)
    sentCountRef.current = mensajes.length
    try {
      await chatMutation.mutateAsync({ workspaceId, mensaje: valor })
    } catch {
      message.error('Error al enviar mensaje')
      setPendingUserMsg(null)
    }
  }, [workspaceId, chatInput, chatMutation, message, mensajes.length])

  // ── Items para Bubble.List ──────────────────────────────────────────────────

  const bubbleItems = [
    ...mensajes.map((msg, i) => {
      const isUser = msg.rol === 'user'
      return {
        key: `msg-${i}`,
        content: isUser
          ? msg.contenido
          : (
            <div className="mpm-chat-markdown" style={{ fontSize: 13 }}>
              <ReactMarkdown>{normalizarMarkdown(msg.contenido)}</ReactMarkdown>
            </div>
          ),
        footer: (
          <div
            style={{
              fontSize: 11,
              color: '#94a3b8',
              marginTop: 3,
              textAlign: isUser ? 'right' : 'left',
              paddingInline: 4,
            }}
          >
            {new Date(msg.createdAt).toLocaleTimeString('es-CL', {
              hour: '2-digit',
              minute: '2-digit',
            })}
          </div>
        ),
        ...(isUser ? BUBBLE_USER : BUBBLE_AI),
      }
    }),

    // Mensaje optimista del usuario
    ...(pendingUserMsg
      ? [{ key: 'pending-user', content: pendingUserMsg, ...BUBBLE_USER }]
      : []),

    // Indicador "escribiendo" de la IA — prop loading nativo de Bubble
    ...(chatMutation.isPending
      ? [{ key: 'typing-ai', content: '', loading: true, ...BUBBLE_AI }]
      : []),
  ]

  const isEmpty = bubbleItems.length === 0 && !chatLoading

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: maxHeight,
      }}
    >
      {/* ── Área de mensajes (position: relative para centrar el estado vacío) */}
      <div
        style={{
          flex: 1,
          overflowY: 'auto',
          position: 'relative',
          marginBottom: 12,
        }}
      >
        {/* Estado de carga inicial del historial */}
        {chatLoading && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Spin size="default" />
          </div>
        )}

        {/* Estado vacío: absolute para no crear gap entre él y el Sender */}
        {isEmpty && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              padding: '16px 20px',
              gap: 12,
            }}
          >
            {/* Ícono con glow */}
            <div
              style={{
                width: 56,
                height: 56,
                borderRadius: 16,
                background: 'linear-gradient(135deg, #f3e8ff, #ede9fe)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '0 4px 20px rgba(139,92,246,0.2)',
                border: '1px solid #ddd6fe',
                flexShrink: 0,
              }}
            >
            <SparklesIcon size={26} color="#8b5cf6" />
            </div>

            <div style={{ textAlign: 'center' }}>
              <p style={{ color: '#1e293b', fontSize: 14, fontWeight: 600, margin: 0, marginBottom: 4 }}>
                Asistente IA disponible
              </p>
              <p style={{ color: '#94a3b8', fontSize: 12, margin: 0, lineHeight: 1.5 }}>
                Pregunta sobre este análisis — factores de pérdida,<br />
                brechas de puntaje, recomendaciones estratégicas…
              </p>
            </div>

            {/* Sugerencias rápidas */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, width: '100%' }}>
              {[
                '¿Cuál fue el factor más importante de la pérdida?',
                '¿Qué mejorar para ganar la próxima?',
                'Resume los puntos críticos del análisis',
              ].map((sugerencia) => (
                <button
                  key={sugerencia}
                  onClick={() => handleEnviarChat(sugerencia)}
                  style={{
                    background: 'white',
                    border: '1px solid #e2e8f0',
                    borderRadius: 10,
                    padding: '8px 12px',
                    fontSize: 12,
                    color: '#475569',
                    cursor: 'pointer',
                    textAlign: 'left',
                    transition: 'all 0.15s ease',
                    lineHeight: 1.4,
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.borderColor = '#a78bfa'
                    e.currentTarget.style.background = '#faf5ff'
                    e.currentTarget.style.color = '#6d28d9'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.borderColor = '#e2e8f0'
                    e.currentTarget.style.background = 'white'
                    e.currentTarget.style.color = '#475569'
                  }}
                >
                  {sugerencia}
                </button>
              ))}
            </div>
          </div>
        )}

        {/* Bubble.List — historial con Ant Design X */}
        {bubbleItems.length > 0 && (
          <Bubble.List
            items={bubbleItems}
            style={{ padding: '4px 10px' }}
          />
        )}

        {/* Centinela auto-scroll */}
        <div ref={chatEndRef} />
      </div>

      {/* ── Divisor sutil ─────────────────────────────────────────────────── */}
      <div
        style={{
          height: 1,
          background: 'linear-gradient(90deg, transparent, #e2e8f0, transparent)',
          marginBottom: 12,
          flexShrink: 0,
        }}
      />

      {/* ── Sender — compositor de mensajes ───────────────────────────────── */}
      <div style={{ flexShrink: 0 }}>
        <Sender
          value={chatInput}
          onChange={setChatInput}
          onSubmit={handleEnviarChat}
          loading={chatMutation.isPending}
          disabled={chatMutation.isPending}
          placeholder="Pregunta sobre el análisis..."
          prefix={<SparklesIcon size={15} color="#8b5cf6" />}
          style={{
            borderRadius: 14,
            border: '1.5px solid #ddd6fe',
            boxShadow: '0 2px 12px rgba(139,92,246,0.1)',
            background: 'white',
          }}
          submitType="enter"
          styles={{
            actions: {
              background: 'linear-gradient(135deg, #8b5cf6, #a78bfa)',
              borderRadius: 10,
              color: 'white',
              boxShadow: '0 2px 8px rgba(139,92,246,0.4)',
            },
          }}
        />
        <p style={{ fontSize: 11, color: '#cbd5e1', textAlign: 'center', margin: '6px 0 0' }}>
          Enter para enviar · IA puede cometer errores
        </p>
      </div>
    </div>
  )
}
