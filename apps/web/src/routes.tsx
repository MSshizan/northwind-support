import { createBrowserRouter, Navigate, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { App } from './App';
import { NotFoundPage } from './features/NotFoundPage';
import { CustomerDetailPage } from './features/customers/CustomerDetailPage';
import { CustomerListPage } from './features/customers/CustomerListPage';
import { CreateTicketPage } from './features/tickets/CreateTicketPage';
import { TicketDetailPage } from './features/tickets/TicketDetailPage';
import { TicketListPage } from './features/tickets/TicketListPage';
import { LoginPage } from './features/auth/LoginPage';
import { useAuth } from './features/auth/AuthContext';

function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  const location = useLocation();

  if (!session) {
    return (
      <Navigate
        to="/login"
        replace
        state={{ from: location }}
      />
    );
  }

  return children;
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      { path: 'login', element: <LoginPage /> },
      {
        index: true,
        element: (
          <RequireAuth>
            <Navigate to="/tickets" replace />
          </RequireAuth>
        ),
      },
      {
        path: 'tickets',
        element: (
          <RequireAuth>
            <TicketListPage />
          </RequireAuth>
        ),
      },
      {
        path: 'tickets/new',
        element: (
          <RequireAuth>
            <CreateTicketPage />
          </RequireAuth>
        ),
      },
      {
        path: 'tickets/:id',
        element: (
          <RequireAuth>
            <TicketDetailPage />
          </RequireAuth>
        ),
      },
      {
        path: 'customers',
        element: (
          <RequireAuth>
            <CustomerListPage />
          </RequireAuth>
        ),
      },
      {
        path: 'customers/:id',
        element: (
          <RequireAuth>
            <CustomerDetailPage />
          </RequireAuth>
        ),
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
