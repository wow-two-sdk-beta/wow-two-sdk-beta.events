import { downloadText } from '@wow-two-beta/fe/foundation/files';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';
import type { EventQuery } from '../../contracts';
import type { EventsClient } from '../client/CreateEventsClient';
import type { EventsFailure } from '../client/EventsFailure';

/** Describes a calendar download: the catalog, the filter and the file name. */
export interface EventsCalendarRequest {
  readonly client: EventsClient;
  readonly catalog: string;
  /** The events filter; the service exports published events with exact dates and exact deadlines only. */
  readonly query?: EventQuery;
  /** The file name; `{catalog}-events.ics` when omitted. */
  readonly fileName?: string;
  readonly signal?: AbortSignal;
}

/**
 * Downloads the service's iCalendar export of a catalog's events as a local `.ics` file; it never connects to or changes
 * a calendar account. Resolves `true` when the browser took the download.
 */
export async function downloadEventsCalendar(request: EventsCalendarRequest): Promise<Result<boolean, EventsFailure>> {
  const result = await request.client.exportCalendar(request.catalog, request.query, request.signal);
  if (!result.ok) return result;
  const fileName = request.fileName ?? `${request.catalog}-events.ics`;
  return ResultExtensions.ok(downloadText(result.value, fileName, 'text/calendar;charset=utf-8'));
}
