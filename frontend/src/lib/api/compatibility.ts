import { api } from './client'
import type { CompatibilityResponse } from '../../types/compatibility'

export function fetchCompatibility(): Promise<CompatibilityResponse> {
  return api.get<CompatibilityResponse>('/api/v1/compatibility')
}
