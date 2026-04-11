import { get, post, put, del } from './client';
import type {
  AiFragmentDto,
  AiFragmentDetailDto,
  CreateAiFragmentRequest,
  UpdateAiFragmentRequest,
} from './types';

const PATH = '/fragments';

export const fragmentsApi = {
  getById: (id: string, signal?: AbortSignal) =>
    get<AiFragmentDto>(`${PATH}/${id}`, signal),

  getByVideoId: (videoId: string, signal?: AbortSignal) =>
    get<AiFragmentDto[]>(`${PATH}/video/${videoId}`, signal),

  getWithDetails: (id: string, signal?: AbortSignal) =>
    get<AiFragmentDetailDto>(`${PATH}/${id}/details`, signal),

  create: (req: CreateAiFragmentRequest, signal?: AbortSignal) =>
    post<AiFragmentDto>(PATH, req, signal),

  update: (id: string, req: UpdateAiFragmentRequest, signal?: AbortSignal) =>
    put<AiFragmentDto>(`${PATH}/${id}`, req, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),

  getThumbnailUrl: (id: string, signal?: AbortSignal) =>
    get<{ url: string }>(`${PATH}/${id}/thumbnail-url`, signal),
};
