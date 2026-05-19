export interface DiagnosticFindingDto {
  category: string
  title: string
  detail: string | null
  recommendedFix: string | null
  affectedPath: string | null
  severity: 'Ok' | 'Info' | 'Warning' | 'Error' | 'Critical'
}

export interface DiagnosticsResponse {
  findings: DiagnosticFindingDto[]
  totalFindings: number
  errorCount: number
  warningCount: number
}
