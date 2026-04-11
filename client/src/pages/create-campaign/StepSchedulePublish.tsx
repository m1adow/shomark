import { useState, useMemo } from 'react';
import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Checkbox } from 'primereact/checkbox';
import { Card } from 'primereact/card';
import { Tag } from 'primereact/tag';
import { Message } from 'primereact/message';
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
  const [calendarMonth, setCalendarMonth] = useState(new Date());

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

  // Build a map: dateString -> Set of platformIds that have posts
  const scheduledDatePlatformMap = useMemo(() => {
    const map = new Map<string, Set<string>>();
    scheduledPosts.forEach((p) => {
      if (p.scheduledAt) {
        const key = new Date(p.scheduledAt).toDateString();
        if (!map.has(key)) map.set(key, new Set());
        map.get(key)!.add(p.platformId);
      }
    });
    return map;
  }, [scheduledPosts]);

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
                      <i className={platformIcon(p.platformType)} />
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
            <Calendar
              value={calendarMonth}
              onChange={(e) => setCalendarMonth(e.value as Date)}
              inline
              selectionMode="single"
              readOnlyInput
              className="w-full schedule-calendar pointer-events-none-dates"
              dateTemplate={(date) => {
                const d = new Date(date.year, date.month, date.day);
                const platformIds = scheduledDatePlatformMap.get(d.toDateString());
                const hasPost = !!platformIds && platformIds.size > 0;
                return (
                  <div className="flex flex-col items-center">
                    <span className={hasPost ? 'font-bold text-blue-600' : ''}>
                      {date.day}
                    </span>
                    {hasPost && (
                      <div className="flex gap-0.5 mt-0.5">
                        {Array.from(platformIds).slice(0, 4).map((pid) => {
                          const platform = platforms.find((pl) => pl.id === pid);
                          return (
                            <span
                              key={pid}
                              className={`block w-1.5 h-1.5 rounded-full ${platformDotColor(platform?.platformType)}`}
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
          </Card>

          {/* Upcoming scheduled posts list */}
          {scheduledPosts.length > 0 && (
            <Card className="shadow-sm mt-4">
              <h4 className="text-sm font-medium text-gray-700 mb-3">Upcoming</h4>
              <div className="space-y-2">
                {scheduledPosts
                  .filter((p) => p.scheduledAt)
                  .sort(
                    (a, b) =>
                      new Date(a.scheduledAt!).getTime() - new Date(b.scheduledAt!).getTime(),
                  )
                  .slice(0, 10)
                  .map((p) => {
                    const platform = platforms.find((pl) => pl.id === p.platformId);
                    return (
                      <div
                        key={p.id}
                        className="flex items-center justify-between p-2 bg-gray-50 rounded-lg"
                      >
                        <div className="flex items-center gap-2 min-w-0">
                          {platform && (
                            <i className={platformIcon(platform.platformType)} />
                          )}
                          <span className="text-sm text-gray-700 truncate">
                            {p.title ?? 'Untitled'}
                          </span>
                        </div>
                        <div className="flex items-center gap-2 flex-shrink-0">
                          <Tag value={p.status} severity="info" className="text-xs" />
                          <span className="text-xs text-gray-500">
                            {new Date(p.scheduledAt!).toLocaleString()}
                          </span>
                        </div>
                      </div>
                    );
                  })}
              </div>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}

function platformIcon(type: string) {
  switch (type) {
    case 'Instagram': return 'pi pi-instagram text-pink-500';
    case 'TikTok': return 'pi pi-tiktok text-gray-900';
    case 'YouTube': return 'pi pi-youtube text-red-600';
    case 'X': return 'pi pi-twitter text-gray-800';
    case 'LinkedIn': return 'pi pi-linkedin text-blue-700';
    case 'Telegram': return 'pi pi-telegram text-blue-500';
    default: return 'pi pi-globe text-gray-500';
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
