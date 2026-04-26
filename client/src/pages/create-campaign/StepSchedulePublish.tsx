import { useState, useMemo } from 'react';
import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Checkbox } from 'primereact/checkbox';
import { Card } from 'primereact/card';
import { Tag } from 'primereact/tag';
import { Message } from 'primereact/message';
import { SocialIcon } from 'react-social-icons';
import type { AiFragmentDto, PlatformDto, PostDto } from '../../api/types';
import { useFragmentThumbnailUrl } from '../../hooks/useFragments';

function FragmentThumb({ fragmentId }: { fragmentId: string }) {
  const { data } = useFragmentThumbnailUrl(fragmentId);
  return (
    <div className="w-full aspect-video rounded-lg overflow-hidden bg-gray-200">
      {data?.url ? (
        <img src={data.url} alt="" className="w-full h-full object-cover" loading="lazy" />
      ) : (
        <div className="flex items-center justify-center w-full h-full">
          <i className="pi pi-image text-gray-400 text-2xl" />
        </div>
      )}
    </div>
  );
}

interface Props {
  approvedFragments: AiFragmentDto[];
  platforms: PlatformDto[];
  platformsLoading: boolean;
  scheduledPosts: PostDto[];
  onSchedule: (platformIds: string[], scheduledAt: Date) => void;
  onPublishNow: (platformIds: string[]) => void;
  onBack: () => void;
  onSaveAsDraft: () => void;
  publishing: boolean;
  publishError: string | null;
}

export default function StepSchedulePublish({
  approvedFragments,
  platforms,
  platformsLoading,
  scheduledPosts,
  onSchedule,
  onPublishNow,
  onBack,
  onSaveAsDraft,
  publishing,
  publishError,
}: Props) {
  const [selectedPlatformIds, setSelectedPlatformIds] = useState<string[]>([]);
  const [scheduledDate, setScheduledDate] = useState<Date | null>(null);
  const [scheduledTime, setScheduledTime] = useState<Date | null>(null);

  const combinedDateTime = useMemo(() => {
    if (!scheduledDate) return null;
    const dt = new Date(scheduledDate);
    if (scheduledTime) {
      dt.setHours(scheduledTime.getHours(), scheduledTime.getMinutes(), 0, 0);
    }
    return dt;
  }, [scheduledDate, scheduledTime]);

  const canSchedule = selectedPlatformIds.length > 0 && combinedDateTime !== null && !publishing;
  const canPublish = selectedPlatformIds.length > 0 && !publishing;

  // Build draft preview rows for the Upcoming list (one per selected platform)
  const draftRows = useMemo(() => {
    if (!scheduledDate || selectedPlatformIds.length === 0) return [];
    return selectedPlatformIds.map((pid) => ({
      id: `draft-${pid}`,
      platformId: pid,
      title: 'This post (draft)',
      status: 'Draft',
      scheduledAt: combinedDateTime?.toISOString() ?? scheduledDate.toISOString(),
      isDraft: true,
    }));
  }, [scheduledDate, combinedDateTime, selectedPlatformIds]);

  // Build a map: dateString -> { platformIds, isPreDraft }
  const scheduledDatePlatformMap = useMemo(() => {
    const map = new Map<string, { platformIds: Set<string>; isPreDraft: boolean }>();
    scheduledPosts.forEach((p) => {
      if (p.scheduledAt) {
        const key = new Date(p.scheduledAt).toDateString();
        if (!map.has(key)) map.set(key, { platformIds: new Set(), isPreDraft: false });
        map.get(key)!.platformIds.add(p.platformId);
      }
    });
    // Add pre-draft entry for the user-selected date + platforms (UI-only)
    if (scheduledDate && selectedPlatformIds.length > 0) {
      const key = scheduledDate.toDateString();
      if (!map.has(key)) map.set(key, { platformIds: new Set(), isPreDraft: true });
      const entry = map.get(key)!;
      selectedPlatformIds.forEach((id) => entry.platformIds.add(id));
      // Mark as pre-draft only if all entries came from local selection
      if (entry.platformIds.size === selectedPlatformIds.length) {
        entry.isPreDraft = true;
      }
    }
    return map;
  }, [scheduledPosts, scheduledDate, selectedPlatformIds]);

  const fragment = approvedFragments[0] ?? null;

  function togglePlatform(id: string) {
    setSelectedPlatformIds((prev) =>
      prev.includes(id) ? prev.filter((p) => p !== id) : [...prev, id],
    );
  }

  return (
    <div>
      {/* Top bar: Go Back + Save as Draft */}
      <div className="flex items-center justify-end gap-3 mb-6">
        <Button
          label="Go Back"
          icon="pi pi-arrow-left"
          severity="secondary"
          outlined
          onClick={onBack}
        />
        <Button
          label="Save as Draft"
          icon="pi pi-save"
          severity="secondary"
          onClick={onSaveAsDraft}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left: Post Card (1/3) */}
        <div>
          <Card className="shadow-sm">
            {/* Thumbnail */}
            {fragment && <FragmentThumb fragmentId={fragment.id} />}

            {/* Description */}
            {fragment && (
              <div className="mt-4">
                <p className="text-sm text-gray-700">
                  {fragment.description ?? 'No caption'}
                </p>
                {fragment.hashtags && (
                  <p className="text-xs text-blue-600 mt-1">{fragment.hashtags}</p>
                )}
              </div>
            )}

            {/* Platform Selection */}
            <div className="mt-5">
              <h4 className="text-sm font-medium text-gray-900 mb-2">Platforms</h4>
              {platformsLoading ? (
                <p className="text-sm text-gray-500">Loading…</p>
              ) : platforms.length === 0 ? (
                <Message
                  severity="warn"
                  text="No platforms connected. Go to Settings to add."
                  className="w-full"
                />
              ) : (
                <div className="space-y-2">
                  {platforms.map((p) => (
                    <label
                      key={p.id}
                      className="flex items-center gap-3 p-2 rounded-lg border border-gray-200 cursor-pointer hover:bg-gray-50 transition-colors"
                    >
                      <Checkbox
                        checked={selectedPlatformIds.includes(p.id)}
                        onChange={() => togglePlatform(p.id)}
                      />
                      <SocialIcon
                        network={platformNetwork(p.platformType)}
                        style={{ width: 24, height: 24 }}
                      />
                      <span className="text-sm font-medium text-gray-900">
                        {p.platformType}
                      </span>
                      {p.accountName && (
                        <span className="text-xs text-gray-500">@{p.accountName}</span>
                      )}
                    </label>
                  ))}
                </div>
              )}
            </div>

            {/* Date & Time Pickers */}
            <div className="mt-5">
              <h4 className="text-sm font-medium text-gray-900 mb-2">Schedule Date & Time</h4>
              <div className="flex flex-col gap-3">
                <Calendar
                  value={scheduledDate}
                  onChange={(e) => setScheduledDate(e.value as Date | null)}
                  minDate={new Date()}
                  placeholder="Pick date"
                  dateFormat="dd/mm/yy"
                  showIcon
                  className="w-full"
                />
                <Calendar
                  value={scheduledTime}
                  onChange={(e) => setScheduledTime(e.value as Date | null)}
                  timeOnly
                  hourFormat="24"
                  placeholder="Pick time"
                  showIcon
                  icon="pi pi-clock"
                  className="w-full"
                />
              </div>
            </div>

            {publishError && (
              <Message severity="error" text={publishError} className="w-full mt-4" />
            )}

            {/* Action Buttons */}
            <div className="flex flex-col gap-2 mt-5">
              <Button
                label="Schedule Post"
                icon="pi pi-calendar"
                onClick={() => combinedDateTime && onSchedule(selectedPlatformIds, combinedDateTime)}
                disabled={!canSchedule}
                loading={publishing}
                className="w-full"
              />
              <Button
                label="Publish Now"
                icon="pi pi-send"
                severity="secondary"
                outlined
                onClick={() => onPublishNow(selectedPlatformIds)}
                disabled={!canPublish}
                loading={publishing}
                className="w-full"
              />
            </div>
          </Card>
        </div>

        {/* Right: Calendar View (2/3) */}
        <div className="lg:col-span-2 flex flex-col">
          <Card className="shadow-sm flex-1 schedule-card">
            <h3 className="text-lg font-medium text-gray-900 mb-4">Scheduled Posts</h3>
            <div className="pointer-events-none">
            <Calendar
              value={scheduledDate}
              onChange={() => {}}
              inline
              minDate={new Date()}
              className="w-full schedule-calendar"
              dateTemplate={(date) => {
                const d = new Date(date.year, date.month, date.day);
                const entry = scheduledDatePlatformMap.get(d.toDateString());
                const hasPost = !!entry && entry.platformIds.size > 0;
                return (
                  <div className="flex flex-col items-center">
                    <span className={hasPost ? 'font-bold text-blue-600' : ''}>
                      {date.day}
                    </span>
                    {hasPost && (
                      <div className="flex gap-0.5 mt-0.5">
                        {Array.from(entry.platformIds).slice(0, 4).map((pid) => {
                          const platform = platforms.find((pl) => pl.id === pid);
                          return (
                            <span
                              key={pid}
                              className={`block w-1.5 h-1.5 rounded-full ${platformDotColor(platform?.platformType)} ${entry.isPreDraft ? 'pre-draft-dot' : ''}`}
                              title={platform?.platformType ?? 'Unknown'}
                            />
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              }}
            />
            </div>
          </Card>

          {/* Upcoming scheduled posts list — always shown when there are draft or real scheduled posts */}
          {(draftRows.length > 0 || scheduledPosts.some((p) => p.scheduledAt)) && ((() => {
            type UpcomingRow = {
              id: string;
              platformId: string;
              title: string | null | undefined;
              status: string;
              scheduledAt: string;
              isDraft: boolean;
            };
            const realRows: UpcomingRow[] = scheduledPosts
              .filter((p): p is typeof p & { scheduledAt: string } => !!p.scheduledAt)
              .map((p) => ({ ...p, isDraft: false, status: p.status ?? '' }));
            const merged = [...realRows, ...draftRows]
              .sort((a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime())
              .slice(0, 12);
            return (
            <Card className="shadow-sm mt-4">
              <h4 className="text-sm font-medium text-gray-700 mb-3">Upcoming</h4>
              <div className="space-y-2">
                {merged.map((p) => {
                  const platform = platforms.find((pl) => pl.id === p.platformId);
                  return (
                    <div
                      key={p.id}
                      className={`flex items-center justify-between p-2 rounded-lg border-l-4 ${
                        p.isDraft
                          ? 'bg-amber-50 border-amber-400 border border-dashed'
                          : 'bg-gray-50 border-blue-400'
                      }`}
                    >
                      <div className="flex items-center gap-2 min-w-0">
                        {platform && (
                          <SocialIcon
                            network={platformNetwork(platform.platformType)}
                            style={{ width: 20, height: 20 }}
                          />
                        )}
                        <span className={`text-sm truncate ${p.isDraft ? 'text-amber-800 italic' : 'text-gray-700'}`}>
                          {p.title ?? 'Untitled'}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 flex-shrink-0">
                        <Tag
                          value={p.status}
                          severity={p.isDraft ? 'warning' : 'info'}
                          className="text-xs"
                        />
                        <span className="text-xs text-gray-500">
                          {new Date(p.scheduledAt).toLocaleString()}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            </Card>
          );
          })())}
        </div>
      </div>
    </div>
  );
}

function platformNetwork(type: string): string {
  switch (type) {
    case 'Instagram': return 'instagram';
    case 'TikTok': return 'tiktok';
    case 'YouTube': return 'youtube';
    case 'X': return 'x';
    case 'LinkedIn': return 'linkedin';
    case 'Telegram': return 'telegram';
    default: return 'sharethis';
  }
}

function platformDotColor(type?: string) {
  switch (type) {
    case 'Instagram': return 'bg-pink-500';
    case 'TikTok': return 'bg-gray-900';
    case 'YouTube': return 'bg-red-600';
    case 'X': return 'bg-gray-800';
    case 'LinkedIn': return 'bg-blue-700';
    case 'Telegram': return 'bg-blue-500';
    default: return 'bg-gray-400';
  }
}
