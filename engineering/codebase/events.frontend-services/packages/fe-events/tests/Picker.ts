import { flushPromises } from '@vue/test-utils';
import { vi } from 'vitest';

/**
 * Drives the fe sdk's `SelectPicker` the way a person does: open the trigger, read the panel, click an option. The panel
 * is the sdk's `PopoverContent` (a `role="dialog"` layer teleported to the document body) holding a `role="listbox"`.
 * No test here sets a `<select>`'s value: the model is read from the trigger's label or the hidden form input.
 */
const panelOptions = () => [
  ...document.querySelectorAll<HTMLElement>('[role="dialog"] [role="listbox"] [role="option"]'),
];

/** The labels the open panel lists, in order; empty while no picker is open. */
export const panelLabels = () => panelOptions().map((option) => option.textContent?.trim() ?? '');

/** Opens the picker behind `trigger` (its button) and waits for the panel. */
export async function openPicker(trigger: Element): Promise<void> {
  (trigger as HTMLElement).click();
  await flushPromises();
}

/** Clicks the open panel's option with this label and waits for the panel to close. */
export async function pickOption(label: string): Promise<void> {
  const option = panelOptions().find((item) => item.textContent?.trim() === label);
  if (!option) throw new Error(`No option "${label}" in the open panel: [${panelLabels().join(', ')}]`);
  option.click();
  await flushPromises();
  // The panel leaves after its exit frames (`Presence`), not at the click.
  await vi.waitFor(() => {
    if (document.querySelector('[role="dialog"]')) throw new Error('The picker panel is still open');
  });
}

/** Opens the picker behind `trigger` and picks the option with this label. */
export async function pick(trigger: Element, label: string): Promise<void> {
  await openPicker(trigger);
  await pickOption(label);
}

/** The value the picker ships to a native form: its hidden input, found by name under `root`; null with no selection. */
export function submitted(root: Element, name: string): string | null {
  return root.querySelector<HTMLInputElement>(`input[type="hidden"][name="${name}"]`)?.value ?? null;
}
