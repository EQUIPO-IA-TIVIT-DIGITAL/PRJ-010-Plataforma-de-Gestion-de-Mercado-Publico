import { Row, Col, Input, Select, DatePicker, Button } from 'antd';
import { SearchOutlined, ClearOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useCatalogos } from '../hooks/useCatalogos';
import type { LicitacionFilter } from '../types/licitacion';

interface Props {
  filter: LicitacionFilter;
  onChange: (partial: Partial<LicitacionFilter>) => void;
  onReset?: () => void;
}

export function LicitacionFilterBar({ filter, onChange, onReset }: Props) {
  const { data: catalogos } = useCatalogos();

  const estadoOptions = (catalogos?.estadosLicitacion ?? []).map(e => ({
    value: e.codigo,
    label: e.nombre,
  }));

  const tipoOptions = (catalogos?.tiposLicitacion ?? []).map(t => ({
    value: t.slug,
    label: t.nombre,
  }));

  return (
    <Row gutter={[10, 8]} align="middle">
      <Col xs={24} sm={12} md={7}>
        <Input
          placeholder="Buscar por código o nombre..."
          prefix={<SearchOutlined />}
          allowClear
          value={filter.search ?? ''}
          onChange={e => onChange({ search: e.target.value || undefined })}
          data-testid="filter-busqueda"
        />
      </Col>
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
