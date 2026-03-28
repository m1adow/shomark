import { useCallback } from 'react';
import { videosApi } from '../api';
import type { CreateVideoRequest, UpdateVideoRequest, ProcessVideoRequest } from '../api';
import { useApiQuery, useApiMutation } from './useApi';

export function useVideos() {
  return useApiQuery(
    (signal) => videosApi.getAll(signal),
    [],
  );
}

export function useVideo(id: string) {
  return useApiQuery(
    (signal) => videosApi.getById(id, signal),
    [id],
  );
}

export function useVideoWithFragments(id: string, enabled = true) {
  return useApiQuery(
    (signal) => videosApi.getWithFragments(id, signal),
    [id],
    { enabled },
  );
}

export function useCreateVideo() {
  return useApiMutation(
    useCallback((req: CreateVideoRequest) => videosApi.create(req), []),
  );
}

export function useUpdateVideo() {
  return useApiMutation(
    useCallback(
      (id: string, req: UpdateVideoRequest) => videosApi.update(id, req),
      [],
    ),
  );
}

export function useDeleteVideo() {
  return useApiMutation(
    useCallback((id: string) => videosApi.delete(id), []),
  );
}

export function useProcessVideo() {
  return useApiMutation(
    useCallback(
      (id: string, req: ProcessVideoRequest) => videosApi.process(id, req),
      [],
    ),
  );
}

export function useUploadVideo() {
  return useApiMutation(
    useCallback((file: File) => videosApi.upload(file), []),
  );
}

export function useVideoUrl(id: string, enabled = true) {
  return useApiQuery(
    (signal) => videosApi.getVideoUrl(id, signal),
    [id],
    { enabled },
  );
}
