import { Typography } from 'antd';

const { Text } = Typography;

interface Props {
  userName: string | null;
}

export function TypingIndicator({ userName }: Props) {
  if (!userName) return null;

  return (
    <div style={{ padding: '8px 16px' }} data-testid="typing-indicator">
      <Text type="secondary" italic>
        {userName} está escribiendo...
      </Text>
    </div>
  );
}
