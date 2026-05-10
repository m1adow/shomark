import { get } from './client';
import type { CampaignAnalyticsDto } from './types';

const PATH = '/analytics';

export const analyticsApi = {
  getCampaignAnalytics: (campaignId: string, signal?: AbortSignal) =>
    get<CampaignAnalyticsDto>(`${PATH}/campaigns/${campaignId}`, signal),
};
