import { analyticsApi } from '../api';
import { useApiQuery } from './useApi';

export function useCampaignAnalytics(campaignId: string) {
  return useApiQuery(
    (signal) => analyticsApi.getCampaignAnalytics(campaignId, signal),
    [campaignId],
    { enabled: campaignId.length > 0 },
  );
}
