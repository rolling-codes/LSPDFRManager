import { api } from './client'
import type {
  BackupsListResponse,
  CreateBackupResponse,
  RestoreBackupRequest,
  RestoreBackupResponse,
} from '../../types/backups'

export function fetchBackups(): Promise<BackupsListResponse> {
  return api.get<BackupsListResponse>('/api/v1/backups')
}

export function startBackup(): Promise<CreateBackupResponse> {
  return api.post<CreateBackupResponse>('/api/v1/backups', {})
}

export function restoreBackup(request: RestoreBackupRequest): Promise<RestoreBackupResponse> {
  return api.post<RestoreBackupResponse>('/api/v1/backups/restore', request)
}

export function deleteBackup(fileName: string): Promise<void> {
  return api.delete<void>(`/api/v1/backups/${encodeURIComponent(fileName)}`)
}
