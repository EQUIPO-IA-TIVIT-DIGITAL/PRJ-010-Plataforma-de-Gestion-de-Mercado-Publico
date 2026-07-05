import { Modal, Form, Input, Select, Button, Spin } from 'antd';
import { useState, useCallback, useMemo, useRef } from 'react';
import { useUsuarios } from '../hooks/useUsuarios';
import type { CrearConversacionRequest, TipoConversacion } from '../types/mensajeria';

interface Props {
  open: boolean;
  onClose: () => void;
  onCreate: (data: CrearConversacionRequest) => void;
  isPending: boolean;
}

export function CrearConversacionModal({ open, onClose, onCreate, isPending }: Props) {
  const [form] = Form.useForm();
  const [tipo, setTipo] = useState<TipoConversacion>('directo');
  const [searchText, setSearchText] = useState('');
  const debounceRef = useRef<ReturnType<typeof setTimeout>>();

  const { data: usuarios = [], isLoading } = useUsuarios(searchText);

  const handleSearch = useCallback((value: string) => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => setSearchText(value), 300);
  }, []);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      onCreate({
        tipo,
        asunto: tipo === 'grupal' ? values.asunto : null,
        licitacionId: values.licitacionId || null,
        participanteIds: values.participanteIds || [],
      });
      form.resetFields();
      setSearchText('');
      onClose();
    } catch (err) {
      /* validation error handled by antd */
    }
  };

  const handleClose = useCallback(() => {
    form.resetFields();
    setSearchText('');
    onClose();
  }, [form, onClose]);

  const selectValue = useMemo(() => {
    if (tipo === 'directo') return undefined;
    return undefined;
  }, [tipo]);

  return (
    <Modal
      title="Nueva conversación"
      open={open}
      onCancel={handleClose}
      destroyOnClose
      footer={[
        <Button key="cancel" onClick={handleClose}>Cancelar</Button>,
        <Button key="submit" type="primary" onClick={handleSubmit} loading={isPending}>Crear</Button>,
      ]}
    >
      <Form form={form} layout="vertical" initialValues={{ participanteIds: [] }}>
        <Form.Item label="Tipo">
          <Select value={tipo} onChange={setTipo}>
            <Select.Option value="directo">Directa (1 a 1)</Select.Option>
            <Select.Option value="grupal">Grupal</Select.Option>
          </Select>
        </Form.Item>
        {tipo === 'grupal' && (
          <Form.Item name="asunto" label="Asunto" rules={[{ required: true, message: 'Asunto requerido' }]}>
            <Input placeholder="Nombre del grupo" />
          </Form.Item>
        )}
        <Form.Item
          name="participanteIds"
          label="Participantes"
          rules={[{ required: true, message: 'Selecciona al menos un participante' }]}
        >
          <Select
            mode={tipo === 'directo' ? undefined : 'multiple'}
            placeholder="Buscar personas..."
            showSearch
            filterOption={false}
            onSearch={handleSearch}
            loading={isLoading}
            notFoundContent={isLoading ? <Spin size="small" /> : 'Sin resultados'}
            value={selectValue}
            style={{ width: '100%' }}
          >
            {usuarios
              .filter(u => u.id.toString() !== (JSON.parse(localStorage.getItem('mpm_user') || '{}').userId || ''))
              .map(u => (
                <Select.Option key={u.id} value={u.id.toString()}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontWeight: 500 }}>{u.nombre}</span>
                    <span style={{ fontSize: 11, color: '#94a3b8' }}>{u.email}</span>
                  </div>
                </Select.Option>
              ))}
          </Select>
        </Form.Item>
        <Form.Item name="licitacionId" label="Vincular a licitación (opcional)">
          <Input type="number" placeholder="ID de licitación" />
        </Form.Item>
      </Form>
    </Modal>
  );
}
