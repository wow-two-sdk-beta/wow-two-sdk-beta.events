import { createApp } from 'vue';
import FeedGallery from '../presentation/gallery/FeedGallery.vue';
import './index.css';

/** Mounts the events-feed page of `feed.html`, a development page with no session, router or console. */
createApp(FeedGallery).mount('#root');
