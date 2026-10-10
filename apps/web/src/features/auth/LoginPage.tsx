import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { useAuth } from './AuthContext';

export function LoginPage() {
  const { session, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);

  const from = (
    location.state as { from?: { pathname?: string } } | null
  )?.from?.pathname ?? '/tickets';

  if (session) {
    return <Navigate to={from} replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');

    if (!email.trim() || !password) {
      setError('Enter your email address and password.');
      return;
    }

    setPending(true);

    try {
      await login(email, password);
      navigate(from, { replace: true });
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 401
          ? 'Invalid email or password.'
          : 'Unable to sign in. Please try again.',
      );
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="card login">
      <h1>Sign in</h1>
      <p className="page-header__subtitle">
        Sign in with your support agent account.
      </p>

      <form className="form" onSubmit={handleSubmit} noValidate>
        <div className="field">
          <label className="field__label" htmlFor="login-email">
            Email address
          </label>
          <input
            id="login-email"
            name="email"
            type="email"
            autoComplete="username"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            aria-invalid={Boolean(error)}
            aria-describedby={error ? 'login-error' : undefined}
          />
        </div>

        <div className="field">
          <label className="field__label" htmlFor="login-password">
            Password
          </label>
          <input
            id="login-password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
            aria-invalid={Boolean(error)}
            aria-describedby={error ? 'login-error' : undefined}
          />
        </div>

        {error && (
          <p id="login-error" className="error" role="alert">
            {error}
          </p>
        )}

        <button
          className="button button--primary"
          type="submit"
          disabled={pending}
        >
          {pending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </section>
  );
}
