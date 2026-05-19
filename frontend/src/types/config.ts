export type BackupScheduleMode =
  | 'ManualOnly'
  | 'EveryLaunch'
  | 'Daily'
  | 'Weekly'
  | 'BeforeProfileSwitch'
  | 'BeforeInstall'
  | 'BeforeSafeLaunch'

export interface AppConfigDto {
  gtaPath: string
  backupPath: string
  autoBackupOnInstall: boolean
  confirmBeforeUninstall: boolean
  autoLaunchAfterInstall: boolean
  autoInstallHighConfidence: boolean
  deleteTempAfterInstall: boolean
  maxInstallLogEntries: number
  minimumFreeDiskSpaceMb: number
  autoStartBrowseApi: boolean
  browseApiPath: string | null
  browseApiBaseUrl: string
  autoBackupEnabled: boolean
  backupScheduleMode: BackupScheduleMode
  maxBackupCount: number
  compressBackups: boolean
  showSetupWizardOnStartup: boolean
  checkForUpdatesOnStartup: boolean
  uiScale: number
}

export interface UpdateConfigRequest {
  gtaPath?: string
  backupPath?: string
  autoBackupOnInstall?: boolean
  confirmBeforeUninstall?: boolean
  autoLaunchAfterInstall?: boolean
  autoInstallHighConfidence?: boolean
  deleteTempAfterInstall?: boolean
  maxInstallLogEntries?: number
  minimumFreeDiskSpaceMb?: number
  autoStartBrowseApi?: boolean
  browseApiPath?: string | null
  browseApiBaseUrl?: string
  autoBackupEnabled?: boolean
  backupScheduleMode?: BackupScheduleMode
  maxBackupCount?: number
  compressBackups?: boolean
  showSetupWizardOnStartup?: boolean
  checkForUpdatesOnStartup?: boolean
  uiScale?: number
}

export interface ValidateGtaPathResponse {
  valid: boolean
  error: string | null
}
