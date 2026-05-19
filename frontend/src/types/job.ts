export interface JobStatusDto {
  jobId: string
  state: 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'
  progressPct: number
  error: string | null
  resultJson: string | null
}
