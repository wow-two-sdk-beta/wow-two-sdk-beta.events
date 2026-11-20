/** Names a small outline icon the media card draws itself: fe-events carries no icon library, so each is a few paths. */
export type EventGlyphName = 'calendar' | 'shirt' | 'mic' | 'store' | 'users' | 'flag' | 'more';

/** Holds each glyph's strokes, on a 24 × 24 grid drawn with a 2-unit round line. */
export const EventGlyphPaths: Readonly<Record<EventGlyphName, readonly string[]>> = {
  calendar: [
    'M8 2v4',
    'M16 2v4',
    'M3 10h18',
    'M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z',
  ],
  shirt: [
    'M20.38 3.46 16 2a4 4 0 0 1-8 0L3.62 3.46a2 2 0 0 0-1.34 2.23l.58 3.47a1 1 0 0 0 .99.84H6v10c0 1.1.9 2 2 2h8a2 2 0 0 0 2-2V10h2.15a1 1 0 0 0 .99-.84l.58-3.47a2 2 0 0 0-1.34-2.23z',
  ],
  mic: ['M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3z', 'M19 10v2a7 7 0 0 1-14 0v-2', 'M12 19v3'],
  store: ['M4 4h16l2 5H2l2-5z', 'M4 9v11h16V9', 'M10 20v-5h4v5'],
  users: [
    'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2',
    'M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z',
    'M22 21v-2a4 4 0 0 0-3-3.87',
    'M16 3.13a4 4 0 0 1 0 7.75',
  ],
  flag: ['M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z', 'M4 22v-7'],
  more: ['M12 12h.01', 'M19 12h.01', 'M5 12h.01'],
};

/** Maps well-known kind keys to a glyph; a catalog's own kinds carry no icon, so any other key reads as a calendar. */
const KindGlyphs: Readonly<Record<string, EventGlyphName>> = {
  runway: 'shirt',
  fashion: 'shirt',
  'fashion-week': 'shirt',
  show: 'shirt',
  showroom: 'shirt',
  conference: 'mic',
  forum: 'mic',
  summit: 'mic',
  congress: 'mic',
  seminar: 'mic',
  talk: 'mic',
  exhibition: 'store',
  expo: 'store',
  fair: 'store',
  market: 'store',
  'trade-show': 'store',
  'pop-up': 'store',
  workshop: 'users',
  masterclass: 'users',
  meetup: 'users',
  networking: 'users',
  training: 'users',
};

/** Returns the glyph for an event kind key, a calendar when the key is unknown. */
export function kindGlyph(kind: string | null | undefined): EventGlyphName {
  const key = (kind ?? '')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-');
  return KindGlyphs[key] ?? 'calendar';
}
