import axios, { type InternalAxiosRequestConfig } from 'axios'
import type { AuthTokens } from '../types'

const STORAGE_KEY = 'recording-app-tokens'

export function getStoredTokens(): AuthTokens | null {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as AuthTokens
  } catch {
    return null
  }
}

export function storeTokens(tokens: AuthTokens | null) {
  if (tokens) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens))
  } else {
    localStorage.removeItem(STORAGE_KEY)
  }
}

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
})

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const tokens = getStoredTokens()
  if (tokens?.accessToken) {
    config.headers.Authorization = `Bearer ${tokens.accessToken}`
  }
  return config
})

let refreshPromise: Promise<AuthTokens | null> | null = null

async function refreshTokens(): Promise<AuthTokens | null> {
  const current = getStoredTokens()
  if (!current?.refreshToken) return null
  try {
    const res = await axios.post<AuthTokens>(
      `${api.defaults.baseURL}/auth/refresh`,
      { refreshToken: current.refreshToken },
    )
    storeTokens(res.data)
    return res.data
  } catch {
    storeTokens(null)
    return null
  }
}

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true
      refreshPromise ??= refreshTokens()
      const tokens = await refreshPromise
      refreshPromise = null
      if (tokens) {
        original.headers.Authorization = `Bearer ${tokens.accessToken}`
        return api(original)
      }
      window.location.assign('/login')
    }
    return Promise.reject(error)
  },
)
