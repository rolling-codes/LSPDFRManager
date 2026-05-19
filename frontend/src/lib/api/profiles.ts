import { api } from './client'
import type { ModProfileDto, ProfilesListResponse, CreateProfileRequest } from '../../types/profiles'

export function fetchProfiles(): Promise<ProfilesListResponse> {
  return api.get<ProfilesListResponse>('/api/v1/profiles')
}

export function createProfile(req: CreateProfileRequest): Promise<ModProfileDto> {
  return api.post<ModProfileDto>('/api/v1/profiles', req)
}

export function updateProfile(id: string, req: { name?: string; notes?: string }): Promise<ModProfileDto> {
  return api.put<ModProfileDto>(`/api/v1/profiles/${id}`, req)
}

export function deleteProfile(id: string): Promise<void> {
  return api.delete<void>(`/api/v1/profiles/${id}`)
}
