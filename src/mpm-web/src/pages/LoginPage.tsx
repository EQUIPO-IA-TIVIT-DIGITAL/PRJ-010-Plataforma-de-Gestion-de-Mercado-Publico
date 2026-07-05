import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Form, Input, Button, Checkbox, Typography, Divider } from 'antd';
import { MailOutlined, LockOutlined } from '@ant-design/icons';
import { useAuth, SESSION_EXPIRED_KEY } from '../hooks/useAuth';

export function LoginPage() {
  const navigate = useNavigate();
  const { login, rememberedEmail } = useAuth();
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [sessionExpiredMsg, setSessionExpiredMsg] = useState(false);
  const [form] = Form.useForm();

  useEffect(() => {
    if (rememberedEmail) {
      form.setFieldsValue({ email: rememberedEmail, remember: true });
    }
  }, [rememberedEmail, form]);

  useEffect(() => {
    if (sessionStorage.getItem(SESSION_EXPIRED_KEY)) {
      sessionStorage.removeItem(SESSION_EXPIRED_KEY);
      setSessionExpiredMsg(true);
    }
  }, []);

  const handleSubmit = async (values: { email: string; password: string; remember?: boolean }) => {
    setLoading(true);
    setErrorMsg('');
    try {
      const response = await fetch('/api/v1/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: values.email, password: values.password }),
      });
      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        setErrorMsg(errorData?.message || 'Credenciales inválidas. Intenta nuevamente.');
        return;
      }
      const data = await response.json();
      login(data.data.token, data.data.user, values.remember);
      navigate('/licitaciones');
    } catch {
      setErrorMsg('Error de conexión. Verifica tu red.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="mpm-auth-bg">
      {/* Background dot pattern */}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          backgroundImage: `radial-gradient(circle, rgba(255,255,255,0.04) 1px, transparent 1px)`,
          backgroundSize: '32px 32px',
          pointerEvents: 'none',
        }}
      />

      {/* Card */}
      <div
        style={{
          background: 'rgba(255,255,255,0.97)',
          borderRadius: 20,
          boxShadow: '0 25px 80px rgba(0,0,0,0.35)',
          width: 440,
          padding: 40,
          position: 'relative',
          zIndex: 1,
        }}
      >
        {/* Logo & Header */}
        <div style={{ textAlign: 'center', marginBottom: 36 }}>
          <img
            src="/images/icon_tivit.svg"
            alt="TIVIT"
            style={{
              width: 56,
              height: 56,
              marginBottom: 16,
            }}
          />
          <Typography.Title
            level={3}
            style={{
              margin: 0,
              fontFamily: 'Inter, sans-serif',
              fontWeight: 800,
              fontSize: 26,
              letterSpacing: '-0.03em',
              color: '#0f172a',
              lineHeight: 1.2,
            }}
          >
            TIVIT
          </Typography.Title>
          <Typography.Text
            style={{
              color: '#64748b',
              fontSize: 14,
              display: 'block',
              marginTop: 4,
            }}
          >
            Mercado Público — Ingresa a tu cuenta
          </Typography.Text>
        </div>

        {/* Session expired notice */}
        {sessionExpiredMsg && (
          <div
            data-testid="session-expired-alert"
            style={{
              background: '#fffbeb',
              border: '1px solid #fde68a',
              borderRadius: 10,
              padding: '10px 14px',
              marginBottom: 20,
              display: 'flex',
              alignItems: 'center',
              gap: 8,
            }}
          >
            <span style={{ color: '#b45309', fontSize: 13 }}>
              ⏱ Tu sesión expiró. Inicia sesión nuevamente.
            </span>
          </div>
        )}

        {/* Error message */}
        {errorMsg && (
          <div
            style={{
              background: '#fef2f2',
              border: '1px solid #fecaca',
              borderRadius: 10,
              padding: '10px 14px',
              marginBottom: 20,
              display: 'flex',
              alignItems: 'center',
              gap: 8,
            }}
          >
            <span style={{ color: '#ef4444', fontSize: 13 }}>⚠ {errorMsg}</span>
          </div>
        )}

        {/* Form */}
        <Form
          form={form}
          layout="vertical"
          onFinish={handleSubmit}
          autoComplete="off"
          requiredMark={false}
          size="large"
        >
          <Form.Item
            name="email"
            label={
              <span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>
                Correo electrónico
              </span>
            }
            rules={[
              { required: true, message: 'El email es requerido' },
              { type: 'email', message: 'Ingresa un email válido' },
            ]}
            validateTrigger="onBlur"
            style={{ marginBottom: 16 }}
          >
            <Input
              data-testid="login-email"
              prefix={<MailOutlined style={{ color: '#94a3b8' }} />}
              placeholder="correo@empresa.com"
              style={{
                height: 44,
                borderRadius: 10,
                fontSize: 14,
                border: '1.5px solid #e2e8f0',
              }}
            />
          </Form.Item>

          <Form.Item
            name="password"
            label={
              <span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>
                Contraseña
              </span>
            }
            rules={[
              { required: true, message: 'La contraseña es requerida' },
              { min: 6, message: 'Mínimo 6 caracteres' },
            ]}
            validateTrigger="onBlur"
            style={{ marginBottom: 16 }}
          >
            <Input.Password
              data-testid="login-password"
              prefix={<LockOutlined style={{ color: '#94a3b8' }} />}
              placeholder="••••••••"
              style={{
                height: 44,
                borderRadius: 10,
                fontSize: 14,
                border: '1.5px solid #e2e8f0',
              }}
            />
          </Form.Item>

          {/* Remember me */}
          <div
            style={{
              display: 'flex',
              justifyContent: 'flex-start',
              alignItems: 'center',
              marginBottom: 24,
            }}
          >
            <Form.Item name="remember" valuePropName="checked" noStyle>
              <Checkbox style={{ fontSize: 13, color: '#64748b' }}>Recordarme</Checkbox>
            </Form.Item>
          </div>

          {/* Submit */}
          <Form.Item style={{ marginBottom: 0 }}>
            <Button
              type="primary"
              htmlType="submit"
              block
              loading={loading}
              data-testid="login-submit"
              style={{
                height: 46,
                borderRadius: 10,
                fontWeight: 700,
                fontSize: 15,
                background: loading
                  ? '#f1f5f9'
                  : 'linear-gradient(135deg, #E30613 0%, #ff3a46 100%)',
                border: 'none',
                boxShadow: loading ? 'none' : '0 4px 16px rgba(227, 6, 19, 0.35)',
                color: loading ? '#94a3b8' : 'white',
                transition: 'all 0.2s ease',
              }}
            >
              {loading ? 'Ingresando...' : 'Ingresar'}
            </Button>
          </Form.Item>
        </Form>

        {/* Footer */}
        <Divider style={{ margin: '24px 0', borderColor: '#f1f5f9' }} />
        <p
          style={{
            textAlign: 'center',
            fontSize: 12,
            color: '#94a3b8',
            margin: 0,
          }}
        >
          © {new Date().getFullYear()} TIVIT · Mercado Público
        </p>
      </div>
    </div>
  );
}
