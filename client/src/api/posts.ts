import { get, post, put, del } from './client';
import type {
  PostDto,
  CreatePostRequest,
  UpdatePostRequest,
  PostWithAnalyticsDto,
} from './types';
import type { PostStatus } from './types';

const PATH = '/posts';

export const postsApi = {
  getById: (id: string, signal?: AbortSignal) =>
    get<PostDto>(`${PATH}/${id}`, signal),

  getByFragmentId: (fragmentId: string, signal?: AbortSignal) =>
    get<PostDto[]>(`${PATH}/fragment/${fragmentId}`, signal),

  getByStatus: (status: PostStatus, signal?: AbortSignal) =>
    get<PostDto[]>(`${PATH}/status/${status}`, signal),

  getWithAnalytics: (id: string, signal?: AbortSignal) =>
    get<PostWithAnalyticsDto>(`${PATH}/${id}/analytics`, signal),

  create: (req: CreatePostRequest, signal?: AbortSignal) =>
    post<PostDto>(PATH, req, signal),

  update: (id: string, req: UpdatePostRequest, signal?: AbortSignal) =>
    put<PostDto>(`${PATH}/${id}`, req, signal),

  getByCampaignId: (campaignId: string, signal?: AbortSignal) =>
    get<PostDto[]>(`${PATH}/campaign/${campaignId}`, signal),

  getScheduledInRange: (from: string, to: string, signal?: AbortSignal) =>
    get<PostDto[]>(`${PATH}/scheduled?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),
};
