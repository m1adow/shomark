import type { ApiError } from './types';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

let tokenProvider: (() => string | null) | null = null;

/** Register a function that returns the current JWT token. */
export function setTokenProvider(provider: () => string | null) {
  tokenProvider = provider;
}

/** Get the current token provider function. */
export function getTokenProvider() {
  return tokenProvider;
}

class ApiResponseError extends Error {
  status: number;
  body: ApiError;

  constructor(status: number, body: ApiError) {
    super(body.error ?? `API error ${status}`);
    this.name = 'ApiResponseError';
    this.status = status;
    this.body = body;
  }
}

export { ApiResponseError };

async function handleResponse<T>(res: Response): Promise<T> {
  if (res.status === 204) return undefined as T;

  const body = await res.json();

  if (!res.ok) {
    throw new ApiResponseError(res.status, body as ApiError);
  }

  return body as T;
}

function headers(extra?: HeadersInit): HeadersInit {
  const h: Record<string, string> = {
    'Content-Type': 'application/json',
  };

  const token = tokenProvider?.();
  if (token) {
    h['Authorization'] = `Bearer ${token}`;
  }

  return { ...h, ...extra };
}

export async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'GET',
    headers: headers(),
    signal,
  });
  return handleResponse<T>(res);
}

export async function post<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: headers(),
    body: body !== undefined ? JSON.stringify(body) : undefined,
    signal,
  });
  return handleResponse<T>(res);
}

export async function put<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'PUT',
    headers: headers(),
    body: JSON.stringify(body),
    signal,
  });
  return handleResponse<T>(res);
}

export async function del<T = void>(path: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'DELETE',
    headers: headers(),
    signal,
  });
  return handleResponse<T>(res);
}
