<script setup lang="ts">
import { EventGlyphPaths, type EventGlyphName } from '../facts/EventGlyphs';

/** Draws one of the media card's own outline icons in the current text colour; it is decorative. */
defineOptions({ name: 'EventGlyph' });

withDefaults(defineProps<{ name: EventGlyphName; size?: number }>(), { size: 16 });
</script>

<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    focusable="false"
    :data-glyph="name"
  >
    <path v-for="path in EventGlyphPaths[name]" :key="path" :d="path" />
  </svg>
</template>
