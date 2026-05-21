import { api } from './client'
import type { EmergencyRecoveryPlanDto, SafeModeApplyResponse } from '../../types/safe-mode'

export function fetchSafeModePlan(mode?: string): Promise<EmergencyRecoveryPlanDto> {
  const query = mode ? `?mode=${encodeURIComponent(mode)}` : ''
  return api.get<EmergencyRecoveryPlanDto>(`/api/v1/safe-mode/plan${query}`)
}

export function applySafeMode(mode?: string): Promise<SafeModeApplyResponse> {
  return api.post<SafeModeApplyResponse>('/api/v1/safe-mode/apply', mode ? { mode } : {})
}

export function restoreFromSafeMode(): Promise<SafeModeApplyResponse> {
  return api.post<SafeModeApplyResponse>('/api/v1/safe-mode/restore', {})
}
