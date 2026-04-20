import { get, post, put, del } from './client';
import type {
  CampaignDto,
  CreateCampaignRequest,
  UpdateCampaignRequest,
} from './types';

const PATH = '/campaigns';

export const campaignsApi = {
  getAll: (signal?: AbortSignal) =>
    get<CampaignDto[]>(PATH, signal),

  getById: (id: string, signal?: AbortSignal) =>
    get<CampaignDto>(`${PATH}/${id}`, signal),

  create: (req: CreateCampaignRequest, signal?: AbortSignal) =>
    post<CampaignDto>(PATH, req, signal),

  update: (id: string, req: UpdateCampaignRequest, signal?: AbortSignal) =>
    put<CampaignDto>(`${PATH}/${id}`, req, signal),

  getByVideoId: (videoId: string, signal?: AbortSignal) =>
    get<CampaignDto[]>(`${PATH}/video/${videoId}`, signal),

  checkName: (name: string, signal?: AbortSignal) =>
    get<{ isAvailable: boolean }>(`${PATH}/check-name?name=${encodeURIComponent(name)}`, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),
};
