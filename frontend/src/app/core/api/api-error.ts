import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../models/api.models';

/**
 * Turns an RFC 7807 error response into UI-ready pieces:
 * a general message + per-field errors keyed by camelCase field name.
 */
export function parseApiError(error: unknown): {
  message: string;
  fieldErrors: Record<string, string>;
  status: number;
} {
  const fallback = {
    message: 'Something went wrong. Please try again.',
    fieldErrors: {},
    status: 0,
  };

  if (!(error instanceof HttpErrorResponse)) return fallback;
  if (error.status === 0) {
    return { message: 'Cannot reach the server. Is the API running?', fieldErrors: {}, status: 0 };
  }

  const problem = error.error as ProblemDetails | null;
  const fieldErrors: Record<string, string> = {};

  if (problem?.errors) {
    for (const [key, messages] of Object.entries(problem.errors)) {
      // Backend uses PascalCase property names; forms use camelCase
      const field = key.charAt(0).toLowerCase() + key.slice(1);
      fieldErrors[field] = messages[0];
    }
  }

  return {
    message: problem?.detail ?? problem?.title ?? fallback.message,
    fieldErrors,
    status: error.status,
  };
}
