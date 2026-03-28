import { useState, useMemo } from 'react';
import { Button } from 'primereact/button';
import { Calendar } from 'primereact/calendar';
import { Checkbox } from 'primereact/checkbox';
import { Card } from 'primereact/card';
import { Tag } from 'primereact/tag';
import { Message } from 'primereact/message';
import type { AiFragmentDto, PlatformDto, PostDto } from '../../api/types';

interface Props {
  approvedFragments: AiFragmentDto[];
  platforms: PlatformDto[];
  platformsLoading: boolean;
  scheduledPosts: PostDto[];
  onSchedule: (platformIds: string[], scheduledAt: Date) => void;
  onPublishNow: (platformIds: string[]) => void;
  onBack: () => void;
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
  publishing,
  publishError,
}: Props) {
  const [selectedPlatformIds, setSelectedPlatformIds] = useState<string[]>([]);
  const [scheduledDate, setScheduledDate] = useState<Date | null>(null);
  const [calendarMonth, setCalendarMonth] = useState(new Date());

  const canSchedule = selectedPlatformIds.length > 0 && scheduledDate !== null && !publishing;
  const canPublish = selectedPlatformIds.length > 0 && !publishing;

  // Build a map of dates that have scheduled posts for calendar highlight
  const scheduledDatesSet = useMemo(() => {
    const set = new Set<string>();
    scheduledPosts.forEach((p) => {
      if (p.scheduledAt) {
        set.add(new Date(p.scheduledAt).toDateString());
      }
    });
    return set;
  }, [scheduledPosts]);

  function togglePlatform(id: string) {
    setSelectedPlatformIds((prev) =>
      prev.includes(id) ? prev.filter((p) => p !== id) : [...prev, id],
    );
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      {/* Left: Post Preview + Controls */}
      <div className="space-y-6">
        {/* Approved clips summary */}
        <div>
          <h3 className="text-lg font-medium text-gray-900 mb-3">
            Posts to Publish ({approvedFragments.length} clips)
          </h3>
          <div className="space-y-2 max-h-40 overflow-y-auto">
            {approvedFragments.map((frag, idx) => (
              <div key={frag.id} className="flex items-center gap-3 p-2 bg-gray-50 rounded-lg">
                <span className="text-xs font-medium text-gray-500 w-14">Clip {idx + 1}</span>
                <span className="text-sm text-gray-700 truncate flex-1">
                  {frag.description ?? 'No caption'}
                </span>
                {frag.hashtags && (
                  <span className="text-xs text-blue-600 truncate max-w-40">{frag.hashtags}</span>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Platform Selection */}
        <div>
          <h3 className="text-lg font-medium text-gray-900 mb-3">Platforms</h3>
          {platformsLoading ? (
            <p className="text-sm text-gray-500">Loading platforms…</p>
          ) : platforms.length === 0 ? (
            <Message
              severity="warn"
              text="No platforms connected. Go to Settings to add platforms."
              className="w-full"
            />
          ) : (
            <div className="space-y-2">
              {platforms.map((p) => (
                <label
                  key={p.id}
                  className="flex items-center gap-3 p-3 rounded-lg border border-gray-200 cursor-pointer hover:bg-gray-50"
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

        {/* Date/Time Picker */}
        <div>
          <h3 className="text-lg font-medium text-gray-900 mb-3">Schedule</h3>
          <Calendar
            value={scheduledDate}
            onChange={(e) => setScheduledDate(e.value as Date | null)}
            showTime
            hourFormat="24"
            minDate={new Date()}
            placeholder="Pick date & time"
            className="w-full"
          />
        </div>

        {publishError && <Message severity="error" text={publishError} className="w-full" />}

        {/* Actions */}
        <div className="flex flex-wrap gap-3 pt-2">
          <Button
            label="Go Back"
            icon="pi pi-arrow-left"
            severity="secondary"
            onClick={onBack}
          />
          <Button
            label="Schedule"
            icon="pi pi-calendar"
            onClick={() => scheduledDate && onSchedule(selectedPlatformIds, scheduledDate)}
            disabled={!canSchedule}
            loading={publishing}
          />
          <Button
            label="Publish Now"
            icon="pi pi-send"
            severity="success"
            onClick={() => onPublishNow(selectedPlatformIds)}
            disabled={!canPublish}
            loading={publishing}
          />
        </div>
      </div>

      {/* Right: Calendar View */}
      <div>
        <h3 className="text-lg font-medium text-gray-900 mb-3">Scheduled Posts</h3>
        <Card className="shadow-sm">
          <Calendar
            value={calendarMonth}
            onChange={(e) => setCalendarMonth(e.value as Date)}
            inline
            className="w-full"
            dateTemplate={(date) => {
              const d = new Date(date.year, date.month, date.day);
              const hasPost = scheduledDatesSet.has(d.toDateString());
              return (
                <span className={hasPost ? 'font-bold text-blue-600' : ''}>
                  {date.day}
                  {hasPost && <span className="block w-1.5 h-1.5 rounded-full bg-blue-600 mx-auto mt-0.5" />}
                </span>
              );
            }}
          />
        </Card>

        {/* Upcoming scheduled posts list */}
        {scheduledPosts.length > 0 && (
          <div className="mt-4 space-y-2">
            <h4 className="text-sm font-medium text-gray-700">Upcoming</h4>
            {scheduledPosts
              .filter((p) => p.scheduledAt)
              .sort((a, b) => new Date(a.scheduledAt!).getTime() - new Date(b.scheduledAt!).getTime())
              .slice(0, 10)
              .map((p) => (
                <div key={p.id} className="flex items-center justify-between p-2 bg-gray-50 rounded">
                  <span className="text-sm text-gray-700 truncate">{p.title ?? 'Untitled'}</span>
                  <div className="flex items-center gap-2">
                    <Tag value={p.status} severity="info" className="text-xs" />
                    <span className="text-xs text-gray-500">
                      {new Date(p.scheduledAt!).toLocaleString()}
                    </span>
                  </div>
                </div>
              ))}
          </div>
        )}
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
