export interface PatrolReadinessResultDto {
  blocking: string[]
  warnings: string[]
  passing: string[]
  isReady: boolean
}
