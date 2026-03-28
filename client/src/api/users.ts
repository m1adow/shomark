import { get, post, put, del } from './client';
import type {
  UserDto,
  CreateUserRequest,
  UpdateUserRequest,
  UserWithPlatformsDto,
} from './types';

const PATH = '/users';

export const usersApi = {
  getAll: (signal?: AbortSignal) =>
    get<UserDto[]>(PATH, signal),

  getById: (id: string, signal?: AbortSignal) =>
    get<UserDto>(`${PATH}/${id}`, signal),

  getWithPlatforms: (id: string, signal?: AbortSignal) =>
    get<UserWithPlatformsDto>(`${PATH}/${id}/platforms`, signal),

  create: (req: CreateUserRequest, signal?: AbortSignal) =>
    post<UserDto>(PATH, req, signal),

  update: (id: string, req: UpdateUserRequest, signal?: AbortSignal) =>
    put<UserDto>(`${PATH}/${id}`, req, signal),

  delete: (id: string, signal?: AbortSignal) =>
    del(`${PATH}/${id}`, signal),
};
