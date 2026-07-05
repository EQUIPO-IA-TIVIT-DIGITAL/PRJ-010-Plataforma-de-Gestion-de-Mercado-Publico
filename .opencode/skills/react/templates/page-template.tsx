import { PageHeader, Button, Space } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { EntityList } from './components/{Entity}List';
import { EntityForm } from './components/{Entity}Form';
import { use{Feature}Logic } from './hooks/use{Feature}Logic';

export default function {FeatureName}Page() {
  const {
    entities,
    isLoading,
    error,
    pagination,
    modalOpen,
    editingItem,
    isCreating,
    isUpdating,
    handleCreate,
    handleUpdate,
    handleDelete,
    handlePageChange,
    handleOpenCreateModal,
    handleOpenEditModal,
    handleCloseModal,
  } = use{Feature}Logic();

  return (
    <section>
      <PageHeader
        title="{Feature} Management"
        extra={
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={handleOpenCreateModal}
            data-testid="btn-create"
          >
            Create {Entity}
          </Button>
        }
      />

      <EntityList
        data={entities}
        isLoading={isLoading}
        error={error}
        pagination={pagination}
        onPageChange={handlePageChange}
        onEdit={handleOpenEditModal}
        onDelete={handleDelete}
      />

      <EntityForm
        open={modalOpen}
        editingItem={editingItem}
        isSubmitting={isCreating || isUpdating}
        onSubmit={editingItem ? handleUpdate : handleCreate}
        onCancel={handleCloseModal}
      />
    </section>
  );
}
