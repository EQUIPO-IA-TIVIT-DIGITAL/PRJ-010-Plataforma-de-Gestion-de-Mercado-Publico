import { Table, Button, Tag, Space, Spin, Empty, Alert } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { {Entity}ListItem } from '../../types';

interface {Entity}ListProps {
  data: {Entity}ListItem[];
  isLoading: boolean;
  error: Error | null;
  pagination: { page: number; pageSize: number; totalRecords: number };
  onPageChange: (page: number, pageSize: number) => void;
  onEdit: (id: number) => void;
  onDelete: (id: number) => void;
}

const statusColors: Record<string, string> = {
  ACTIVE: 'green',
  DRAFT: 'gold',
  CLOSED: 'red',
};

export default function {Entity}List({
  data,
  isLoading,
  error,
  pagination,
  onPageChange,
  onEdit,
  onDelete,
}: {Entity}ListProps) {
  if (error) {
    return (
      <Alert
        message="Error loading {entities}"
        description={error.message}
        type="error"
        showIcon
      />
    );
  }

  const columns: ColumnsType<{Entity}ListItem> = [
    {
      title: 'ID',
      dataIndex: 'id',
      key: 'id',
      width: 80,
    },
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      sorter: true,
    },
    {
      title: 'Code',
      dataIndex: 'code',
      key: 'code',
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => (
        <Tag color={statusColors[status] || 'default'}>{status}</Tag>
      ),
    },
    {
      title: 'Created Date',
      dataIndex: 'createdDate',
      key: 'createdDate',
      sorter: true,
      render: (date: string) => new Date(date).toLocaleDateString(),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => (
        <Space>
          <Button type="link" onClick={() => onEdit(record.id)}>
            Edit
          </Button>
          <Button type="link" danger onClick={() => onDelete(record.id)}>
            Delete
          </Button>
        </Space>
      ),
    },
  ];

  if (isLoading && data.length === 0) {
    return <Spin size="large" style={{ display: 'block', margin: '48px auto' }} />;
  }

  if (!isLoading && data.length === 0) {
    return <Empty description="No {entities} found" />;
  }

  return (
    <Table
      dataSource={data}
      columns={columns}
      rowKey="id"
      loading={isLoading}
      pagination={{
        current: pagination.page,
        pageSize: pagination.pageSize,
        total: pagination.totalRecords,
        showSizeChanger: true,
        showTotal: (total) => `Total ${total} records`,
      }}
      onChange={(pag) => onPageChange(pag.current ?? 1, pag.pageSize ?? 20)}
    />
  );
}
