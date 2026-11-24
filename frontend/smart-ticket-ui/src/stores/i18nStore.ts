import { defineStore } from 'pinia'
import { translations, type Locale } from '@/locales/translations'

export const useI18nStore = defineStore('i18n', {
  state: (): { locale: Locale } => ({
    locale: (localStorage.getItem('stms_lang') as Locale) === 'my' ? 'my' : 'en'
  }),
  actions: {
    setLocale(lang: Locale) {
      this.locale = lang
      localStorage.setItem('stms_lang', lang)
      document.documentElement.lang = lang
    },
    toggleLocale() {
      this.setLocale(this.locale === 'en' ? 'my' : 'en')
    },
    t(key: string): string {
      return translations[this.locale]?.[key] ?? translations.en[key] ?? key
    }
  }
})
