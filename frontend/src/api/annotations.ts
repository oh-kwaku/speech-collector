import { api } from './client'
import type { Annotation, AnnotatorStats, IrrRecording, Recording, RecordingQueueEntry } from '../types'

export function listRecordings(params?: { annotated?: boolean }) {
  return api.get<Recording[]>('/recordings', { params })
}

export function listAnnotations() {
  return api.get<Annotation[]>('/annotations')
}

// IRR: recordings that still need more independent annotators and that the
// current user hasn't already annotated. Replaces listRecordings({annotated:
// false}) as the queue's data source, since "has any annotation" is no
// longer the same question as "still needs annotators" once a recording can
// take more than one.
export function listAnnotationQueue() {
  return api.get<RecordingQueueEntry[]>('/annotations/queue')
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

// IRR: admin-only report of recordings with 2+ independent annotations.
export function getIrrReport() {
  return api.get<IrrRecording[]>('/annotations/irr')
}

// IRR: admin-only override of which annotation is used for the training-data
// export (defaults to the first one submitted).
export function setCanonicalAnnotation(recordingId: string, annotationId: string) {
  return api.put(`/recordings/${recordingId}/canonical-annotation`, { annotationId })
}
