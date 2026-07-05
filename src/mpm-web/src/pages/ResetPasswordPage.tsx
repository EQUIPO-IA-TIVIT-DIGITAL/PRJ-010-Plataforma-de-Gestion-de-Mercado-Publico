import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Form, Input, Button, Typography, Spin, Divider } from 'antd';
import { LockOutlined, CheckCircleOutlined, WarningOutlined, ArrowLeftOutlined, FileTextOutlined } from '@ant-design/icons';

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const { token } = useParams<{ token: string }>();
  const [form] = Form.useForm();

  const [loading, setLoading] = useState(false);
  const [validating, setValidating] = useState(true);
  const [tokenValid, setTokenValid] = useState(false);
  const [resetSuccess, setResetSuccess] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  useEffect(() => {
    const validateToken = async () => {
      if (!token) {
        navigate('/login');
        return;
      }
      try {
        const response = await fetch(`/api/v1/auth/validate-reset-token/${token}`);
        setTokenValid(response.ok);
      } catch {
        setTokenValid(false);
      } finally {
        setValidating(false);
      }
    };
    validateToken();
  }, [token, navigate]);

  const handleSubmit = async (values: { newPassword: string }) => {
    if (!token) return;
    setLoading(true);
    setErrorMsg('');
    try {
      const response = await fetch('/api/v1/auth/reset-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, newPassword: values.newPassword }),
      });
      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        setErrorMsg(errorData?.message || 'Error al restablecer la contraseña');
        return;
      }
      setResetSuccess(true);
    } catch {
      setErrorMsg('Error de conexión con el servidor');
    } finally {
      setLoading(false);
    }
  };

  const AuthCard = ({ children }: { children: React.ReactNode }) => (
    <div className="mpm-auth-bg">
      <div style={{ position: 'absolute', inset: 0, backgroundImage: `radial-gradient(circle, rgba(255,255,255,0.04) 1px, transparent 1px)`, backgroundSize: '32px 32px', pointerEvents: 'none' }} />
      <div style={{ background: 'rgba(255,255,255,0.97)', borderRadius: 20, boxShadow: '0 25px 80px rgba(0,0,0,0.35)', width: 440, padding: 40, position: 'relative', zIndex: 1 }}>
        {children}
        <Divider style={{ margin: '24px 0', borderColor: '#f1f5f9' }} />
        <p style={{ textAlign: 'center', fontSize: 12, color: '#94a3b8', margin: 0 }}>
          © {new Date().getFullYear()} TIVIT · Mercado Público
        </p>
      </div>
    </div>
  );

  // ---- Estado: validando token ----
  if (validating) {
    return (
      <AuthCard>
        <div style={{ textAlign: 'center', padding: '20px 0' }}>
          <Spin size="large" />
          <Typography.Text style={{ display: 'block', marginTop: 16, color: '#64748b', fontSize: 14 }}>
            Validando enlace de recuperación…
          </Typography.Text>
        </div>
      </AuthCard>
    );
  }

  // ---- Estado: token inválido ----
  if (!tokenValid) {
    return (
      <AuthCard>
        <div style={{ textAlign: 'center' }}>
          <div style={{ width: 64, height: 64, borderRadius: '50%', background: '#fef2f2', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', marginBottom: 20, border: '2px solid rgba(239,68,68,0.2)' }}>
            <WarningOutlined style={{ color: '#ef4444', fontSize: 28 }} />
          </div>
          <Typography.Title level={3} style={{ margin: 0, fontWeight: 800, fontSize: 22, letterSpacing: '-0.02em', color: '#0f172a' }}>
            Enlace inválido
          </Typography.Title>
          <Typography.Text style={{ color: '#64748b', fontSize: 14, display: 'block', marginTop: 8, marginBottom: 28, lineHeight: 1.6 }}>
            Este enlace de recuperación ha expirado o ya fue utilizado. Solicita uno nuevo.
          </Typography.Text>
          <Button
            type="primary"
            block
            onClick={() => navigate('/forgot-password')}
            style={{ height: 46, borderRadius: 10, fontWeight: 700, fontSize: 15, background: 'linear-gradient(135deg, #E30613, #ff3a46)', border: 'none', boxShadow: '0 4px 16px rgba(227,6,19,0.35)', marginBottom: 12 }}
          >
            Solicitar nuevo enlace
          </Button>
          <Button type="link" icon={<ArrowLeftOutlined />} onClick={() => navigate('/login')} style={{ fontSize: 13, color: '#64748b', padding: 0 }}>
            Volver al login
          </Button>
        </div>
      </AuthCard>
    );
  }

  // ---- Estado: éxito ----
  if (resetSuccess) {
    return (
      <AuthCard>
        <div style={{ textAlign: 'center' }}>
          <div style={{ width: 64, height: 64, borderRadius: '50%', background: 'linear-gradient(135deg, #10b981, #34d399)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', marginBottom: 20, boxShadow: '0 8px 24px rgba(16,185,129,0.35)' }}>
            <CheckCircleOutlined style={{ color: 'white', fontSize: 30 }} />
          </div>
          <Typography.Title level={3} style={{ margin: 0, fontWeight: 800, fontSize: 22, letterSpacing: '-0.02em', color: '#0f172a' }}>
            ¡Contraseña restablecida!
          </Typography.Title>
          <Typography.Text style={{ color: '#64748b', fontSize: 14, display: 'block', marginTop: 8, marginBottom: 28, lineHeight: 1.6 }}>
            Tu contraseña fue actualizada exitosamente. Ya puedes iniciar sesión.
          </Typography.Text>
          <Button
            type="primary"
            block
            onClick={() => navigate('/login')}
            style={{ height: 46, borderRadius: 10, fontWeight: 700, fontSize: 15, background: 'linear-gradient(135deg, #E30613, #ff3a46)', border: 'none', boxShadow: '0 4px 16px rgba(227,6,19,0.35)' }}
          >
            Ir al login
          </Button>
        </div>
      </AuthCard>
    );
  }

  // ---- Formulario ----
  return (
    <AuthCard>
      {/* Logo & Header */}
      <div style={{ textAlign: 'center', marginBottom: 32 }}>
        <div style={{ width: 56, height: 56, borderRadius: 14, background: 'linear-gradient(135deg, #E30613, #ff3a46)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', marginBottom: 16, boxShadow: '0 8px 24px rgba(227,6,19,0.35)' }}>
          <FileTextOutlined style={{ color: 'white', fontSize: 26 }} />
        </div>
        <Typography.Title level={3} style={{ margin: 0, fontWeight: 800, fontSize: 22, letterSpacing: '-0.03em', color: '#0f172a', lineHeight: 1.2 }}>
          Restablecer contraseña
        </Typography.Title>
        <Typography.Text style={{ color: '#64748b', fontSize: 14, display: 'block', marginTop: 8, lineHeight: 1.6 }}>
          Crea una nueva contraseña de al menos 6 caracteres.
        </Typography.Text>
      </div>

      {/* Error */}
      {errorMsg && (
        <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 10, padding: '10px 14px', marginBottom: 20 }}>
          <span style={{ color: '#ef4444', fontSize: 13 }}>⚠ {errorMsg}</span>
        </div>
      )}

      {/* Form */}
      <Form form={form} layout="vertical" onFinish={handleSubmit} autoComplete="off" requiredMark={false} size="large">
        <Form.Item
          name="newPassword"
          label={<span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>Nueva contraseña</span>}
          rules={[
            { required: true, message: 'La contraseña es requerida' },
            { min: 6, message: 'Mínimo 6 caracteres' },
          ]}
          validateTrigger="onBlur"
          style={{ marginBottom: 16 }}
        >
          <Input.Password
            data-testid="reset-password"
            prefix={<LockOutlined style={{ color: '#94a3b8' }} />}
            placeholder="Mínimo 6 caracteres"
            style={{ height: 44, borderRadius: 10, fontSize: 14, border: '1.5px solid #e2e8f0' }}
          />
        </Form.Item>

        <Form.Item
          name="confirmPassword"
          label={<span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>Confirmar contraseña</span>}
          dependencies={['newPassword']}
          rules={[
            { required: true, message: 'Confirma tu contraseña' },
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || getFieldValue('newPassword') === value) {
                  return Promise.resolve();
                }
                return Promise.reject(new Error('Las contraseñas no coinciden'));
              },
            }),
          ]}
          validateTrigger="onBlur"
          style={{ marginBottom: 24 }}
        >
          <Input.Password
            data-testid="reset-confirm-password"
            prefix={<LockOutlined style={{ color: '#94a3b8' }} />}
            placeholder="Repite tu contraseña"
            style={{ height: 44, borderRadius: 10, fontSize: 14, border: '1.5px solid #e2e8f0' }}
          />
        </Form.Item>

        {/* Password strength hint */}
        <div style={{ background: '#f8faff', border: '1px solid #e2e8f0', borderRadius: 10, padding: '10px 14px', marginBottom: 20 }}>
          <Typography.Text style={{ fontSize: 12, color: '#64748b' }}>
            🔒 Usa al menos 6 caracteres. Combina letras, números y símbolos para mayor seguridad.
          </Typography.Text>
        </div>

        <Form.Item style={{ marginBottom: 16 }}>
          <Button
            type="primary"
            htmlType="submit"
            block
            loading={loading}
            data-testid="reset-submit"
            style={{
              height: 46, borderRadius: 10, fontWeight: 700, fontSize: 15,
              background: loading ? '#f1f5f9' : 'linear-gradient(135deg, #E30613, #ff3a46)',
              border: 'none',
              boxShadow: loading ? 'none' : '0 4px 16px rgba(227,6,19,0.35)',
              color: loading ? '#94a3b8' : 'white',
            }}
          >
            {loading ? 'Restableciendo...' : 'Restablecer contraseña'}
          </Button>
        </Form.Item>

        <div style={{ textAlign: 'center' }}>
          <Button type="link" icon={<ArrowLeftOutlined />} onClick={() => navigate('/login')} style={{ fontSize: 14, color: '#64748b', padding: 0 }}>
            Volver al login
          </Button>
        </div>
      </Form>
    </AuthCard>
  );
}
