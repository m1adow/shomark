import { useCallback } from 'react';
import { fragmentsApi } from '../api';
import type { CreateAiFragmentRequest, UpdateAiFragmentRequest } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function useFragment(id: string) {
  return useApiQuery(
    (signal) => fragmentsApi.getById(id, signal),
    [id],
  );
}

export function useVideoFragments(videoId: string, enabled = true) {
  return useApiQuery(
    (signal) => fragmentsApi.getByVideoId(videoId, signal),
    [videoId],
    { enabled },
  );
}

export function useFragmentDetails(id: string) {
  return useApiQuery(
    (signal) => fragmentsApi.getWithDetails(id, signal),
    [id],
  );
}

export function useCreateFragment() {
  return useApiMutation(
    useCallback(
      (req: CreateAiFragmentRequest) => fragmentsApi.create(req),
      [],
    ),
  );
}

export function useUpdateFragment() {
  return useApiMutation(
    useCallback(
      (id: string, req: UpdateAiFragmentRequest) => fragmentsApi.update(id, req),
      [],
    ),
  );
}

export function useDeleteFragment() {
  return useApiMutation(
    useCallback((id: string) => fragmentsApi.delete(id), []),
  );
}

export function useFragmentThumbnailUrl(id: string, enabled = true) {
  return useApiQuery(
    (signal) => fragmentsApi.getThumbnailUrl(id, signal),
    [id],
    { enabled },
  );
}
