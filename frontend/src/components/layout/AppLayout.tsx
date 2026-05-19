import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'

export function AppLayout() {
  return (
    <>
      <Sidebar />
      <main
        style={{
          flex: 1,
          overflowY: 'auto',
          padding: '24px',
        }}
      >
        <Outlet />
      </main>
    </>
  )
}
