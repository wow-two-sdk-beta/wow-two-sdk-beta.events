<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge } from '@wow-two-beta/fe/presentation/display';
import { EventsLocaleValues, FailureNotice, type EventsLocale } from '@wow-two-beta/fe-events/kit';
import { computed } from 'vue';
import { RouterLink } from 'vue-router';
import { useConsole } from '../../../application/console/UseConsole';
import { useSystemStatus } from '../../../application/system/UseSystemStatus';
import { consoleText, type ConsoleMessageKey } from '../../../domain/console/ConsoleMessages';
import { catalogQueryParameter, ConsolePaths } from '../../../domain/console/ConsolePaths';
import { organizationLabel, personLabel } from '../../../domain/session/ConsoleSession';

/**
 * Frames every console page: the navigation, the catalog picker, the language, the person and organization signed in
 * through Identity's BFF — or the way to sign in — and the running versions. Pages render only for a live session.
 */
defineOptions({ name: 'ConsoleShell' });

const app = useConsole();
const { status } = useSystemStatus();
const appVersion = __APP_VERSION__;
const locale = app.locale;

const session = computed(() => (app.session.value?.isAuthenticated ? app.session.value : null));
const signedOut = computed(() => app.session.value?.isAuthenticated === false);
const links = computed(() =>
  (
    [
      ['review', ConsolePaths.review],
      ['observations', ConsolePaths.observations],
      ['events', ConsolePaths.events],
      ['intake', ConsolePaths.intake],
      ['imports', ConsolePaths.imports],
    ] as const satisfies readonly (readonly [ConsoleMessageKey, string])[]
  ).map(([key, path]) => ({
    key,
    to: { path, query: app.catalog.value ? { [catalogQueryParameter]: app.catalog.value.slug } : {} },
  })),
);
const text = (key: ConsoleMessageKey, params?: Record<string, string>) => consoleText(key, locale.value, params);
function setLocale(value: string) {
  locale.value = value as EventsLocale;
}
</script>

<template>
  <div class="min-h-screen bg-background font-sans text-foreground">
    <header class="border-b border-border">
      <div class="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-3 px-6 py-4">
        <h1 class="text-xl font-semibold">{{ text('title') }}</h1>
        <nav v-if="session" class="flex flex-wrap gap-4 text-sm" :aria-label="text('title')">
          <RouterLink
            v-for="link in links"
            :key="link.key"
            :to="link.to"
            class="underline-offset-4 hover:underline"
            active-class="font-semibold"
            exact-active-class="font-semibold"
          >
            {{ text(link.key) }}
          </RouterLink>
        </nav>
        <div class="ml-auto flex flex-wrap items-center gap-3 text-sm">
          <label v-if="app.catalogs.value?.length" class="flex items-center gap-2">
            <span class="text-muted-foreground">{{ text('catalog') }}</span>
            <select
              name="catalog"
              class="rounded-md border border-border bg-background px-2 py-1"
              :value="app.catalog.value?.slug"
              @change="app.selectCatalog(($event.target as HTMLSelectElement).value)"
            >
              <option v-for="item in app.catalogs.value" :key="item.slug" :value="item.slug">{{ item.name }}</option>
            </select>
          </label>
          <label class="flex items-center gap-2">
            <span class="sr-only">{{ text('language') }}</span>
            <select
              name="locale"
              class="rounded-md border border-border bg-background px-2 py-1"
              :value="locale"
              @change="setLocale(($event.target as HTMLSelectElement).value)"
            >
              <option v-for="item in EventsLocaleValues" :key="item" :value="item">{{ item }}</option>
            </select>
          </label>
          <template v-if="session">
            <span aria-label="Signed in" data-test="session">
              <span class="font-medium">{{ personLabel(session.person) }}</span>
              <span v-if="session.organization" class="text-muted-foreground">
                · {{ organizationLabel(session.organization) }}
              </span>
            </span>
            <Button size="sm" variant="ghost" @click="app.signOut()">{{ text('signOut') }}</Button>
          </template>
          <a
            v-else-if="signedOut"
            :href="app.signInHref()"
            class="rounded-md bg-primary px-3 py-1.5 font-medium text-primary-foreground"
            data-test="sign-in"
          >
            {{ text('signIn') }}
          </a>
        </div>
      </div>
    </header>

    <main class="mx-auto max-w-6xl space-y-4 px-6 py-8">
      <p v-if="!app.session.value && !app.sessionFailed.value" role="status" class="text-muted-foreground">
        {{ text('checkingSession') }}
      </p>
      <div v-else-if="app.sessionFailed.value" class="flex flex-wrap items-center gap-3" role="alert">
        <span>{{ text('sessionFailed') }}</span>
        <Button size="sm" variant="outline" @click="app.reloadSession()">{{ text('retry') }}</Button>
      </div>
      <section
        v-else-if="signedOut"
        class="max-w-lg space-y-4 rounded-lg border border-border p-6"
        data-test="sign-in-panel"
      >
        <p>{{ text('signInPrompt') }}</p>
        <a
          :href="app.signInHref()"
          class="inline-block rounded-md bg-primary px-4 py-2 font-medium text-primary-foreground"
        >
          {{ text('signIn') }}
        </a>
      </section>
      <template v-else-if="session">
        <p
          v-if="!session.organization"
          class="rounded-md bg-warning-soft px-3 py-2 text-sm text-warning-soft-foreground"
          role="alert"
        >
          {{ text('noOrganization') }}
        </p>
        <p
          v-else-if="!app.canReview.value"
          class="rounded-md bg-warning-soft px-3 py-2 text-sm text-warning-soft-foreground"
          role="alert"
          data-test="missing-review"
        >
          {{ text('missingReview', { organization: organizationLabel(session.organization) }) }}
        </p>
        <FailureNotice v-if="app.catalogsFailure.value" :failure="app.catalogsFailure.value" :locale="locale" />
        <p v-else-if="app.catalogs.value && !app.catalogs.value.length" class="text-sm text-muted-foreground">
          {{ text('noCatalogs') }}
        </p>
        <slot></slot>
      </template>
    </main>

    <footer class="mx-auto flex max-w-6xl gap-2 px-6 pb-8">
      <span class="contents" aria-label="App version"><Badge>{{ text('appVersion', { version: appVersion }) }}</Badge></span>
      <span v-if="status" class="contents" aria-label="API version">
        <Badge>{{ text('apiVersion', { version: status.version }) }}</Badge>
      </span>
    </footer>
  </div>
</template>
