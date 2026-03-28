// ── Enums ────────────────────────────────────────────────────────────────────

export const CampaignStatus = {
  Draft: 0,
  Active: 1,
  Completed: 2,
  Archived: 3,
} as const;
export type CampaignStatus = (typeof CampaignStatus)[keyof typeof CampaignStatus];

export const PlatformType = {
  Instagram: 0,
  TikTok: 1,
  YouTube: 2,
  X: 3,
  LinkedIn: 4,
  Telegram: 5,
} as const;
export type PlatformType = (typeof PlatformType)[keyof typeof PlatformType];

export const PostStatus = {
  Draft: 0,
  Scheduled: 1,
  Published: 2,
  Failed: 3,
} as const;
export type PostStatus = (typeof PostStatus)[keyof typeof PostStatus];

// ── Analytics ────────────────────────────────────────────────────────────────

export interface AnalyticsDto {
  id: string;
  postId: string;
  views: number;
  likes: number;
  shares: number;
  comments: number;
  lastSyncedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpdateAnalyticsRequest {
  views: number;
  likes: number;
  shares: number;
  comments: number;
}

export interface AnalyticsSummaryDto {
  views: number;
  likes: number;
  shares: number;
  comments: number;
  lastSyncedAt: string | null;
}

export const TargetAudience = {
  Applicants: 0,
  Masters: 1,
  Professionals: 2,
} as const;
export type TargetAudience = (typeof TargetAudience)[keyof typeof TargetAudience];

// ── Campaigns ────────────────────────────────────────────────────────────────

export interface CampaignDto {
  id: string;
  userId: string;
  fragmentId: string | null;
  videoId: string | null;
  name: string | null;
  targetAudience: string | null;
  description: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCampaignRequest {
  userId: string;
  fragmentId?: string;
  videoId?: string;
  name?: string;
  targetAudience?: TargetAudience;
  description?: string;
}

export interface UpdateCampaignRequest {
  name?: string;
  status?: CampaignStatus;
  targetAudience?: TargetAudience;
  description?: string;
  videoId?: string;
  fragmentId?: string;
}

// ── Fragments ────────────────────────────────────────────────────────────────

export interface AiFragmentDto {
  id: string;
  videoId: string;
  description: string | null;
  startTime: number;
  endTime: number;
  minioKey: string | null;
  viralScore: number | null;
  hashtags: string | null;
  thumbnailKey: string | null;
  isApproved: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateAiFragmentRequest {
  videoId: string;
  description?: string;
  startTime: number;
  endTime: number;
  minioKey?: string;
  viralScore?: number;
  hashtags?: string;
  thumbnailKey?: string;
  tagIds?: string[];
}

export interface UpdateAiFragmentRequest {
  description?: string;
  startTime: number;
  endTime: number;
  minioKey?: string;
  viralScore?: number;
  hashtags?: string;
  isApproved?: boolean;
}

export interface AiFragmentDetailDto {
  id: string;
  videoId: string;
  description: string | null;
  startTime: number;
  endTime: number;
  minioKey: string | null;
  viralScore: number | null;
  hashtags: string | null;
  thumbnailKey: string | null;
  isApproved: boolean;
  createdAt: string;
  tags: TagSummaryDto[];
  posts: PostSummaryDto[];
}

export interface TagSummaryDto {
  id: string;
  name: string;
  slug: string;
}

export interface PostSummaryDto {
  id: string;
  title: string | null;
  status: string;
}

// ── Platforms ─────────────────────────────────────────────────────────────────

export interface PlatformDto {
  id: string;
  userId: string;
  platformType: string;
  accountName: string | null;
  tokenExpiresAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePlatformRequest {
  userId: string;
  platformType: PlatformType;
  accountName?: string;
  accessToken?: string;
  refreshToken?: string;
  tokenExpiresAt?: string;
}

export interface UpdatePlatformRequest {
  accountName?: string;
  accessToken?: string;
  refreshToken?: string;
  tokenExpiresAt?: string;
}

// ── Posts ─────────────────────────────────────────────────────────────────────

export interface PostDto {
  id: string;
  fragmentId: string;
  platformId: string;
  campaignId: string | null;
  title: string | null;
  content: string | null;
  externalUrl: string | null;
  status: string;
  scheduledAt: string | null;
  publishedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePostRequest {
  fragmentId: string;
  platformId: string;
  campaignId?: string;
  title?: string;
  content?: string;
  scheduledAt?: string;
}

export interface UpdatePostRequest {
  title?: string;
  content?: string;
  externalUrl?: string;
  status?: PostStatus;
  scheduledAt?: string;
  publishedAt?: string;
}

export interface PostWithAnalyticsDto {
  id: string;
  fragmentId: string;
  platformId: string;
  campaignId: string | null;
  title: string | null;
  content: string | null;
  externalUrl: string | null;
  status: string;
  scheduledAt: string | null;
  publishedAt: string | null;
  createdAt: string;
  analytics: AnalyticsSummaryDto | null;
}

// ── Tags ─────────────────────────────────────────────────────────────────────

export interface TagDto {
  id: string;
  name: string;
  slug: string;
  createdAt: string;
}

export interface CreateTagRequest {
  name: string;
  slug: string;
}

export interface UpdateTagRequest {
  name: string;
  slug: string;
}

// ── Users ────────────────────────────────────────────────────────────────────

export interface UserDto {
  id: string;
  name: string;
  email: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateUserRequest {
  name: string;
  email: string;
}

export interface UpdateUserRequest {
  name: string;
  email: string;
}

export interface UserWithPlatformsDto {
  id: string;
  name: string;
  email: string;
  createdAt: string;
  platforms: PlatformSummaryDto[];
}

export interface PlatformSummaryDto {
  id: string;
  platformType: string;
  accountName: string | null;
  tokenExpiresAt: string | null;
}

// ── Videos ───────────────────────────────────────────────────────────────────

export interface VideoDto {
  id: string;
  title: string;
  minioKey: string;
  originalFileName: string | null;
  durationSeconds: number | null;
  fileSize: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateVideoRequest {
  title: string;
  minioKey: string;
  originalFileName?: string;
  durationSeconds?: number;
  fileSize?: number;
}

export interface UpdateVideoRequest {
  title: string;
  originalFileName?: string;
  durationSeconds?: number;
  fileSize?: number;
}

export interface VideoWithFragmentsDto {
  id: string;
  title: string;
  minioKey: string;
  originalFileName: string | null;
  durationSeconds: number | null;
  fileSize: number | null;
  createdAt: string;
  fragments: FragmentSummaryDto[];
}

export interface FragmentSummaryDto {
  id: string;
  description: string | null;
  startTime: number;
  endTime: number;
  minioKey: string | null;
  viralScore: number | null;
  hashtags: string | null;
  thumbnailKey: string | null;
  isApproved: boolean;
}

export interface ProcessVideoRequest {
  outputBucket?: string;
  outputPrefix?: string;
}

// ── API Error ────────────────────────────────────────────────────────────────

export interface ApiError {
  error: string;
  errorCode?: string;
}
