import { useState } from 'react';
import { Button } from 'primereact/button';
import { Card } from 'primereact/card';
import { Badge } from 'primereact/badge';
import { InputTextarea } from 'primereact/inputtextarea';
import { ProgressSpinner } from 'primereact/progressspinner';
import { Tag } from 'primereact/tag';
import { Message } from 'primereact/message';
import type { AiFragmentDto } from '../../api/types';

interface Props {
  fragments: AiFragmentDto[];
  loading: boolean;
  error: string | null;
  videoUrl: string | null;
  onApprove: (id: string, approved: boolean) => void;
  onUpdateCaption: (id: string, description: string) => void;
  onRegenerate: () => void;
  onNext: () => void;
  onBack: () => void;
  regenerating: boolean;
}

function viralBadgeSeverity(score: number | null) {
  if (score == null) return 'secondary' as const;
  if (score >= 0.8) return 'success' as const;
  if (score >= 0.5) return 'warning' as const;
  return 'danger' as const;
}

function viralScoreLabel(score: number | null) {
  if (score == null) return 'N/A';
  return `${Math.round(score * 100)}%`;
}

export default function StepAiReview({
  fragments,
  loading,
  error,
  videoUrl,
  onApprove,
  onUpdateCaption,
  onRegenerate,
  onNext,
  onBack,
  regenerating,
}: Props) {
  const [editingCaptions, setEditingCaptions] = useState<Record<string, string>>({});
  const approvedCount = fragments.filter((f) => f.isApproved).length;

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center py-16 gap-4">
        <ProgressSpinner style={{ width: '48px', height: '48px' }} />
        <p className="text-gray-600">AI is analyzing your video…</p>
        <p className="text-xs text-gray-400">This may take a few minutes</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-2xl mx-auto space-y-4">
        <Message severity="error" text={error} className="w-full" />
        <div className="flex gap-3">
          <Button label="Go Back" icon="pi pi-arrow-left" severity="secondary" onClick={onBack} />
          <Button label="Retry" icon="pi pi-refresh" onClick={onRegenerate} />
        </div>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      {/* Left: Video Preview */}
      <div>
        <h3 className="text-lg font-medium text-gray-900 mb-3">Original Video</h3>
        {videoUrl ? (
          <video
            src={videoUrl}
            controls
            className="w-full rounded-lg bg-black aspect-video"
          />
        ) : (
          <div className="flex items-center justify-center bg-gray-100 rounded-lg aspect-video">
            <span className="text-gray-400">Video preview unavailable</span>
          </div>
        )}
      </div>

      {/* Right: Fragment Clip Cards */}
      <div className="flex flex-col">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-lg font-medium text-gray-900">
            AI Clips ({fragments.length})
          </h3>
          <Button
            label="Regenerate"
            icon="pi pi-refresh"
            severity="secondary"
            size="small"
            loading={regenerating}
            onClick={onRegenerate}
          />
        </div>

        <div className="flex-1 overflow-y-auto space-y-4 max-h-[60vh] pr-1">
          {fragments.length === 0 ? (
            <p className="text-gray-500 text-sm">No clips generated yet.</p>
          ) : (
            fragments.map((frag, idx) => (
              <Card key={frag.id} className="shadow-sm">
                <div className="flex items-start gap-4">
                  {/* Thumbnail placeholder */}
                  <div className="flex-shrink-0 w-24 h-16 bg-gray-200 rounded flex items-center justify-center">
                    <span className="text-xs text-gray-500">Clip {idx + 1}</span>
                  </div>

                  <div className="flex-1 min-w-0 space-y-2">
                    {/* Header: time range + viral score */}
                    <div className="flex items-center justify-between">
                      <span className="text-xs text-gray-500">
                        {formatTime(frag.startTime)} – {formatTime(frag.endTime)}
                      </span>
                      <Badge
                        value={`Viral ${viralScoreLabel(frag.viralScore)}`}
                        severity={viralBadgeSeverity(frag.viralScore)}
                      />
                    </div>

                    {/* Editable caption */}
                    <InputTextarea
                      value={editingCaptions[frag.id] ?? frag.description ?? ''}
                      onChange={(e) =>
                        setEditingCaptions((prev) => ({ ...prev, [frag.id]: e.target.value }))
                      }
                      onBlur={() => {
                        const newCaption = editingCaptions[frag.id];
                        if (newCaption !== undefined && newCaption !== frag.description) {
                          onUpdateCaption(frag.id, newCaption);
                        }
                      }}
                      rows={2}
                      className="w-full text-sm"
                      placeholder="Edit caption…"
                    />

                    {/* Hashtags */}
                    {frag.hashtags && (
                      <div className="flex flex-wrap gap-1">
                        {frag.hashtags.split(/[\s,]+/).filter(Boolean).map((tag) => (
                          <Tag key={tag} value={tag} severity="info" className="text-xs" />
                        ))}
                      </div>
                    )}

                    {/* Approve toggle */}
                    <Button
                      label={frag.isApproved ? 'Approved' : 'Approve'}
                      icon={frag.isApproved ? 'pi pi-check-circle' : 'pi pi-circle'}
                      size="small"
                      severity={frag.isApproved ? 'success' : 'secondary'}
                      outlined={!frag.isApproved}
                      onClick={() => onApprove(frag.id, !frag.isApproved)}
                    />
                  </div>
                </div>
              </Card>
            ))
          )}
        </div>

        {/* Actions */}
        <div className="flex justify-between items-center pt-4 border-t border-gray-100 mt-4">
          <Button
            label="Go Back"
            icon="pi pi-arrow-left"
            severity="secondary"
            onClick={onBack}
          />
          <div className="flex items-center gap-3">
            <span className="text-sm text-gray-500">{approvedCount} clip(s) approved</span>
            <Button
              label="Publish Approved Clips"
              icon="pi pi-send"
              onClick={onNext}
              disabled={approvedCount === 0}
            />
          </div>
        </div>
      </div>
    </div>
  );
}

function formatTime(seconds: number) {
  const m = Math.floor(seconds / 60);
  const s = Math.round(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}
