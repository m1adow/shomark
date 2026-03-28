import { NavLink } from 'react-router-dom';

const navItems = [
  { label: 'Dashboard', icon: 'pi pi-th-large', to: '/' },
  { label: 'Campaigns', icon: 'pi pi-megaphone', to: '/campaigns' },
  { label: 'Analytics', icon: 'pi pi-chart-bar', to: '/analytics' },
  { label: 'Settings', icon: 'pi pi-cog', to: '/settings' },
] as const;

export default function Sidebar() {
  return (
    <aside className="hidden md:flex flex-col w-64 min-h-screen bg-white border-r border-gray-200">
      {/* Brand */}
      <div className="flex items-center gap-2 px-6 py-5 border-b border-gray-200">
        <span className="pi pi-bolt text-blue-600 text-xl" aria-hidden="true" />
        <span className="text-xl font-semibold text-gray-900 tracking-tight">
          ShoMark
        </span>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-3 py-4" aria-label="Main navigation">
        <ul className="flex flex-col gap-1 list-none m-0 p-0">
          {navItems.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                    isActive
                      ? 'bg-blue-50 text-blue-700'
                      : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
                  }`
                }
              >
                <i className={item.icon} aria-hidden="true" />
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      {/* Footer */}
      <div className="px-6 py-4 border-t border-gray-200">
        <p className="text-xs text-gray-400">&copy; 2026 ShoMark</p>
      </div>
    </aside>
  );
}
