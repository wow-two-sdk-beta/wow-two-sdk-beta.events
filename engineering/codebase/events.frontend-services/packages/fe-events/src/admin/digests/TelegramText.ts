import { defineComponent, h, type PropType, type VNode } from 'vue';

/**
 * Shows a digest's text as Telegram shows it: the HTML subset the service writes (`b`, `strong`, `i`, `em`, `a href`)
 * becomes those elements, a link opens in a new tab, and anything else shows as plain text. Never sets inner HTML.
 */
export const TelegramText = defineComponent({
  name: 'TelegramText',
  props: { text: { type: String as PropType<string>, required: true } },
  setup(props) {
    return () => {
      const body = new DOMParser().parseFromString(`<body>${props.text}</body>`, 'text/html').body;
      return h(
        'div',
        {
          class: 'whitespace-pre-wrap break-words rounded-md border border-border bg-muted/30 p-3 text-sm',
          'data-test': 'telegram-text',
        },
        [...body.childNodes].map(render),
      );
    };
  },
});

function render(node: Node): VNode | string {
  if (node.nodeType === Node.TEXT_NODE) return node.textContent ?? '';
  if (node.nodeType !== Node.ELEMENT_NODE) return '';
  const element = node as Element;
  const children = [...element.childNodes].map(render);
  switch (element.tagName.toLowerCase()) {
    case 'b':
    case 'strong':
      return h('strong', children);
    case 'i':
    case 'em':
      return h('em', children);
    case 'a': {
      const href = element.getAttribute('href') ?? '';
      return /^https?:\/\//i.test(href)
        ? h('a', { href, target: '_blank', rel: 'noopener noreferrer', class: 'underline' }, children)
        : h('span', children);
    }
    default:
      return h('span', children);
  }
}
