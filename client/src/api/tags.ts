import { get, post, put, del } from './client';
import type { TagDto, CreateTagRequest, UpdateTagRequest } from './types';

const PATH = '/tags';

export const tagsApi = {
  getAll: (signal?: AbortSignal) =>
    get<TagDto[]>(PATH, signal),

  getById: (id: string, signal?: AbortSignal) =>
    get<TagDto>(`${PATH}/${id}`, signal),

  create: (req: CreateTagRequest, signal?: AbortSignal) =>
    post<TagDto>(PATH, req, signal),

  update: (id: string, req: UpdateTagRequest, signal?: AbortSignal) =>
    put<TagDto>(`${PATH}/${id}`, req, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),
};
