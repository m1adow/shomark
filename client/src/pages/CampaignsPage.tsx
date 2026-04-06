import { useNavigate } from 'react-router-dom';
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import { Tag } from 'primereact/tag';
import { Button } from 'primereact/button';
import { Card } from 'primereact/card';
import { ProgressSpinner } from 'primereact/progressspinner';
import { useMyCampaigns } from '../hooks/useCampaigns';
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
  const { data: campaigns, loading, error } = useMyCampaigns();

  return (
    <div>
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
          <DataTable value={campaigns ?? []} paginator rows={10} emptyMessage="No campaigns found.">
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
          </DataTable>
        )}
      </Card>
    </div>
  );
}
