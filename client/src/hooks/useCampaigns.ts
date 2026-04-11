import { useCallback } from 'react';
import { campaignsApi } from '../api';
import type { CreateCampaignRequest, UpdateCampaignRequest } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function useCampaign(id: string, enabled = true) {
  return useApiQuery(
    (signal) => campaignsApi.getById(id, signal),
    [id],
    { enabled },
  );
}

export function useMyCampaigns() {
  return useApiQuery(
    (signal) => campaignsApi.getAll(signal),
    [],
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
