import { useState } from 'react'
import {
  Card, Table, Button, Input, Space, Tag, Typography, Modal, Form, Select,
  Popconfirm, Switch, Tooltip, Alert, message, Avatar,
} from 'antd'
import {
  UserAddOutlined, SearchOutlined, ReloadOutlined, MailOutlined,
  SafetyCertificateOutlined, QuestionCircleOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { useAuth } from '../../hooks/useAuth'
import {
  useAdminUsuarios, useCrearAdminUsuario, useActualizarEstadoUsuario,
  useActualizarRolUsuario, useSetAccountManager, useEnviarRecuperacion,
} from '../../hooks/useAdminUsuarios'
import type { AdminRol, AdminUsuarioItem } from '../../types/admin'

const { Title, Text } = Typography

const ROL_META: Record<AdminRol, { color: string; label: string; desc: string }> = {
  SuperAdmin: { color: 'red', label: 'Super Admin', desc: 'Control total: gestiona admins, IA y logs' },
  Admin: { color: 'orange', label: 'Administrador', desc: 'Crea usuarios, gestiona analistas/usuarios y ve logs' },
  Analista: { color: 'blue', label: 'Analista', desc: 'Acceso a toda la plataforma (análisis, alertas, competidores)' },
  Usuario: { color: 'default', label: 'Usuario', desc: 'Acceso a toda la plataforma' },
}

function RolTag({ rol }: { rol: AdminRol }) {
  return <Tag color={ROL_META[rol]?.color ?? 'default'} style={{ borderRadius: 6, fontWeight: 600 }}>{ROL_META[rol]?.label ?? rol}</Tag>
}

export default function AdminUsuariosPage() {
  const { user } = useAuth()
  const esSuperAdmin = user?.roles?.includes('SuperAdmin') ?? false

  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [pagina, setPagina] = useState(1)
  const [pageSize] = useState(20)

  const { data: usuarios, isLoading } = useAdminUsuarios(search, pagina, pageSize)
  const crearMutation = useCrearAdminUsuario()
  const estadoMutation = useActualizarEstadoUsuario()
  const rolMutation = useActualizarRolUsuario()
  const accountManagerMutation = useSetAccountManager()
  const recuperacionMutation = useEnviarRecuperacion()

  // Modal de creación
  const [createOpen, setCreateOpen] = useState(false)
  const [form] = Form.useForm()
  const rolSeleccionado = Form.useWatch('rol', form) as AdminRol | undefined

  // Modal de cambio de rol
  const [rolTarget, setRolTarget] = useState<AdminUsuarioItem | null>(null)
  const [rolForm] = Form.useForm()
  const rolTargetSeleccionado = Form.useWatch('rol', rolForm) as AdminRol | undefined

  const rolesDisponibles = esSuperAdmin
    ? (Object.keys(ROL_META) as AdminRol[])
    : (['Analista', 'Usuario'] as AdminRol[])

  const puedeGestionar = (item: AdminUsuarioItem) =>
    esSuperAdmin || !item.roles.some((r) => r === 'SuperAdmin' || r === 'Admin')

  const handleCrear = async () => {
    try {
      const values = await form.validateFields()
      await crearMutation.mutateAsync({
        email: values.email.trim(),
        nombre: values.nombre.trim(),
        password: values.password,
        rol: values.rol,
        tenantNombre: values.tenantNombre?.trim() || null,
      })
      message.success(`Usuario ${values.email} creado correctamente`)
      setCreateOpen(false)
      form.resetFields()
    } catch (err) {
      if (err instanceof Error) message.error(err.message)
    }
  }

  const handleToggleEstado = async (item: AdminUsuarioItem) => {
    try {
      await estadoMutation.mutateAsync({ id: item.id, activo: !item.activo })
      message.success(item.activo ? `Usuario ${item.email} desactivado` : `Usuario ${item.email} activado`)
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'No se pudo actualizar el estado')
    }
  }

  const handleCambiarRol = async () => {
    if (!rolTarget) return
    try {
      const values = await rolForm.validateFields()
      await rolMutation.mutateAsync({ id: rolTarget.id, rol: values.rol })
      message.success(`Rol de ${rolTarget.email} actualizado a ${ROL_META[values.rol as AdminRol].label}`)
      setRolTarget(null)
      rolForm.resetFields()
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'No se pudo cambiar el rol')
    }
  }

  const handleAccountManager = async (item: AdminUsuarioItem, esAccountManager: boolean) => {
    try {
      await accountManagerMutation.mutateAsync({ id: item.id, esAccountManager })
      message.success(
        esAccountManager
          ? `${item.email} ahora es account manager de gobierno`
          : `${item.email} ya no es account manager de gobierno`
      )
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'No se pudo actualizar el flag')
    }
  }

  const handleEnviarRecuperacion = async (item: AdminUsuarioItem) => {
    try {
      await recuperacionMutation.mutateAsync(item.email)
      message.success(`Correo de recuperación enviado a ${item.email}`)
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'No se pudo enviar el correo')
    }
  }

  const columns = [
    {
      title: 'Usuario',
      key: 'usuario',
      render: (_: unknown, item: AdminUsuarioItem) => (
        <Space>
          <Avatar style={{ background: 'linear-gradient(135deg, #E30613, #ff3a46)', fontSize: 12, fontWeight: 700 }}>
            {item.nombre.split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase()}
          </Avatar>
          <div style={{ lineHeight: 1.3 }}>
            <div style={{ fontWeight: 600, fontSize: 13 }}>{item.nombre}</div>
            <Text type="secondary" style={{ fontSize: 12 }}>{item.email}</Text>
          </div>
        </Space>
      ),
    },
    {
      title: 'Rol',
      dataIndex: 'roles',
      key: 'rol',
      width: 180,
      render: (roles: AdminRol[]) => <RolTag rol={roles[0]} />,
    },
    {
      title: 'Estado',
      dataIndex: 'activo',
      key: 'activo',
      width: 110,
      render: (activo: boolean) =>
        activo
          ? <Tag color="success" style={{ borderRadius: 6 }}>Activo</Tag>
          : <Tag color="error" style={{ borderRadius: 6 }}>Desactivado</Tag>,
    },
    {
      title: 'Último acceso',
      dataIndex: 'ultimoLogin',
      key: 'ultimoLogin',
      width: 150,
      render: (v: string | null) =>
        v ? dayjs(v).format('DD/MM/YYYY HH:mm') : <Text type="secondary">Nunca</Text>,
    },
    {
      title: 'Acc. gobierno',
      dataIndex: 'esAccountManager',
      key: 'esAccountManager',
      width: 130,
      render: (es: boolean, item: AdminUsuarioItem) => (
        <Tooltip title="Los account managers de gobierno reciben alertas de destinatarios gubernamentales">
          <Switch
            size="small"
            checked={es}
            disabled={!puedeGestionar(item)}
            onChange={(v) => handleAccountManager(item, v)}
          />
        </Tooltip>
      ),
    },
    {
      title: 'Acciones',
      key: 'acciones',
      width: 240,
      render: (_: unknown, item: AdminUsuarioItem) => (
        <Space size={4} wrap>
          <Tooltip title={puedeGestionar(item) ? 'Cambiar rol' : 'Solo un Super Admin puede gestionar roles privilegiados'}>
            <Button
              size="small"
              icon={<SafetyCertificateOutlined />}
              disabled={!puedeGestionar(item)}
              onClick={() => { setRolTarget(item); rolForm.setFieldsValue({ rol: item.roles[0] }) }}
            >
              Rol
            </Button>
          </Tooltip>
          <Tooltip title="Envía el correo de recuperación para que defina una nueva contraseña">
            <Button size="small" icon={<MailOutlined />} onClick={() => handleEnviarRecuperacion(item)}>
              Recuperar
            </Button>
          </Tooltip>
          {puedeGestionar(item) && item.activo ? (
            <Popconfirm
              title="Desactivar usuario"
              description={`${item.email} no podrá iniciar sesión. ¿Continuar?`}
              okText="Desactivar"
              okButtonProps={{ danger: true }}
              onConfirm={() => handleToggleEstado(item)}
            >
              <Button size="small" danger>Desactivar</Button>
            </Popconfirm>
          ) : puedeGestionar(item) ? (
            <Button size="small" type="primary" style={{ background: '#E30613', border: 'none' }} onClick={() => handleToggleEstado(item)}>
              Activar
            </Button>
          ) : null}
        </Space>
      ),
    },
  ]

  return (
    <div style={{ maxWidth: 1200, margin: '0 auto' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 16, gap: 16, flexWrap: 'wrap' }}>
        <div>
          <Title level={3} style={{ marginBottom: 4 }}>Usuarios del sistema</Title>
          <Text type="secondary">
            Crea cuentas, asigna roles y controla el acceso a la plataforma.
          </Text>
        </div>
        <Button
          type="primary"
          icon={<UserAddOutlined />}
          style={{ background: '#E30613', border: 'none', fontWeight: 600, height: 40, padding: '0 20px' }}
          onClick={() => setCreateOpen(true)}
        >
          Nuevo usuario
        </Button>
      </div>

      <Card style={{ borderRadius: 12 }}>
        <Space style={{ marginBottom: 16, width: '100%', justifyContent: 'space-between' }} wrap>
          <Space>
            <Input
              prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
              placeholder="Buscar por nombre o correo..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onPressEnter={() => { setPagina(1); setSearch(searchInput) }}
              style={{ width: 280, borderRadius: 8 }}
              allowClear
            />
            <Button icon={<SearchOutlined />} onClick={() => { setPagina(1); setSearch(searchInput) }}>Buscar</Button>
            <Button icon={<ReloadOutlined />} onClick={() => { setSearchInput(''); setSearch(''); setPagina(1) }}>Limpiar</Button>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            <QuestionCircleOutlined /> Un Admin crea Analistas y Usuarios; solo un Super Admin crea Admins.
          </Text>
        </Space>

        {!esSuperAdmin && (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16, borderRadius: 8 }}
            message="Estás operando como Administrador"
            description="Puedes crear y gestionar Analistas y Usuarios. Los usuarios con rol Admin o Super Admin solo los gestiona un Super Admin."
          />
        )}

        <Table
          rowKey="id"
          columns={columns}
          dataSource={usuarios ?? []}
          loading={isLoading || crearMutation.isPending || estadoMutation.isPending || rolMutation.isPending || accountManagerMutation.isPending}
          pagination={{
            current: pagina,
            pageSize,
            total: usuarios?.[0]?.totalCount ?? 0,
            showSizeChanger: false,
            showTotal: (total) => `${total} usuarios`,
            onChange: (p) => setPagina(p),
          }}
        />
      </Card>

      {/* Modal crear usuario */}
      <Modal
        title={<span style={{ fontWeight: 700 }}><UserAddOutlined style={{ color: '#E30613', marginRight: 8 }} />Nuevo usuario</span>}
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={handleCrear}
        confirmLoading={crearMutation.isPending}
        okText="Crear usuario"
        cancelText="Cancelar"
        width={520}
      >
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16, borderRadius: 8 }}
          message="Contraseña temporal"
          description="Crearás el usuario con una contraseña inicial que deberá cambiar en su primer ingreso (menú de usuario → Cambiar contraseña)."
        />
        <Form form={form} layout="vertical">
          <Form.Item
            name="nombre"
            label="Nombre completo"
            rules={[{ required: true, message: 'El nombre es requerido' }]}
          >
            <Input placeholder="ej. María Pérez" style={{ borderRadius: 8 }} />
          </Form.Item>
          <Form.Item
            name="email"
            label="Correo electrónico"
            rules={[
              { required: true, message: 'El correo es requerido' },
              { type: 'email', message: 'Ingresa un correo válido' },
            ]}
          >
            <Input placeholder="ej. maria.perez@tivit.cl" style={{ borderRadius: 8 }} />
          </Form.Item>
          <Form.Item
            name="rol"
            label="Rol"
            rules={[{ required: true, message: 'Selecciona un rol' }]}
            extra={
              <Text type="secondary" style={{ fontSize: 12 }}>
                {rolSeleccionado ? ROL_META[rolSeleccionado].desc : 'Elige un rol para ver qué puede hacer el usuario'}
              </Text>
            }
          >
            <Select
              placeholder="¿Qué puede hacer este usuario?"
              options={rolesDisponibles.map((r) => ({ value: r, label: ROL_META[r].label }))}
              virtual={false}
              style={{ borderRadius: 8 }}
            />
          </Form.Item>
          <Space style={{ width: '100%' }} size={16}>
            <Form.Item
              name="password"
              label="Contraseña inicial"
              style={{ flex: 1 }}
              rules={[
                { required: true, message: 'La contraseña es requerida' },
                { min: 6, message: 'Mínimo 6 caracteres' },
              ]}
            >
              <Input.Password placeholder="Mínimo 6 caracteres" style={{ borderRadius: 8 }} />
            </Form.Item>
            <Form.Item
              name="confirmar"
              label="Confirmar contraseña"
              style={{ flex: 1 }}
              dependencies={['password']}
              rules={[
                { required: true, message: 'Confirma la contraseña' },
                ({ getFieldValue }) => ({
                  validator: (_, value) =>
                    !value || getFieldValue('password') === value
                      ? Promise.resolve()
                      : Promise.reject(new Error('Las contraseñas no coinciden')),
                }),
              ]}
            >
              <Input.Password placeholder="Repite la contraseña" style={{ borderRadius: 8 }} />
            </Form.Item>
          </Space>
          <Form.Item name="tenantNombre" label="Organización (opcional)">
            <Input placeholder="ej. TIVIT Chile" style={{ borderRadius: 8 }} />
          </Form.Item>
        </Form>
      </Modal>

      {/* Modal cambiar rol */}
      <Modal
        title={<span style={{ fontWeight: 700 }}><SafetyCertificateOutlined style={{ color: '#E30613', marginRight: 8 }} />Cambiar rol de {rolTarget?.email}</span>}
        open={!!rolTarget}
        onCancel={() => setRolTarget(null)}
        onOk={handleCambiarRol}
        confirmLoading={rolMutation.isPending}
        okText="Guardar rol"
        cancelText="Cancelar"
        width={480}
      >
        <Form form={rolForm} layout="vertical">
          <Form.Item
            name="rol"
            label="Nuevo rol"
            rules={[{ required: true, message: 'Selecciona un rol' }]}
            extra={
              <Text type="secondary" style={{ fontSize: 12 }}>
                {rolTargetSeleccionado ? ROL_META[rolTargetSeleccionado].desc : 'Elige un rol para ver qué puede hacer el usuario'}
              </Text>
            }
          >
            <Select
              options={rolesDisponibles.map((r) => ({ value: r, label: ROL_META[r].label }))}
              virtual={false}
              style={{ borderRadius: 8 }}
            />
          </Form.Item>
        </Form>
        {rolTarget && (
          <Alert
            type="warning"
            showIcon
            message="El cambio de rol aplica de inmediato"
            description={`${rolTarget.nombre} tendrá los permisos del nuevo rol en su próxima sesión.`}
          />
        )}
      </Modal>
    </div>
  )
}
