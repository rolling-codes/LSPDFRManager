import { api } from './client'
import type { CleanupScanResponse, CleanupApplyRequest, CleanupApplyResponse } from '../../types/cleanup'

export function fetchCleanupScan(): Promise<CleanupScanResponse> {
  return api.get<CleanupScanResponse>('/api/v1/cleanup/scan')
}

export function applyCleanup(req: CleanupApplyRequest): Promise<CleanupApplyResponse> {
  return api.post<CleanupApplyResponse>('/api/v1/cleanup/apply', req)
}
