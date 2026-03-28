import { useCallback } from 'react';
import { usersApi } from '../api';
import type { CreateUserRequest, UpdateUserRequest } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function useUsers() {
  return useApiQuery(
    (signal) => usersApi.getAll(signal),
    [],
  );
}

export function useUser(id: string) {
  return useApiQuery(
    (signal) => usersApi.getById(id, signal),
    [id],
  );
}

export function useUserWithPlatforms(id: string) {
  return useApiQuery(
    (signal) => usersApi.getWithPlatforms(id, signal),
    [id],
  );
}

export function useCreateUser() {
  return useApiMutation(
    useCallback((req: CreateUserRequest) => usersApi.create(req), []),
  );
}

export function useUpdateUser() {
  return useApiMutation(
    useCallback(
      (id: string, req: UpdateUserRequest) => usersApi.update(id, req),
      [],
    ),
  );
}

export function useDeleteUser() {
  return useApiMutation(
    useCallback((id: string) => usersApi.delete(id), []),
  );
}
