import { api } from './client'
import type { AppConfigDto, UpdateConfigRequest, ValidateGtaPathResponse } from '../../types/config'

export function fetchConfig(): Promise<AppConfigDto> {
  return api.get<AppConfigDto>('/api/v1/config')
}

export function updateConfig(req: UpdateConfigRequest): Promise<AppConfigDto> {
  return api.put<AppConfigDto>('/api/v1/config', req)
}

export function validateGtaPath(path: string): Promise<ValidateGtaPathResponse> {
  return api.post<ValidateGtaPathResponse>('/api/v1/config/validate-gta-path', { path })
}
