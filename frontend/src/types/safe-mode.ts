export interface EmergencyRecoveryActionDto {
  description: string
  affectedPath: string
  willDisable: boolean
}

export interface EmergencyRecoveryPlanDto {
  mode: string
  actions: EmergencyRecoveryActionDto[]
  createdAt: string
}

export interface SafeModeApplyResponse {
  success: boolean
  error: string | null
  filesDisabled: number
}
