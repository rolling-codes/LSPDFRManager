export interface ChangeHistoryEntryDto {
  id: string
  action: string
  description: string
  affectedFile: string | null
  detail: string | null
  occurredAt: string
}

export interface HistoryResponse {
  entries: ChangeHistoryEntryDto[]
  total: number
}
