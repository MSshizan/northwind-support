import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from './features/auth/AuthContext';

export function App() {
  const { session, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        {session && (
          <>
            <nav className="app__nav" aria-label="Main">
              <NavLink to="/tickets">Tickets</NavLink>
              <NavLink to="/customers">Customers</NavLink>
            </nav>

            <div className="app__account">
              <span>{session.agent.fullName}</span>
              <button
                className="button button--ghost"
                type="button"
                onClick={handleLogout}
              >
                Log out
              </button>
            </div>
          </>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
