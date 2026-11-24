<template>
  <div>
    <div class="d-flex flex-wrap justify-space-between align-center ga-3 mb-6">
      <h1 class="text-h5 font-weight-bold">{{ i18n.t('auditTitle') }}</h1>

      <v-text-field
        v-model="actionFilter"
        :placeholder="i18n.t('filterEventPlaceholder')"
        prepend-inner-icon="mdi-magnify"
        density="compact"
        hide-details
        style="max-width: 260px; width: 100%"
        @update:model-value="loadLogs"
      />
    </div>

    <v-card class="liquid-glass-panel pa-4">
      <v-table class="bg-transparent" density="comfortable">
        <thead>
          <tr>
            <th>{{ i18n.t('colTime') }}</th>
            <th>{{ i18n.t('colTicket') }}</th>
            <th>{{ i18n.t('colActor') }}</th>
            <th>{{ i18n.t('colEvent') }}</th>
            <th>{{ i18n.t('colChangeDetails') }}</th>
            <th>{{ i18n.t('colSourceIp') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="log in visibleLogs" :key="log.id">
            <td class="text-caption text-medium-emphasis text-no-wrap">{{ new Date(log.createdAt).toLocaleString() }}</td>
            <td class="font-weight-bold text-primary text-no-wrap">#{{ log.ticketId || 'SYS' }}</td>
            <td class="text-body-2 text-no-wrap">{{ log.actorName }}</td>
            <td class="text-no-wrap">
              <v-chip size="x-small" :color="eventColor(log.action)" variant="tonal" class="font-weight-medium">
                {{ formatActionLabel(log.action) }}
              </v-chip>
            </td>
            <td class="py-2">
              <div class="d-flex flex-wrap align-center ga-1">
                <template v-if="parseJson(log.oldValue)">
                  <v-chip
                    v-for="(val, key) in parseJson(log.oldValue)"
                    :key="'old-' + key"
                    size="x-small"
                    color="error"
                    variant="tonal"
                  >
                    {{ formatKey(String(key)) }}: {{ val ?? 'None' }}
                  </v-chip>
                  <v-icon size="13" class="mx-1 text-medium-emphasis">mdi-arrow-right</v-icon>
                </template>

                <template v-if="parseJson(log.newValue)">
                  <v-chip
                    v-for="(val, key) in parseJson(log.newValue)"
                    :key="'new-' + key"
                    size="x-small"
                    color="success"
                    variant="tonal"
                  >
                    {{ formatKey(String(key)) }}: {{ val ?? 'None' }}
                  </v-chip>
                </template>
              </div>
            </td>
            <td class="text-caption text-medium-emphasis text-no-wrap">{{ formatIp(log.ipAddress) }}</td>
          </tr>
        </tbody>
      </v-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import http from '@/api/http'
import { useI18nStore } from '@/stores/i18nStore'

const i18n = useI18nStore()
const logs = ref<any[]>([])
const actionFilter = ref('')

const visibleLogs = computed(() =>
  logs.value.filter((log) => !log.oldValue || log.oldValue !== log.newValue)
)

const loadLogs = async () => {
  try {
    const { data } = await http.get('/auditlogs', {
      params: { action: actionFilter.value || undefined }
    })
    logs.value = data
  } catch {
    logs.value = []
  }
}

const parseJson = (raw: string | null) => {
  if (!raw) return null
  try {
    return JSON.parse(raw)
  } catch {
    return { Detail: raw }
  }
}

const formatKey = (key: string) => {
  return key
    .replace(/_/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/^./, (s) => s.toUpperCase())
}

const formatActionLabel = (action: string) => action.replace(/_/g, ' ')

const formatIp = (ip: string | null) => {
  if (!ip) return '-'
  if (ip === '::1' || ip === '127.0.0.1') return i18n.t('localIpLabel')
  return ip
}

const eventColor = (action: string) => {
  if (action.includes('BREACHED')) return 'error'
  if (action.includes('ESCALATED')) return 'warning'
  if (action.includes('LINKED') || action.includes('CASCADE')) return 'info'
  return 'primary'
}

onMounted(loadLogs)
</script>
