
import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail, TicketEscalation } from '../../../types/api';
import { formatDateTime } from '../../../lib/format';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
}

export function EscalationPanel({ ticket, onUpdated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [history, setHistory] = useState<TicketEscalation[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [historyError, setHistoryError] = useState('');
  const [success, setSuccess] = useState('');

  const loadHistory = useCallback(async () => {
    setLoadingHistory(true);
    setHistoryError('');

    try {
      const entries = await ticketsApi.getEscalations(ticket.id);
      setHistory(entries);
    } catch (err) {
      setHistoryError(
        toErrorMessage(err, 'Unable to load escalation history.'),
      );
    } finally {
      setLoadingHistory(false);
    }
  }, [ticket.id]);

  useEffect(() => {
    void loadHistory();
  }, [loadHistory]);

  const canEscalate =
    ticket.status !== 'Resolved' &&
    ticket.status !== 'Closed' &&
    ticket.priority !== 'Critical';

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedReason = reason.trim();

    if (trimmedReason.length < 5 || trimmedReason.length > 500) {
      setError('The reason must contain between 5 and 500 characters.');
      return;
    }

    setSubmitting(true);
    setError('');
    setSuccess('');

    try {
      const updatedTicket = await ticketsApi.escalateTicket(ticket.id, {
        reason: trimmedReason,
      });

      onUpdated(updatedTicket);
      setReason('');
      setSuccess('Ticket escalated successfully.');

      await loadHistory();
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setError(err.message);
      } else {
        setError(toErrorMessage(err, 'Unable to escalate this ticket.'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="card" aria-labelledby="escalation-heading">
      <h2 id="escalation-heading">Ticket escalation</h2>

      {canEscalate ? (
        <form onSubmit={(event) => void handleSubmit(event)}>
          <div>
            <label htmlFor="escalation-reason">Reason for escalation</label>
            <textarea
              id="escalation-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              minLength={5}
              maxLength={500}
              rows={3}
              required
              aria-describedby="escalation-reason-help"
            />
            <p id="escalation-reason-help" className="summary__note">
              Enter 5–500 characters. {reason.trim().length}/500
            </p>
          </div>

          {error && (
            <p role="alert" className="error-message">
              {error}
            </p>
          )}

          {success && <p role="status">{success}</p>}

          <button type="submit" disabled={submitting}>
            {submitting ? 'Escalating…' : `Escalate from ${ticket.priority}`}
          </button>
        </form>
      ) : (
        <p>
          {ticket.priority === 'Critical'
            ? 'This ticket is already at Critical priority.'
            : 'Resolved or closed tickets cannot be escalated.'}
        </p>
      )}

      <h3>Escalation history</h3>

      {loadingHistory && <p>Loading escalation history…</p>}

      {!loadingHistory && historyError && (
        <div>
          <p role="alert">{historyError}</p>
          <button type="button" onClick={() => void loadHistory()}>
            Retry
          </button>
        </div>
      )}

      {!loadingHistory && !historyError && history.length === 0 && (
        <p>No escalation history yet.</p>
      )}

      {!historyError && history.length > 0 && (
        <ol>
          {history.map((entry) => (
            <li key={entry.id}>
              <p>
                <strong>
                  {entry.fromPriority} → {entry.toPriority}
                </strong>
              </p>
              <p>{entry.reason}</p>
              <p>
                <strong>Escalated by:</strong> {entry.escalatedBy}
              </p>
              <p>
                <strong>Previous agent:</strong>{' '}
                {entry.fromAgentId ?? 'Unassigned'}
              </p>
              <p>
                <strong>New agent:</strong>{' '}
                {entry.toAgentId ?? 'Unassigned'}
              </p>
              <p>
                <strong>Previous SLA due:</strong>{' '}
                {entry.fromDueAtUtc
                  ? formatDateTime(entry.fromDueAtUtc)
                  : 'Not set'}
              </p>
              <p>
                <strong>New SLA due:</strong>{' '}
                {entry.toDueAtUtc
                  ? formatDateTime(entry.toDueAtUtc)
                  : 'Not set'}
              </p>
              <p>{formatDateTime(entry.escalatedAtUtc)}</p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
