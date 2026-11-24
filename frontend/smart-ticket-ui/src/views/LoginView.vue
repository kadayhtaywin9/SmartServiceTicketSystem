<template>
  <v-container class="fill-height d-flex align-center justify-center py-8">
    <v-card class="liquid-glass-panel pa-8" width="420" max-width="100%">
      <div class="d-flex justify-end align-center ga-2 mb-4">
        <v-btn
          size="x-small"
          variant="tonal"
          color="primary"
          class="font-weight-bold text-none px-3"
          @click="i18n.toggleLocale()"
        >
          {{ i18n.locale === 'en' ? 'မြန်မာ' : 'EN' }}
        </v-btn>
        <v-btn icon size="x-small" variant="tonal" color="primary" @click="toggleTheme">
          <v-icon size="16">{{ isDark ? 'mdi-white-balance-sunny' : 'mdi-moon-waning-crescent' }}</v-icon>
        </v-btn>
      </div>

      <div class="text-center mb-6">
        <v-avatar color="primary" size="48" class="mb-3 cyan-glow">
          <v-icon size="24" color="white">mdi-hexagon-multiple</v-icon>
        </v-avatar>
        <h1 class="text-h5 font-weight-bold">{{ i18n.t('loginTitle') }}</h1>
        <p class="text-body-2 text-medium-emphasis mt-1">{{ i18n.t('loginSubtitle') }}</p>
      </div>

      <v-alert v-if="errorMsg" type="error" variant="tonal" density="compact" class="mb-4" rounded="lg">
        {{ errorMsg }}
      </v-alert>

      <v-form @submit.prevent="submitLogin">
        <v-text-field
          v-model="email"
          :label="i18n.t('workEmail')"
          prepend-inner-icon="mdi-email-outline"
          required
          class="mb-2"
        />
        <v-text-field
          v-model="password"
          :label="i18n.t('password')"
          type="password"
          prepend-inner-icon="mdi-lock-outline"
          required
          class="mb-4"
        />
        <v-btn
          type="submit"
          color="primary"
          block
          size="large"
          :loading="loading"
          class="cyan-glow font-weight-bold text-none"
        >
          {{ i18n.t('continueWorkspace') }}
        </v-btn>
      </v-form>

      <div class="d-flex flex-wrap justify-center ga-2 mt-6">
        <v-chip size="x-small" color="primary" variant="tonal" @click="fillDemo('admin@company.local', 'Admin@123')">
          SuperAdmin
        </v-chip>
        <v-chip size="x-small" color="primary" variant="tonal" @click="fillDemo('lead@company.local', 'Lead@123')">
          Dept Lead
        </v-chip>
        <v-chip size="x-small" color="primary" variant="tonal" @click="fillDemo('dev@company.local', 'Dev@123')">
          Dev/QA
        </v-chip>
        <v-chip size="x-small" color="primary" variant="tonal" @click="fillDemo('it@company.local', 'It@123')">
          IT/Net
        </v-chip>
        <v-chip size="x-small" color="primary" variant="tonal" @click="fillDemo('hr@company.local', 'Hr@123')">
          HR
        </v-chip>
        <v-chip size="x-small" color="secondary" variant="tonal" @click="fillDemo('employee@company.local', 'User@123')">
          Employee
        </v-chip>
      </div>
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useTheme } from 'vuetify'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { useI18nStore } from '@/stores/i18nStore'

const theme = useTheme()
const router = useRouter()
const auth = useAuthStore()
const i18n = useI18nStore()

const email = ref('admin@company.local')
const password = ref('Admin@123')
const loading = ref(false)
const errorMsg = ref('')

const isDark = computed(() => theme.global.name.value === 'liquidGlassDark')
const toggleTheme = () => {
  const next = isDark.value ? 'liquidGlassLight' : 'liquidGlassDark'
  theme.global.name.value = next
  localStorage.setItem('stms_theme', next)
}

const fillDemo = (demoEmail: string, demoPass: string) => {
  email.value = demoEmail
  password.value = demoPass
}

const submitLogin = async () => {
  loading.value = true
  errorMsg.value = ''
  try {
    await auth.login(email.value, password.value)
    router.push('/inbox')
  } catch (err: any) {
    errorMsg.value = err.response?.data?.message || i18n.t('loginErrorFallback')
  } finally {
    loading.value = false
  }
}
</script>
