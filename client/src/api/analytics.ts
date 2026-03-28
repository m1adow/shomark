import { get, put } from './client';
import type { AnalyticsDto, UpdateAnalyticsRequest } from './types';

const PATH = '/analytics';

export const analyticsApi = {
  getByPostId: (postId: string, signal?: AbortSignal) =>
    get<AnalyticsDto>(`${PATH}/post/${postId}`, signal),

  upsert: (postId: string, req: UpdateAnalyticsRequest, signal?: AbortSignal) =>
    put<AnalyticsDto>(`${PATH}/post/${postId}`, req, signal),
};
