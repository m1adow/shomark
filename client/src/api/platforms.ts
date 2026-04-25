import { get, post, put, del } from './client';
import type {
  PlatformDto,
  CreatePlatformRequest,
  UpdatePlatformRequest,
  OAuthConnectResponse,
  OAuthPlatform,
} from './types';

const PATH = '/platforms';
const OAUTH_PATH = '/oauth';

export const platformsApi = {
  getAll: (signal?: AbortSignal) =>
    get<PlatformDto[]>(PATH, signal),

  getById: (id: string, signal?: AbortSignal) =>
    get<PlatformDto>(`${PATH}/${id}`, signal),

  create: (req: CreatePlatformRequest, signal?: AbortSignal) =>
    post<PlatformDto>(PATH, req, signal),

  update: (id: string, req: UpdatePlatformRequest, signal?: AbortSignal) =>
    put<PlatformDto>(`${PATH}/${id}`, req, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),

  // OAuth
  getConnectUrl: (platform: OAuthPlatform, signal?: AbortSignal) =>
    get<OAuthConnectResponse>(`${OAUTH_PATH}/${platform}/connect`, signal),

  disconnect: (platform: OAuthPlatform, signal?: AbortSignal) =>
    post<void>(`${OAUTH_PATH}/${platform}/disconnect`, undefined, signal),

  refreshToken: (platform: OAuthPlatform, signal?: AbortSignal) =>
    post<PlatformDto>(`${OAUTH_PATH}/${platform}/refresh`, undefined, signal),
};