import type { QueryClient } from '@tanstack/react-query'

/**
 * Invalidates every query whose results are derived from the configured GTA V
 * path. Call this immediately after a successful config save that changes
 * GtaPath so pages such as Dashboard, Patrol Readiness, Library, Logs, etc.
 * all refetch fresh data without requiring an app restart.
 */
export function invalidateEnvironmentQueries(queryClient: QueryClient): Promise<void[]> {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: ['compatibility'] }),
    queryClient.invalidateQueries({ queryKey: ['patrol-readiness'] }),
    queryClient.invalidateQueries({ queryKey: ['mods'] }),
    queryClient.invalidateQueries({ queryKey: ['logs'] }),
    queryClient.invalidateQueries({ queryKey: ['diagnostics'] }),
    queryClient.invalidateQueries({ queryKey: ['cleanup'] }),
    queryClient.invalidateQueries({ queryKey: ['backups'] }),
    queryClient.invalidateQueries({ queryKey: ['history'] }),
  ])
}
