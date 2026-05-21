export interface ModProfileDto {
  id: string
  name: string
  notes: string | null
  createdAt: string
  lastUsedAt: string | null
  entryCount: number
}

export interface ProfilesListResponse {
  profiles: ModProfileDto[]
  activeProfileId: string | null
}

export interface CreateProfileRequest {
  name: string
  notes?: string
}
