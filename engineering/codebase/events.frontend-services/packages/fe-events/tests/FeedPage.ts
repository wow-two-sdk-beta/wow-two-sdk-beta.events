import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent, h, type PropType, type VNodeChild } from 'vue';
import {
  EventsFeed,
  eventsFilterLabels,
  useEventsFilters,
  type EventsFilters,
  type EventsFiltersOptions,
} from '../src/kit';
import { fakeService } from './Fixtures';

/** A component whose setup runs `build`, so a page's setup can be written in a test and its lifecycle hooks attach. */
export const Host = defineComponent({
  props: { build: { type: Function as PropType<() => () => VNodeChild>, required: true } },
  setup: (props) => props.build(),
});

type Responder = Parameters<typeof fakeService>[number];

/** Holds the feed's props that the filter state shares: the page passes each to both `useEventsFilters` and the feed. */
const sharedKeys = ['locale', 'kinds', 'regions', 'workflow'] as const;

/**
 * Mounts `EventsFeed` the way a page does: `useEventsFilters` runs in a host's setup and the result goes in as `filters`.
 * `props` are the feed's props, plus `tracking` and `hiddenStages`, which belong to the filter state; `locale`, `kinds`,
 * `regions` and `workflow` go to both.
 */
export async function mountFeed(
  props: Record<string, unknown>,
  responder: Responder,
  options: { slots?: Record<string, unknown>; attachTo?: HTMLElement } = {},
) {
  const service = fakeService(responder);
  const { tracking, hiddenStages, ...feedProps } = props;
  let filters!: EventsFilters;
  const host = mount(Host, {
    props: {
      build: () => {
        const shared = Object.fromEntries(
          sharedKeys.flatMap((key) => (key in feedProps ? [[key, feedProps[key]]] : [])),
        );
        filters = useEventsFilters({
          client: service.client,
          catalog: 'ocharo',
          today: '2026-11-10',
          tracking: Boolean(tracking),
          ...(hiddenStages ? { hiddenStages: hiddenStages as string[] } : {}),
          ...(shared as Partial<EventsFiltersOptions>),
        });
        return () =>
          h(
            EventsFeed,
            { client: service.client, catalog: 'ocharo', today: '2026-11-10', ...feedProps, filters } as never,
            (options.slots ?? {}) as never,
          );
      },
    },
    ...(options.attachTo ? { attachTo: options.attachTo } : {}),
  });
  await flushPromises();
  return { ...service, host, wrapper: host.findComponent(EventsFeed), filters };
}

/**
 * A minimal filter bar a page could build from the headless state: a search box, the match pill, and each facet's
 * options as buttons (a toggle as one button). It reads `facetDefinitions`, `search`, `selection`, `matchCount` and
 * `totalCount` and the kit's `eventsFilterLabels`, and nothing else.
 */
export function filterBar(filters: EventsFilters, locale: 'en' | 'ru' | 'uz' = 'en'): VNodeChild {
  const labels = eventsFilterLabels(locale);
  const picked = filters.selection.value;
  const total = filters.totalCount.value;
  const match = filters.matchCount.value;
  return h('div', { 'data-test': 'page-bar' }, [
    h('input', {
      type: 'search',
      'aria-label': labels.searchLabel,
      placeholder: labels.searchPlaceholder,
      value: filters.search.value,
      onInput: (event: Event) => (filters.search.value = (event.target as HTMLInputElement).value),
    }),
    match !== undefined
      ? h(
          'span',
          { 'data-test': 'page-bar-match' },
          labels.matchLabel.replace('{matched}', String(match)).replace('{total}', String(total ?? match)),
        )
      : null,
    ...filters.facetDefinitions.value.map((facet) =>
      h(
        'div',
        { 'data-facet': facet.key, 'data-placement': facet.placement },
        facet.kind === 'toggle'
          ? [
              h(
                'button',
                {
                  type: 'button',
                  'aria-pressed': picked[facet.key] === true,
                  onClick: () => {
                    const next = { ...picked };
                    if (picked[facet.key] === true) delete next[facet.key];
                    else next[facet.key] = true;
                    filters.selection.value = next;
                  },
                },
                facet.label,
              ),
            ]
          : facet.options.map((option) =>
              h(
                'button',
                {
                  type: 'button',
                  'data-value': option.value,
                  onClick: () =>
                    (filters.selection.value = {
                      ...picked,
                      [facet.key]: facet.kind === 'multi' ? [option.value] : option.value,
                    }),
                },
                option.count === undefined ? option.label : `${option.label} ${option.count}`,
              ),
            ),
      ),
    ),
  ]);
}
