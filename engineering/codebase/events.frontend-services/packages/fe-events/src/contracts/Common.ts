/** Mirrors a localized text of the be contract: an object of locale → text, `und` when the language is unknown. */
export type LocalizedText = Readonly<Record<string, string>>;

/** Represents any JSON value, as the contract carries custom fields, payloads and legacy rows. */
export type JsonValue = string | number | boolean | null | readonly JsonValue[] | { readonly [key: string]: JsonValue };

/** Mirrors the success envelope every Events answer is wrapped in, except the calendar file. */
export interface ApiEnvelope<T> {
  readonly data: T;
}

/** Mirrors `CalendarFile`: an exported iCalendar file. */
export interface CalendarFile {
  readonly fileName: string;
  readonly content: string;
}
