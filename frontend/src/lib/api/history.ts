import { api } from './client'
import type { HistoryResponse } from '../../types/history'

export function fetchHistory(limit = 50, offset = 0): Promise<HistoryResponse> {
  return api.get<HistoryResponse>(`/api/v1/history?limit=${limit}&offset=${offset}`)
}
