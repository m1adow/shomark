import { useCallback } from 'react';
import { platformsApi } from '../api';
import type { CreatePlatformRequest, UpdatePlatformRequest, OAuthPlatform } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function usePlatform(id: string) {
  return useApiQuery(
    (signal) => platformsApi.getById(id, signal),
    [id],
  );
}

export function useMyPlatforms() {
  return useApiQuery(
    (signal) => platformsApi.getAll(signal),
    [],
  );
}

export function useCreatePlatform() {
  return useApiMutation(
    useCallback((req: CreatePlatformRequest) => platformsApi.create(req), []),
  );
}

export function useUpdatePlatform() {
  return useApiMutation(
    useCallback(
      (id: string, req: UpdatePlatformRequest) => platformsApi.update(id, req),
      [],
    ),
  );
}

export function useDeletePlatform() {
  return useApiMutation(
    useCallback((id: string) => platformsApi.delete(id), []),
  );
}

export function useConnectPlatform() {
  return useApiMutation(
    useCallback((platform: OAuthPlatform) => platformsApi.getConnectUrl(platform), []),
  );
}

export function useDisconnectPlatform() {
  return useApiMutation(
    useCallback((platform: OAuthPlatform) => platformsApi.disconnect(platform), []),
  );
}

export function useRefreshPlatformToken() {
  return useApiMutation(
    useCallback((platform: OAuthPlatform) => platformsApi.refreshToken(platform), []),
  );
}
