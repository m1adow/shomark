import { get, post, put, del } from './client';
import type {
  PlatformDto,
  CreatePlatformRequest,
  UpdatePlatformRequest,
} from './types';

const PATH = '/platforms';

export const platformsApi = {
  getById: (id: string, signal?: AbortSignal) =>
    get<PlatformDto>(`${PATH}/${id}`, signal),

  getByUserId: (userId: string, signal?: AbortSignal) =>
    get<PlatformDto[]>(`${PATH}/user/${userId}`, signal),

  create: (req: CreatePlatformRequest, signal?: AbortSignal) =>
    post<PlatformDto>(PATH, req, signal),

  update: (id: string, req: UpdatePlatformRequest, signal?: AbortSignal) =>
    put<PlatformDto>(`${PATH}/${id}`, req, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),
};
