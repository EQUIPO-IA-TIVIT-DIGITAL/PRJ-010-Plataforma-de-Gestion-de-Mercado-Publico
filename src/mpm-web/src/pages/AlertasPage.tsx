import { useState } from 'react';
import {
  Space, Table, Button, Modal, Form, Input, InputNumber, Switch, Tag, Select,
  App as AntApp, Popconfirm, Empty,
} from 'antd';
import { BellOutlined, PlusOutlined, ExperimentOutlined, DeleteOutlined, SendOutlined } from '@ant-design/icons';
import { useAlertas, useCrearAlerta, useToggleAlerta, useEliminarAlerta, useProbarAlerta, useGuardarMiTelegram, useGenerarLinkTelegram } from '../hooks/useAlertas';
import { useLicitaciones } from '../hooks/useLicitaciones';
import type { ReglaAlerta, CrearReglaRequest } from '../types/alertas';

export function AlertasPage() {
  const { message } = AntApp.useApp();
  const { data, isLoading } = useAlertas();
  const crear = useCrearAlerta();
  const toggle = useToggleAlerta();
  const eliminar = useEliminarAlerta();
  const probar = useProbarAlerta();

  const [modalCrear, setModalCrear] = useState(false);
  const [form] = Form.useForm<CrearReglaRequest>();

  const [modalTelegram, setModalTelegram] = useState(false);
  const [formTelegram] = Form.useForm<{ telegramChatId: string }>();
  const guardarTelegram = useGuardarMiTelegram();
  const generarLink = useGenerarLinkTelegram();

  const handleGuardarTelegram = async (values: { telegramChatId: string }) => {
    try {
      await guardarTelegram.mutateAsync(values.telegramChatId);
      message.success('Chat de Telegram guardado — ya podés recibir alertas ahí');
      setModalTelegram(false);
      formTelegram.resetFields();
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'No se pudo guardar el chat de Telegram');
    }
  };

  const handleConectarTelegram = async () => {
    try {
      const { data } = await generarLink.mutateAsync();
      window.open(data.url, '_blank', 'noopener,noreferrer');
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'No se pudo generar el link de Telegram');
    }
  };

  const [modalProbar, setModalProbar] = useState<ReglaAlerta | null>(null);
  const [licitacionSeleccionada, setLicitacionSeleccionada] = useState<number | null>(null);
  const { data: licitacionesData } = useLicitaciones({ page: 1, pageSize: 50, sortBy: 'fecha_publicacion', sortDir: 'desc' });

  const reglas = data?.data ?? [];
  const licitaciones = licitacionesData?.data?.items ?? [];

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

  const handleProbar = async () => {
    if (!modalProbar || licitacionSeleccionada == null) return;
    const licitacion = licitaciones.find((l) => l.id === licitacionSeleccionada);
    if (!licitacion) return;

    try {
      const resultado = await probar.mutateAsync({
        id: modalProbar.id,
        request: {
          licitacionId: licitacion.id,
          codigoExterno: licitacion.codigoExterno,
          nombre: licitacion.nombre,
          monto: licitacion.montoEstimado,
          tipoLicitacion: licitacion.tipo,
          organismo: licitacion.organismo,
        },
      });

      if (resultado.data.notificacionInAppCreada) {
        message.success('Alerta de prueba disparada — revisa Notificaciones');
      } else {
        message.warning('No se generó notificación (¿la licitación no coincide con la regla?)');
      }
      if (resultado.data.notificacionTelegramError) {
        message.info(`Telegram: ${resultado.data.notificacionTelegramError}`);
      }
      setModalProbar(null);
      setLicitacionSeleccionada(null);
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'No se pudo probar la alerta');
    }
  };

  const columns = [
    {
      title: 'Keyword',
      dataIndex: 'keyword',
      key: 'keyword',
      render: (v: string) => <span style={{ fontWeight: 600 }}>{v}</span>,
    },
    {
      title: 'Sinónimos IA',
      dataIndex: 'sinonimosIa',
      key: 'sinonimosIa',
      render: (sinonimos: string[] | null) =>
        sinonimos && sinonimos.length > 0 ? (
          <Space size={4} wrap>
            {sinonimos.slice(0, 4).map((s) => (
              <Tag key={s} color="purple">{s}</Tag>
            ))}
            {sinonimos.length > 4 && <Tag>+{sinonimos.length - 4}</Tag>}
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
      title: 'Telegram',
      dataIndex: 'notificarTelegram',
      key: 'notificarTelegram',
      render: (v: boolean) => (v ? <Tag color="blue">Sí</Tag> : <Tag>No</Tag>),
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
      render: (_: unknown, record: ReglaAlerta) => (
        <Space>
          <Button
            size="small"
            icon={<ExperimentOutlined />}
            onClick={() => setModalProbar(record)}
          >
            Probar
          </Button>
          <Popconfirm title="¿Eliminar esta alerta?" onConfirm={() => handleEliminar(record.id)}>
            <Button size="small" danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={20} style={{ width: '100%' }}>
      <div className="mpm-page-header">
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
            <div style={{ width: 32, height: 32, borderRadius: 8, background: 'linear-gradient(135deg, #3b82f6, #60a5fa)', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 4px 10px rgba(59,130,246,0.3)' }}>
              <BellOutlined style={{ color: 'white', fontSize: 15 }} />
            </div>
            <h1 className="mpm-page-title">Alertas Inteligentes</h1>
          </div>
          <p className="mpm-page-subtitle">
            Recibí notificaciones automáticas cuando aparezca una licitación relevante — la IA expande cada palabra clave a sinónimos y conceptos relacionados.
          </p>
        </div>
        <Space>
          <Button icon={<SendOutlined />} onClick={() => setModalTelegram(true)}>
            Mi Telegram
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setModalCrear(true)}>
            Nueva alerta
          </Button>
        </Space>
      </div>

      <Table<ReglaAlerta>
        columns={columns}
        dataSource={reglas}
        rowKey="id"
        loading={isLoading}
        pagination={false}
        locale={{ emptyText: <Empty description="Sin alertas configuradas todavía" /> }}
      />

      <Modal
        title="Nueva alerta"
        open={modalCrear}
        onCancel={() => setModalCrear(false)}
        onOk={() => form.submit()}
        confirmLoading={crear.isPending}
        okText="Crear"
      >
        <Form form={form} layout="vertical" onFinish={handleCrear} initialValues={{ notificarTelegram: false }}>
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
          <Form.Item name="notificarTelegram" label="Notificar también por Telegram" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={`Probar alerta: ${modalProbar?.keyword ?? ''}`}
        open={modalProbar !== null}
        onCancel={() => { setModalProbar(null); setLicitacionSeleccionada(null); }}
        onOk={handleProbar}
        confirmLoading={probar.isPending}
        okText="Disparar prueba"
        okButtonProps={{ disabled: licitacionSeleccionada == null }}
      >
        <p style={{ color: '#64748b', fontSize: 13, marginBottom: 12 }}>
          Elegí una licitación real existente para simular el disparo de esta alerta — útil para demostrar el sistema sin esperar a que llegue una licitación nueva.
        </p>
        <Select
          style={{ width: '100%' }}
          placeholder="Buscar licitación..."
          showSearch
          optionFilterProp="label"
          value={licitacionSeleccionada}
          onChange={setLicitacionSeleccionada}
          options={licitaciones.map((l) => ({ value: l.id, label: `${l.codigoExterno} — ${l.nombre}` }))}
        />
      </Modal>

      <Modal
        title="Mi Telegram"
        open={modalTelegram}
        onCancel={() => setModalTelegram(false)}
        footer={null}
      >
        <p style={{ color: '#64748b', fontSize: 13, marginBottom: 12 }}>
          Conectá tu Telegram con un clic — se abre el chat del bot con todo listo para apretar "Iniciar".
        </p>
        <Button type="primary" icon={<SendOutlined />} block loading={generarLink.isPending} onClick={handleConectarTelegram} style={{ marginBottom: 20 }}>
          Conectar con Telegram
        </Button>

        <p style={{ color: '#94a3b8', fontSize: 12, marginBottom: 8 }}>
          ¿El botón no te funcionó (ej. estás en un entorno sin webhook público configurado)? Pegá tu Chat ID a mano:
        </p>
        <Form form={formTelegram} layout="vertical" onFinish={handleGuardarTelegram}>
          <Form.Item
            name="telegramChatId"
            label="Chat ID de Telegram"
            rules={[{ required: true, message: 'Ingresá tu Chat ID' }]}
          >
            <Input placeholder="ej. 123456789" />
          </Form.Item>
          <Button htmlType="submit" loading={guardarTelegram.isPending}>Guardar Chat ID manualmente</Button>
        </Form>
      </Modal>
    </Space>
  );
}
