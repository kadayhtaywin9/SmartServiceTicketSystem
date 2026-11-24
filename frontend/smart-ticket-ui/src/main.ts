import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import vuetify from './plugins/vuetify'
import './styles/liquid-glass.css'

const savedTheme = localStorage.getItem('stms_theme')
if (savedTheme === 'liquidGlassLight' || savedTheme === 'liquidGlassDark') {
  vuetify.theme.global.name.value = savedTheme
}

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.use(vuetify)
app.mount('#app')
