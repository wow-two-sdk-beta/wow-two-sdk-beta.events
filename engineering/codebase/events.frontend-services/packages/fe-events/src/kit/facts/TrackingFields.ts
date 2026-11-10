import type { JsonValue, LocalizedText } from '../../contracts';
import { pickText, type EventsLocale } from '../text/EventsLocale';

/** Describes one custom tracking field the form offers, read from the workflow's JSON Schema. */
export interface TrackingFieldDefinition {
  readonly key: string;
  readonly label: string;
  readonly type: 'text' | 'number' | 'boolean';
  readonly multiline: boolean;
}

/** Holds a custom field's value as the form edits it. */
export type TrackingFieldValue = string | number | boolean | null;

const isObject = (value: unknown): value is Readonly<Record<string, unknown>> =>
  value !== null && typeof value === 'object' && !Array.isArray(value);

function titleOf(schema: Readonly<Record<string, unknown>>, key: string, locale: EventsLocale): string {
  const title = schema['title'];
  if (typeof title === 'string' && title.trim()) return title;
  if (isObject(title)) return pickText(title as LocalizedText, locale) || key;
  return key;
}

/**
 * Reads the custom fields a workflow declares: the `properties` of its JSON Schema, as text, number or yes/no fields.
 * Without a schema the tracking's own field keys are offered as text, so nothing stored is hidden.
 */
export function trackingFieldDefinitions(
  schema: JsonValue | undefined,
  stored: JsonValue | undefined,
  locale: EventsLocale,
): TrackingFieldDefinition[] {
  const properties = isObject(schema) && isObject(schema['properties']) ? schema['properties'] : {};
  const definitions: TrackingFieldDefinition[] = Object.entries(properties).map(([key, value]) => {
    const property = isObject(value) ? value : {};
    const type = property['type'];
    return {
      key,
      label: titleOf(property, key, locale),
      type: type === 'number' || type === 'integer' ? 'number' : type === 'boolean' ? 'boolean' : 'text',
      multiline: property['format'] === 'textarea' || property['maxLength'] === undefined,
    };
  });
  if (isObject(stored)) {
    for (const key of Object.keys(stored)) {
      if (!definitions.some((definition) => definition.key === key))
        definitions.push({ key, label: key, type: 'text', multiline: true });
    }
  }
  return definitions;
}

/** Reads the stored values for the form, one per definition. */
export function trackingFieldValues(
  definitions: readonly TrackingFieldDefinition[],
  stored: JsonValue | undefined,
): Record<string, TrackingFieldValue> {
  const values: Record<string, TrackingFieldValue> = {};
  for (const definition of definitions) {
    const value = isObject(stored) ? stored[definition.key] : undefined;
    values[definition.key] =
      typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean'
        ? value
        : definition.type === 'boolean'
          ? false
          : null;
  }
  return values;
}

/** Writes the form's values as the tracking's `fields` object, leaving blank text out and keeping unknown keys. */
export function trackingFieldsPayload(
  values: Readonly<Record<string, TrackingFieldValue>>,
  stored: JsonValue | undefined,
): { [key: string]: JsonValue } {
  const payload: { [key: string]: JsonValue } = isObject(stored) ? { ...(stored as { [key: string]: JsonValue }) } : {};
  for (const [key, value] of Object.entries(values)) {
    if (value === null || (typeof value === 'string' && value.trim() === '')) delete payload[key];
    else payload[key] = typeof value === 'string' ? value.trim() : value;
  }
  return payload;
}
