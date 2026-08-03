import { useState, useMemo } from 'react';
import {
  Space, Table, Button, Modal, Form, Input, InputNumber, Switch, Tag,
  App as AntApp, Popconfirm, Empty, Card, Row, Col, Statistic, Popover,
} from 'antd';
import { BellOutlined, PlusOutlined, DeleteOutlined, CheckCircleOutlined, InfoCircleOutlined } from '@ant-design/icons';
import { useAlertas, useCrearAlerta, useToggleAlerta, useEliminarAlerta } from '../hooks/useAlertas';
import type { ReglaAlerta, CrearReglaRequest } from '../types/alertas';

export function AlertasPage() {
  const { message } = AntApp.useApp();
  const { data, isLoading } = useAlertas();
  const crear = useCrearAlerta();
  const toggle = useToggleAlerta();
  const eliminar = useEliminarAlerta();

  const [modalCrear, setModalCrear] = useState(false);
  const [form] = Form.useForm<CrearReglaRequest>();

  const reglas = data?.data ?? [];

  // Metrics calculations
  const totalReglas = reglas.length;
  const reglasActivas = useMemo(() => reglas.filter((r) => r.activa).length, [reglas]);
  const sinonimosTotales = useMemo(() => {
    return reglas.reduce((acc, r) => acc + (r.sinonimosIa?.length ?? 0), 0);
  }, [reglas]);

  const handleCrear = async (values: CrearReglaRequest) => {
    try {
      const creada = await crear.mutateAsync(values);
      if (creada.data.sinonimosIa && creada.data.sinonimosIa.length > 0) {
        message.success('Alerta creada — sinónimos generados por IA aplicados automáticamente');
      } else {
        message.warning('Alerta creada, pero no se pudieron generar sinónimos por IA (solo coincidirá con la palabra literal)');
      }
      setModalCrear(false);
      form.resetFields();
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'No se pudo crear la alerta');
    }
  };

  const handleToggle = async (id: number) => {
    try {
      await toggle.mutateAsync(id);
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'No se pudo cambiar el estado');
    }
  };

  const handleEliminar = async (id: number) => {
    try {
      await eliminar.mutateAsync(id);
      message.success('Alerta eliminada');
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'No se pudo eliminar');
    }
  };

  const columns = [
    {
      title: 'Palabra clave',
      dataIndex: 'keyword',
      key: 'keyword',
      render: (v: string) => <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{v}</span>,
    },
    {
      title: 'Sinónimos IA',
      dataIndex: 'sinonimosIa',
      key: 'sinonimosIa',
      render: (sinonimos: string[] | null) =>
        sinonimos && sinonimos.length > 0 ? (
          <Space size={4} wrap>
            {sinonimos.slice(0, 4).map((s) => (
              <Tag key={s} color="purple" style={{ borderRadius: 6 }}>{s}</Tag>
            ))}
            {sinonimos.length > 4 && (
              <Popover
                title="Todos los sinónimos"
                content={
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6, maxWidth: 300 }}>
                    {sinonimos.map((s) => (
                      <span key={s}>• {s}</span>
                    ))}
                  </div>
                }
                trigger="hover"
                placement="bottom"
              >
                <Tag color="purple" style={{ borderRadius: 6, cursor: 'pointer', borderStyle: 'dashed' }}>
                  +{sinonimos.length - 4}
                </Tag>
              </Popover>
            )}
          </Space>
        ) : (
          <span style={{ color: '#94a3b8', fontSize: 12 }}>Sin sinónimos aún</span>
        ),
    },
    {
      title: 'Monto mínimo',
      dataIndex: 'montoMinimo',
      key: 'montoMinimo',
      render: (v: number | null) => (v ? `$${v.toLocaleString('es-CL')}` : '—'),
    },
    {
      title: 'Estado',
      dataIndex: 'activa',
      key: 'activa',
      render: (activa: boolean, record: ReglaAlerta) => (
        <Switch checked={activa} onChange={() => handleToggle(record.id)} checkedChildren="Activa" unCheckedChildren="Pausada" />
      ),
    },
    {
      title: 'Acciones',
      key: 'acciones',
      width: 100,
      render: (_: unknown, record: ReglaAlerta) => (
        <Popconfirm title="¿Eliminar esta alerta?" onConfirm={() => handleEliminar(record.id)}>
          <Button size="small" danger icon={<DeleteOutlined />} style={{ borderRadius: 8 }} />
        </Popconfirm>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%', padding: '8px 0' }}>
      {/* Header */}
      <div className="mpm-page-header" style={{ marginBottom: 4 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
            <div style={{ width: 32, height: 32, borderRadius: 8, background: 'linear-gradient(135deg, #3b82f6, #60a5fa)', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 4px 10px rgba(59,130,246,0.3)' }}>
              <BellOutlined style={{ color: 'white', fontSize: 15 }} />
            </div>
            <h1 className="mpm-page-title">Alertas Inteligentes</h1>
          </div>
          <p className="mpm-page-subtitle">
            Recibe notificaciones automáticas cuando aparezca una licitación relevante — la IA expande cada palabra clave a sinónimos y conceptos relacionados.
          </p>
        </div>
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => setModalCrear(true)}
          style={{
            height: 38, borderRadius: 10, fontWeight: 700,
            background: 'linear-gradient(135deg, #E30613, #ff3a46)', border: 'none',
            boxShadow: '0 4px 12px rgba(227,6,19,0.3)',
          }}
        >
          Nueva alerta
        </Button>
      </div>

      {/* Metrics Cards */}
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <Card bordered={false} style={{ background: '#ffffff', borderRadius: 14, boxShadow: 'var(--shadow-card)' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', color: 'var(--text-secondary)' }}>Reglas de alerta</span>}
              value={totalReglas}
              prefix={<BellOutlined style={{ color: '#3b82f6', marginRight: 8 }} />}
              valueStyle={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card bordered={false} style={{ background: '#ffffff', borderRadius: 14, boxShadow: 'var(--shadow-card)' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', color: 'var(--text-secondary)' }}>Alertas activas</span>}
              value={reglasActivas}
              prefix={<CheckCircleOutlined style={{ color: '#10b981', marginRight: 8 }} />}
              valueStyle={{ fontSize: 22, fontWeight: 700, color: '#10b981' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card bordered={false} style={{ background: '#ffffff', borderRadius: 14, boxShadow: 'var(--shadow-card)' }}>
            <Statistic
              title={<span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', color: 'var(--text-secondary)' }}>Conceptos relacionados IA</span>}
              value={sinonimosTotales}
              prefix={<InfoCircleOutlined style={{ color: '#8b5cf6', marginRight: 8 }} />}
              valueStyle={{ fontSize: 22, fontWeight: 700, color: '#8b5cf6' }}
            />
          </Card>
        </Col>
      </Row>

      {/* Rules Table (Full Width) */}
      <div style={{ background: 'white', border: '1px solid var(--border)', borderRadius: 14, boxShadow: 'var(--shadow-card)', overflow: 'hidden', marginTop: 4 }}>
        <Table<ReglaAlerta>
          columns={columns}
          dataSource={reglas}
          rowKey="id"
          loading={isLoading}
          pagination={false}
          locale={{ emptyText: <div style={{ padding: '40px 0' }}><Empty description="Sin alertas configuradas todavía" /></div> }}
        />
      </div>

      {/* Modales */}
      <Modal
        title="Nueva alerta"
        open={modalCrear}
        onCancel={() => setModalCrear(false)}
        onOk={() => form.submit()}
        confirmLoading={crear.isPending}
        okText="Crear"
        okButtonProps={{ style: { borderRadius: 10, background: 'linear-gradient(135deg, #E30613, #ff3a46)', border: 'none', fontWeight: 600 } }}
        cancelButtonProps={{ style: { borderRadius: 10 } }}
      >
        <Form form={form} layout="vertical" onFinish={handleCrear}>
          <Form.Item
            name="keyword"
            label="Palabra clave"
            rules={[{ required: true, min: 2, message: 'Ingresa al menos 2 caracteres' }]}
          >
            <Input placeholder="ej. cloud, SOC, data center" />
          </Form.Item>
          <Form.Item name="montoMinimo" label="Monto mínimo (opcional)">
            <InputNumber style={{ width: '100%' }} min={0} placeholder="ej. 10000000" />
          </Form.Item>
          <Form.Item name="montoMaximo" label="Monto máximo (opcional)">
            <InputNumber style={{ width: '100%' }} min={0} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
