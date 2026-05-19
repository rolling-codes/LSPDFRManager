import { api } from './client'
import type { JobStatusDto } from '../../types/job'

export function fetchJobStatus(jobId: string): Promise<JobStatusDto> {
  return api.get<JobStatusDto>(`/api/v1/jobs/${jobId}`)
}
