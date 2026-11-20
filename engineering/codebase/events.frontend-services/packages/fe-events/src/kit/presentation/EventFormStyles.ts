/**
 * The native fields of the kit's and the admin's forms: design tokens only, and 40 px high at every width, so a thumb
 * lands on a field and the fe sdk's `TextInput` (also 40 px) sits level with a select or a date.
 */
export const eventFieldClass =
  'min-h-10 rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground';

/** The one label style of an event form: a field's name and a date's three parts read alike. */
export const eventLabelClass = 'text-sm font-medium';

/** A native checkbox: 20 px and tinted by the primary token, where the browser's own box is 13 px. */
export const eventCheckboxClass = 'size-5 shrink-0 accent-primary';

/** The row that holds a checkbox and its text: the whole row is the tap target, at least 40 px high. */
export const eventCheckRowClass = 'flex min-h-10 items-center gap-2 text-sm';
