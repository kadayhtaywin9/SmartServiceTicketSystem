<template>
  <v-navigation-drawer
    v-model="drawer"
    :permanent="$vuetify.display.mdAndUp"
    width="238"
    class="liquid-glass-sidebar"
  >
    <div class="pa-5 pb-4 d-flex align-center ga-3">
      <v-avatar color="primary" size="36" class="cyan-glow">
        <v-icon color="white" size="20">mdi-hexagon-multiple</v-icon>
      </v-avatar>
      <div class="text-subtitle-1 font-weight-bold text-truncate">{{ i18n.t('appName') }}</div>
    </div>

    <div class="px-4 mb-4">
      <v-btn
        block
        color="primary"
        prepend-icon="mdi-plus"
        to="/new-request"
        class="cyan-glow text-none font-weight-semibold"
      >
        {{ i18n.t('newRequest') }}
      </v-btn>
    </div>

    <v-list nav density="comfortable" class="px-3 bg-transparent">
      <v-list-item
        to="/inbox"
        prepend-icon="mdi-inbox-outline"
        :title="auth.isStaff ? i18n.t('serviceInbox') : i18n.t('myRequests')"
        rounded="pill"
        color="primary"
      />
      <v-list-item
        to="/knowledge-base"
        prepend-icon="mdi-book-open-outline"
        :title="i18n.t('knowledgeBase')"
        rounded="pill"
        color="primary"
      />
      <v-list-item
        v-if="auth.hasPermission('admin.manage')"
        to="/team-reports"
        prepend-icon="mdi-account-group-outline"
        :title="i18n.t('teamReports')"
        rounded="pill"
        color="primary"
      />
      <v-list-item
        v-if="auth.hasPermission('audit.view')"
        to="/audit-logs"
        prepend-icon="mdi-history"
        :title="i18n.t('activityAudit')"
        rounded="pill"
        color="primary"
      />
    </v-list>

    <template #append>
      <div class="pa-3">
        <div class="liquid-glass-panel rounded-xl pa-3">
          <div class="d-flex align-center justify-space-between mb-3">
            <div style="min-width: 0">
              <div class="text-body-2 font-weight-bold text-truncate">{{ auth.session?.fullName }}</div>
              <div class="text-caption text-primary">{{ auth.session?.role }}</div>
            </div>
            <v-btn icon size="x-small" variant="text" color="error" @click="handleLogout" :title="i18n.t('signOut')">
              <v-icon size="18">mdi-logout</v-icon>
            </v-btn>
          </div>

          <div class="d-flex ga-2">
            <v-btn
              size="x-small"
              variant="tonal"
              color="primary"
              class="flex-grow-1 font-weight-bold text-none"
              @click="i18n.toggleLocale()"
            >
              {{ i18n.t('languageToggle') }}
            </v-btn>
            <v-btn
              size="x-small"
              variant="tonal"
              color="primary"
              icon
              @click="toggleTheme"
              :title="isDark ? i18n.t('switchToLight') : i18n.t('switchToDark')"
            >
              <v-icon size="16">{{ isDark ? 'mdi-white-balance-sunny' : 'mdi-moon-waning-crescent' }}</v-icon>
            </v-btn>
          </div>
        </div>
      </div>
    </template>
  </v-navigation-drawer>

  <v-app-bar v-if="!$vuetify.display.mdAndUp" flat class="liquid-glass-sidebar px-2" height="56">
    <v-app-bar-nav-icon @click="drawer = !drawer" />
    <span class="font-weight-bold ml-2">{{ i18n.t('appName') }}</span>
    <v-spacer />
    <v-btn size="x-small" variant="tonal" color="primary" class="mr-2 font-weight-bold" @click="i18n.toggleLocale()">
      {{ i18n.t('languageToggle') }}
    </v-btn>
    <v-btn icon size="small" variant="tonal" color="primary" @click="toggleTheme">
      <v-icon size="18">{{ isDark ? 'mdi-white-balance-sunny' : 'mdi-moon-waning-crescent' }}</v-icon>
    </v-btn>
  </v-app-bar>

  <v-main>
    <v-container fluid class="pa-4 pa-md-6" style="max-width: 1520px">
      <router-view />
    </v-container>
  </v-main>
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
const drawer = ref(true)

const isDark = computed(() => theme.global.name.value === 'liquidGlassDark')

const toggleTheme = () => {
  const next = isDark.value ? 'liquidGlassLight' : 'liquidGlassDark'
  theme.global.name.value = next
  localStorage.setItem('stms_theme', next)
}

const handleLogout = () => {
  auth.logout()
  router.push('/login')
}
</script>
