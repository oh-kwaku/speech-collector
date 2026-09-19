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
  isAnnotated: boolean
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
