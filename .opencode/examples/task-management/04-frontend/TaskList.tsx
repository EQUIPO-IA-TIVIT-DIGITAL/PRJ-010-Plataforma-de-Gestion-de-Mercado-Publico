import { Table, Tag, Space, Button, Spin, Empty, Alert, Input } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { SearchOutlined } from '@ant-design/icons';
import type { TaskListItem, Pagination } from '../types';

const statusColors: Record<string, string> = {
  DRAFT: 'default',
  ACTIVE: 'blue',
  CLOSED: 'green',
};

const priorityColors: Record<string, string> = {
  LOW: 'green',
  MEDIUM: 'gold',
  HIGH: 'orange',
  CRITICAL: 'red',
};

interface TaskListProps {
  data: TaskListItem[];
  isLoading: boolean;
  error: Error | null;
  pagination: Pagination;
  searchFilter: string;
  onSearchChange: (value: string) => void;
  onPageChange: (page: number, pageSize: number) => void;
  onEdit: (id: number) => void;
  onDelete: (id: number) => void;
  onActivate: (id: number) => void;
  onComplete: (id: number) => void;
}

export default function TaskList({
  data,
  isLoading,
  error,
  pagination,
  searchFilter,
  onSearchChange,
  onPageChange,
  onEdit,
  onDelete,
  onActivate,
  onComplete,
}: TaskListProps) {
  if (error) {
    return (
      <Alert message="Error loading tasks" description={error.message} type="error" showIcon />
    );
  }

  const columns: ColumnsType<TaskListItem> = [
    {
      title: 'ID',
      dataIndex: 'taskId',
      key: 'taskId',
      width: 70,
    },
    {
      title: 'Title',
      dataIndex: 'title',
      key: 'title',
      sorter: true,
    },
    {
      title: 'Priority',
      dataIndex: 'priority',
      key: 'priority',
      sorter: true,
      render: (p: string) => (
        <Tag color={priorityColors[p] || 'default'}>{p}</Tag>
      ),
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      sorter: true,
      render: (s: string) => (
        <Tag color={statusColors[s] || 'default'}>{s}</Tag>
      ),
    },
    {
      title: 'Assigned To',
      dataIndex: 'assignedToName',
      key: 'assignedToName',
    },
    {
      title: 'Created Date',
      dataIndex: 'createdDate',
      key: 'createdDate',
      sorter: true,
      render: (d: string) => new Date(d).toLocaleDateString(),
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 240,
      render: (_, record) => (
        <Space>
          <Button type="link" data-testid="btn-edit" onClick={() => onEdit(record.taskId)}>
            Edit
          </Button>
          {record.status === 'DRAFT' && (
            <Button type="link" onClick={() => onActivate(record.taskId)}>
              Activate
            </Button>
          )}
          {record.status === 'ACTIVE' && (
            <Button type="link" onClick={() => onComplete(record.taskId)}>
              Complete
            </Button>
          )}
          <Button type="link" danger data-testid="btn-delete" onClick={() => onDelete(record.taskId)}>
            Delete
          </Button>
        </Space>
      ),
    },
  ];

  if (isLoading && data.length === 0) {
    return <Spin size="large" style={{ display: 'block', margin: '48px auto' }} />;
  }

  return (
    <div>
      <Input
        placeholder="Search tasks..."
        prefix={<SearchOutlined />}
        value={searchFilter}
        onChange={(e) => onSearchChange(e.target.value)}
        style={{ marginBottom: 16, width: 300 }}
        data-testid="input-search"
        allowClear
      />

      {!isLoading && data.length === 0 ? (
        <Empty description="No tasks found" />
      ) : (
        <Table
          dataSource={data}
          columns={columns}
          rowKey="taskId"
          loading={isLoading}
          data-testid="data-table"
          pagination={{
            current: pagination.page,
            pageSize: pagination.pageSize,
            total: pagination.totalRecords,
            showSizeChanger: true,
            showTotal: (total) => `Total ${total} tasks`,
          }}
          onChange={(pag) => onPageChange(pag.current ?? 1, pag.pageSize ?? 20)}
        />
      )}
    </div>
  );
}
