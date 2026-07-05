import { Input, Button, Tooltip, Typography } from 'antd';
import { SendOutlined, PaperClipOutlined, CloseOutlined, FileOutlined } from '@ant-design/icons';
import { useState, useRef, useCallback } from 'react';

const { Text } = Typography;
const { TextArea } = Input;
const MAX_SIZE = 10 * 1024 * 1024;

interface ArchivoSeleccionado {
  file: File;
  id: string;
}

interface Props {
  onSend: (contenido: string, archivos?: File[]) => void;
  onTyping: (escribiendo: boolean) => void;
  isPending: boolean;
}

export function MensajeInput({ onSend, onTyping, isPending }: Props) {
  const [contenido, setContenido] = useState('');
  const [archivos, setArchivos] = useState<ArchivoSeleccionado[]>([]);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const hasContent = contenido.trim().length > 0 || archivos.length > 0;

  const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setError(null);
    const selected = Array.from(e.target.files || []);
    const validos: ArchivoSeleccionado[] = [];
    for (const f of selected) {
      if (f.size > MAX_SIZE) {
        setError(`"${f.name}" excede el límite de 10MB`);
        continue;
      }
      validos.push({ file: f, id: `${f.name}-${Date.now()}-${Math.random()}` });
    }
    setArchivos(prev => [...prev, ...validos]);
    if (fileInputRef.current) fileInputRef.current.value = '';
  }, []);

  const quitarArchivo = useCallback((id: string) => {
    setArchivos(prev => prev.filter(a => a.id !== id));
  }, []);

  const handleSend = () => {
    if (hasContent && !isPending) {
      onSend(contenido, archivos.map(a => a.file));
      setContenido('');
      setArchivos([]);
      setError(null);
      onTyping(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setContenido(e.target.value);
    onTyping(e.target.value.length > 0);
  };

  function formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  return (
    <div>
      {/* File preview chips */}
      {archivos.length > 0 && (
        <div
          style={{
            padding: '8px 20px 0',
            borderTop: '1px solid var(--border)',
            background: 'white',
            display: 'flex',
            flexWrap: 'wrap',
            gap: 6,
          }}
        >
          {archivos.map(a => (
            <div
              key={a.id}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                background: 'var(--bg-muted)',
                borderRadius: 8,
                padding: '4px 8px',
                border: '1px solid var(--border)',
                fontSize: 12,
              }}
            >
              <FileOutlined style={{ color: '#3b82f6', fontSize: 14 }} />
              <span style={{ fontWeight: 500, color: 'var(--text-primary)', maxWidth: 150, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {a.file.name}
              </span>
              <Text style={{ fontSize: 11, color: 'var(--text-muted)' }}>{formatSize(a.file.size)}</Text>
              <Button
                type="text"
                size="small"
                icon={<CloseOutlined />}
                onClick={() => quitarArchivo(a.id)}
                style={{ color: '#94a3b8', width: 18, height: 18, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 0 }}
              />
            </div>
          ))}
        </div>
      )}

      <div
        style={{
          padding: '14px 20px',
          borderTop: '1px solid var(--border)',
          background: 'white',
          display: 'flex',
          gap: 10,
          alignItems: 'flex-end',
          flexShrink: 0,
        }}
      >
        {/* Hidden file input */}
        <input
          ref={fileInputRef}
          type="file"
          multiple
          onChange={handleFileSelect}
          style={{ display: 'none' }}
          data-testid="file-input"
        />

        {/* Adjuntar */}
        <Tooltip title="Adjuntar archivo (máx 10MB)">
          <Button
            type="text"
            icon={<PaperClipOutlined />}
            data-testid="btn-adjuntar"
            onClick={() => fileInputRef.current?.click()}
            style={{
              color: 'var(--text-muted)',
              borderRadius: 10,
              width: 38,
              height: 38,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
              transition: 'all 0.15s',
            }}
          />
        </Tooltip>

        {/* Input */}
        <div
          style={{
            flex: 1,
            background: 'var(--bg-muted)',
            borderRadius: 14,
            border: error ? '1.5px solid #ef4444' : '1.5px solid var(--border)',
            transition: 'all 0.15s',
            overflow: 'hidden',
          }}
          onFocus={(e) => {
            if (!error) {
              e.currentTarget.style.borderColor = '#E30613';
              e.currentTarget.style.boxShadow = '0 0 0 3px rgba(227,6,19,0.08)';
            }
          }}
          onBlur={(e) => {
            if (!error) {
              e.currentTarget.style.borderColor = 'var(--border)';
              e.currentTarget.style.boxShadow = 'none';
            }
          }}
        >
          <TextArea
            value={contenido}
            onChange={handleChange}
            onKeyDown={handleKeyDown}
            placeholder="Escribe un mensaje… (Enter para enviar)"
            autoSize={{ minRows: 1, maxRows: 5 }}
            data-testid="mensaje-input"
            style={{
              background: 'transparent',
              border: 'none',
              boxShadow: 'none',
              resize: 'none',
              padding: '10px 14px',
              fontSize: 14,
              lineHeight: 1.5,
              outline: 'none',
            }}
          />
        </div>

        {/* Error tooltip */}
        {error && (
          <div style={{ position: 'absolute', bottom: 70, left: 20, background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 8, padding: '6px 10px' }}>
            <Text style={{ fontSize: 12, color: '#ef4444' }}>⚠ {error}</Text>
          </div>
        )}

        {/* Enviar */}
        <Button
          type="primary"
          icon={<SendOutlined />}
          onClick={handleSend}
          loading={isPending}
          disabled={!hasContent}
          data-testid="btn-enviar"
          style={{
            width: 42,
            height: 42,
            borderRadius: 12,
            background: hasContent
              ? 'linear-gradient(135deg, #E30613, #ff3a46)'
              : 'var(--bg-muted)',
            border: 'none',
            boxShadow: hasContent ? '0 2px 8px rgba(227,6,19,0.35)' : 'none',
            transition: 'all 0.2s ease',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
            padding: 0,
          }}
        />
      </div>
    </div>
  );
}
