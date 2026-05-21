import { api } from './client'
import type { PatrolReadinessResultDto } from '../../types/patrol-readiness'

export function fetchPatrolReadiness(): Promise<PatrolReadinessResultDto> {
  return api.get<PatrolReadinessResultDto>('/api/v1/patrol-readiness')
}
