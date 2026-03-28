import { useCallback } from 'react';
import { campaignsApi } from '../api';
import type { CreateCampaignRequest, UpdateCampaignRequest } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function useCampaign(id: string) {
  return useApiQuery(
    (signal) => campaignsApi.getById(id, signal),
    [id],
  );
}

export function useUserCampaigns(userId: string) {
  return useApiQuery(
    (signal) => campaignsApi.getByUserId(userId, signal),
    [userId],
  );
}

export function useCreateCampaign() {
  return useApiMutation(
    useCallback((req: CreateCampaignRequest) => campaignsApi.create(req), []),
  );
}

export function useUpdateCampaign() {
  return useApiMutation(
    useCallback(
      (id: string, req: UpdateCampaignRequest) => campaignsApi.update(id, req),
      [],
    ),
  );
}

export function useDeleteCampaign() {
  return useApiMutation(
    useCallback((id: string) => campaignsApi.delete(id), []),
  );
}

export function useCampaignsByVideo(videoId: string, enabled = true) {
  return useApiQuery(
    (signal) => campaignsApi.getByVideoId(videoId, signal),
    [videoId],
    { enabled },
  );
}
