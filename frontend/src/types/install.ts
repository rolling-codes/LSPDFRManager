export interface StartInstallRequest {
  sourcePath: string
}

export interface StartInstallResponse {
  jobId: string
}

export interface InstallResultDto {
  success: boolean
  userMessage: string | null
  error: string | null
  filesInstalled: number
}
