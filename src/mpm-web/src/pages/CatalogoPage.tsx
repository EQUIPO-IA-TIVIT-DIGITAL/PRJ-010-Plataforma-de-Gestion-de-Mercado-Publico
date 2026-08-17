import { useState, useMemo } from 'react';
import {
  Alert,
  App as AntdApp,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Spin,
  Table,
  Tabs,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import {
  CheckCircleOutlined,
  DollarOutlined,
  SafetyCertificateOutlined,
  TrophyOutlined,
  FileTextOutlined,
  SyncOutlined,
  PlusOutlined,
  SearchOutlined,
  EditOutlined,
  DeleteOutlined,
  FilePdfOutlined,
} from '@ant-design/icons';
import { useSearchParams } from 'react-router-dom';
import dayjs from 'dayjs';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useCatalogos } from '../hooks/useCatalogos';
import {
  useActualizarExperiencia,
  useCatalogoCapitulos,
  useCatalogoCertificaciones,
  useCatalogoExperiencias,
  useCrearCertificacion,
  useCrearExperiencia,
  useEliminarCertificacion,
  useEliminarExperiencia,
  useSincronizarCertificacionesCensus,
} from '../hooks/usePropuestas';
import type { CatalogoCapitulo, CatalogoCertificacion, CatalogoExperiencia } from '../types/propuestas';
import type { EstadoItem, MonedaItem } from '../types/catalogo';
import type { StatusBadgeVariant } from '../components/StatusBadge';

const ESTADO_VARIANT: Record<number, StatusBadgeVariant> = {
  1: 'info', 2: 'warning', 3: 'neutral', 4: 'error',
  5: 'success', 6: 'neutral', 7: 'tertiary', 8: 'warning',
};

function formatFecha(iso: string | null): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  return new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(d);
}

export function CatalogoPage() {
  const { message } = AntdApp.useApp();
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTabKey = searchParams.get('tab') || 'experiencias';

  const { data: mpData, isLoading: mpLoading } = useCatalogos();
  const experienciasQuery = useCatalogoExperiencias();
  const certificacionesQuery = useCatalogoCertificaciones();
  const capitulosQuery = useCatalogoCapitulos();

  const syncCensusMutation = useSincronizarCertificacionesCensus();
  const crearExpMutation = useCrearExperiencia();
  const actualizarExpMutation = useActualizarExperiencia();
  const eliminarExpMutation = useEliminarExperiencia();
  const crearCertMutation = useCrearCertificacion();
  const eliminarCertMutation = useEliminarCertificacion();

  // Estados locales para filtros y modales
  const [filtroExp, setFiltroExp] = useState('');
  const [filtroCert, setFiltroCert] = useState('');
  const [modalExpVisible, setModalExpVisible] = useState(false);
  const [editingExp, setEditingExp] = useState<CatalogoExperiencia | null>(null);
  const [formExp] = Form.useForm();

  const [modalCertVisible, setModalCertVisible] = useState(false);
  const [formCert] = Form.useForm();

  // ── Casos de Éxito / Experiencias ──────────────────────────────────────────
  const experiencias = experienciasQuery.data?.data?.items ?? [];
  const experienciasFiltradas = useMemo(() => {
    if (!filtroExp.trim()) return experiencias;
    const q = filtroExp.toLowerCase();
    return experiencias.filter(
      (e) =>
        e.cliente.toLowerCase().includes(q) ||
        e.titulo.toLowerCase().includes(q) ||
        (e.descripcion && e.descripcion.toLowerCase().includes(q)),
    );
  }, [experiencias, filtroExp]);

  const abrirModalNuevaExp = () => {
    setEditingExp(null);
    formExp.resetFields();
    setModalExpVisible(true);
  };

  const abrirModalEditarExp = (exp: CatalogoExperiencia) => {
    setEditingExp(exp);
    formExp.setFieldsValue({
      titulo: exp.titulo,
      cliente: exp.cliente,
      pais: exp.pais || 'Chile',
      montoUsd: exp.montoUsd,
      descripcion: exp.descripcion,
      fechas: [exp.fechaInicio ? dayjs(exp.fechaInicio) : null, exp.fechaFin ? dayjs(exp.fechaFin) : null],
    });
    setModalExpVisible(true);
  };

  const handleGuardarExp = async () => {
    try {
      const values = await formExp.validateFields();
      const payload: Partial<CatalogoExperiencia> = {
        titulo: values.titulo,
        cliente: values.cliente,
        pais: values.pais || 'Chile',
        montoUsd: values.montoUsd,
        descripcion: values.descripcion,
        fechaInicio: values.fechas?.[0] ? values.fechas[0].format('YYYY-MM-DD') : null,
        fechaFin: values.fechas?.[1] ? values.fechas[1].format('YYYY-MM-DD') : null,
      };

      if (editingExp) {
        await actualizarExpMutation.mutateAsync({ id: editingExp.id, data: payload });
        message.success('Caso de éxito actualizado correctamente');
      } else {
        await crearExpMutation.mutateAsync(payload);
        message.success('Caso de éxito creado correctamente');
      }
      setModalExpVisible(false);
    } catch (e) {
      if (e instanceof Error) message.error(e.message);
    }
  };

  const handleEliminarExp = async (id: number) => {
    try {
      await eliminarExpMutation.mutateAsync(id);
      message.success('Caso de éxito eliminado del catálogo');
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al eliminar');
    }
  };

  // ── Certificaciones ────────────────────────────────────────────────────────
  const certificaciones = certificacionesQuery.data?.data?.items ?? [];
  const certificacionesFiltradas = useMemo(() => {
    if (!filtroCert.trim()) return certificaciones;
    const q = filtroCert.toLowerCase();
    return certificaciones.filter(
      (c) => c.nombre.toLowerCase().includes(q) || (c.institucion && c.institucion.toLowerCase().includes(q)),
    );
  }, [certificaciones, filtroCert]);

  const handleSincronizarCensus = () => {
    syncCensusMutation.mutate(undefined, {
      onSuccess: (res) => {
        const d = res.data;
        message.success(
          `Sincronización Census completada: ${d?.insertadas ?? 0} nuevas, ${d?.actualizadas ?? 0} actualizadas en ${d?.durationMs ?? 0}ms`,
        );
      },
      onError: (err) => message.error(err instanceof Error ? err.message : 'Error al sincronizar con Census'),
    });
  };

  const handleGuardarCert = async () => {
    try {
      const values = await formCert.validateFields();
      await crearCertMutation.mutateAsync({
        nombre: values.nombre,
        institucion: values.institucion,
        vigencia: values.vigencia,
      });
      message.success('Certificación agregada al catálogo');
      setModalCertVisible(false);
      formCert.resetFields();
    } catch (e) {
      if (e instanceof Error) message.error(e.message);
    }
  };

  const handleEliminarCert = async (id: number) => {
    try {
      await eliminarCertMutation.mutateAsync(id);
      message.success('Certificación eliminada');
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al eliminar');
    }
  };

  const handleVerArchivoCert = async (fileId: string) => {
    try {
      const token = localStorage.getItem('mpm_auth_token') || sessionStorage.getItem('mpm_auth_token');
      const res = await fetch(`/api/v1/censo/certificaciones/archivo/${encodeURIComponent(fileId)}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (!res.ok) throw new Error('No se pudo obtener el archivo');
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al abrir certificación');
    }
  };

  // ── Capítulos DOCX ─────────────────────────────────────────────────────────
  const capitulos = capitulosQuery.data?.data?.items ?? [];

  return (
    <div>
      <PageHeader
        title="Catálogos Corporativos y del Sistema"
        subtitle="Administración de casos de éxito, certificaciones corporativas TIVIT y parámetros del portal Mercado Público."
      />

      <Card size="small">
        <Tabs
          activeKey={activeTabKey}
          onChange={(key) => setSearchParams({ tab: key })}
          items={[
            // TAB 1: CASOS DE ÉXITO Y EXPERIENCIAS
            {
              key: 'experiencias',
              label: (
                <span style={{ fontWeight: 600 }}>
                  <TrophyOutlined style={{ color: '#fa8c16' }} /> Casos de Éxito & Experiencias ({experiencias.length})
                </span>
              ),
              children: (
                <div style={{ padding: '8px 0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, flexWrap: 'wrap', gap: 12 }}>
                    <Input
                      placeholder="Buscar por cliente o proyecto..."
                      prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
                      value={filtroExp}
                      onChange={(e) => setFiltroExp(e.target.value)}
                      style={{ maxWidth: 360 }}
                      allowClear
                    />
                    <Button type="primary" icon={<PlusOutlined />} onClick={abrirModalNuevaExp}>
                      Nuevo Caso de Éxito
                    </Button>
                  </div>

                  <Table<CatalogoExperiencia>
                    size="small"
                    rowKey="id"
                    loading={experienciasQuery.isLoading}
                    dataSource={experienciasFiltradas}
                    pagination={{ pageSize: 10, showSizeChanger: true }}
                    columns={[
                      {
                        title: 'Cliente',
                        dataIndex: 'cliente',
                        key: 'cliente',
                        width: 220,
                        render: (c: string, row) => (
                          <div>
                            <Typography.Text strong style={{ display: 'block' }}>{c}</Typography.Text>
                            <Tag style={{ fontSize: 10 }}>{row.pais || 'Chile'}</Tag>
                          </div>
                        ),
                      },
                      {
                        title: 'Proyecto / Experiencia',
                        dataIndex: 'titulo',
                        key: 'titulo',
                        render: (t: string, row) => (
                          <div>
                            <Typography.Text strong style={{ color: '#0958d9', fontSize: 13 }}>{t}</Typography.Text>
                            {row.descripcion && (
                              <Typography.Paragraph
                                ellipsis={{ rows: 2, expandable: true, symbol: 'ver más' }}
                                style={{ margin: '4px 0 0', color: '#595959', fontSize: 12 }}
                              >
                                {row.descripcion}
                              </Typography.Paragraph>
                            )}
                          </div>
                        ),
                      },
                      {
                        title: 'Período',
                        key: 'periodo',
                        width: 170,
                        render: (_, row) => (
                          <span style={{ fontSize: 12 }}>
                            {formatFecha(row.fechaInicio)} — {formatFecha(row.fechaFin)}
                          </span>
                        ),
                      },
                      {
                        title: 'Monto USD',
                        dataIndex: 'montoUsd',
                        key: 'montoUsd',
                        width: 130,
                        render: (m: number | null) => (
                          <span style={{ fontWeight: 600, fontSize: 12 }}>
                            {m != null ? `$${m.toLocaleString('en-US')}` : '-'}
                          </span>
                        ),
                      },
                      {
                        title: 'Acciones',
                        key: 'acciones',
                        width: 100,
                        render: (_, row) => (
                          <Space size={4}>
                            <Button
                              type="text"
                              size="small"
                              icon={<EditOutlined />}
                              onClick={() => abrirModalEditarExp(row)}
                            />
                            <Popconfirm
                              title="¿Eliminar caso de éxito?"
                              description="Esta acción quitará el caso de éxito del catálogo corporativo."
                              onConfirm={() => handleEliminarExp(row.id)}
                              okText="Sí, eliminar"
                              cancelText="Cancelar"
                            >
                              <Button type="text" danger size="small" icon={<DeleteOutlined />} />
                            </Popconfirm>
                          </Space>
                        ),
                      },
                    ]}
                  />
                </div>
              ),
            },

            // TAB 2: CERTIFICACIONES CORPORATIVAS
            {
              key: 'certificaciones',
              label: (
                <span style={{ fontWeight: 600 }}>
                  <SafetyCertificateOutlined style={{ color: '#722ed1' }} /> Certificaciones TIVIT ({certificaciones.length})
                </span>
              ),
              children: (
                <div style={{ padding: '8px 0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, flexWrap: 'wrap', gap: 12 }}>
                    <Input
                      placeholder="Buscar certificación o institución..."
                      prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
                      value={filtroCert}
                      onChange={(e) => setFiltroCert(e.target.value)}
                      style={{ maxWidth: 360 }}
                      allowClear
                    />
                    <Space>
                      <Button
                        type="primary"
                        icon={<SyncOutlined spin={syncCensusMutation.isPending} />}
                        loading={syncCensusMutation.isPending}
                        onClick={handleSincronizarCensus}
                      >
                        Sincronizar con Census
                      </Button>
                      <Button icon={<PlusOutlined />} onClick={() => setModalCertVisible(true)}>
                        Registrar Certificación
                      </Button>
                    </Space>
                  </div>

                  <Table<CatalogoCertificacion>
                    size="small"
                    rowKey="id"
                    loading={certificacionesQuery.isLoading}
                    dataSource={certificacionesFiltradas}
                    pagination={{ pageSize: 15, showSizeChanger: true }}
                    columns={[
                      {
                        title: 'Nombre de la Certificación',
                        dataIndex: 'nombre',
                        key: 'nombre',
                        render: (n: string, row) => (
                          <Space align="center">
                            <Tag color="purple" style={{ fontWeight: 600 }}>{n}</Tag>
                            {row.fileIdCensus && (
                              <Tooltip title="Ver documento PDF acreditado en Census">
                                <Button
                                  type="link"
                                  size="small"
                                  icon={<FilePdfOutlined style={{ color: '#ff4d4f' }} />}
                                  onClick={() => void handleVerArchivoCert(row.fileIdCensus!)}
                                >
                                  PDF
                                </Button>
                              </Tooltip>
                            )}
                          </Space>
                        ),
                      },
                      {
                        title: 'Institución Emisora',
                        dataIndex: 'institucion',
                        key: 'institucion',
                        width: 220,
                        render: (i: string | null) => i || <Typography.Text type="secondary">-</Typography.Text>,
                      },
                      {
                        title: 'Vigencia',
                        dataIndex: 'vigencia',
                        key: 'vigencia',
                        width: 140,
                        render: (v: string | null) => v || <Typography.Text type="secondary">Permanente</Typography.Text>,
                      },
                      {
                        title: 'Acciones',
                        key: 'acciones',
                        width: 80,
                        render: (_, row) => (
                          <Popconfirm
                            title="¿Eliminar certificación del catálogo?"
                            onConfirm={() => handleEliminarCert(row.id)}
                            okText="Sí, eliminar"
                            cancelText="Cancelar"
                          >
                            <Button type="text" danger size="small" icon={<DeleteOutlined />} />
                          </Popconfirm>
                        ),
                      },
                    ]}
                  />
                </div>
              ),
            },

            // TAB 3: CAPÍTULOS DE LA PROPUESTA DOCX
            {
              key: 'capitulos',
              label: (
                <span style={{ fontWeight: 600 }}>
                  <FileTextOutlined style={{ color: '#1677ff' }} /> Capítulos Propuesta DOCX ({capitulos.length})
                </span>
              ),
              children: (
                <div style={{ padding: '8px 0' }}>
                  <Alert
                    type="info"
                    showIcon
                    message="Estructura estándar de propuestas comerciales"
                    description="Estos capítulos componen la plantilla base de Word (.docx) editable que el sistema genera para cada licitación adjudicable."
                    style={{ marginBottom: 16 }}
                  />
                  <Table<CatalogoCapitulo>
                    size="small"
                    rowKey="id"
                    loading={capitulosQuery.isLoading}
                    dataSource={capitulos}
                    pagination={false}
                    columns={[
                      { title: 'Orden', dataIndex: 'orden', key: 'orden', width: 80, render: (o: number) => <Tag color="blue">#{o}</Tag> },
                      { title: 'Título del Capítulo', dataIndex: 'titulo', key: 'titulo', render: (t: string) => <Typography.Text strong>{t}</Typography.Text> },
                      {
                        title: 'Estado',
                        dataIndex: 'activo',
                        key: 'activo',
                        width: 120,
                        render: (a: boolean) => <Tag color={a ? 'success' : 'default'}>{a ? 'Activo' : 'Inactivo'}</Tag>,
                      },
                    ]}
                  />
                </div>
              ),
            },

            // TAB 4: PARÁMETROS MERCADO PÚBLICO
            {
              key: 'portal',
              label: (
                <span style={{ fontWeight: 500 }}>
                  <CheckCircleOutlined style={{ color: '#10b981' }} /> Parámetros Mercado Público
                </span>
              ),
              children: (
                <div style={{ padding: '8px 0' }}>
                  <Typography.Title level={5} style={{ marginBottom: 12 }}>
                    Estados de Licitación en Mercado Público
                  </Typography.Title>
                  <Table<EstadoItem>
                    size="small"
                    rowKey="codigo"
                    loading={mpLoading}
                    dataSource={mpData?.estados ?? []}
                    pagination={false}
                    style={{ marginBottom: 24 }}
                    columns={[
                      { title: 'Nombre', dataIndex: 'nombre', key: 'nombre', render: (v: string) => <span style={{ fontWeight: 500 }}>{v}</span> },
                      {
                        title: 'Estado',
                        key: 'tag',
                        width: 180,
                        render: (_, record) => <StatusBadge variant={ESTADO_VARIANT[record.codigo] ?? 'neutral'} label={record.nombre} />,
                      },
                    ]}
                  />

                  <Typography.Title level={5} style={{ marginBottom: 12 }}>
                    Monedas Reconocidas
                  </Typography.Title>
                  <Table<MonedaItem>
                    size="small"
                    rowKey="codigo"
                    loading={mpLoading}
                    dataSource={mpData?.monedas ?? []}
                    pagination={false}
                    columns={[
                      { title: 'Nombre', dataIndex: 'nombre', key: 'nombre' },
                      { title: 'Símbolo', dataIndex: 'simbolo', key: 'simbolo', width: 100, align: 'center', render: (s: string) => <strong>{s}</strong> },
                      { title: 'Código ISO', dataIndex: 'codigoIso', key: 'codigoIso', width: 120, render: (iso: string) => <Tag color="green">{iso}</Tag> },
                    ]}
                  />
                </div>
              ),
            },
          ]}
        />
      </Card>

      {/* Modal Crear / Editar Experiencia */}
      <Modal
        open={modalExpVisible}
        title={editingExp ? 'Editar Caso de Éxito' : 'Registrar Nuevo Caso de Éxito'}
        okText={editingExp ? 'Guardar Cambios' : 'Crear Caso de Éxito'}
        cancelText="Cancelar"
        onOk={handleGuardarExp}
        onCancel={() => setModalExpVisible(false)}
        confirmLoading={crearExpMutation.isPending || actualizarExpMutation.isPending}
        width={650}
      >
        <Form form={formExp} layout="vertical" style={{ marginTop: 16 }}>
          <Form.Item
            label="Cliente / Organismo"
            name="cliente"
            rules={[{ required: true, message: 'Ingresa el nombre del cliente' }]}
          >
            <Input placeholder="Ej.: Banco Santander, Ministerio de Salud, Cencosud" />
          </Form.Item>

          <Form.Item
            label="Título del Proyecto / Caso de Éxito"
            name="titulo"
            rules={[{ required: true, message: 'Ingresa el título del proyecto' }]}
          >
            <Input placeholder="Ej.: Migración Cloud Multi-Región y Administración 24/7" />
          </Form.Item>

          <Space style={{ display: 'flex' }} align="start">
            <Form.Item label="País" orientation="vertical" name="pais" initialValue="Chile" style={{ minWidth: 160 }}>
              <Select options={['Chile', 'Brasil', 'Perú', 'Colombia', 'Argentina', 'México'].map((p) => ({ value: p, label: p }))} />
            </Form.Item>
            <Form.Item label="Monto Referencial (USD)" name="montoUsd" style={{ minWidth: 200 }}>
              <InputNumber style={{ width: '100%' }} min={0} placeholder="Ej: 500000" />
            </Form.Item>
            <Form.Item label="Período (Inicio / Fin)" name="fechas">
              <DatePicker.RangePicker format="DD/MM/YYYY" placeholder={['Inicio', 'Término']} />
            </Form.Item>
          </Space>

          <Form.Item
            label="Alcance y Tecnologías Utilizadas"
            name="descripcion"
            rules={[{ required: true, message: 'Describe el alcance del proyecto' }]}
          >
            <Input.TextArea
              rows={4}
              placeholder="Describe los entregables, tecnologías clave (Linux, AWS, Kubernetes, etc.) y resultados obtenidos..."
              maxLength={2000}
              showCount
            />
          </Form.Item>
        </Form>
      </Modal>

      {/* Modal Registrar Certificación Manual */}
      <Modal
        open={modalCertVisible}
        title="Registrar Certificación Corporativa"
        okText="Registrar"
        cancelText="Cancelar"
        onOk={handleGuardarCert}
        onCancel={() => setModalCertVisible(false)}
        confirmLoading={crearCertMutation.isPending}
      >
        <Form form={formCert} layout="vertical" style={{ marginTop: 16 }}>
          <Form.Item
            label="Nombre de la Certificación"
            name="nombre"
            rules={[{ required: true, message: 'Ingresa el nombre oficial de la certificación' }]}
          >
            <Input placeholder="Ej.: ISO/IEC 27001 Seguridad de la Información, AWS Partner Advanced Tier" />
          </Form.Item>
          <Form.Item label="Institución Emisora" name="institucion">
            <Input placeholder="Ej.: Bureau Veritas, Amazon Web Services, Microsoft" />
          </Form.Item>
          <Form.Item label="Vigencia / Período" name="vigencia">
            <Input placeholder="Ej.: 2024 - 2027 o Permanente" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
