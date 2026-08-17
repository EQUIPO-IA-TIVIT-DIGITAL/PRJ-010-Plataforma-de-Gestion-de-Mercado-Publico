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
  Table,
  Tabs,
  Tag,
  Tooltip,
  Typography,
  Upload,
} from 'antd';
import {
  CheckCircleOutlined,
  DollarOutlined,
  TrophyOutlined,
  FileTextOutlined,
  PlusOutlined,
  SearchOutlined,
  EditOutlined,
  DeleteOutlined,
  FilePdfOutlined,
  BankOutlined,
  UploadOutlined,
} from '@ant-design/icons';
import { useSearchParams } from 'react-router-dom';
import dayjs from 'dayjs';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { useCatalogos } from '../hooks/useCatalogos';
import { apiDownload, apiPostForm } from '../lib/apiClient';
import {
  useActualizarExperiencia,
  useCatalogoCapitulos,
  useCatalogoCertificaciones,
  useCatalogoExperiencias,
  useCrearCertificacion,
  useCrearExperiencia,
  useEliminarCertificacion,
  useEliminarExperiencia,
} from '../hooks/usePropuestas';
import type { CatalogoCapitulo, CatalogoCertificacion, CatalogoExperiencia } from '../types/propuestas';
import type { EstadoItem, MonedaItem, TipoLicitacionItem } from '../types/catalogo';
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
  const certsCorporativasQuery = useCatalogoCertificaciones('corporativa');
  const capitulosQuery = useCatalogoCapitulos();

  const crearExpMutation = useCrearExperiencia();
  const actualizarExpMutation = useActualizarExperiencia();
  const eliminarExpMutation = useEliminarExperiencia();
  const crearCertMutation = useCrearCertificacion();
  const eliminarCertMutation = useEliminarCertificacion();

  // Estados locales para filtros y modales
  const [filtroExp, setFiltroExp] = useState('');
  const [filtroCertCorp, setFiltroCertCorp] = useState('');
  const [subiendoCertId, setSubiendoCertId] = useState<number | null>(null);

  const [modalExpVisible, setModalExpVisible] = useState(false);
  const [editingExp, setEditingExp] = useState<CatalogoExperiencia | null>(null);
  const [formExp] = Form.useForm();

  const [modalCertCorpVisible, setModalCertCorpVisible] = useState(false);
  const [formCertCorp] = Form.useForm();

  const tiposAgrupados = useMemo(() => {
    const vistos = new Set<string>();
    return (mpData?.tiposLicitacion ?? []).filter((t) => {
      if (vistos.has(t.nombre)) return false;
      vistos.add(t.nombre);
      return true;
    });
  }, [mpData?.tiposLicitacion]);

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

  // ── Certificaciones Corporativas TIVIT (Empresa) ──────────────────────────
  const certsCorporativas = certsCorporativasQuery.data?.data?.items ?? [];
  const certsCorporativasFiltradas = useMemo(() => {
    if (!filtroCertCorp.trim()) return certsCorporativas;
    const q = filtroCertCorp.toLowerCase();
    return certsCorporativas.filter(
      (c) =>
        c.nombre.toLowerCase().includes(q) ||
        (c.institucion && c.institucion.toLowerCase().includes(q)) ||
        (c.titular && c.titular.toLowerCase().includes(q)),
    );
  }, [certsCorporativas, filtroCertCorp]);

  const handleGuardarCertCorp = async () => {
    try {
      const values = await formCertCorp.validateFields();
      await crearCertMutation.mutateAsync({
        nombre: values.nombre,
        institucion: values.institucion,
        vigencia: values.vigencia,
        titular: values.titular || 'TIVIT SpA',
        tipo: 'corporativa',
      });
      message.success('Certificación corporativa agregada al catálogo');
      setModalCertCorpVisible(false);
      formCertCorp.resetFields();
      void certsCorporativasQuery.refetch();
    } catch (e) {
      if (e instanceof Error) message.error(e.message);
    }
  };

  const handleEliminarCert = async (id: number) => {
    try {
      await eliminarCertMutation.mutateAsync(id);
      message.success('Certificación eliminada');
      void certsCorporativasQuery.refetch();
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al eliminar');
    }
  };

  const handleVerArchivoCert = async (fileId: string) => {
    try {
      const blob = await apiDownload(`/api/v1/censo/certificaciones/archivo/${encodeURIComponent(fileId)}`);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al abrir certificación PDF');
    }
  };

  const handleSubirPdfOficial = async (certId: number, file: File) => {
    try {
      setSubiendoCertId(certId);
      const formData = new FormData();
      formData.append('file', file);
      await apiPostForm(`/api/v1/propuestas/catalogos/certificaciones/${certId}/archivo`, formData);
      message.success(`PDF oficial adjuntado correctamente: ${file.name}`);
      void certsCorporativasQuery.refetch();
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Error al subir el archivo PDF');
    } finally {
      setSubiendoCertId(null);
    }
  };

  // ── Capítulos DOCX ─────────────────────────────────────────────────────────
  const capitulos = capitulosQuery.data?.data?.items ?? [];

  return (
    <div>
      <PageHeader
        title="Catálogos Corporativos y del Sistema"
        subtitle="Administración de casos de éxito, acreditaciones oficiales de empresa, plantilla de propuestas y parámetros Mercado Público."
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

            // TAB 2: CERTIFICACIONES CORPORATIVAS TIVIT (EMPRESA)
            {
              key: 'certificaciones-empresa',
              label: (
                <span style={{ fontWeight: 600 }}>
                  <BankOutlined style={{ color: '#1677ff' }} /> Certificaciones Empresa ({certsCorporativas.length})
                </span>
              ),
              children: (
                <div style={{ padding: '8px 0' }}>
                  <Alert
                    type="info"
                    showIcon
                    message="Acreditaciones Oficiales de TIVIT como Organización"
                    description="Catálogo de acreditaciones institucionales (ISO 27001, ISO 9001, Tier III, PCI-DSS, Partner Tiers). El equipo comercial puede adjuntar el documento PDF original escaneado/firmado de cada certificación para incluirlo en las ofertas."
                    style={{ marginBottom: 16 }}
                  />
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, flexWrap: 'wrap', gap: 12 }}>
                    <Input
                      placeholder="Buscar certificación institucional o entidad..."
                      prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
                      value={filtroCertCorp}
                      onChange={(e) => setFiltroCertCorp(e.target.value)}
                      style={{ maxWidth: 360 }}
                      allowClear
                    />
                    <Button type="primary" icon={<PlusOutlined />} onClick={() => setModalCertCorpVisible(true)}>
                      Nueva Certificación Empresa
                    </Button>
                  </div>

                  <Table<CatalogoCertificacion>
                    size="small"
                    rowKey="id"
                    loading={certsCorporativasQuery.isLoading}
                    dataSource={certsCorporativasFiltradas}
                    pagination={{ pageSize: 12, showSizeChanger: true }}
                    columns={[
                      {
                        title: 'Certificación / Acreditación',
                        dataIndex: 'nombre',
                        key: 'nombre',
                        render: (n: string) => (
                          <Tag color="blue" style={{ fontWeight: 700, fontSize: 12, padding: '2px 8px' }}>
                            {n}
                          </Tag>
                        ),
                      },
                      {
                        title: 'Titular Certificado',
                        dataIndex: 'titular',
                        key: 'titular',
                        width: 180,
                        render: (t: string | null) => (
                          <Tag color="cyan" style={{ fontWeight: 500 }}>
                            <BankOutlined style={{ marginRight: 4 }} />
                            {t || 'TIVIT SpA'}
                          </Tag>
                        ),
                      },
                      {
                        title: 'Casa Certificadora / Emisora',
                        dataIndex: 'institucion',
                        key: 'institucion',
                        width: 180,
                        render: (i: string | null) => i || <Typography.Text type="secondary">-</Typography.Text>,
                      },
                      {
                        title: 'Vigencia',
                        dataIndex: 'vigencia',
                        key: 'vigencia',
                        width: 130,
                        render: (v: string | null) => v || <Typography.Text type="secondary">Permanente</Typography.Text>,
                      },
                      {
                        title: 'Documento PDF Oficial',
                        key: 'pdf',
                        width: 240,
                        render: (_, row) => {
                          const estaSubiendo = subiendoCertId === row.id;
                          return (
                            <Space size={6} wrap>
                              {row.fileIdCensus ? (
                                <>
                                  <Button
                                    type="primary"
                                    size="small"
                                    icon={<FilePdfOutlined />}
                                    style={{ background: '#1677ff', color: '#ffffff', fontWeight: 500 }}
                                    onClick={() => void handleVerArchivoCert(row.fileIdCensus!)}
                                  >
                                    Ver Certificado PDF
                                  </Button>
                                  <Upload
                                    accept=".pdf,application/pdf"
                                    showUploadList={false}
                                    beforeUpload={(file) => {
                                      void handleSubirPdfOficial(row.id, file);
                                      return false;
                                    }}
                                  >
                                    <Tooltip title="Reemplazar archivo PDF oficial">
                                      <Button size="small" icon={<UploadOutlined />} loading={estaSubiendo} />
                                    </Tooltip>
                                  </Upload>
                                </>
                              ) : (
                                <>
                                  <Tag color="default" style={{ fontSize: 11 }}>Sin PDF adjunto</Tag>
                                  <Upload
                                    accept=".pdf,application/pdf"
                                    showUploadList={false}
                                    beforeUpload={(file) => {
                                      void handleSubirPdfOficial(row.id, file);
                                      return false;
                                    }}
                                  >
                                    <Button
                                      size="small"
                                      type="dashed"
                                      icon={<UploadOutlined />}
                                      loading={estaSubiendo}
                                      style={{ fontSize: 11 }}
                                    >
                                      Adjuntar PDF
                                    </Button>
                                  </Upload>
                                </>
                              )}
                            </Space>
                          );
                        },
                      },
                      {
                        title: 'Acciones',
                        key: 'acciones',
                        width: 60,
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
                    Estados de Licitación en Mercado Público ({mpData?.estadosLicitacion?.length ?? 0})
                  </Typography.Title>
                  <Table<EstadoItem>
                    size="small"
                    rowKey="codigo"
                    loading={mpLoading}
                    dataSource={mpData?.estadosLicitacion ?? []}
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
                    Tipos de Licitación ({tiposAgrupados.length})
                  </Typography.Title>
                  <Table<TipoLicitacionItem>
                    size="small"
                    rowKey="codigo"
                    loading={mpLoading}
                    dataSource={tiposAgrupados}
                    pagination={false}
                    style={{ marginBottom: 24 }}
                    columns={[
                      { title: 'Nombre', dataIndex: 'nombre', key: 'nombre', render: (v: string) => <span style={{ fontWeight: 500 }}>{v}</span> },
                      { title: 'Código', dataIndex: 'codigo', key: 'codigo', width: 120, render: (c: string) => <Tag color="blue">{c}</Tag> },
                    ]}
                  />

                  <Typography.Title level={5} style={{ marginBottom: 12 }}>
                    Monedas Reconocidas ({mpData?.monedas?.length ?? 0})
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

      {/* Modal Registrar Certificación Corporativa (Empresa) */}
      <Modal
        open={modalCertCorpVisible}
        title="Registrar Certificación Oficial de la Empresa"
        okText="Registrar"
        cancelText="Cancelar"
        onOk={handleGuardarCertCorp}
        onCancel={() => setModalCertCorpVisible(false)}
        confirmLoading={crearCertMutation.isPending}
      >
        <Form form={formCertCorp} layout="vertical" style={{ marginTop: 16 }}>
          <Form.Item
            label="Nombre de la Certificación / Acreditación"
            name="nombre"
            rules={[{ required: true, message: 'Ingresa el nombre oficial de la certificación' }]}
          >
            <Input placeholder="Ej.: ISO/IEC 27001:2022 Seguridad de la Información" />
          </Form.Item>
          <Form.Item
            label="Titular / Razón Social Certificada"
            name="titular"
            initialValue="TIVIT SpA"
            rules={[{ required: true, message: 'Ingresa la razón social certificada' }]}
          >
            <Input placeholder="Ej.: TIVIT SpA / TIVIT Latam" />
          </Form.Item>
          <Form.Item label="Casa Certificadora / Entidad Emisora" name="institucion">
            <Input placeholder="Ej.: Bureau Veritas, AENOR, Uptime Institute, AWS" />
          </Form.Item>
          <Form.Item label="Vigencia / Período" name="vigencia">
            <Input placeholder="Ej.: 2024 - 2027 o Permanente" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
