import { get, post, put, del } from './client';
import type {
  CampaignDto,
  CreateCampaignRequest,
  UpdateCampaignRequest,
} from './types';

const PATH = '/campaigns';

export const campaignsApi = {
  getById: (id: string, signal?: AbortSignal) =>
    get<CampaignDto>(`${PATH}/${id}`, signal),

  getByUserId: (userId: string, signal?: AbortSignal) =>
    get<CampaignDto[]>(`${PATH}/user/${userId}`, signal),

  create: (req: CreateCampaignRequest, signal?: AbortSignal) =>
    post<CampaignDto>(PATH, req, signal),

  update: (id: string, req: UpdateCampaignRequest, signal?: AbortSignal) =>
    put<CampaignDto>(`${PATH}/${id}`, req, signal),

  getByVideoId: (videoId: string, signal?: AbortSignal) =>
    get<CampaignDto[]>(`${PATH}/video/${videoId}`, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),
};
