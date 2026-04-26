import { useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import { Tag } from 'primereact/tag';
import { Button } from 'primereact/button';
import { Card } from 'primereact/card';
import { ProgressSpinner } from 'primereact/progressspinner';
import { Toast } from 'primereact/toast';
import { ConfirmDialog, confirmDialog } from 'primereact/confirmdialog';
import { useMyCampaigns, useDeleteCampaign } from '../hooks/useCampaigns';
import type { CampaignDto } from '../api';

function statusSeverity(status: string) {
  switch (status) {
    case 'Active': return 'success' as const;
    case 'Draft': return 'warning' as const;
    case 'Completed': return 'info' as const;
    case 'Archived': return 'secondary' as const;
    default: return undefined;
  }
}

export default function CampaignsPage() {
  const navigate = useNavigate();
  const { data: campaigns, loading, error, refetch } = useMyCampaigns();
  const { execute: deleteCampaign, loading: deleteLoading } = useDeleteCampaign();
  const toast = useRef<Toast>(null);

  function handleDelete(id: string) {
    confirmDialog({
      message: 'Are you sure you want to delete this campaign?',
      header: 'Delete Campaign',
      icon: 'pi pi-exclamation-triangle',
      acceptClassName: 'p-button-danger',
      accept: async () => {
        try {
          await deleteCampaign(id);
          toast.current?.show({ severity: 'success', summary: 'Campaign deleted', life: 3000 });
          refetch();
        } catch {
          toast.current?.show({ severity: 'error', summary: 'Failed to delete campaign', life: 3000 });
        }
      },
    });
  }

  return (
    <div>
      <Toast ref={toast} position="top-right" />
      <ConfirmDialog />

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-gray-900">Campaigns</h1>
        <Button label="New Campaign" icon="pi pi-plus" size="small" onClick={() => navigate('/campaigns/create')} />
      </div>

      <Card className="shadow-sm mb-4">
        {error ? (
          <p className="text-red-600 text-sm">Error: {error}</p>
        ) : loading ? (
          <div className="flex justify-center py-8">
            <ProgressSpinner style={{ width: '40px', height: '40px' }} />
          </div>
        ) : (
          <DataTable
            value={campaigns ?? []}
            paginator
            rows={10}
            emptyMessage="No campaigns found."
            selectionMode="single"
            onRowSelect={(e) => {
              const row = e.data as CampaignDto;
              if (row.status === 'Draft') {
                navigate(`/campaigns/${row.id}/edit`);
              } else {
                navigate(`/campaigns/${row.id}`);
              }
            }}
            rowClassName={() => 'cursor-pointer'}
          >
            <Column field="name" header="Name" sortable body={(row: CampaignDto) => row.name ?? 'Untitled'} />
            <Column
              field="status"
              header="Status"
              sortable
              body={(row: CampaignDto) => <Tag value={row.status} severity={statusSeverity(row.status)} />}
            />
            <Column
              field="createdAt"
              header="Created"
              sortable
              body={(row: CampaignDto) => new Date(row.createdAt).toLocaleDateString()}
            />
            <Column
              field="updatedAt"
              header="Updated"
              sortable
              body={(row: CampaignDto) => new Date(row.updatedAt).toLocaleDateString()}
            />
            <Column
              header=""
              style={{ width: '4rem' }}
              body={(row: CampaignDto) => (
                <Button
                  icon="pi pi-trash"
                  severity="danger"
                  text
                  rounded
                  size="small"
                  loading={deleteLoading}
                  onClick={(e) => {
                    e.stopPropagation();
                    handleDelete(row.id);
                  }}
                />
              )}
            />
          </DataTable>
        )}
      </Card>
    </div>
  );
}
