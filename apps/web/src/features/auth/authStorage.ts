import type { AuthSession } from './authTypes';

const SESSION_KEY = 'support-desk-auth';

export function getAuthSession(): AuthSession | null {
  const stored = sessionStorage.getItem(SESSION_KEY);

  if (!stored) {
    return null;
  }

  try {
    const session = JSON.parse(stored) as AuthSession;

    if (
      !session.accessToken ||
      !session.agent ||
      !session.expiresAtUtc ||
      new Date(session.expiresAtUtc).getTime() <= Date.now()
    ) {
      clearAuthSession();
      return null;
    }

    return session;
  } catch {
    clearAuthSession();
    return null;
  }
}

export function saveAuthSession(session: AuthSession): void {
  sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

export function clearAuthSession(): void {
  sessionStorage.removeItem(SESSION_KEY);
}
