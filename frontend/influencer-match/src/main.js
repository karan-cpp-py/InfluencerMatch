import { createApp } from 'vue';
import App from './App.vue';
import router from './router';
import 'bootstrap/dist/css/bootstrap.min.css';
import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import './assets/style.css';
import { loadPlatformConfig } from './services/platform';

const app = createApp(App);
app.use(router);

loadPlatformConfig().finally(() => {
	app.mount('#app');
});
