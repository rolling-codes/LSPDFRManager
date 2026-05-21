import { useQuery } from '@tanstack/react-query'
import { fetchJobStatus } from '../api/job'
import type { JobStatusDto } from '../../types/job'

const TERMINAL_STATES = new Set<JobStatusDto['state']>(['Completed', 'Failed', 'Cancelled'])

function isTerminal(state: string | undefined): boolean {
  return !!state && TERMINAL_STATES.has(state as JobStatusDto['state'])
}

export function useJob(jobId: string | null) {
  const { data: jobStatus } = useQuery<JobStatusDto>({
    queryKey: ['job', jobId],
    queryFn: () => fetchJobStatus(jobId!),
    enabled: jobId !== null,
    refetchInterval: (query) => {
      const state = (query.state.data as JobStatusDto | undefined)?.state
      return isTerminal(state) ? false : 750
    },
  })

  const isPolling =
    jobId !== null &&
    (!jobStatus || !TERMINAL_STATES.has(jobStatus.state))

  return { jobStatus: jobStatus ?? null, isPolling }
}
