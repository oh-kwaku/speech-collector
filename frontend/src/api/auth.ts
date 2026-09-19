import { api } from './client'
import type { AuthTokens, AuthUser, OtpChannel } from '../types'

export function requestOtp(identifier: string, channel: OtpChannel) {
  return api.post('/auth/otp/request', { identifier, channel })
}

export function verifyOtp(identifier: string, channel: OtpChannel, code: string) {
  return api.post<AuthTokens & { user: AuthUser }>('/auth/otp/verify', {
    identifier,
    channel,
    code,
  })
}

export function acceptInvite(token: string, code: string) {
  return api.post<AuthTokens & { user: AuthUser }>('/auth/invites/accept', {
    token,
    code,
  })
}

export function fetchMe() {
  return api.get<AuthUser>('/auth/me')
}

export function logout(refreshToken: string) {
  return api.post('/auth/logout', { refreshToken })
}
