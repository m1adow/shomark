import { get, post, put, del } from './client';
import type {
  VideoDto,
  CreateVideoRequest,
  UpdateVideoRequest,
  VideoWithFragmentsDto,
  ProcessVideoRequest,
} from './types';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

const PATH = '/videos';

export const videosApi = {
  getAll: (signal?: AbortSignal) =>
    get<VideoDto[]>(PATH, signal),

  getById: (id: string, signal?: AbortSignal) =>
    get<VideoDto>(`${PATH}/${id}`, signal),

  getWithFragments: (id: string, signal?: AbortSignal) =>
    get<VideoWithFragmentsDto>(`${PATH}/${id}/fragments`, signal),

  create: (req: CreateVideoRequest, signal?: AbortSignal) =>
    post<VideoDto>(PATH, req, signal),

  upload: async (file: File, signal?: AbortSignal): Promise<VideoDto> => {
    const formData = new FormData();
    formData.append('file', file);

    const tokenProvider = (await import('./client')).getTokenProvider();
    const headers: Record<string, string> = {};
    if (tokenProvider) {
      const token = tokenProvider();
      if (token) headers['Authorization'] = `Bearer ${token}`;
    }

    const res = await fetch(`${BASE}${PATH}/upload`, {
      method: 'POST',
      headers,
      body: formData,
      signal,
    });

    if (!res.ok) {
      const body = await res.json();
      throw new Error(body.error ?? `Upload failed (${res.status})`);
    }

    return res.json();
  },

  getVideoUrl: (id: string, signal?: AbortSignal) =>
    get<{ url: string }>(`${PATH}/${id}/url`, signal),

  update: (id: string, req: UpdateVideoRequest, signal?: AbortSignal) =>
    put<VideoDto>(`${PATH}/${id}`, req, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),

  process: (id: string, req: ProcessVideoRequest, signal?: AbortSignal) =>
    post<{ message: string }>(`${PATH}/${id}/process`, req, signal),
};
