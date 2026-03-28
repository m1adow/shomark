import { useState } from 'react';
import { Card } from 'primereact/card';
import { InputText } from 'primereact/inputtext';
import { Button } from 'primereact/button';
import { ProgressSpinner } from 'primereact/progressspinner';
import { usePostAnalytics } from '../hooks/useAnalytics';

export default function AnalyticsPage() {
  const [postId, setPostId] = useState('');
  const [activePostId, setActivePostId] = useState('');
  const { data: analytics, loading, error } = usePostAnalytics(activePostId);

  const handleLookup = () => {
    if (postId.trim()) setActivePostId(postId.trim());
  };

  return (
    <div>
      <h1 className="text-2xl font-semibold text-gray-900 mb-6">Analytics</h1>

      <Card className="shadow-sm mb-6">
        <div className="flex items-center gap-3">
          <label htmlFor="postId" className="text-sm font-medium text-gray-700">Post ID:</label>
          <InputText
            id="postId"
            value={postId}
            onChange={(e) => setPostId(e.target.value)}
            placeholder="Enter a post GUID"
            className="text-sm"
            style={{ width: '360px' }}
          />
          <Button label="Lookup" icon="pi pi-search" size="small" onClick={handleLookup} />
        </div>
      </Card>

      {activePostId && (
        <>
          {error ? (
            <p className="text-red-600 text-sm">Error: {error}</p>
          ) : loading ? (
            <div className="flex justify-center py-8">
              <ProgressSpinner style={{ width: '40px', height: '40px' }} />
            </div>
          ) : analytics ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <Card className="shadow-sm">
                <div className="text-sm text-gray-500">Views</div>
                <div className="text-3xl font-bold text-gray-900 mt-1">{analytics.views.toLocaleString()}</div>
              </Card>
              <Card className="shadow-sm">
                <div className="text-sm text-gray-500">Likes</div>
                <div className="text-3xl font-bold text-gray-900 mt-1">{analytics.likes.toLocaleString()}</div>
              </Card>
              <Card className="shadow-sm">
                <div className="text-sm text-gray-500">Shares</div>
                <div className="text-3xl font-bold text-gray-900 mt-1">{analytics.shares.toLocaleString()}</div>
              </Card>
              <Card className="shadow-sm">
                <div className="text-sm text-gray-500">Comments</div>
                <div className="text-3xl font-bold text-gray-900 mt-1">{analytics.comments.toLocaleString()}</div>
              </Card>
            </div>
          ) : (
            <p className="text-gray-500 text-sm">No analytics data for this post.</p>
          )}
        </>
      )}
    </div>
  );
}
