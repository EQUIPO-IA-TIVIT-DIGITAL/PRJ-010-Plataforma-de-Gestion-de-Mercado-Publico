import { Drawer, List, Typography, Tag, Button } from 'antd';
import { UserOutlined } from '@ant-design/icons';
import type { ParticipanteItem } from '../types/mensajeria';

const { Text } = Typography;

interface Props {
  open: boolean;
  onClose: () => void;
  participantes: ParticipanteItem[];
}

export function ParticipantesDrawer({ open, onClose, participantes }: Props) {
  return (
    <Drawer title="Participantes" open={open} onClose={onClose} width={400}>
      <List
        dataSource={participantes.filter(p => p.userId !== 'system')}
        renderItem={(p) => (
          <List.Item>
            <List.Item.Meta
              avatar={<UserOutlined />}
              title={p.nombre}
              description={<Tag color={p.rol === 'admin' ? 'blue' : 'default'}>{p.rol}</Tag>}
            />
          </List.Item>
        )}
      />
    </Drawer>
  );
}
