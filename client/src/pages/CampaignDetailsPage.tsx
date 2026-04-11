import { useParams, useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Card } from 'primereact/card';
import { Tag } from 'primereact/tag';
import { ProgressSpinner } from 'primereact/progressspinner';
import { useCampaign } from '../hooks/useCampaigns';

function statusSeverity(status: string) {
  switch (status) {
    case 'Active': return 'success' as const;
    case 'Draft': return 'warning' as const;
    case 'Completed': return 'info' as const;
    case 'Archived': return 'secondary' as const;
    default: return undefined;
  }
}

export default function CampaignDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: campaign, loading, error } = useCampaign(id!);

  if (loading) {
    return (
      <div className="flex justify-center py-16">
        <ProgressSpinner style={{ width: '48px', height: '48px' }} />
      </div>
    );
  }

  if (error || !campaign) {
    return (
      <div className="space-y-4">
        <p className="text-red-600">
          {error ?? 'Campaign not found.'}
        </p>
        <Button label="Back to Campaigns" icon="pi pi-arrow-left" severity="secondary" onClick={() => navigate('/campaigns')} />
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <Button icon="pi pi-arrow-left" text rounded onClick={() => navigate('/campaigns')} />
          <h1 className="text-2xl font-semibold text-gray-900">{campaign.name ?? 'Untitled Campaign'}</h1>
          <Tag value={campaign.status} severity={statusSeverity(campaign.status)} />
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card title="Details" className="shadow-sm">
          <div className="space-y-4">
            <InfoRow label="Status" value={campaign.status} />
            <InfoRow label="Target Audience" value={campaign.targetAudience ?? '—'} />
            <InfoRow label="Description" value={campaign.description ?? '—'} />
            <InfoRow label="Created" value={new Date(campaign.createdAt).toLocaleString()} />
            <InfoRow label="Updated" value={new Date(campaign.updatedAt).toLocaleString()} />
          </div>
        </Card>

        <Card title="Performance" className="shadow-sm">
          <div className="flex flex-col items-center justify-center py-8 text-gray-400">
            <i className="pi pi-chart-bar text-4xl mb-3" />
            <p className="text-sm">Analytics coming soon</p>
          </div>
        </Card>
      </div>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between">
      <span className="text-sm text-gray-500">{label}</span>
      <span className="text-sm font-medium text-gray-900">{value}</span>
    </div>
  );
}
