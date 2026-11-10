import type { EventsLocale } from './EventsLocale';

type Entry = Readonly<Record<EventsLocale, string>>;
const entry = (en: string, ru: string, uz: string): Entry => ({ en, ru, uz });

/** Holds every text the kit and the admin screens show, in English, Russian and Uzbek (Latin). */
export const EventsMessages = {
  // Dates
  datesNotAnnounced: entry('Dates not announced', 'Даты не объявлены', 'Sanalar e’lon qilinmagan'),
  dateNotAnnounced: entry('Date not announced', 'Дата не объявлена', 'Sana e’lon qilinmagan'),
  expected: entry('≈ {date} (expected)', '≈ {date} (ожидается)', '≈ {date} (kutilmoqda)'),
  yearOnly: entry('{year}, month not announced', '{year}, месяц не объявлен', '{year}, oyi e’lon qilinmagan'),
  deadlinePassed: entry('Deadline passed', 'Срок истёк', 'Muddat o‘tgan'),
  deadlineToday: entry('Deadline today', 'Срок сегодня', 'Muddat bugun'),
  oneDayLeft: entry('1 day left', 'Остался 1 день', '1 kun qoldi'),
  daysLeft: entry('{count} days left', 'Осталось дней: {count}', '{count} kun qoldi'),
  never: entry('Never', 'Никогда', 'Hech qachon'),

  // Review states
  reviewCandidate: entry('Awaiting review', 'Ожидает проверки', 'Tekshiruv kutilmoqda'),
  reviewPublished: entry('Published', 'Опубликовано', 'E’lon qilingan'),
  reviewRejected: entry('Rejected', 'Отклонено', 'Rad etilgan'),
  reviewArchived: entry('Archived', 'В архиве', 'Arxivlangan'),

  // Event statuses
  statusScheduled: entry('Scheduled', 'По плану', 'Rejalashtirilgan'),
  statusPostponed: entry('Postponed', 'Отложено', 'Keyinga surilgan'),
  statusRescheduled: entry('Rescheduled', 'Перенесено', 'Sanasi o‘zgargan'),
  statusCancelled: entry('Cancelled', 'Отменено', 'Bekor qilingan'),
  statusMovedOnline: entry('Moved online', 'Перенесено в онлайн', 'Onlayn formatga o‘tgan'),

  // Attendance and audience
  attendanceOffline: entry('In person', 'Очно', 'Oflayn'),
  attendanceOnline: entry('Online', 'Онлайн', 'Onlayn'),
  attendanceMixed: entry('In person and online', 'Очно и онлайн', 'Oflayn va onlayn'),
  audiencePublic: entry('Open to the public', 'Открыто для всех', 'Hamma uchun ochiq'),
  audienceTrade: entry('Trade visitors only', 'Только для специалистов', 'Faqat mutaxassislar uchun'),
  audienceInviteOnly: entry('By invitation', 'По приглашениям', 'Taklif bo‘yicha'),

  // Deadline kinds
  deadlineApplication: entry('Application', 'Заявка', 'Ariza'),
  deadlineRegistration: entry('Registration', 'Регистрация', 'Ro‘yxatdan o‘tish'),
  deadlineEarlyBird: entry('Early bird', 'Ранняя регистрация', 'Erta ro‘yxatdan o‘tish'),
  deadlineSubmission: entry('Submission', 'Подача работ', 'Ish topshirish'),
  deadlinePayment: entry('Payment', 'Оплата', 'To‘lov'),
  deadlineOther: entry('Other deadline', 'Другой срок', 'Boshqa muddat'),

  // Offer kinds
  offerTicket: entry('Ticket', 'Билет', 'Chipta'),
  offerExhibitor: entry('Exhibitor', 'Экспонент', 'Ko‘rgazmachi'),
  offerSponsorship: entry('Sponsorship', 'Спонсорство', 'Homiylik'),
  offerSubmission: entry('Submission', 'Подача', 'Topshiriq'),
  offerOther: entry('Other offer', 'Другое предложение', 'Boshqa taklif'),
  free: entry('Free', 'Бесплатно', 'Bepul'),
  priceNotStated: entry('Price not stated', 'Цена не указана', 'Narxi ko‘rsatilmagan'),

  // Priorities
  priorityHigh: entry('High', 'Высокий', 'Yuqori'),
  priorityNormal: entry('Normal', 'Обычный', 'Oddiy'),
  priorityLow: entry('Low', 'Низкий', 'Past'),
  highPriority: entry('High priority', 'Высокий приоритет', 'Yuqori ustuvorlik'),

  // Precision and certainty
  precisionUnknown: entry('Not announced', 'Не объявлено', 'E’lon qilinmagan'),
  precisionYear: entry('Year', 'Год', 'Yil'),
  precisionMonth: entry('Month', 'Месяц', 'Oy'),
  precisionDay: entry('Day', 'День', 'Kun'),
  precisionMinute: entry('Day and time', 'День и время', 'Kun va vaqt'),
  certaintyAnnounced: entry('Announced', 'Объявлено', 'E’lon qilingan'),
  certaintyConfirmed: entry('Confirmed', 'Подтверждено', 'Tasdiqlangan'),
  certaintyEstimated: entry('Estimated', 'Ожидается', 'Taxminiy'),

  // Observation states
  observationApplied: entry('Applied', 'Применено', 'Qo‘llangan'),
  observationPending: entry('Pending change', 'Ожидающее изменение', 'Kutilayotgan o‘zgarish'),
  observationSuperseded: entry('Superseded', 'Заменено новым', 'Yangisi bilan almashgan'),
  observationQuarantined: entry('Quarantined', 'В карантине', 'Karantinda'),
  observationRecorded: entry('Recorded', 'Записано', 'Qayd etilgan'),
  observationAccepted: entry('Accepted', 'Принято', 'Qabul qilingan'),
  observationDismissed: entry('Dismissed', 'Отклонено', 'Bekor qilingan'),
  observationIgnored: entry('Ignored', 'Пропущено', 'E’tiborsiz qoldirilgan'),

  // Failures
  failureValidation: entry(
    'Some values are not accepted.',
    'Некоторые значения не приняты.',
    'Ba’zi qiymatlar qabul qilinmadi.',
  ),
  failureSignIn: entry('Sign in to continue.', 'Войдите, чтобы продолжить.', 'Davom etish uchun tizimga kiring.'),
  failureForbidden: entry(
    'You do not have permission for this in the catalog owner’s organization.',
    'У вас нет на это прав в организации-владельце каталога.',
    'Katalog egasi tashkilotida bunga ruxsatingiz yo‘q.',
  ),
  failureReviewForbidden: entry(
    'Reviewing needs the events:review permission in the catalog owner’s organization.',
    'Для проверки нужно право events:review в организации-владельце каталога.',
    'Tekshirish uchun katalog egasi tashkilotida events:review ruxsati kerak.',
  ),
  failureAdminForbidden: entry(
    'Intake status needs the events:admin permission.',
    'Для статуса приёма нужно право events:admin.',
    'Qabul holati uchun events:admin ruxsati kerak.',
  ),
  failureNotFound: entry(
    'Not found, or not visible to your organization.',
    'Не найдено или не видно вашей организации.',
    'Topilmadi yoki tashkilotingizga ko‘rinmaydi.',
  ),
  failureConflict: entry(
    'This record changed since it was loaded. Reload and try again.',
    'Запись изменилась после загрузки. Обновите и попробуйте снова.',
    'Yozuv yuklangandan keyin o‘zgardi. Yangilab, qayta urinib ko‘ring.',
  ),
  failureUnavailable: entry(
    'The Events service is not answering. Try again shortly.',
    'Сервис мероприятий не отвечает. Попробуйте чуть позже.',
    'Tadbirlar xizmati javob bermayapti. Birozdan so‘ng urinib ko‘ring.',
  ),
  failureProtocol: entry(
    'The Events service answered in an unexpected shape.',
    'Сервис мероприятий ответил в неожиданном формате.',
    'Tadbirlar xizmati kutilmagan shaklda javob berdi.',
  ),
  failureUnexpected: entry('Something went wrong.', 'Что-то пошло не так.', 'Nimadir noto‘g‘ri ketdi.'),

  // Common actions and labels
  refresh: entry('Refresh', 'Обновить', 'Yangilash'),
  reload: entry('Reload', 'Обновить', 'Qayta yuklash'),
  loadMore: entry('Load more', 'Показать ещё', 'Yana ko‘rsatish'),
  cancel: entry('Cancel', 'Отмена', 'Bekor qilish'),
  save: entry('Save', 'Сохранить', 'Saqlash'),
  saved: entry('Saved.', 'Сохранено.', 'Saqlandi.'),
  loading: entry('Loading…', 'Загрузка…', 'Yuklanmoqda…'),
  all: entry('All', 'Все', 'Barchasi'),
  title: entry('Title', 'Название', 'Nomi'),
  kind: entry('Kind', 'Тип', 'Turi'),
  allKinds: entry('All kinds', 'Все типы', 'Barcha turlar'),
  status: entry('Status', 'Статус', 'Holati'),
  country: entry('Country', 'Страна', 'Mamlakat'),
  city: entry('City', 'Город', 'Shahar'),
  venue: entry('Venue', 'Площадка', 'Joy'),
  organizer: entry('Organizer', 'Организатор', 'Tashkilotchi'),
  website: entry('Website', 'Сайт', 'Veb-sayt'),
  tickets: entry('Tickets', 'Билеты', 'Chiptalar'),
  joinOnline: entry('Join online', 'Подключиться онлайн', 'Onlayn qo‘shilish'),
  starts: entry('Starts', 'Начало', 'Boshlanishi'),
  ends: entry('Ends', 'Окончание', 'Tugashi'),
  date: entry('Date', 'Дата', 'Sana'),
  precision: entry('Precision', 'Точность', 'Aniqlik'),
  certainty: entry('Certainty', 'Достоверность', 'Ishonchlilik'),
  when: entry('When', 'Когда', 'Qachon'),
  where: entry('Where', 'Где', 'Qayerda'),
  deadlines: entry('Deadlines', 'Сроки', 'Muddatlar'),
  offers: entry('Offers', 'Предложения', 'Takliflar'),
  links: entry('Links', 'Ссылки', 'Havolalar'),
  languages: entry('Languages', 'Языки', 'Tillar'),
  tags: entry('Tags', 'Теги', 'Teglar'),
  sources: entry('Sources', 'Источники', 'Manbalar'),
  firstSeen: entry('First seen', 'Впервые замечено', 'Birinchi ko‘rilgan'),
  lastSeen: entry('Last seen', 'Последний раз', 'Oxirgi marta ko‘rilgan'),
  imageBy: entry('Image: {attribution}', 'Изображение: {attribution}', 'Rasm: {attribution}'),

  // Feed
  eventsTitle: entry('Events', 'Мероприятия', 'Tadbirlar'),
  searchEvents: entry('Search events', 'Поиск мероприятий', 'Tadbirlarni qidirish'),
  searchPlaceholder: entry(
    'Title, organizer or city',
    'Название, организатор или город',
    'Nomi, tashkilotchi yoki shahar',
  ),
  showPast: entry('Show past events', 'Показывать прошедшие', 'O‘tgan tadbirlarni ko‘rsatish'),
  noEventsMatch: entry(
    'No events match these filters.',
    'Нет мероприятий по этим фильтрам.',
    'Bu filtrlarga mos tadbir topilmadi.',
  ),
  noUpcoming: entry('No upcoming events.', 'Нет предстоящих мероприятий.', 'Yaqinlashayotgan tadbirlar yo‘q.'),
  loadingEvents: entry('Loading events…', 'Загрузка мероприятий…', 'Tadbirlar yuklanmoqda…'),
  downloadCalendar: entry('Download calendar', 'Скачать календарь', 'Taqvimni yuklab olish'),
  downloadFailed: entry(
    'The calendar could not be downloaded.',
    'Не удалось скачать календарь.',
    'Taqvimni yuklab bo‘lmadi.',
  ),
  proposeEvent: entry('Propose an event', 'Предложить мероприятие', 'Tadbir taklif qilish'),
  sendForReview: entry('Send for review', 'Отправить на проверку', 'Tekshiruvga yuborish'),
  proposed: entry(
    'Sent for review: “{title}” is awaiting review and is not published yet.',
    'Отправлено на проверку: «{title}» ожидает проверки и пока не опубликовано.',
    'Tekshiruvga yuborildi: “{title}” tekshiruvni kutmoqda va hali e’lon qilinmagan.',
  ),
  titleRequired: entry(
    'Enter a title of up to 200 characters.',
    'Введите название до 200 символов.',
    '200 belgigacha nom kiriting.',
  ),
  dateShape: entry(
    'Write the date as the precision says: 2026, 2026-03, 2026-03-14 or 2026-03-14T19:00.',
    'Укажите дату по точности: 2026, 2026-03, 2026-03-14 или 2026-03-14T19:00.',
    'Sanani aniqlikka mos yozing: 2026, 2026-03, 2026-03-14 yoki 2026-03-14T19:00.',
  ),
  countryShape: entry(
    'Use a two-letter country code, such as UZ.',
    'Укажите двухбуквенный код страны, например UZ.',
    'Ikki harfli mamlakat kodini yozing, masalan UZ.',
  ),
  urlShape: entry(
    'Enter a full http:// or https:// address.',
    'Укажите полный адрес http:// или https://.',
    'To‘liq http:// yoki https:// manzilini kiriting.',
  ),

  // Deadlines
  nextDeadlines: entry('Next deadlines', 'Ближайшие сроки', 'Yaqin muddatlar'),
  noDeadlines: entry('No upcoming deadlines.', 'Ближайших сроков нет.', 'Yaqinlashayotgan muddatlar yo‘q.'),

  // Detail
  eventUnavailable: entry('Event unavailable', 'Мероприятие недоступно', 'Tadbir mavjud emas'),
  loadingEvent: entry('Loading event…', 'Загрузка мероприятия…', 'Tadbir yuklanmoqda…'),

  // Tracking
  tracking: entry('Tracking', 'Отслеживание', 'Kuzatuv'),
  stage: entry('Stage', 'Этап', 'Bosqich'),
  priority: entry('Priority', 'Приоритет', 'Ustuvorlik'),
  notes: entry('Notes', 'Заметки', 'Izohlar'),
  fields: entry('Fields', 'Поля', 'Maydonlar'),
  saveTracking: entry('Save tracking', 'Сохранить', 'Kuzatuvni saqlash'),
  notTracked: entry('Not tracked yet.', 'Пока не отслеживается.', 'Hali kuzatilmayapti.'),
  stopTracking: entry('Stop tracking', 'Перестать отслеживать', 'Kuzatishni to‘xtatish'),
  confirmStopTracking: entry(
    'Confirm: stop tracking',
    'Подтвердить: перестать отслеживать',
    'Tasdiqlash: kuzatishni to‘xtatish',
  ),
  history: entry('History', 'История', 'Tarix'),

  // Review queue
  reviewQueue: entry('Review queue', 'Очередь проверки', 'Tekshiruv navbati'),
  nothingToReview: entry('Nothing to review.', 'Проверять нечего.', 'Tekshiriladigan narsa yo‘q.'),
  pendingChanges: entry('{count} pending changes', 'Ожидающих изменений: {count}', '{count} ta kutilayotgan o‘zgarish'),
  accept: entry('Accept', 'Принять', 'Qabul qilish'),
  acceptWithEdits: entry('Accept with edits', 'Принять с правками', 'Tahrir bilan qabul qilish'),
  reject: entry('Reject', 'Отклонить', 'Rad etish'),
  confirmReject: entry('Confirm reject', 'Подтвердить отклонение', 'Rad etishni tasdiqlash'),
  mergeInto: entry('Merge into…', 'Объединить с…', 'Birlashtirish…'),
  mergeSearch: entry('Search published events', 'Поиск опубликованных', 'E’lon qilinganlardan qidirish'),
  merge: entry('Merge', 'Объединить', 'Birlashtirish'),
  apply: entry('Apply', 'Применить', 'Qo‘llash'),
  applyWithEdits: entry('Apply with edits', 'Применить с правками', 'Tahrir bilan qo‘llash'),
  dismiss: entry('Dismiss', 'Отклонить', 'Bekor qilish'),
  accepted: entry('Accepted and published.', 'Принято и опубликовано.', 'Qabul qilindi va e’lon qilindi.'),
  rejected: entry('Rejected.', 'Отклонено.', 'Rad etildi.'),
  merged: entry('Merged.', 'Объединено.', 'Birlashtirildi.'),
  applied: entry('Change applied.', 'Изменение применено.', 'O‘zgarish qo‘llandi.'),
  dismissed: entry('Change dismissed.', 'Изменение отклонено.', 'O‘zgarish bekor qilindi.'),
  current: entry('Current', 'Сейчас', 'Hozirgi'),
  proposedValue: entry('Proposed', 'Предложено', 'Taklif qilingan'),
  changedFields: entry('Changed fields', 'Изменённые поля', 'O‘zgargan maydonlar'),
  noChangedFields: entry(
    'No field differs from the event.',
    'Поля не отличаются от мероприятия.',
    'Maydonlar tadbirdan farq qilmaydi.',
  ),
  evidence: entry('Evidence', 'Доказательства', 'Dalillar'),
  sourceEvidence: entry('Source evidence', 'Данные источника', 'Manba dalili'),
  sourceEvidenceNote: entry(
    'Source evidence for review only — never shown as event content.',
    'Данные источника только для проверки — не показываются как содержимое мероприятия.',
    'Manba dalili faqat tekshiruv uchun — tadbir mazmuni sifatida ko‘rsatilmaydi.',
  ),
  observed: entry('Observed', 'Замечено', 'Kuzatilgan'),
  received: entry('Received', 'Получено', 'Qabul qilingan'),
  captured: entry('Captured', 'Снимок сделан', 'Suratga olingan'),
  capture: entry('Capture', 'Снимок', 'Surat'),
  sourceId: entry('Source id', 'Id источника', 'Manba id'),
  quarantineReason: entry('Quarantine reason', 'Причина карантина', 'Karantin sababi'),
  payload: entry('Payload', 'Данные', 'Ma’lumotlar'),
  sourceFields: entry('Source fields', 'Поля источника', 'Manba maydonlari'),
  delivery: entry('Delivery', 'Доставка', 'Yetkazish'),
  post: entry('Post', 'Запись', 'Yozuv'),
  revision: entry('Revision', 'Ревизия', 'Tahrir'),
  mappingVersion: entry('Mapping version', 'Версия сопоставления', 'Moslash versiyasi'),
  subscription: entry('Subscription', 'Подписка', 'Obuna'),
  pipeline: entry('Pipeline', 'Конвейер', 'Konveyer'),
  instance: entry('Instance', 'Экземпляр', 'Nusxa'),
  item: entry('Item', 'Элемент', 'Element'),
  reason: entry('Reason', 'Причина', 'Sabab'),

  // Observations
  observations: entry('Observations', 'Наблюдения', 'Kuzatuvlar'),
  noObservations: entry('No observations.', 'Наблюдений нет.', 'Kuzatuvlar yo‘q.'),
  state: entry('State', 'Состояние', 'Holat'),

  // Intake
  intake: entry('Intake', 'Приём', 'Qabul'),
  candidates: entry('Candidates', 'Кандидаты', 'Nomzodlar'),
  pendingChangesTotal: entry('Pending changes', 'Ожидающие изменения', 'Kutilayotgan o‘zgarishlar'),
  oldestUnresolved: entry('Oldest unresolved', 'Самое старое нерешённое', 'Eng eski hal qilinmagan'),
  pollNow: entry('Poll now', 'Опросить сейчас', 'Hozir so‘rash'),
  enabled: entry('Enabled', 'Включён', 'Yoqilgan'),
  disabled: entry('Disabled', 'Выключен', 'O‘chirilgan'),
  lastPolled: entry('Last polled', 'Последний опрос', 'Oxirgi so‘rov'),
  lastReceived: entry('Last received', 'Последнее получение', 'Oxirgi qabul'),
  newestObserved: entry('Newest observed', 'Самое новое', 'Eng yangi kuzatilgan'),
  quarantined: entry('Quarantined', 'В карантине', 'Karantinda'),
  failures: entry('Failures in a row', 'Ошибок подряд', 'Ketma-ket xatolar'),
  lastError: entry('Last error', 'Последняя ошибка', 'Oxirgi xato'),
  noSources: entry('No sources configured.', 'Источники не настроены.', 'Manbalar sozlanmagan.'),

  // Catalog events
  catalogEvents: entry('Catalog events', 'Мероприятия каталога', 'Katalog tadbirlari'),
  reviewState: entry('Review state', 'Состояние проверки', 'Tekshiruv holati'),
  allStates: entry('All states', 'Все состояния', 'Barcha holatlar'),
  dates: entry('Dates', 'Даты', 'Sanalar'),
  place: entry('Place', 'Место', 'Joy'),
  updated: entry('Updated', 'Обновлено', 'Yangilangan'),
  archive: entry('Archive', 'В архив', 'Arxivlash'),
  confirmArchive: entry('Confirm archive', 'Подтвердить архивирование', 'Arxivlashni tasdiqlash'),
  archivedDone: entry('Archived.', 'Отправлено в архив.', 'Arxivlandi.'),
  noEvents: entry('No events.', 'Мероприятий нет.', 'Tadbirlar yo‘q.'),
} as const satisfies Record<string, Entry>;

export type EventsMessageKey = keyof typeof EventsMessages;

/** Writes one message in `locale`, filling `{name}` placeholders from `params`. */
export function eventsText(
  key: EventsMessageKey,
  locale: EventsLocale,
  params: Readonly<Record<string, string | number>> = {},
): string {
  const template = EventsMessages[key][locale];
  return template.replace(/\{(\w+)\}/g, (match, name: string) =>
    params[name] === undefined ? match : String(params[name]),
  );
}
