import { api } from './client'
import type { BrowseSearchResponse } from '../../types/browse'

export function searchBrowse(q: string, page = 1): Promise<BrowseSearchResponse> {
  return api.get<BrowseSearchResponse>(`/api/v1/browse/search?q=${encodeURIComponent(q)}&page=${page}`)
}
