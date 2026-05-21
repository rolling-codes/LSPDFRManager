import { api } from './client'
import type { StartInstallRequest, StartInstallResponse } from '../../types/install'

export function startInstall(request: StartInstallRequest): Promise<StartInstallResponse> {
  return api.post<StartInstallResponse>('/api/v1/install', request)
}
