export type Role = 'Admin' | 'Collector' | 'Annotator'

export type OtpChannel = 'Email' | 'Sms'

export interface AuthUser {
  id: string
  email: string | null
  phoneNumber: string | null
  roles: Role[]
}

export interface AuthTokens {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface Speaker {
  id: string
  gender: 'Male' | 'Female' | 'Other'
  ageYears: number
  createdAt: string
}

export interface RecordingSession {
  id: string
  speakerId: string
  status: 'Active' | 'Completed'
  startedAt: string
  recordingCount: number
}

export interface Photo {
  id: string
  url: string
}

export interface Recording {
  id: string
  sessionId: string
  speakerId: string
  photoId: string
  photoUrl: string
  audioUrl: string
  durationSeconds: number
  createdByUserId: string
  createdAt: string
  // IRR: how many independent annotators have annotated this recording so
  // far, out of how many it was sampled to need (usually 1).
  annotationCount: number
  requiredAnnotatorCount: number
}

export interface Annotation {
  id: string
  recordingId: string
  annotatorUserId: string
  annotatorEmail: string | null
  annotatorPhoneNumber: string | null
  collectorUserId: string
  collectorEmail: string | null
  collectorPhoneNumber: string | null
  text: string
  audioUrl: string
  photoUrl: string
  speakerId: string
  createdAt: string
  updatedAt: string
  // IRR: whether this is the annotation used for the training-data export.
  isCanonical: boolean
}

// IRR: a queue entry never carries annotation text (the recording may
// already have one from another annotator) so the viewer stays blind to it.
export interface RecordingQueueEntry {
  recordingId: string
  speakerId: string
  photoUrl: string
  audioUrl: string
  createdAt: string
  annotationCount: number
  targetAnnotatorCount: number
}

export interface IrrAnnotationEntry {
  annotationId: string
  annotatorUserId: string
  annotatorEmail: string | null
  annotatorPhoneNumber: string | null
  text: string
  updatedAt: string
  isCanonical: boolean
}

export interface IrrRecording {
  recordingId: string
  speakerId: string
  photoUrl: string
  audioUrl: string
  annotations: IrrAnnotationEntry[]
  // Average pairwise word-level similarity across all annotators (1 =
  // identical, 0 = completely different). List is sorted by this ascending.
  agreementScore: number
}

export interface AdminUser {
  id: string
  name: string | null
  email: string | null
  phoneNumber: string | null
  location: string | null
  roles: Role[]
  status: 'Invited' | 'Active' | 'Disabled'
  createdAt: string
}

export interface AdminStats {
  totalRecordings: number
  totalAnnotations: number
  recordingsByGender: Record<string, number>
  recordingsByAge: Record<string, number>
}

export interface AnnotatorStats {
  totalAnnotations: number
}

export interface CollectorStats {
  speakersRecorded: number
  confirmed: number
  unconfirmed: number
}

export interface UserWorkStats {
  userId: string
  name: string | null
  location: string | null
  totalRecordings: number
  totalSpeakers: number
  totalAnnotations: number
}

export interface ExportRow {
  photoId: string
  sessionId: string
  userId: string
  speakerId: string
  annotation: string
  created_at: string
  speaker_gender: string
  speaker_age: number
}
