import { api } from './client'
import type { Annotation, AnnotatorStats, Recording } from '../types'

export function listRecordings(params?: { annotated?: boolean }) {
  return api.get<Recording[]>('/recordings', { params })
}

export function listAnnotations() {
  return api.get<Annotation[]>('/annotations')
}

export function createAnnotation(recordingId: string, text: string) {
  return api.post<Annotation>(`/recordings/${recordingId}/annotations`, { text })
}

export function updateAnnotation(annotationId: string, text: string) {
  return api.put<Annotation>(`/annotations/${annotationId}`, { text })
}

export function getMyAnnotatorStats() {
  return api.get<AnnotatorStats>('/annotations/stats/mine')
}
