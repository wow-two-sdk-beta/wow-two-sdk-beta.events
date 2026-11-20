/**
 * Reads an element's Tailwind classes the way a browser at a given width would, since happy-dom does no layout: which
 * utilities apply at 390 px (a phone) or 1440 px (a desktop), `max-sm:` and `sm:` resolved against Tailwind's
 * breakpoints, and a later breakpoint overriding an earlier one for the same utility (`min-h-10 sm:min-h-8`).
 * It resolves the classes that decide a layout defect (heights, widths, columns, wrapping); it measures no pixels.
 */
export const phone = 390;
export const desktop = 1440;

const minWidths: Readonly<Record<string, number>> = { sm: 640, md: 768, lg: 1024, xl: 1280, '2xl': 1536 };

/** One utility and where it applies: `selector` is null for the element itself, else the descendants `[&_…]` names. */
export interface Rule {
  readonly selector: string | null;
  readonly utility: string;
}

/** Splits `max-sm:[&_[data-x]]:flex-col` at the colons outside brackets: the variants, then the utility. */
function split(name: string): { variants: string[]; utility: string } {
  const parts: string[] = [];
  let depth = 0;
  let start = 0;
  for (let index = 0; index < name.length; index++) {
    const char = name[index];
    if (char === '[' || char === '(') depth++;
    else if (char === ']' || char === ')') depth--;
    else if (char === ':' && depth === 0) {
      parts.push(name.slice(start, index));
      start = index + 1;
    }
  }
  return { variants: parts, utility: name.slice(start) };
}

/** Whether one breakpoint variant holds at `width`; null when the variant is no breakpoint. */
function holds(variant: string, width: number): boolean | null {
  if (variant.startsWith('max-')) {
    const limit = minWidths[variant.slice(4)];
    return limit === undefined ? null : width < limit;
  }
  const limit = minWidths[variant];
  return limit === undefined ? null : width >= limit;
}

/** Lists the rules of an element's classes that hold at `width`, in cascade order: base, `max-*`, then `sm`, `md`, … */
export function rulesAt(element: Element, width: number): Rule[] {
  const ranked: { rank: number; rule: Rule }[] = [];
  for (const name of (element.getAttribute('class') ?? '').split(/\s+/).filter(Boolean)) {
    const { variants, utility } = split(name);
    let selector: string | null = null;
    let rank = 0;
    let applies = true;
    for (const variant of variants) {
      const descendants = /^\[&_(.+)\]$/.exec(variant);
      if (descendants) {
        selector = descendants[1]!;
        continue;
      }
      const breakpoint = holds(variant, width);
      if (breakpoint === null)
        applies = false; // hover:, data-[…]:, focus-visible: … never hold for a static read
      else if (!breakpoint) applies = false;
      else rank = variant.startsWith('max-') ? 1 : 2 + minWidths[variant]!;
    }
    if (applies) ranked.push({ rank, rule: { selector, utility } });
  }
  return ranked.sort((a, b) => a.rank - b.rank).map((item) => item.rule);
}

/** The utility's family: its name without its value, so `min-h-10` and `min-h-8` compete; `col-auto` undoes `col-span-2`. */
const family = (utility: string) =>
  utility === 'col-auto'
    ? 'col-span'
    : /^(.*?)-(\d+(?:\.\d+)?|\[.+\]|full|auto|px|[0-9a-z]+xl|xs|sm|md|lg)$/.exec(utility)?.[1];

/**
 * The utilities that win at `width` for the element itself (or for the descendants `selector` names): one per utility
 * family, the last rule of the cascade. A trailing `!` is dropped, so `p-0!` reads `p-0`.
 */
export function utilitiesAt(element: Element, width: number, selector: string | null = null): string[] {
  const winners = new Map<string, string>();
  for (const rule of rulesAt(element, width)) {
    if (rule.selector !== selector) continue;
    const utility = rule.utility.replace(/!$/, '');
    winners.set(family(utility) ?? utility, utility);
  }
  return [...winners.values()];
}

/** Converts a spacing utility's value to px: `10` is 40 px, `[calc(2.5rem*var(…))]` is 40 px, `3xl` is a width scale. */
function pixels(value: string): number | null {
  if (/^\d+(\.\d+)?$/.test(value)) return Number(value) * 4;
  const rem = /^\[(?:calc\()?(\d+(?:\.\d+)?)rem/.exec(value);
  if (rem) return Number(rem[1]) * 16;
  return null;
}

/** The height a control has at `width` from its classes: the taller of `h-*` / `size-*` and `min-h-*`; 0 when none. */
export function heightAt(element: Element, width: number): number {
  let tallest = 0;
  for (const utility of utilitiesAt(element, width)) {
    const match = /^(h|size|min-h)-(.+)$/.exec(utility);
    const value = match ? pixels(match[2]!) : null;
    if (value !== null) tallest = Math.max(tallest, value);
  }
  return tallest;
}

/** The widest an element may be at `width` from its `max-w-*` class (`max-w-3xl` is 768 px); null when uncapped. */
export function maxWidthAt(element: Element, width: number): number | null {
  const scale: Readonly<Record<string, number>> = { xl: 576, '2xl': 672, '3xl': 768, '4xl': 896, '5xl': 1024 };
  for (const utility of utilitiesAt(element, width)) {
    const match = /^max-w-(.+)$/.exec(utility);
    if (match && scale[match[1]!] !== undefined) return scale[match[1]!]!;
  }
  return null;
}

/** How many columns a grid has at `width` from its `grid-cols-N` class: 1 when it has none. */
export function columnsAt(element: Element, width: number): number {
  for (const utility of utilitiesAt(element, width)) {
    const match = /^grid-cols-(\d+)$/.exec(utility);
    if (match) return Number(match[1]);
  }
  return 1;
}
