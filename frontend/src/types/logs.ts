export interface LogFileInfoDto {
  name: string
  label: string
}

export interface LogsAvailableResponse {
  logs: LogFileInfoDto[]
}

export interface LogLinesResponse {
  name: string
  label: string
  lines: string[]
  totalLines: number
}
