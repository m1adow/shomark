import { useState, useRef } from 'react';
import { Button } from 'primereact/button';
import { Card } from 'primereact/card';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { Message } from 'primereact/message';
import { motion, AnimatePresence } from 'framer-motion';
import type { AiFragmentDto } from '../../api/types';
import { useFragmentThumbnailUrl } from '../../hooks/useFragments';

function FragmentThumbnail({ fragmentId, index }: { fragmentId: string; index: number }) {
  const { data } = useFragmentThumbnailUrl(fragmentId);
  const [imgLoaded, setImgLoaded] = useState(false);

  return (
    <div className="relative w-full h-full">
      {/* Shimmer skeleton shown until image loads */}
      {(!data?.url || !imgLoaded) && (
        <div className="absolute inset-0 flex items-center justify-center bg-gray-100 animate-pulse">
          <svg viewBox="0 0 24 24" className="w-10 h-10 text-gray-300" fill="currentColor">
            <path d="M21 3H3C2 3 1 4 1 5v14c0 1.1.9 2 2 2h18c1 0 2-1 2-2V5c0-1-1-2-2-2zm0 16H3V5h18v14zm-9-2l-4-5-3 4h14l-5-6-2 3z" />
          </svg>
        </div>
      )}
      {data?.url && (
        <motion.img
          src={data.url}
          alt={`Clip ${index + 1}`}
          className="absolute inset-0 w-full h-full object-cover"
          loading="lazy"
          onLoad={() => setImgLoaded(true)}
          initial={{ opacity: 0 }}
          animate={{ opacity: imgLoaded ? 1 : 0 }}
          transition={{ duration: 0.4 }}
        />
      )}
    </div>
  );
}

function VideoPlayer({ url }: { url: string | null }) {
  const [videoReady, setVideoReady] = useState(false);

  const showPlaceholder = !url || !videoReady;

  return (
    <div className="relative w-full aspect-video rounded-xl overflow-hidden">
      {/* Placeholder — shown while loading or when URL is unavailable */}
      <AnimatePresence>
        {showPlaceholder && (
          <motion.div
            key="video-placeholder"
            className="absolute inset-0 flex flex-col items-center justify-center gap-3"
            style={{ background: 'linear-gradient(135deg, #1e1b4b 0%, #312e81 50%, #1e40af 100%)' }}
            initial={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.5 }}
          >
            {url ? (
              /* URL exists but video hasn't loaded yet */
              <>
                <motion.div
                  className="w-16 h-16 rounded-full bg-white/10 flex items-center justify-center"
                  animate={{ scale: [1, 1.1, 1], opacity: [0.6, 1, 0.6] }}
                  transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
                >
                  <i className="pi pi-play text-white text-xl ml-1" />
                </motion.div>
                <p className="text-white/60 text-sm">Loading video…</p>
              </>
            ) : (
              /* No URL at all */
              <>
                <div className="w-16 h-16 rounded-full bg-white/10 flex items-center justify-center">
                  <i className="pi pi-video text-white/40 text-2xl" />
                </div>
                <p className="text-white/40 text-sm">Video preview unavailable</p>
              </>
            )}
          </motion.div>
        )}
      </AnimatePresence>

      {url && (
        <motion.video
          src={url}
          controls
          className="absolute inset-0 w-full h-full object-contain bg-black"
          onLoadedData={() => setVideoReady(true)}
          initial={{ opacity: 0 }}
          animate={{ opacity: videoReady ? 1 : 0 }}
          transition={{ duration: 0.5 }}
        />
      )}
    </div>
  );
}

function HashtagEditor({
  hashtags,
  onChange,
}: {
  hashtags: string;
  onChange: (value: string) => void;
}) {
  const tags = hashtags ? hashtags.split(/[\s,]+/).filter(Boolean) : [];
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [editValue, setEditValue] = useState('');
  const [addingNew, setAddingNew] = useState(false);
  const [newValue, setNewValue] = useState('');
  const newInputRef = useRef<HTMLInputElement>(null);

  const save = (newTags: string[]) => onChange(newTags.join(' '));

  const startEdit = (idx: number) => {
    setEditingIndex(idx);
    setEditValue(tags[idx]);
  };

  const commitEdit = () => {
    if (editingIndex === null) return;
    const trimmed = editValue.trim();
    const newTags = [...tags];
    if (trimmed) {
      newTags[editingIndex] = trimmed.startsWith('#') ? trimmed : `#${trimmed}`;
    } else {
      newTags.splice(editingIndex, 1);
    }
    setEditingIndex(null);
    save(newTags);
  };

  const removeTag = (idx: number) => {
    save(tags.filter((_, i) => i !== idx));
  };

  const commitNew = () => {
    const trimmed = newValue.trim();
    if (trimmed) {
      save([...tags, trimmed.startsWith('#') ? trimmed : `#${trimmed}`]);
    }
    setAddingNew(false);
    setNewValue('');
  };

  return (
    <div className="flex flex-wrap items-center gap-1.5 min-h-[28px]">
      {tags.map((tag, idx) =>
        editingIndex === idx ? (
          <InputText
            key={idx}
            autoFocus
            value={editValue}
            onChange={(e) => setEditValue(e.target.value)}
            onBlur={commitEdit}
            onKeyDown={(e) => {
              if (e.key === 'Enter') commitEdit();
              if (e.key === 'Escape') setEditingIndex(null);
            }}
            className="text-xs !py-0.5 !px-2 w-24 rounded-full"
          />
        ) : (
          <span
            key={idx}
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 text-xs font-medium"
          >
            <span className="cursor-pointer hover:text-blue-900" onClick={() => startEdit(idx)}>{tag}</span>
            <button
              onClick={() => removeTag(idx)}
              className="hover:text-red-500 leading-none"
            >
              <i className="pi pi-times" style={{ fontSize: '9px' }} />
            </button>
          </span>
        ),
      )}
      {addingNew ? (
        <InputText
          ref={newInputRef}
          autoFocus
          value={newValue}
          onChange={(e) => setNewValue(e.target.value)}
          onBlur={commitNew}
          onKeyDown={(e) => {
            if (e.key === 'Enter') commitNew();
            if (e.key === 'Escape') { setAddingNew(false); setNewValue(''); }
          }}
          className="text-xs !py-0.5 !px-2 w-24"
          placeholder="#tag"
        />
      ) : (
        <button
          onClick={() => setAddingNew(true)}
          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full border border-dashed border-gray-300 text-gray-400 text-xs hover:border-blue-400 hover:text-blue-500 transition-colors"
        >
          <i className="pi pi-plus" style={{ fontSize: '9px' }} />
          Add tag
        </button>
      )}
    </div>
  );
}

function formatTime(seconds: number) {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.round(seconds % 60);
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
}

interface Props {
  fragments: AiFragmentDto[];
  loading: boolean;
  error: string | null;
  videoUrl: string | null;
  onApprove: (id: string) => Promise<void>;
  onUpdateCaption: (id: string, description: string) => void;
  onUpdateHashtags: (id: string, hashtags: string) => void;
  onRegenerate: () => void;
  regenerating: boolean;
}

function viralScoreColor(score: number) {
  if (score >= 8) return 'text-green-600';
  if (score >= 5) return 'text-yellow-600';
  return 'text-red-500';
}

export default function StepAiReview({
  fragments,
  loading,
  error,
  videoUrl,
  onApprove,
  onUpdateCaption,
  onUpdateHashtags,
  onRegenerate,
  regenerating,
}: Props) {
  const [editingCaptions, setEditingCaptions] = useState<Record<string, string>>({});
  const [editingHashtags, setEditingHashtags] = useState<Record<string, string>>({});
  const [approvingId, setApprovingId] = useState<string | null>(null);

  const handleApproveClick = async (id: string) => {
    setApprovingId(id);
    try {
      await onApprove(id);
    } finally {
      setApprovingId(null);
    }
  };

  if (loading || (fragments.length === 0 && !error && !regenerating)) {
    return (
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left: Original video while AI processes */}
        <div>
          <h3 className="text-lg font-medium text-gray-900 mb-3">Original Video</h3>
          <VideoPlayer url={videoUrl} />
        </div>

        {/* Right: Generation animation */}
        <div className="flex flex-col relative">
          <h3 className="text-lg font-medium text-gray-900 mb-3">AI Clips</h3>

          {/* Skeletons behind (z-0) */}
          <div className="flex-1 space-y-4 relative z-0">
            {[0, 1, 2].map((i) => (
              <motion.div
                key={i}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: [0.3, 0.6, 0.3] }}
                transition={{
                  opacity: { duration: 1.5, repeat: Infinity, delay: i * 0.3 },
                  y: { duration: 0.4, delay: i * 0.15 },
                }}
              >
                <Card className="shadow-sm">
                  <div className="flex items-start gap-4">
                    <div className="flex-shrink-0 w-40 h-28 rounded bg-gray-200 animate-pulse" />
                    <div className="flex-1 space-y-3">
                      <div className="flex items-center justify-between">
                        <div className="h-3 w-24 bg-gray-200 rounded animate-pulse" />
                        <div className="h-4 w-10 bg-gray-200 rounded animate-pulse" />
                      </div>
                      <div className="space-y-2">
                        <div className="h-3 w-full bg-gray-200 rounded animate-pulse" />
                        <div className="h-3 w-3/4 bg-gray-200 rounded animate-pulse" />
                      </div>
                      <div className="flex gap-1.5">
                        <div className="h-5 w-14 bg-blue-100 rounded-full animate-pulse" />
                        <div className="h-5 w-18 bg-blue-100 rounded-full animate-pulse" />
                        <div className="h-5 w-12 bg-blue-100 rounded-full animate-pulse" />
                      </div>
                      <div className="h-8 w-full bg-gray-200 rounded animate-pulse" />
                    </div>
                  </div>
                </Card>
              </motion.div>
            ))}
          </div>

          {/* Loader overlay (z-10) */}
          <motion.div
            className="absolute inset-0 top-10 z-10 flex flex-col items-center justify-center"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ duration: 0.5 }}
          >
            <div className="flex flex-col items-center gap-3 bg-white/80 backdrop-blur-sm rounded-2xl px-8 py-6 shadow-lg">
              <motion.i
                className="pi pi-star-fill text-3xl text-purple-600"
                animate={{ rotate: 360, scale: [1, 1.15, 1] }}
                transition={{
                  rotate: { duration: 3, repeat: Infinity, ease: 'linear' },
                  scale: { duration: 1.5, repeat: Infinity, ease: 'easeInOut' },
                }}
              />
              <motion.p
                animate={{ opacity: [0.6, 1, 0.6] }}
                transition={{ duration: 2, repeat: Infinity }}
                className="text-gray-700 text-sm font-medium"
              >
                AI is analyzing your video…
              </motion.p>
              <p className="text-xs text-gray-400">This may take a few minutes</p>
            </div>
          </motion.div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-2xl mx-auto space-y-4">
        <Message severity="error" text={error} className="w-full" />
        <Button label="Retry" icon="pi pi-refresh" onClick={onRegenerate} />
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      {/* Left: Video Preview */}
      <div>
        <h3 className="text-lg font-medium text-gray-900 mb-3">Original Video</h3>
        <VideoPlayer url={videoUrl} />
      </div>

      {/* Right: Fragment Clip Cards */}
      <div className="relative flex flex-col">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-lg font-medium text-gray-900">
            AI Clips ({fragments.length})
          </h3>

          {/* Fancy Regenerate Button */}
          <button
            disabled={regenerating}
            onClick={onRegenerate}
            className={`
              relative group px-5 py-2.5 rounded-xl font-semibold text-sm
              transition-all duration-300 ease-out overflow-hidden
              ${regenerating
                ? 'bg-gray-200 text-gray-400 cursor-not-allowed shadow-none'
                : 'bg-gradient-to-r from-purple-600 via-blue-600 to-indigo-600 text-white shadow-lg hover:shadow-xl hover:shadow-purple-500/25 hover:scale-105 active:scale-95'
              }
            `}
          >
            {!regenerating && (
              <span className="absolute inset-0 bg-gradient-to-r from-purple-400 via-blue-400 to-indigo-400 opacity-0 group-hover:opacity-30 transition-opacity duration-300" />
            )}
            {regenerating && (
              <span className="absolute inset-0 bg-gradient-to-r from-purple-600 via-blue-600 to-indigo-600 opacity-20 animate-pulse" />
            )}
            <span className="relative flex items-center gap-2">
              <i className={`pi pi-refresh ${regenerating ? 'animate-spin' : 'group-hover:rotate-180 transition-transform duration-500'}`} />
              {regenerating ? 'Regenerating…' : 'Regenerate'}
            </span>
          </button>
        </div>

        <div className="flex-1 overflow-y-auto space-y-4 max-h-[60vh] pr-1">
          <AnimatePresence mode="popLayout">
            {fragments.length === 0 ? (
              <motion.p
                key="empty"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                className="text-gray-500 text-sm"
              >
                No clips generated yet.
              </motion.p>
            ) : (
              fragments.map((frag, idx) => (
                <motion.div
                  key={frag.id}
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -20 }}
                  transition={{ delay: idx * 0.08, duration: 0.3 }}
                  layout
                >
                  <Card className="shadow-sm hover:shadow-md transition-shadow duration-200">
                    <div className="flex items-start gap-4">
                      {/* Thumbnail */}
                      <div className="flex-shrink-0 w-40 h-28 rounded overflow-hidden bg-gray-200">
                        <FragmentThumbnail fragmentId={frag.id} index={idx} />
                      </div>

                      <div className="flex-1 min-w-0 space-y-2">
                        {/* Header: time range + viral score */}
                        <div className="flex items-center justify-between">
                          <span className="text-xs text-gray-500 font-mono">
                            {formatTime(frag.startTime)} – {formatTime(frag.endTime)}
                          </span>
                          <span className={`text-sm font-semibold ${viralScoreColor(frag.viralScore)}`}>
                            {frag.viralScore}/10
                          </span>
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

                        {/* Hashtags as editable chips */}
                        <HashtagEditor
                          hashtags={editingHashtags[frag.id] ?? frag.hashtags ?? ''}
                          onChange={(value) => {
                            setEditingHashtags((prev) => ({ ...prev, [frag.id]: value }));
                            onUpdateHashtags(frag.id, value);
                          }}
                        />

                        {/* Approve button */}
                        <Button
                          label={
                            approvingId === frag.id
                              ? 'Approving…'
                              : frag.isApproved
                                ? 'Approved'
                                : 'Approve & Continue'
                          }
                          icon={frag.isApproved ? 'pi pi-check-circle' : 'pi pi-check'}
                          size="small"
                          severity={frag.isApproved ? 'success' : 'info'}
                          loading={approvingId === frag.id}
                          disabled={approvingId !== null}
                          onClick={() => handleApproveClick(frag.id)}
                          className="w-full"
                        />
                      </div>
                    </div>
                  </Card>
                </motion.div>
              ))
            )}
          </AnimatePresence>
        </div>

        {/* Regeneration overlay */}
        <AnimatePresence>
          {regenerating && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="absolute inset-0 bg-white/80 backdrop-blur-sm flex flex-col items-center justify-center rounded-lg z-10 gap-3"
            >
              <motion.i
                animate={{ rotate: 360 }}
                transition={{ duration: 2, repeat: Infinity, ease: 'linear' }}
                className="pi pi-cog text-4xl text-purple-600"
              />
              <p className="text-sm text-gray-600 font-medium">Regenerating clips…</p>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  );
}
