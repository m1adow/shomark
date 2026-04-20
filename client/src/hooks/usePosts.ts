import { useCallback } from 'react';
import { postsApi } from '../api';
import type { CreatePostRequest, UpdatePostRequest, PostStatus } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function usePost(id: string) {
  return useApiQuery(
    (signal) => postsApi.getById(id, signal),
    [id],
  );
}

export function useFragmentPosts(fragmentId: string) {
  return useApiQuery(
    (signal) => postsApi.getByFragmentId(fragmentId, signal),
    [fragmentId],
  );
}

export function usePostsByStatus(status: PostStatus) {
  return useApiQuery(
    (signal) => postsApi.getByStatus(status, signal),
    [status],
  );
}

export function usePostWithAnalytics(id: string) {
  return useApiQuery(
    (signal) => postsApi.getWithAnalytics(id, signal),
    [id],
  );
}

export function useCreatePost() {
  return useApiMutation(
    useCallback((req: CreatePostRequest) => postsApi.create(req), []),
  );
}

export function useUpdatePost() {
  return useApiMutation(
    useCallback(
      (id: string, req: UpdatePostRequest) => postsApi.update(id, req),
      [],
    ),
  );
}

export function useDeletePost() {
  return useApiMutation(
    useCallback((id: string) => postsApi.delete(id), []),
  );
}

export function useCampaignPosts(campaignId: string, enabled = true) {
  return useApiQuery(
    (signal) => postsApi.getByCampaignId(campaignId, signal),
    [campaignId],
    { enabled },
  );
}

export function useScheduledPostsInRange(from: string, to: string, enabled = true) {
  return useApiQuery(
    (signal) => postsApi.getScheduledInRange(from, to, signal),
    [from, to],
    { enabled },
  );
}

export function usePublishPost() {
  return useApiMutation(
    useCallback((id: string) => postsApi.publish(id), []),
  );
}
