import { useState, useCallback } from 'react';
import { PageHeader, Button, Modal, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { TaskList } from './TaskList';
import { TaskForm } from './TaskForm';
import { useTaskList } from './useTaskList';
import {
  useCreateTask,
  useUpdateTask,
  useDeleteTask,
  useActivateTask,
  useCompleteTask,
} from './useTaskMutation';
import type { TaskDetail, TaskListParams, CreateTaskRequest, UpdateTaskRequest } from './types';

export default function TasksPage() {
  // Local state
  const [params, setParams] = useState<TaskListParams>({
    page: 1,
    pageSize: 20,
    sortBy: 'CreatedDate',
    sortOrder: 'DESC',
  });
  const [searchFilter, setSearchFilter] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<TaskDetail | null>(null);

  // Queries
  const queryParams = { ...params, searchFilter: searchFilter || undefined };
  const { data, isLoading, error } = useTaskList(queryParams);

  // Mutations
  const { create, isPending: isCreating } = useCreateTask();
  const { update, isPending: isUpdating } = useUpdateTask();
  const { delete: deleteTask, isPending: isDeleting } = useDeleteTask();
  const { activate } = useActivateTask();
  const { complete } = useCompleteTask();

  // Derived data
  const tasks = data?.data?.items ?? [];
  const pagination = data?.data?.pagination ?? { page: 1, pageSize: 20, totalRecords: 0, totalPages: 0 };

  // Handlers
  const handlePageChange = useCallback((page: number, pageSize: number) => {
    setParams((prev) => ({ ...prev, page, pageSize }));
  }, []);

  const handleOpenCreateModal = useCallback(() => {
    setEditingItem(null);
    setModalOpen(true);
  }, []);

  const handleOpenEditModal = useCallback((id: number) => {
    const task = tasks.find((t) => t.taskId === id);
    if (task) {
      setEditingItem(task as unknown as TaskDetail);
      setModalOpen(true);
    }
  }, [tasks]);

  const handleCloseModal = useCallback(() => {
    setModalOpen(false);
    setEditingItem(null);
  }, []);

  const handleCreate = useCallback((values: CreateTaskRequest) => {
    create(values, {
      onSuccess: () => {
        message.success('Task created successfully');
        handleCloseModal();
      },
    });
  }, [create, handleCloseModal]);

  const handleUpdate = useCallback((values: UpdateTaskRequest) => {
    if (!editingItem) return;
    update(editingItem.taskId, values, {
      onSuccess: () => {
        message.success('Task updated successfully');
        handleCloseModal();
      },
    });
  }, [update, editingItem, handleCloseModal]);

  const handleDelete = useCallback((id: number) => {
    Modal.confirm({
      title: 'Delete Task',
      content: 'Are you sure you want to delete this task?',
      onOk: () =>
        deleteTask(id, {
          onSuccess: () => message.success('Task deleted successfully'),
        }),
    });
  }, [deleteTask]);

  const handleActivate = useCallback((id: number) => {
    activate(id, {
      onSuccess: () => message.success('Task activated'),
    });
  }, [activate]);

  const handleComplete = useCallback((id: number) => {
    complete(id, {
      onSuccess: () => message.success('Task completed'),
    });
  }, [complete]);

  return (
    <section>
      <PageHeader
        title="Task Management"
        data-testid="page-title"
        extra={
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={handleOpenCreateModal}
            data-testid="btn-create"
          >
            Create Task
          </Button>
        }
      />

      <TaskList
        data={tasks}
        isLoading={isLoading}
        error={error as Error | null}
        pagination={pagination}
        searchFilter={searchFilter}
        onSearchChange={(value) => {
          setSearchFilter(value);
          setParams((prev) => ({ ...prev, page: 1 }));
        }}
        onPageChange={handlePageChange}
        onEdit={handleOpenEditModal}
        onDelete={handleDelete}
        onActivate={handleActivate}
        onComplete={handleComplete}
      />

      <TaskForm
        open={modalOpen}
        editingItem={editingItem}
        isSubmitting={isCreating || isUpdating}
        onSubmit={editingItem ? handleUpdate : handleCreate}
        onCancel={handleCloseModal}
      />
    </section>
  );
}
