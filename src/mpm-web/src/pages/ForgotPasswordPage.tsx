import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Form, Input, Button, Typography, Divider } from 'antd';
import { MailOutlined, ArrowLeftOutlined, FileTextOutlined, CheckCircleOutlined } from '@ant-design/icons';

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [emailSent, setEmailSent] = useState(false);
  const [form] = Form.useForm();

  const handleSubmit = async (values: { email: string }) => {
    setLoading(true);
    try {
      await fetch('/api/v1/auth/forgot-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });
      // Por seguridad, siempre mostramos éxito aunque el email no exista
      setEmailSent(true);
    } catch {
      setEmailSent(true);
    } finally {
      setLoading(false);
    }
  };

  // ---- Estado: correo enviado ----
  if (emailSent) {
    return (
      <div className="mpm-auth-bg">
        <div style={{ position: 'absolute', inset: 0, backgroundImage: `radial-gradient(circle, rgba(255,255,255,0.04) 1px, transparent 1px)`, backgroundSize: '32px 32px', pointerEvents: 'none' }} />
        <div style={{ background: 'rgba(255,255,255,0.97)', borderRadius: 20, boxShadow: '0 25px 80px rgba(0,0,0,0.35)', width: 440, padding: 40, position: 'relative', zIndex: 1, textAlign: 'center' }}>
          {/* Success icon */}
          <div style={{ width: 64, height: 64, borderRadius: '50%', background: 'linear-gradient(135deg, #10b981, #34d399)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', marginBottom: 20, boxShadow: '0 8px 24px rgba(16,185,129,0.35)' }}>
            <CheckCircleOutlined style={{ color: 'white', fontSize: 30 }} />
          </div>

          <Typography.Title level={3} style={{ margin: 0, fontWeight: 800, fontSize: 22, letterSpacing: '-0.02em', color: '#0f172a' }}>
            ¡Correo enviado!
          </Typography.Title>
          <Typography.Text style={{ color: '#64748b', fontSize: 14, display: 'block', marginTop: 8, marginBottom: 28, lineHeight: 1.6 }}>
            Si el email existe en nuestro sistema, recibirás instrucciones para restablecer tu contraseña en los próximos minutos.
          </Typography.Text>

          <div style={{ background: '#f0fdf4', border: '1px solid rgba(16,185,129,0.2)', borderRadius: 10, padding: '12px 16px', marginBottom: 28, textAlign: 'left' }}>
            <Typography.Text style={{ fontSize: 13, color: '#065f46' }}>
              📬 Revisa también tu carpeta de spam si no encuentras el correo.
            </Typography.Text>
          </div>

          <Button
            type="primary"
            block
            onClick={() => navigate('/login')}
            style={{ height: 46, borderRadius: 10, fontWeight: 700, fontSize: 15, background: 'linear-gradient(135deg, #E30613, #ff3a46)', border: 'none', boxShadow: '0 4px 16px rgba(227,6,19,0.35)' }}
          >
            Volver al login
          </Button>

          <Divider style={{ margin: '24px 0', borderColor: '#f1f5f9' }} />
          <p style={{ textAlign: 'center', fontSize: 12, color: '#94a3b8', margin: 0 }}>
            © {new Date().getFullYear()} TIVIT · Mercado Público
          </p>
        </div>
      </div>
    );
  }

  // ---- Formulario ----
  return (
    <div className="mpm-auth-bg">
      <div style={{ position: 'absolute', inset: 0, backgroundImage: `radial-gradient(circle, rgba(255,255,255,0.04) 1px, transparent 1px)`, backgroundSize: '32px 32px', pointerEvents: 'none' }} />

      <div style={{ background: 'rgba(255,255,255,0.97)', borderRadius: 20, boxShadow: '0 25px 80px rgba(0,0,0,0.35)', width: 440, padding: 40, position: 'relative', zIndex: 1 }}>

        {/* Logo & Header */}
        <div style={{ textAlign: 'center', marginBottom: 32 }}>
          <div style={{ width: 56, height: 56, borderRadius: 14, background: 'linear-gradient(135deg, #E30613, #ff3a46)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', marginBottom: 16, boxShadow: '0 8px 24px rgba(227,6,19,0.35)' }}>
            <FileTextOutlined style={{ color: 'white', fontSize: 26 }} />
          </div>
          <Typography.Title level={3} style={{ margin: 0, fontWeight: 800, fontSize: 22, letterSpacing: '-0.03em', color: '#0f172a', lineHeight: 1.2 }}>
            Recuperar contraseña
          </Typography.Title>
          <Typography.Text style={{ color: '#64748b', fontSize: 14, display: 'block', marginTop: 8, lineHeight: 1.6 }}>
            Ingresa tu email y te enviaremos instrucciones para restablecer tu contraseña.
          </Typography.Text>
        </div>

        {/* Form */}
        <Form form={form} layout="vertical" onFinish={handleSubmit} autoComplete="off" requiredMark={false} size="large">
          <Form.Item
            name="email"
            label={<span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>Correo electrónico</span>}
            rules={[
              { required: true, message: 'El email es requerido' },
              { type: 'email', message: 'Ingresa un email válido' },
            ]}
            validateTrigger="onBlur"
            style={{ marginBottom: 20 }}
          >
            <Input
              data-testid="forgot-email"
              prefix={<MailOutlined style={{ color: '#94a3b8' }} />}
              placeholder="correo@empresa.com"
              style={{ height: 44, borderRadius: 10, fontSize: 14, border: '1.5px solid #e2e8f0' }}
            />
          </Form.Item>

          <Form.Item style={{ marginBottom: 16 }}>
            <Button
              type="primary"
              htmlType="submit"
              block
              loading={loading}
              data-testid="forgot-submit"
              style={{
                height: 46,
                borderRadius: 10,
                fontWeight: 700,
                fontSize: 15,
                background: loading ? '#f1f5f9' : 'linear-gradient(135deg, #E30613, #ff3a46)',
                border: 'none',
                boxShadow: loading ? 'none' : '0 4px 16px rgba(227,6,19,0.35)',
                color: loading ? '#94a3b8' : 'white',
              }}
            >
              {loading ? 'Enviando...' : 'Enviar instrucciones'}
            </Button>
          </Form.Item>

          <div style={{ textAlign: 'center' }}>
            <Button
              type="link"
              icon={<ArrowLeftOutlined />}
              onClick={() => navigate('/login')}
              style={{ fontSize: 14, color: '#64748b', padding: 0 }}
            >
              Volver al login
            </Button>
          </div>
        </Form>

        <Divider style={{ margin: '24px 0', borderColor: '#f1f5f9' }} />
        <p style={{ textAlign: 'center', fontSize: 12, color: '#94a3b8', margin: 0 }}>
          © {new Date().getFullYear()} TIVIT · Mercado Público
        </p>
      </div>
    </div>
  );
}
