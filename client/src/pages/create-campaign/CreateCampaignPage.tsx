import { useState, useCallback, useRef, useEffect, useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Steps } from 'primereact/steps';
import { Toast } from 'primereact/toast';
import { ProgressSpinner } from 'primereact/progressspinner';
import { motion, AnimatePresence } from 'framer-motion';
import { useAuth } from '../../auth';
import { campaignsApi } from '../../api/campaigns';
import { videosApi } from '../../api/videos';
import { useCreateCampaign, useUpdateCampaign, useCampaign } from '../../hooks/useCampaigns';
import { useUploadVideo, useProcessVideo, useVideoUrl } from '../../hooks/useVideos';
import { useVideoFragments, useUpdateFragment } from '../../hooks/useFragments';
import { useCreatePost, usePublishPost, useCampaignPosts, useScheduledPostsInRange } from '../../hooks/usePosts';
import { useMyPlatforms } from '../../hooks/usePlatforms';
import { useVideoProcessingEvents } from '../../hooks/useVideoProcessingEvents';
import StepVideoUpload, { type VideoUploadData } from './StepVideoUpload';
import StepVideoSummarize, { type VideoSummarizeData } from './StepVideoSummarize';
import StepAiReview from './StepAiReview';
import StepSchedulePublish from './StepSchedulePublish';
import type { CampaignDto } from '../../api/types';

const stepItems = [
  { label: 'Upload' },
  { label: 'Configure' },
  { label: 'AI Review' },
  { label: 'Schedule & Publish' },
];

export default function CreateCampaignPage() {
  const { id: editId } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const toast = useRef<Toast>(null);
  useAuth();

  const [activeStep, setActiveStep] = useState(0);
  const [draftReady, setDraftReady] = useState(!editId);

  // â”€â”€ Step 0 state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const [uploadData, setUploadData] = useState<VideoUploadData>({ name: '', file: null });
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [creatingCampaign, setCreatingCampaign] = useState(false);

  // â”€â”€ Step 1 state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const [summarizeData, setSummarizeData] = useState<VideoSummarizeData>({
    targetAudience: null,
    description: '',
  });
  const [summary, setSummary] = useState<string | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [processing, setProcessing] = useState(false);
  const [processError, setProcessError] = useState<string | null>(null);

  // â”€â”€ Shared â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const [campaign, setCampaign] = useState<CampaignDto | null>(null);
  const [videoId, setVideoId] = useState<string | null>(null);
  const [approvedFragmentId, setApprovedFragmentId] = useState<string | null>(null);

  // â”€â”€ Resume existing draft â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const { data: existingCampaign } = useCampaign(editId ?? '', !!editId);

  useEffect(() => {
    if (!existingCampaign) return;

    setCampaign(existingCampaign);
    setUploadData((prev) => ({ ...prev, name: existingCampaign.name ?? '' }));
    setSummarizeData((prev) => ({
      ...prev,
      description: existingCampaign.description ?? '',
    }));

    if (existingCampaign.videoId) {
      setVideoId(existingCampaign.videoId);
      // Fetch summary from video record
      videosApi.getById(existingCampaign.videoId).then((v) => {
        if (v.summary) {
          setSummary(v.summary);
          setSummaryLoading(false);
        } else {
          setSummaryLoading(true); // will update via SSE
        }
      }).catch(() => setSummaryLoading(false));
    }

    if (existingCampaign.fragmentId) {
      setApprovedFragmentId(existingCampaign.fragmentId);
      setActiveStep(3);
      setDraftReady(true);
    } else if (!existingCampaign.videoId) {
      setActiveStep(0);
      setDraftReady(true);
    }
    // else: has videoId but no fragmentId â€” fragment check effect sets draftReady
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [existingCampaign]);

  // â”€â”€ Mutations â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const { execute: createCampaign } = useCreateCampaign();
  const { execute: updateCampaign } = useUpdateCampaign();
  const { execute: uploadVideo } = useUploadVideo();
  const { execute: processVideo } = useProcessVideo();
  const { execute: createPost } = useCreatePost();
  const { execute: publishPost } = usePublishPost();
  const { execute: updateFragment } = useUpdateFragment();

  // â”€â”€ Step 2 queries (only when videoId is set) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const {
    data: fragments,
    loading: fragmentsLoading,
    error: fragmentsError,
    refetch: refetchFragments,
  } = useVideoFragments(videoId ?? '', !!videoId);

  // For draft campaigns: determine landing step once fragments load
  useEffect(() => {
    if (!editId || !existingCampaign?.videoId || existingCampaign?.fragmentId) return;
    if (fragmentsLoading || fragments === null) return;

    const approved = fragments.find((f) => f.isApproved);
    if (approved) {
      setApprovedFragmentId(approved.id);
      setActiveStep(3);
    } else if (fragments.length > 0) {
      setActiveStep(2);
    } else {
      // Phase 2 not yet triggered or still running â†’ land on Configure
      setActiveStep(1);
    }
    setDraftReady(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editId, existingCampaign, fragmentsLoading, fragments]);

  // Auto-advance approved fragment in new campaign flow
  useEffect(() => {
    if (editId) return;
    if (!fragments || fragments.length === 0) return;
    const approved = fragments.find((f) => f.isApproved);
    if (approved && !approvedFragmentId) {
      setApprovedFragmentId(approved.id);
      setActiveStep(3);
    }
  }, [editId, fragments, approvedFragmentId]);

  const { data: videoUrlData } = useVideoUrl(videoId ?? '', !!videoId);

  const [regenerating, setRegenerating] = useState(false);

  // â”€â”€ SSE: handle both processing-complete and transcription-complete â”€â”€â”€â”€
  useVideoProcessingEvents(
    videoId,
    useMemo(() => ({
      onProcessingComplete: () => {
        refetchFragments();
        setRegenerating(false);
        toast.current?.show({
          severity: 'success',
          summary: 'AI Processing Complete',
          detail: 'Video highlights are ready for review.',
          life: 4000,
        });
      },
      onTranscriptionComplete: (evt) => {
        setSummary(evt.summary ?? null);
        setSummaryLoading(false);
      },
    }), [refetchFragments]),
  );

  // â”€â”€ Auto-upload when file is selected in step 0 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const uploadTriggeredRef = useRef(false);
  useEffect(() => {
    if (!uploadData.file || uploading || uploadTriggeredRef.current) return;
    uploadTriggeredRef.current = true;

    let uploadedId: string | null = null;
    setUploadError(null);
    setUploading(true);
    setUploadProgress(10);

    uploadVideo(uploadData.file)
      .then((video) => {
        uploadedId = video.id;
        setVideoId(video.id);
        setUploadProgress(100);
        setSummaryLoading(true);
        // summary will arrive via SSE transcription-complete
      })
      .catch((err) => {
        setUploadError(err instanceof Error ? err.message : 'Upload failed');
        uploadTriggeredRef.current = false; // allow retry on file re-select
        if (uploadedId) {
          videosApi.delete(uploadedId).catch(() => {});
          setVideoId(null);
        }
      })
      .finally(() => {
        setUploading(false);
      });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [uploadData.file]);

  // â”€â”€ Step 0 â†’ Step 1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const handleStep0Next = useCallback(async () => {
    if (!videoId || !uploadData.name.trim()) return;
    // Edit mode: campaign already exists — just advance.
    if (campaign) {
      setActiveStep(1);
      return;
    }
    setUploadError(null);
    try {
      const { isAvailable } = await campaignsApi.checkName(uploadData.name.trim());
      if (!isAvailable) {
        setUploadError('A campaign with this name already exists. Please choose a different name.');
        return;
      }
    } catch {
      setUploadError('Could not validate campaign name. Please try again.');
      return;
    }
    setCreatingCampaign(true);
    try {
      const camp = await createCampaign({ videoId, name: uploadData.name });
      setCampaign(camp);
      setActiveStep(1);
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : 'Failed to create campaign');
    } finally {
      setCreatingCampaign(false);
    }
  }, [videoId, uploadData.name, campaign, createCampaign]);

  // â”€â”€ Step 1 â†’ Step 2: create campaign + trigger Phase 2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const handleStep1Next = useCallback(async () => {
    if (!videoId || !campaign) return;
    setProcessError(null);
    setProcessing(true);
    try {
      // Persist audience/description on the already-created campaign draft.
      await updateCampaign(campaign.id, {
        targetAudience: summarizeData.targetAudience ?? undefined,
        description: summarizeData.description || undefined,
      });

      // Phase 2: highlight detection (transcript cache hit guaranteed)
      await processVideo(videoId, {
        targetAudience: summarizeData.targetAudience ?? undefined,
        description: summarizeData.description || undefined,
      });

      toast.current?.show({
        severity: 'success',
        summary: 'AI Processing Started',
        detail: 'Clips will appear shortly.',
        life: 4000,
      });

      setActiveStep(2);
    } catch (err) {
      setProcessError(err instanceof Error ? err.message : 'Failed to start processing');
    } finally {
      setProcessing(false);
    }
  }, [videoId, campaign, summarizeData, updateCampaign, processVideo]);

  // â”€â”€ Step 2: Approve / Update / Regenerate â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const handleApprove = useCallback(
    async (fragmentId: string) => {
      try {
        await updateFragment(fragmentId, { isApproved: true });
        setApprovedFragmentId(fragmentId);
        setActiveStep(3);
      } catch {
        toast.current?.show({ severity: 'error', summary: 'Failed to approve clip', life: 3000 });
      }
    },
    [updateFragment],
  );

  const handleBackFromSchedule = useCallback(async () => {
    if (approvedFragmentId) {
      try {
        await updateFragment(approvedFragmentId, { isApproved: false });
        refetchFragments();
      } catch {
        // best-effort
      }
      setApprovedFragmentId(null);
    }
    setActiveStep(2);
  }, [approvedFragmentId, updateFragment, refetchFragments]);

  const handleUpdateCaption = useCallback(
    async (fragmentId: string, description: string) => {
      try {
        await updateFragment(fragmentId, { description });
      } catch {
        toast.current?.show({ severity: 'error', summary: 'Failed to update caption', life: 3000 });
        refetchFragments();
      }
    },
    [updateFragment, refetchFragments],
  );

  const handleUpdateHashtags = useCallback(
    async (fragmentId: string, hashtags: string) => {
      try {
        await updateFragment(fragmentId, { hashtags });
      } catch {
        toast.current?.show({ severity: 'error', summary: 'Failed to update hashtags', life: 3000 });
        refetchFragments();
      }
    },
    [updateFragment, refetchFragments],
  );

  const handleRegenerate = useCallback(async (additionalInstructions: string) => {
    if (!videoId) return;
    setRegenerating(true);
    try {
      await processVideo(videoId, { additionalInstructions: additionalInstructions || undefined });
      toast.current?.show({
        severity: 'info',
        summary: 'Regenerating',
        detail: 'AI is reprocessing your video. Please waitâ€¦',
        life: 4000,
      });
    } catch {
      setRegenerating(false);
      toast.current?.show({ severity: 'error', summary: 'Regeneration failed', life: 3000 });
    }
  }, [videoId, processVideo]);

  // â”€â”€ Save as Draft â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const handleSaveAsDraft = useCallback(async () => {
    if (!campaign) return;
    try {
      await updateCampaign(campaign.id, { status: 0 });
      toast.current?.show({
        severity: 'success',
        summary: 'Saved',
        detail: 'Campaign saved as draft.',
        life: 3000,
      });
      navigate('/campaigns');
    } catch {
      toast.current?.show({ severity: 'error', summary: 'Failed to save draft', life: 3000 });
    }
  }, [campaign, updateCampaign, navigate]);

  // â”€â”€ Step 3: Schedule / Publish â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const selectedFragment = fragments?.find((f) => f.id === approvedFragmentId);
  const approvedFragments = selectedFragment ? [selectedFragment] : [];

  const { data: platforms, loading: platformsLoading } = useMyPlatforms();
  const { data: campaignPosts } = useCampaignPosts(campaign?.id ?? '', !!campaign);

  const now = new Date();
  const monthStart = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
  const monthEnd = new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59).toISOString();
  const { data: scheduledPosts } = useScheduledPostsInRange(monthStart, monthEnd);

  const [publishing, setPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);

  const handleSchedule = useCallback(
    async (platformIds: string[], scheduledAt: Date) => {
      if (!campaign) return;
      setPublishing(true);
      setPublishError(null);
      try {
        for (const frag of approvedFragments) {
          for (const platformId of platformIds) {
            await createPost({
              fragmentId: frag.id,
              platformId,
              campaignId: campaign.id,
              title: frag.description ?? undefined,
              content: frag.hashtags ?? undefined,
              scheduledAt: scheduledAt.toISOString(),
            });
          }
        }
        toast.current?.show({
          severity: 'success',
          summary: 'Scheduled!',
          detail: `${approvedFragments.length * platformIds.length} post(s) scheduled.`,
          life: 4000,
        });
        navigate('/campaigns');
      } catch (err) {
        setPublishError(err instanceof Error ? err.message : 'Scheduling failed');
      } finally {
        setPublishing(false);
      }
    },
    [campaign, approvedFragments, createPost, navigate],
  );

  const handlePublishNow = useCallback(
    async (platformIds: string[]) => {
      if (!campaign) return;
      setPublishing(true);
      setPublishError(null);
      try {
        for (const frag of approvedFragments) {
          for (const platformId of platformIds) {
            const createdPost = await createPost({
              fragmentId: frag.id,
              platformId,
              campaignId: campaign.id,
              title: frag.description ?? undefined,
              content: frag.hashtags ?? undefined,
            });
            await publishPost(createdPost.id);
          }
        }
        await updateCampaign(campaign.id, { status: 1 });
        toast.current?.show({
          severity: 'success',
          summary: 'Published!',
          detail: `${approvedFragments.length * platformIds.length} post(s) created.`,
          life: 4000,
        });
        navigate('/campaigns');
      } catch (err) {
        setPublishError(err instanceof Error ? err.message : 'Publishing failed');
      } finally {
        setPublishing(false);
      }
    },
    [campaign, approvedFragments, createPost, publishPost, updateCampaign, navigate],
  );

  const stepVariants = useMemo(() => ({
    initial: { opacity: 0, x: 30 },
    animate: { opacity: 1, x: 0 },
    exit: { opacity: 0, x: -30 },
  }), []);

  if (editId && !draftReady) {
    return (
      <div className="fixed inset-0 flex items-center justify-center">
        <ProgressSpinner style={{ width: '96px', height: '96px' }} />
      </div>
    );
  }

  return (
    <div>
      <Toast ref={toast} />

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-semibold text-gray-900">
          {editId ? 'Edit Campaign' : 'Create Campaign'}
        </h1>
      </div>

      <Steps
        model={stepItems}
        activeIndex={activeStep}
        readOnly
        className="mb-8"
      />

      <AnimatePresence mode="wait" initial={false}>
        {activeStep === 0 && (
          <motion.div
            key="step-0"
            variants={stepVariants}
            initial="initial"
            animate="animate"
            exit="exit"
            transition={{ duration: 0.3, ease: 'easeInOut' }}
          >
            <StepVideoUpload
              data={uploadData}
              onChange={setUploadData}
              onNext={handleStep0Next}
              uploading={uploading}
              uploadProgress={uploadProgress}
              uploadError={uploadError}
              creating={creatingCampaign}
            />
          </motion.div>
        )}

        {activeStep === 1 && (
          <motion.div
            key="step-1"
            variants={stepVariants}
            initial="initial"
            animate="animate"
            exit="exit"
            transition={{ duration: 0.3, ease: 'easeInOut' }}
          >
            <StepVideoSummarize
              data={summarizeData}
              onChange={setSummarizeData}
              summary={summary}
              summaryLoading={summaryLoading}
              onNext={handleStep1Next}
              processing={processing}
              processError={processError}
            />
          </motion.div>
        )}

        {activeStep === 2 && (
          <motion.div
            key="step-2"
            variants={stepVariants}
            initial="initial"
            animate="animate"
            exit="exit"
            transition={{ duration: 0.3, ease: 'easeInOut' }}
          >
            <StepAiReview
              fragments={fragments ?? []}
              loading={fragmentsLoading}
              error={fragmentsError}
              videoUrl={videoUrlData?.url ?? null}
              onApprove={handleApprove}
              onUpdateCaption={handleUpdateCaption}
              onUpdateHashtags={handleUpdateHashtags}
              onRegenerate={handleRegenerate}
              regenerating={regenerating}
            />
          </motion.div>
        )}

        {activeStep === 3 && (
          <motion.div
            key="step-3"
            variants={stepVariants}
            initial="initial"
            animate="animate"
            exit="exit"
            transition={{ duration: 0.3, ease: 'easeInOut' }}
          >
            <StepSchedulePublish
              approvedFragments={approvedFragments}
              platforms={platforms ?? []}
              platformsLoading={platformsLoading}
              scheduledPosts={[...(campaignPosts ?? []), ...(scheduledPosts ?? [])]}
              onSchedule={handleSchedule}
              onPublishNow={handlePublishNow}
              onBack={handleBackFromSchedule}
              onSaveAsDraft={handleSaveAsDraft}
              publishing={publishing}
              publishError={publishError}
            />
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

