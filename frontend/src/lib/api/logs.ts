import { api } from './client'
import type { LogsAvailableResponse, LogLinesResponse } from '../../types/logs'

export function fetchAvailableLogs(): Promise<LogsAvailableResponse> {
  return api.get<LogsAvailableResponse>('/api/v1/logs')
}

export function fetchLogLines(name: string, tail = 200): Promise<LogLinesResponse> {
  return api.get<LogLinesResponse>(`/api/v1/logs/${encodeURIComponent(name)}?tail=${tail}`)
}
