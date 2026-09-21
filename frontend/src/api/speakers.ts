import { api } from './client'
import type { CollectorStats, Photo, Recording, RecordingSession, Speaker } from '../types'

export function listSpeakers() {
  return api.get<Speaker[]>('/speakers')
}

export function createSpeaker(data: { gender: string; ageYears: number }) {
  return api.post<Speaker>('/speakers', data)
}

export function updateSpeaker(speakerId: string, data: { gender: string; ageYears: number }) {
  return api.put<Speaker>(`/speakers/${speakerId}`, data)
}

export function getOrCreateActiveSession(speakerId: string) {
  return api.post<RecordingSession>(`/speakers/${speakerId}/sessions/active`)
}

export function listSessionRecordings(sessionId: string) {
  return api.get<Recording[]>(`/sessions/${sessionId}/recordings`)
}

export function getNextPhoto(sessionId: string) {
  return api.get<Photo>(`/photos/next`, { params: { sessionId } })
}

export function getUploadUrl(sessionId: string, photoId: string) {
  return api.post<{ recordingId: string; uploadUrl: string; s3Key: string }>(
    `/sessions/${sessionId}/recordings/upload-url`,
    { photoId },
  )
}

export function confirmRecording(
  recordingId: string,
  data: { s3Key: string; durationSeconds: number },
) {
  return api.post<Recording>(`/recordings/${recordingId}/confirm`, data)
}

export function listMyRecordings(params?: { from?: string; to?: string }) {
  return api.get<Recording[]>('/recordings/mine', { params })
}

export function getMyCollectorStats(params?: { from?: string; to?: string }) {
  return api.get<CollectorStats>('/recordings/stats/mine', { params })
}

