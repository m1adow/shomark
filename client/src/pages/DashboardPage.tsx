import { Card } from 'primereact/card';
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import { Tag } from 'primereact/tag';
import { ProgressSpinner } from 'primereact/progressspinner';
import { useVideos } from '../hooks/useVideos';
import { useTags } from '../hooks/useTags';

export default function DashboardPage() {
  const { data: videos, loading: videosLoading, error: videosError } = useVideos();
  const { data: tags, loading: tagsLoading } = useTags();

  return (
    <div>
      <h1 className="text-2xl font-semibold text-gray-900 mb-6">Dashboard</h1>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <Card className="shadow-sm">
          <div className="text-sm text-gray-500">Total Videos</div>
          <div className="text-3xl font-bold text-gray-900 mt-1">
            {videosLoading ? '—' : (videos?.length ?? 0)}
          </div>
        </Card>
        <Card className="shadow-sm">
          <div className="text-sm text-gray-500">Tags Created</div>
          <div className="text-3xl font-bold text-gray-900 mt-1">
            {tagsLoading ? '—' : (tags?.length ?? 0)}
          </div>
        </Card>
        <Card className="shadow-sm">
          <div className="text-sm text-gray-500">Engagement Rate</div>
          <div className="text-3xl font-bold text-gray-900 mt-1">—</div>
          <div className="text-xs text-gray-400 mt-1">Connect analytics to track</div>
        </Card>
        <Card className="shadow-sm">
          <div className="text-sm text-gray-500">Active Campaigns</div>
          <div className="text-3xl font-bold text-gray-900 mt-1">—</div>
          <div className="text-xs text-gray-400 mt-1">Select a user to view</div>
        </Card>
      </div>

      {/* Videos Table */}
      <Card title="Videos" className="shadow-sm">
        {videosError ? (
          <p className="text-red-600 text-sm">Failed to load videos: {videosError}</p>
        ) : videosLoading ? (
          <div className="flex justify-center py-8">
            <ProgressSpinner style={{ width: '40px', height: '40px' }} />
          </div>
        ) : (
          <DataTable value={videos ?? []} paginator rows={10} emptyMessage="No videos found.">
            <Column field="title" header="Title" sortable />
            <Column field="originalFileName" header="File" sortable />
            <Column
              field="durationSeconds"
              header="Duration"
              body={(row) =>
                row.durationSeconds != null
                  ? `${Math.floor(row.durationSeconds / 60)}m ${Math.round(row.durationSeconds % 60)}s`
                  : '—'
              }
              sortable
            />
            <Column
              field="fileSize"
              header="Size"
              body={(row) =>
                row.fileSize != null
                  ? `${(row.fileSize / (1024 * 1024)).toFixed(1)} MB`
                  : '—'
              }
              sortable
            />
            <Column
              field="createdAt"
              header="Uploaded"
              body={(row) => new Date(row.createdAt).toLocaleDateString()}
              sortable
            />
            <Column
              header="Status"
              body={() => <Tag value="Ready" severity="success" />}
            />
          </DataTable>
        )}
      </Card>
    </div>
  );
}
