export interface BrowseModDto {
  id: string
  name: string
  author: string | null
  description: string | null
  version: string | null
  imageUrl: string | null
  downloadUrl: string | null
  pageUrl: string | null
}

export interface BrowseSearchResponse {
  results: BrowseModDto[]
  page: number
  totalResults: number
  hasMore: boolean
}
