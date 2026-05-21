export interface InstalledModDto {
  id: string
  name: string
  type: string
  typeColor: string
  typeLabel: string
  isEnabled: boolean
  isFavorite: boolean
  hasConflict: boolean
  version: string
  author: string
  installedAt: string
  totalSizeBytes: number
  totalSizeDisplay: string
  detectionScore: number
  notes: string
  imageUrl: string | null
  thumbnailUrl: string | null
  loadOrderPriority: number
}

export interface ModsListResponse {
  mods: InstalledModDto[]
  total: number
}
