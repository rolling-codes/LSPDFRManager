export interface ComponentVersionDto {
  name: string
  present: boolean
  version: string | null
  hash: string | null
}

export interface CompatibilityResponse {
  components: ComponentVersionDto[]
  gtaPathConfigured: boolean
  detectedAt: string
}
