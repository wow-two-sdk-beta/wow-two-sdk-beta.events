import type { EventsLocale } from '@wow-two-beta/fe-events/kit';

type Entry = Readonly<Record<EventsLocale, string>>;
const entry = (en: string, ru: string, uz: string): Entry => ({ en, ru, uz });

/** Holds the console frame's own texts; the screens bring theirs from `fe-events`. */
export const ConsoleMessages = {
  title: entry('Events console', 'Консоль мероприятий', 'Tadbirlar konsoli'),
  review: entry('Review', 'Проверка', 'Tekshiruv'),
  observations: entry('Observations', 'Наблюдения', 'Kuzatuvlar'),
  events: entry('Events', 'Мероприятия', 'Tadbirlar'),
  intake: entry('Intake', 'Приём', 'Qabul'),
  imports: entry('Imports', 'Импорт', 'Import'),
  catalog: entry('Catalog', 'Каталог', 'Katalog'),
  language: entry('Language', 'Язык', 'Til'),
  signIn: entry('Sign in', 'Войти', 'Kirish'),
  signOut: entry('Sign out', 'Выйти', 'Chiqish'),
  signInPrompt: entry(
    'Sign in with the catalog owner’s organization to review events.',
    'Войдите через организацию-владельца каталога, чтобы проверять мероприятия.',
    'Tadbirlarni tekshirish uchun katalog egasi tashkiloti orqali kiring.',
  ),
  checkingSession: entry('Checking your session…', 'Проверка сессии…', 'Sessiya tekshirilmoqda…'),
  sessionFailed: entry(
    'The session could not be checked. Try again shortly.',
    'Не удалось проверить сессию. Попробуйте чуть позже.',
    'Sessiyani tekshirib bo‘lmadi. Birozdan so‘ng urinib ko‘ring.',
  ),
  retry: entry('Try again', 'Повторить', 'Qayta urinish'),
  noOrganization: entry(
    'Your session has no active organization; sign in again and choose one.',
    'В сессии нет активной организации; войдите снова и выберите её.',
    'Sessiyada faol tashkilot yo‘q; qayta kirib, uni tanlang.',
  ),
  missingReview: entry(
    'Your role in {organization} lacks events:review, so the review screens will refuse.',
    'У вашей роли в {organization} нет права events:review, поэтому экраны проверки откажут.',
    '{organization} dagi rolingizda events:review yo‘q, shuning uchun tekshiruv ekranlari rad etadi.',
  ),
  noCatalogs: entry(
    'No catalog is visible to your organization.',
    'Вашей организации не виден ни один каталог.',
    'Tashkilotingizga hech qanday katalog ko‘rinmaydi.',
  ),
  appVersion: entry('app {version}', 'приложение {version}', 'ilova {version}'),
  apiVersion: entry('api {version}', 'api {version}', 'api {version}'),
} as const satisfies Record<string, Entry>;

export type ConsoleMessageKey = keyof typeof ConsoleMessages;

/** Writes one console text in `locale`, filling `{name}` placeholders. */
export function consoleText(
  key: ConsoleMessageKey,
  locale: EventsLocale,
  params: Readonly<Record<string, string>> = {},
): string {
  return ConsoleMessages[key][locale].replace(/\{(\w+)\}/g, (match, name: string) => params[name] ?? match);
}
