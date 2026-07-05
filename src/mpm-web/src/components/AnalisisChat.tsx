import { useState, useCallback, useRef, useEffect } from 'react'
import { Input, Button, Space, Spin, Typography, App } from 'antd'
import { SendOutlined, RobotOutlined, UserOutlined } from '@ant-design/icons'
import ReactMarkdown from 'react-markdown'
import { useEnviarChat, useChatHistorial } from '../hooks/useAnalisis'
import type { ChatMensaje } from '../types/analisis'

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

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [mensajes, pendingUserMsg, chatMutation.isPending])

  // Limpia el mensaje optimista una vez que el historial real (usuario + respuesta IA) llegó
  useEffect(() => {
    if (pendingUserMsg && mensajes.length > sentCountRef.current) {
      setPendingUserMsg(null)
    }
  }, [mensajes.length, pendingUserMsg])

  const handleEnviarChat = useCallback(async () => {
    if (!workspaceId || !chatInput.trim()) return
    const mensaje = chatInput.trim()
    setChatInput('')
    setPendingUserMsg(mensaje)
    sentCountRef.current = mensajes.length
    try {
      await chatMutation.mutateAsync({ workspaceId, mensaje })
    } catch {
      message.error('Error al enviar mensaje')
      setPendingUserMsg(null)
    }
  }, [workspaceId, chatInput, chatMutation, message, mensajes.length])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: maxHeight }}>
      {/* Messages */}
      <div
        style={{
          flex: 1,
          overflowY: 'auto',
          marginBottom: 16,
          padding: '8px 0',
        }}
      >
        {mensajes.length === 0 && !chatLoading && (
          <div style={{ textAlign: 'center', padding: '32px 0' }}>
            <div
              style={{
                width: 48,
                height: 48,
                borderRadius: 12,
                background: '#faf5ff',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: 12,
              }}
            >
              <RobotOutlined style={{ fontSize: 22, color: '#8b5cf6' }} />
            </div>
            <p style={{ color: 'var(--text-secondary)', fontSize: 14, margin: 0 }}>
              Haz una pregunta sobre el análisis
            </p>
            <p style={{ color: 'var(--text-muted)', fontSize: 12, marginTop: 4 }}>
              Por ejemplo: "¿Cuál fue el factor más importante de la pérdida?"
            </p>
          </div>
        )}

        {chatLoading && (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        )}

        <Space direction="vertical" size={12} style={{ width: '100%' }}>
          {mensajes.map((msg, i) => {
            const isUser = msg.rol === 'user'
            return (
              <div
                key={i}
                style={{
                  display: 'flex',
                  gap: 10,
                  flexDirection: isUser ? 'row-reverse' : 'row',
                  alignItems: 'flex-start',
                }}
              >
                {/* Avatar */}
                <div
                  style={{
                    width: 32,
                    height: 32,
                    borderRadius: '50%',
                    background: isUser
                      ? 'linear-gradient(135deg, #E30613, #ff3a46)'
                      : 'linear-gradient(135deg, #8b5cf6, #a78bfa)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    flexShrink: 0,
                    boxShadow: isUser
                      ? '0 2px 8px rgba(227,6,19,0.3)'
                      : '0 2px 8px rgba(139,92,246,0.3)',
                  }}
                >
                  {isUser
                    ? <UserOutlined style={{ color: 'white', fontSize: 14 }} />
                    : <RobotOutlined style={{ color: 'white', fontSize: 14 }} />
                  }
                </div>

                {/* Bubble */}
                <div style={{ maxWidth: '75%' }}>
                  <div className={isUser ? 'mpm-chat-user' : 'mpm-chat-assistant'}>
                    {isUser ? (
                      <Typography.Text style={{ whiteSpace: 'pre-wrap', color: 'inherit', fontSize: 13 }}>
                        {msg.contenido}
                      </Typography.Text>
                    ) : (
                      <div className="mpm-chat-markdown" style={{ fontSize: 13 }}>
                        <ReactMarkdown>{normalizarMarkdown(msg.contenido)}</ReactMarkdown>
                      </div>
                    )}
                  </div>
                  <div
                    style={{
                      fontSize: 11,
                      color: 'var(--text-muted)',
                      marginTop: 4,
                      textAlign: isUser ? 'right' : 'left',
                      padding: '0 4px',
                    }}
                  >
                    {new Date(msg.createdAt).toLocaleTimeString('es-CL', { hour: '2-digit', minute: '2-digit' })}
                  </div>
                </div>
              </div>
            )
          })}

          {/* Mensaje optimista del usuario: aparece al instante, sin esperar respuesta */}
          {pendingUserMsg && (
            <div style={{ display: 'flex', gap: 10, flexDirection: 'row-reverse', alignItems: 'flex-start' }}>
              <div
                style={{
                  width: 32,
                  height: 32,
                  borderRadius: '50%',
                  background: 'linear-gradient(135deg, #E30613, #ff3a46)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                  boxShadow: '0 2px 8px rgba(227,6,19,0.3)',
                }}
              >
                <UserOutlined style={{ color: 'white', fontSize: 14 }} />
              </div>
              <div style={{ maxWidth: '75%' }}>
                <div className="mpm-chat-user">
                  <Typography.Text style={{ whiteSpace: 'pre-wrap', color: 'inherit', fontSize: 13 }}>
                    {pendingUserMsg}
                  </Typography.Text>
                </div>
              </div>
            </div>
          )}

          {/* Indicador "escribiendo" mientras la IA responde */}
          {chatMutation.isPending && (
            <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start' }}>
              <div
                style={{
                  width: 32,
                  height: 32,
                  borderRadius: '50%',
                  background: 'linear-gradient(135deg, #8b5cf6, #a78bfa)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                  boxShadow: '0 2px 8px rgba(139,92,246,0.3)',
                }}
              >
                <RobotOutlined style={{ color: 'white', fontSize: 14 }} />
              </div>
              <div className="mpm-chat-assistant" style={{ display: 'flex', alignItems: 'center', gap: 4, padding: '10px 14px' }}>
                <span className="mpm-typing-dot" />
                <span className="mpm-typing-dot" />
                <span className="mpm-typing-dot" />
              </div>
            </div>
          )}

          <div ref={chatEndRef} />
        </Space>
      </div>

      {/* Input */}
      <div style={{ display: 'flex', gap: 8 }}>
        <Input
          value={chatInput}
          onChange={(e) => setChatInput(e.target.value)}
          onPressEnter={handleEnviarChat}
          placeholder="Pregunta sobre el análisis..."
          disabled={chatMutation.isPending}
          style={{ borderRadius: 10, flex: 1, height: 42 }}
          prefix={<RobotOutlined style={{ color: '#94a3b8' }} />}
        />
        <Button
          type="primary"
          icon={<SendOutlined />}
          onClick={handleEnviarChat}
          loading={chatMutation.isPending}
          style={{
            height: 42,
            width: 42,
            borderRadius: 10,
            background: 'linear-gradient(135deg, #8b5cf6, #a78bfa)',
            border: 'none',
            boxShadow: '0 2px 8px rgba(139,92,246,0.3)',
            padding: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        />
      </div>
    </div>
  )
}
