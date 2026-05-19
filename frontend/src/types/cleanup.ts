export interface RemovalCandidateDto {
  relativePath: string
  classification: string
  riskLevel: string
  sizeBytes: number
  sizeDisplay: string
  isBlocked: boolean
}

export interface CleanupScanResponse {
  candidates: RemovalCandidateDto[]
  totalSizeBytes: number
}

export interface CleanupApplyRequest {
  relativePaths: string[]
  mode: 'Safe' | 'Aggressive'
}

export interface CleanupApplyResponse {
  success: boolean
  filesDeleted: number
  bytesFreed: number
  error: string | null
}
