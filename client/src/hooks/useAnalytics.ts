import { useCallback } from 'react';
import { analyticsApi } from '../api';
import type { UpdateAnalyticsRequest } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function usePostAnalytics(postId: string) {
  return useApiQuery(
    (signal) => analyticsApi.getByPostId(postId, signal),
    [postId],
    { enabled: postId.length > 0 },
  );
}

export function useUpsertAnalytics() {
  return useApiMutation(
    useCallback(
      (postId: string, req: UpdateAnalyticsRequest) => analyticsApi.upsert(postId, req),
      [],
    ),
  );
}
