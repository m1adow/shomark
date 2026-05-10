import { useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Button } from 'primereact/button';
import { Card } from 'primereact/card';
import { Tag } from 'primereact/tag';
import { ProgressSpinner } from 'primereact/progressspinner';
import { Toast } from 'primereact/toast';
import { ConfirmDialog, confirmDialog } from 'primereact/confirmdialog';
import { Chart } from 'primereact/chart';
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import { useCampaign, useDeleteCampaign } from '../hooks/useCampaigns';
import { useCampaignAnalytics } from '../hooks/useAnalytics';

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
  const { data: analytics, loading: analyticsLoading } = useCampaignAnalytics(id!);
  const { execute: deleteCampaign } = useDeleteCampaign();
  const toast = useRef<Toast>(null);

  function handleDelete() {
    confirmDialog({
      message: `Are you sure you want to delete "${campaign?.name ?? 'this campaign'}"?`,
      header: 'Delete Campaign',
      icon: 'pi pi-exclamation-triangle',
      acceptClassName: 'p-button-danger',
      accept: async () => {
        try {
          await deleteCampaign(id!);
          navigate('/campaigns');
        } catch {
          toast.current?.show({ severity: 'error', summary: 'Failed to delete campaign', life: 3000 });
        }
      },
    });
  }

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

  // Chart data
  const barChartData = {
    labels: analytics?.platforms.map((p) => p.platform) ?? [],
    datasets: [
      { label: 'Views', data: analytics?.platforms.map((p) => p.views) ?? [], backgroundColor: '#6366f1' },
      { label: 'Likes', data: analytics?.platforms.map((p) => p.likes) ?? [], backgroundColor: '#f59e0b' },
      { label: 'Shares', data: analytics?.platforms.map((p) => p.shares) ?? [], backgroundColor: '#10b981' },
      { label: 'Comments', data: analytics?.platforms.map((p) => p.comments) ?? [], backgroundColor: '#3b82f6' },
    ],
  };

  const lineChartData = {
    labels: analytics?.trend.map((t) => t.date) ?? [],
    datasets: [
      { label: 'Views', data: analytics?.trend.map((t) => t.views) ?? [], borderColor: '#6366f1', fill: false, tension: 0.4 },
      { label: 'Likes', data: analytics?.trend.map((t) => t.likes) ?? [], borderColor: '#f59e0b', fill: false, tension: 0.4 },
      { label: 'Engagement %', data: analytics?.trend.map((t) => t.engagementRate) ?? [], borderColor: '#10b981', fill: false, tension: 0.4 },
    ],
  };

  const chartOptions = { maintainAspectRatio: false, responsive: true };

  return (
    <div>
      <Toast ref={toast} position="top-right" />
      <ConfirmDialog />

      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <Button icon="pi pi-arrow-left" text rounded onClick={() => navigate('/campaigns')} />
          <h1 className="text-2xl font-semibold text-gray-900">{campaign.name ?? 'Untitled Campaign'}</h1>
          <Tag value={campaign.status} severity={statusSeverity(campaign.status)} />
        </div>
        <Button
          label="Delete"
          icon="pi pi-trash"
          severity="danger"
          outlined
          size="small"
          onClick={handleDelete}
        />
      </div>

      {/* Campaign metadata */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
        <Card title="Details" className="shadow-sm">
          <div className="space-y-4">
            <InfoRow label="Status" value={campaign.status} />
            <InfoRow label="Target Audience" value={campaign.targetAudience ?? '—'} />
            <InfoRow label="Description" value={campaign.description ?? '—'} />
            <InfoRow label="Created" value={new Date(campaign.createdAt).toLocaleString()} />
            <InfoRow label="Updated" value={new Date(campaign.updatedAt).toLocaleString()} />
          </div>
        </Card>

        {/* KPI summary cards */}
        <Card title="Performance Overview" className="shadow-sm">
          {analyticsLoading ? (
            <div className="flex justify-center py-8">
              <ProgressSpinner style={{ width: '32px', height: '32px' }} />
            </div>
          ) : (
            <div className="grid grid-cols-2 gap-4">
              <KpiCard icon="pi-eye" label="Views" value={analytics?.summary.totalViews ?? 0} color="indigo" />
              <KpiCard icon="pi-heart" label="Likes" value={analytics?.summary.totalLikes ?? 0} color="amber" />
              <KpiCard icon="pi-share-alt" label="Shares" value={analytics?.summary.totalShares ?? 0} color="emerald" />
              <KpiCard icon="pi-comments" label="Comments" value={analytics?.summary.totalComments ?? 0} color="blue" />
              <KpiCard icon="pi-percentage" label="Engagement" value={`${analytics?.summary.engagementRate ?? 0}%`} color="purple" />
              <KpiCard icon="pi-file" label="Posts" value={analytics?.summary.totalPosts ?? 0} color="gray" />
              <KpiCard icon="pi-check-circle" label="Published" value={analytics?.summary.publishedPosts ?? 0} color="green" />
              <KpiCard icon="pi-clock" label="Scheduled" value={analytics?.summary.scheduledPosts ?? 0} color="orange" />
            </div>
          )}
        </Card>
      </div>

      {/* Charts */}
      {!analyticsLoading && analytics && analytics.platforms.length > 0 && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
          <Card title="Platform Breakdown" className="shadow-sm">
            <div style={{ height: '280px' }}>
              <Chart type="bar" data={barChartData} options={chartOptions} />
            </div>
          </Card>
          <Card title="Trend Over Time" className="shadow-sm">
            <div style={{ height: '280px' }}>
              <Chart type="line" data={lineChartData} options={chartOptions} />
            </div>
          </Card>
        </div>
      )}

      {/* Per-post breakdown table */}
      <Card title="Posts" className="shadow-sm">
        {analyticsLoading ? (
          <div className="flex justify-center py-8">
            <ProgressSpinner style={{ width: '32px', height: '32px' }} />
          </div>
        ) : (
          <DataTable
            value={analytics?.posts ?? []}
            emptyMessage="No posts in this campaign yet."
            sortMode="single"
            stripedRows
            size="small"
          >
            <Column field="title" header="Title" sortable body={(row) => row.title ?? '—'} />
            <Column field="platform" header="Platform" sortable />
            <Column field="status" header="Status" sortable body={(row) => <Tag value={row.status} severity={statusSeverity(row.status)} />} />
            <Column field="publishedAt" header="Published" sortable body={(row) => row.publishedAt ? new Date(row.publishedAt).toLocaleDateString() : '—'} />
            <Column field="views" header="Views" sortable />
            <Column field="likes" header="Likes" sortable />
            <Column field="shares" header="Shares" sortable />
            <Column field="comments" header="Comments" sortable />
            <Column field="engagementRate" header="Eng. %" sortable body={(row) => `${row.engagementRate}%`} />
          </DataTable>
        )}
      </Card>
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

function KpiCard({ icon, label, value, color }: { icon: string; label: string; value: number | string; color: string }) {
  return (
    <div className={`flex items-center gap-3 p-3 rounded-lg bg-${color}-50`}>
      <i className={`pi ${icon} text-xl text-${color}-500`} />
      <div>
        <p className="text-xs text-gray-500">{label}</p>
        <p className="text-lg font-semibold text-gray-800">{value.toLocaleString()}</p>
      </div>
    </div>
  );
}

