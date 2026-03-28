import { useCallback } from 'react';
import { tagsApi } from '../api';
import type { CreateTagRequest, UpdateTagRequest } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function useTags() {
  return useApiQuery(
    (signal) => tagsApi.getAll(signal),
    [],
  );
}

export function useTag(id: string) {
  return useApiQuery(
    (signal) => tagsApi.getById(id, signal),
    [id],
  );
}

export function useCreateTag() {
  return useApiMutation(
    useCallback((req: CreateTagRequest) => tagsApi.create(req), []),
  );
}

export function useUpdateTag() {
  return useApiMutation(
    useCallback(
      (id: string, req: UpdateTagRequest) => tagsApi.update(id, req),
      [],
    ),
  );
}

export function useDeleteTag() {
  return useApiMutation(
    useCallback((id: string) => tagsApi.delete(id), []),
  );
}
