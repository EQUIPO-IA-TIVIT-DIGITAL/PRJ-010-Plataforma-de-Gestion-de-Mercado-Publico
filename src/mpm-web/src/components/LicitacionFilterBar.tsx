import { Row, Col, Input, Select, DatePicker, Button, Segmented, Checkbox, InputNumber, Tooltip, Tag } from 'antd';
import { SearchOutlined, ClearOutlined, BulbOutlined, FilterOutlined, DollarOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useCatalogos, useAreasNegocio } from '../hooks/useCatalogos';
import type { LicitacionFilter } from '../types/licitacion';

export type SearchMode = 'filtros' | 'inteligente';

interface Props {
  filter: LicitacionFilter;
  onChange: (partial: Partial<LicitacionFilter>) => void;
  onReset?: () => void;
  searchMode: SearchMode;
  onSearchModeChange: (mode: SearchMode) => void;
  naturalQuery: string;
  onNaturalQueryChange: (q: string) => void;
  onNaturalQuerySubmit: () => void;
  /** F1-T5: monto preferido del usuario (para tag discreto). Si filter.montoDesde === preferenciaActiva se muestra "según tu preferencia". */
  preferenciaActiva?: number | null;
}

export function LicitacionFilterBar({
  filter, onChange, onReset,
  searchMode, onSearchModeChange, naturalQuery, onNaturalQueryChange, onNaturalQuerySubmit,
  preferenciaActiva,
}: Props) {
  const { data: catalogos } = useCatalogos();
  const { data: areasNegocio } = useAreasNegocio();

  const areaOptions = (areasNegocio ?? []).map(a => ({
    value: a.codigo,
    label: a.nombre,
  }));

  const estadoOptions = (catalogos?.estadosLicitacion ?? []).map(e => ({
    value: e.codigo,
    label: e.nombre,
  }));

  const tipoOptions = (catalogos?.tiposLicitacion ?? []).map(t => ({
    value: t.codigo,
    label: `${t.nombre} (${t.codigo})`,
  }));

  return (
    <Row gutter={[10, 8]} align="middle">
      <Col xs={24} md="auto">
        <Segmented
          value={searchMode}
          onChange={v => onSearchModeChange(v as SearchMode)}
          options={[
            { label: 'Filtros', value: 'filtros', icon: <FilterOutlined /> },
            { label: 'Búsqueda inteligente', value: 'inteligente', icon: <BulbOutlined /> },
          ]}
          data-testid="filter-search-mode"
        />
      </Col>
      {searchMode === 'inteligente' ? (
        <Col xs={24} sm={12} md={7}>
          <Input
            placeholder="Ej: ciberseguridad para el sector salud, mayores a 10 millones... (Enter para buscar)"
            prefix={<BulbOutlined />}
            allowClear
            value={naturalQuery}
            onChange={e => onNaturalQueryChange(e.target.value)}
            onPressEnter={onNaturalQuerySubmit}
            data-testid="filter-busqueda-natural"
          />
        </Col>
      ) : (
      <Col xs={24} sm={12} md={7}>
        <Input
          placeholder="Buscar por código, nombre o descripción..."
          prefix={<SearchOutlined />}
          allowClear
          value={filter.search ?? ''}
          onChange={e => onChange({ search: e.target.value || undefined })}
          data-testid="filter-busqueda"
        />
      </Col>
      )}
      <Col xs={12} sm={6} md={4}>
        <Select
          placeholder="Estado"
          allowClear
          style={{ width: '100%' }}
          value={filter.estado}
          onChange={v => onChange({ estado: v })}
          data-testid="filter-estado"
          options={estadoOptions}
        />
      </Col>
      <Col xs={12} sm={6} md={4}>
        <Select
          placeholder="Tipo"
          allowClear
          style={{ width: '100%' }}
          value={filter.tipo}
          onChange={v => onChange({ tipo: v })}
          data-testid="filter-tipo"
          options={tipoOptions}
        />
      </Col>
      <Col xs={12} sm={6} md={4}>
        <Select
          placeholder="Área de negocio"
          allowClear
          style={{ width: '100%' }}
          value={filter.area ?? undefined}
          onChange={v => onChange({ area: v, sinClasificar: v ? undefined : filter.sinClasificar })}
          data-testid="filter-area-negocio"
          options={areaOptions}
        />
      </Col>
      <Col xs={12} sm={6} md="auto">
        <Checkbox
          checked={!!filter.sinClasificar}
          disabled={!!filter.area}
          onChange={e => onChange({ sinClasificar: e.target.checked || undefined, area: e.target.checked ? undefined : filter.area })}
          data-testid="filter-sin-clasificar"
        >
          Sin clasificar
        </Checkbox>
      </Col>
      <Col xs={12} sm={6} md={3}>
        <DatePicker
          placeholder="Desde"
          style={{ width: '100%' }}
          value={filter.fechaDesde ? dayjs(filter.fechaDesde) : null}
          onChange={(_date: unknown, dateStr: string | string[]) => onChange({ fechaDesde: (typeof dateStr === 'string' ? dateStr : dateStr[0]) || undefined })}
        />
      </Col>
      <Col xs={12} sm={6} md={3}>
        <DatePicker
          placeholder="Hasta"
          style={{ width: '100%' }}
          value={filter.fechaHasta ? dayjs(filter.fechaHasta) : null}
          onChange={(_date: unknown, dateStr: string | string[]) => onChange({ fechaHasta: (typeof dateStr === 'string' ? dateStr : dateStr[0]) || undefined })}
        />
      </Col>
      <Col xs={12} sm={6} md={3}>
        <Tooltip title="Monto mínimo (ej 50000000 = 50M) - oculta licitaciones por debajo">
          <InputNumber
            placeholder="Monto desde"
            prefix={<DollarOutlined style={{ color: '#94a3b8' }} />}
            style={{ width: '100%' }}
            value={filter.montoDesde ?? null}
            onChange={(v) => onChange({ montoDesde: v ?? undefined })}
            min={0}
            step={1000000}
            formatter={(value) => value ? `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.') : ''}
            parser={(value) => Number(value?.replace(/\./g, '')) as unknown as number}
            data-testid="filter-monto-desde"
          />
        </Tooltip>
        {preferenciaActiva != null && filter.montoDesde === preferenciaActiva && (
          <Tooltip title="Valor aplicado desde tu preferencia guardada. Puedes editarlo o limpiarlo; la preferencia no cambia.">
            <Tag
              color="blue"
              data-testid="tag-preferencia-monto"
              style={{ marginTop: 4, fontSize: 11, lineHeight: '16px', borderRadius: 999, maxWidth: '100%', overflow: 'hidden', textOverflow: 'ellipsis' }}
            >
              según tu preferencia
            </Tag>
          </Tooltip>
        )}
      </Col>
      <Col xs={12} sm={6} md={3}>
        <Tooltip title="Monto máximo">
          <InputNumber
            placeholder="Monto hasta"
            prefix={<DollarOutlined style={{ color: '#94a3b8' }} />}
            style={{ width: '100%' }}
            value={filter.montoHasta ?? null}
            onChange={(v) => onChange({ montoHasta: v ?? undefined })}
            min={0}
            step={1000000}
            formatter={(value) => value ? `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.') : ''}
            parser={(value) => Number(value?.replace(/\./g, '')) as unknown as number}
            data-testid="filter-monto-hasta"
          />
        </Tooltip>
      </Col>
      <Col xs={24} sm={12} md={3}>
        <Button
          icon={<ClearOutlined />}
          onClick={onReset}
          style={{ width: '100%' }}
          data-testid="filter-reset"
        >
          Reiniciar filtros
        </Button>
      </Col>
    </Row>
  );
}
