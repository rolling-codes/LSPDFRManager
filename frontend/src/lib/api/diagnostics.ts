import { api } from './client'
import type { DiagnosticsResponse } from '../../types/diagnostics'

export function fetchDiagnostics(): Promise<DiagnosticsResponse> {
  return api.get<DiagnosticsResponse>('/api/v1/diagnostics')
}
