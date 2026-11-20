import { createApp } from 'vue';
import CardGallery from '../presentation/gallery/CardGallery.vue';
import './index.css';

/** Mounts the event-card gallery of `cards.html`, a development page with no session, router or console. */
createApp(CardGallery).mount('#root');
