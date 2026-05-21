export interface BackupFileDto {
  fileName: string
  sizeBytes: number
  sizeDisplay: string
  lastWriteUtc: string
}

export interface BackupsListResponse {
  backups: BackupFileDto[]
}

export interface CreateBackupResponse {
  jobId: string
}

export interface RestoreBackupRequest {
  fileName: string
}

export interface RestoreBackupResponse {
  jobId: string
}
