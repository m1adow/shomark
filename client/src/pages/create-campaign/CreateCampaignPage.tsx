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
import StepCampaignSetup, { type CampaignSetupData } from './StepCampaignSetup';
import StepAiReview from './StepAiReview';
import StepSchedulePublish from './StepSchedulePublish';
import type { CampaignDto } from '../../api/types';

const stepItems = [
  { label: 'Campaign Setup' },
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

  // ── Step 1 state ───────────────────────────────────────────────────────
  const [setupData, setSetupData] = useState<CampaignSetupData>({
    name: '',
    targetAudience: null,
    description: '',
    file: null,
  });
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadError, setUploadError] = useState<string | null>(null);

  // Created campaign reference
  const [campaign, setCampaign] = useState<CampaignDto | null>(null);
  const [videoId, setVideoId] = useState<string | null>(null);
  const [approvedFragmentId, setApprovedFragmentId] = useState<string | null>(null);

  // ── Resume existing draft ──────────────────────────────────────────────
  const { data: existingCampaign } = useCampaign(editId ?? '', !!editId);

  useEffect(() => {
    if (!existingCampaign) return;

    setCampaign(existingCampaign);
    setSetupData((prev) => ({
      ...prev,
      name: existingCampaign.name ?? '',
      description: existingCampaign.description ?? '',
    }));

    if (existingCampaign.videoId) {
      setVideoId(existingCampaign.videoId);
    }
    if (existingCampaign.fragmentId) {
      setApprovedFragmentId(existingCampaign.fragmentId);
    }

    // Determine latest step.
    // If videoId exists but no fragmentId, defer draftReady to the fragment
    // check effect below to avoid a visible step-1 → step-2 transition.
    if (existingCampaign.fragmentId) {
      setActiveStep(2);
      setDraftReady(true);
    } else if (!existingCampaign.videoId) {
      setActiveStep(0);
      setDraftReady(true);
    }
    // else: has videoId but no fragmentId → fragment check effect sets draftReady
  }, [existingCampaign]);

  // ── Mutations ──────────────────────────────────────────────────────────
  const { execute: createCampaign } = useCreateCampaign();
  const { execute: updateCampaign } = useUpdateCampaign();
  const { execute: uploadVideo } = useUploadVideo();
  const { execute: processVideo } = useProcessVideo();
  const { execute: createPost } = useCreatePost();
  const { execute: publishPost } = usePublishPost();
  const { execute: updateFragment } = useUpdateFragment();

  // ── Step 2 queries (only when videoId is set) ──────────────────────────
  const {
    data: fragments,
    loading: fragmentsLoading,
    error: fragmentsError,
    refetch: refetchFragments,
  } = useVideoFragments(videoId ?? '', !!videoId);

  // When fragments load and one is already approved, jump to step 2 (new campaign flow).
  useEffect(() => {
    if (editId) return; // handled by the draft step-determination effect below
    if (!fragments || fragments.length === 0) return;
    const approved = fragments.find((f) => f.isApproved);
    if (approved && !approvedFragmentId) {
      setApprovedFragmentId(approved.id);
      setActiveStep(2);
    }
  }, [editId, fragments, approvedFragmentId]);

  // For draft campaigns that have a video but no pre-selected fragment: wait until
  // fragments are loaded before setting draftReady, so the correct step is shown
  // immediately without a step-1 → step-2 transition.
  useEffect(() => {
    if (!editId || !existingCampaign?.videoId || existingCampaign?.fragmentId) return;
    if (fragmentsLoading || fragments === null) return;
    const approved = fragments.find((f) => f.isApproved);
    if (approved) {
      setApprovedFragmentId(approved.id);
      setActiveStep(2);
    } else {
      setActiveStep(1);
    }
    setDraftReady(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editId, existingCampaign, fragmentsLoading, fragments]);

  // Workaround: useVideoFragments doesn't support enabled, pass videoId conditionally
  const {
    data: videoUrlData,
  } = useVideoUrl(videoId ?? '', !!videoId);

  const [regenerating, setRegenerating] = useState(false);

  // ── SSE: auto-refetch fragments when worker completes ──────────────────
  useVideoProcessingEvents(
    activeStep >= 1 ? videoId : null,
    useCallback(() => {
      refetchFragments();
      setRegenerating(false);
      toast.current?.show({
        severity: 'success',
        summary: 'AI Processing Complete',
        detail: 'Video highlights are ready for review.',
        life: 4000,
      });
    }, [refetchFragments]),
  );

  // ── Step 3 queries ─────────────────────────────────────────────────────
  const { data: platforms, loading: platformsLoading } = useMyPlatforms();

  const {
    data: campaignPosts,
  } = useCampaignPosts(campaign?.id ?? '', !!campaign);

  // Scheduled posts for current month
  const now = new Date();
  const monthStart = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
  const monthEnd = new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59).toISOString();
  const { data: scheduledPosts } = useScheduledPostsInRange(monthStart, monthEnd);

  const [publishing, setPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);

  // ── Step 1: Analyze with AI ────────────────────────────────────────────
  const handleStep1Next = useCallback(async () => {
    if (!setupData.file) return;

    setUploadError(null);

    // Layer 1: validate name availability before any upload
    if (setupData.name.trim()) {
      try {
        const { isAvailable } = await campaignsApi.checkName(setupData.name.trim());
        if (!isAvailable) {
          setUploadError('A campaign with this name already exists. Please choose a different name.');
          return;
        }
      } catch {
        setUploadError('Could not validate campaign name. Please try again.');
        return;
      }
    }

    setUploading(true);
    setUploadProgress(10);

    // Layer 2: track uploaded video id for cleanup on failure
    let uploadedVideoId: string | null = null;

    try {
      // 1. Upload video
      const video = await uploadVideo(setupData.file);
      uploadedVideoId = video.id;
      setVideoId(video.id);
      setUploadProgress(50);

      // 2. Create campaign
      const camp = await createCampaign({
        videoId: video.id,
        name: setupData.name,
        targetAudience: setupData.targetAudience ?? undefined,
        description: setupData.description || undefined,
      });
      setCampaign(camp);
      setUploadProgress(70);

      // 3. Trigger AI processing
      await processVideo(video.id, {
        targetAudience: setupData.targetAudience ?? undefined,
        description: setupData.description || undefined,
      });
      setUploadProgress(100);

      toast.current?.show({
        severity: 'success',
        summary: 'Video uploaded',
        detail: 'AI processing started. Clips will appear shortly.',
        life: 4000,
      });

      // Move to step 2
      setActiveStep(1);
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : 'Upload failed');

      // Layer 2: clean up the orphaned video if it was already uploaded
      if (uploadedVideoId !== null) {
        videosApi.delete(uploadedVideoId).catch(() => {});
        setVideoId(null);
      }
    } finally {
      setUploading(false);
    }
  }, [setupData, uploadVideo, createCampaign, processVideo]);

  // ── Step 2: Approve / Update / Regenerate ──────────────────────────────
  const handleApprove = useCallback(
    async (fragmentId: string) => {
      try {
        await updateFragment(fragmentId, { isApproved: true });
        setApprovedFragmentId(fragmentId);
        setActiveStep(2);
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
    setActiveStep(1);
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

  const handleRegenerate = useCallback(async () => {
    if (!videoId) return;
    setRegenerating(true);
    try {
      await processVideo(videoId, {});
      toast.current?.show({
        severity: 'info',
        summary: 'Regenerating',
        detail: 'AI is reprocessing your video. Please wait…',
        life: 4000,
      });
      // SSE callback will set regenerating=false and refetch
    } catch {
      setRegenerating(false);
      toast.current?.show({ severity: 'error', summary: 'Regeneration failed', life: 3000 });
    }
  }, [videoId, processVideo]);

  // ── Save as Draft ──────────────────────────────────────────────────────
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

  // ── Step 3: Schedule / Publish ─────────────────────────────────────────
  const selectedFragment = fragments?.find((f) => f.id === approvedFragmentId);
  const approvedFragments = selectedFragment ? [selectedFragment] : [];

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
        // Mark campaign as Active
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

  // Show spinner while loading an existing draft to prevent step-0 flicker
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
            <StepCampaignSetup
              data={setupData}
              onChange={setSetupData}
              onNext={handleStep1Next}
              uploading={uploading}
              uploadProgress={uploadProgress}
              uploadError={uploadError}
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

        {activeStep === 2 && (
          <motion.div
            key="step-2"
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
