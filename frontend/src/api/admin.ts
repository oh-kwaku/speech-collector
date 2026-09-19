import { api } from './client'
import type { AdminStats, AdminUser, Role, UserWorkStats } from '../types'

export function listUsers() {
  return api.get<AdminUser[]>('/admin/users')
}

export function inviteUser(data: {
  name?: string
  email?: string
  phoneNumber?: string
  location?: string
  roles: Role[]
}) {
  return api.post<AdminUser>('/admin/invites', data)
}

export function updateUserProfile(
  userId: string,
  data: { name?: string; phoneNumber?: string; location?: string; roles?: Role[] },
) {
  return api.put<AdminUser>(`/admin/users/${userId}`, data)
}

export function disableUser(userId: string) {
  return api.post(`/admin/users/${userId}/disable`)
}

export function syncPhotos() {
  return api.post<{ added: number; total: number }>('/admin/photos/sync')
}

export function getAdminStats() {
  return api.get<AdminStats>('/admin/stats')
}

export function getUserWorkStats() {
  return api.get<UserWorkStats[]>('/admin/user-stats')
}
