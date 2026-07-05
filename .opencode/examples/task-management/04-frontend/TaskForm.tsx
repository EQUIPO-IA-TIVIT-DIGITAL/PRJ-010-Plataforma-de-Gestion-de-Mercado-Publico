import { useEffect } from 'react';
import { Modal, Form, Input, Select, message } from 'antd';
import type { TaskDetail, CreateTaskRequest, UpdateTaskRequest, TaskFormValues } from '../types';

interface TaskFormProps {
  open: boolean;
  editingItem: TaskDetail | null;
  isSubmitting: boolean;
  onSubmit: (values: CreateTaskRequest | UpdateTaskRequest) => void;
  onCancel: () => void;
}

const priorityOptions = [
  { value: 'LOW', label: 'Low' },
  { value: 'MEDIUM', label: 'Medium' },
  { value: 'HIGH', label: 'High' },
  { value: 'CRITICAL', label: 'Critical' },
];

export default function TaskForm({
  open,
  editingItem,
  isSubmitting,
  onSubmit,
  onCancel,
}: TaskFormProps) {
  const [form] = Form.useForm<TaskFormValues>();
  const isEditing = editingItem !== null;
  const title = isEditing ? 'Edit Task' : 'Create Task';

  useEffect(() => {
    if (open) {
      if (editingItem) {
        form.setFieldsValue({
          title: editingItem.title,
          description: editingItem.description ?? '',
          priority: editingItem.priority,
          assignedTo: editingItem.assignedTo,
        });
      } else {
        form.resetFields();
      }
    }
  }, [open, editingItem, form]);

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      onSubmit(values);
    } catch {
      message.error('Please fix the form errors');
    }
  };

  return (
    <Modal
      title={title}
      open={open}
      onOk={handleOk}
      onCancel={onCancel}
      confirmLoading={isSubmitting}
      destroyOnClose
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={{ priority: 'MEDIUM' }}
      >
        <Form.Item
          name="title"
          label="Title"
          rules={[
            { required: true, message: 'VAL_001 - Title is required' },
            { max: 200, message: 'VAL_008 - Max 200 characters' },
          ]}
        >
          <Input data-testid="input-name" />
        </Form.Item>

        <Form.Item
          name="description"
          label="Description"
          rules={[{ max: 2000, message: 'VAL_008 - Max 2000 characters' }]}
        >
          <Input.TextArea rows={4} data-testid="input-description" />
        </Form.Item>

        <Form.Item
          name="priority"
          label="Priority"
          rules={[{ required: true, message: 'VAL_001 - Priority is required' }]}
        >
          <Select options={priorityOptions} data-testid="select-status" />
        </Form.Item>

        <Form.Item
          name="assignedTo"
          label="Assigned To"
        >
          <Select
            placeholder="Select user"
            allowClear
            showSearch
            optionFilterProp="label"
            data-testid="select-assigned"
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
