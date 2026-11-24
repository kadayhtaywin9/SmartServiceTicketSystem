<template>
  <div class="mx-auto" style="max-width: 720px">
    <v-card class="liquid-glass-panel pa-6 pa-md-8">
      <h1 class="text-h5 font-weight-bold mb-1">{{ i18n.t('createServiceRequest') }}</h1>
      <p class="text-body-2 text-medium-emphasis mb-6">{{ i18n.t('createRequestDesc') }}</p>

      <v-alert v-if="errorMsg" type="error" variant="tonal" density="compact" class="mb-4" rounded="xl">
        {{ errorMsg }}
      </v-alert>

      <v-alert v-if="createdResult" type="success" variant="tonal" class="mb-5" rounded="xl">
        <div class="font-weight-bold mb-1">
          {{ i18n.t('createdPrefix') }} {{ createdResult.ticketCode }} · {{ i18n.t('priority_' + createdResult.priority) }}
        </div>
        <v-btn size="small" color="primary" variant="tonal" class="mt-2 text-none" to="/inbox">
          {{ i18n.t('openInInbox') }}
        </v-btn>
      </v-alert>

      <v-form @submit.prevent="submitTicket">
        <v-text-field
          v-model="subject"
          :label="i18n.t('subjectLabel')"
          required
          class="mb-3"
          @input="searchKb"
        />

        <!-- Inline KB Suggestions -->
        <div v-if="suggestions.length > 0" class="ai-thinking-box pa-4 rounded-xl mb-4">
          <div class="text-caption font-weight-bold text-primary mb-2">{{ i18n.t('suggestedSolutions') }}</div>
          <div v-for="art in suggestions" :key="art.id" class="mb-2">
            <div class="text-body-2 font-weight-semibold">{{ art.title }}</div>
            <div class="text-caption text-medium-emphasis" style="white-space: pre-line">{{ art.content }}</div>
          </div>
        </div>

        <v-select
          v-model="departmentId"
          :items="departmentOptions"
          item-title="name"
          item-value="id"
          :label="i18n.t('department')"
          class="mb-3"
        />

        <div class="mb-4">
          <div class="text-caption text-medium-emphasis mb-2">{{ i18n.t('urgencyPriority') }}</div>
          <div class="d-flex flex-wrap ga-2">
            <v-chip
              v-for="p in priorityItems"
              :key="p.value"
              :color="priority === p.value ? 'primary' : undefined"
              :variant="priority === p.value ? 'flat' : 'tonal'"
              class="cursor-pointer"
              @click="priority = p.value"
            >
              {{ p.title }}
            </v-chip>
          </div>
        </div>

        <v-textarea
          v-model="description"
          :label="i18n.t('detailedDescription')"
          rows="4"
          required
          class="mb-5"
        />

        <div class="d-flex justify-end">
          <v-btn
            type="submit"
            color="primary"
            size="large"
            :loading="submitting"
            class="cyan-glow text-none font-weight-bold px-8"
          >
            {{ i18n.t('submitRequest') }}
          </v-btn>
        </div>
      </v-form>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import http from '@/api/http'
import { useI18nStore } from '@/stores/i18nStore'

const i18n = useI18nStore()
const subject = ref('')
const departmentId = ref<number | null>(null)
const departments = ref<any[]>([])
const priority = ref('Medium')
const description = ref('')
const submitting = ref(false)
const errorMsg = ref('')
const createdResult = ref<any>(null)
const suggestions = ref<any[]>([])

const departmentOptions = computed(() => [
  { id: null, name: i18n.t('autoRouteAi') },
  ...departments.value
])

const priorityItems = computed(() => [
  { title: i18n.t('priority_Low'), value: 'Low' },
  { title: i18n.t('priority_Medium'), value: 'Medium' },
  { title: i18n.t('priority_High'), value: 'High' },
  { title: i18n.t('priority_Critical'), value: 'Critical' }
])

let timer: any = null

const searchKb = () => {
  clearTimeout(timer)
  timer = setTimeout(async () => {
    if (subject.value.trim().length < 3) {
      suggestions.value = []
      return
    }
    try {
      const { data } = await http.get('/knowledgebase/suggest', { params: { q: subject.value } })
      suggestions.value = data
    } catch {
      suggestions.value = []
    }
  }, 250)
}

const submitTicket = async () => {
  submitting.value = true
  errorMsg.value = ''
  try {
    const { data } = await http.post('/tickets', {
      subject: subject.value,
      description: description.value,
      priority: priority.value,
      departmentId: departmentId.value
    })
    createdResult.value = data
    subject.value = ''
    description.value = ''
    departmentId.value = null
    suggestions.value = []
  } catch (err: any) {
    errorMsg.value = err.response?.data?.message || i18n.t('errSubmitTicket')
  } finally {
    submitting.value = false
  }
}

onMounted(async () => {
  try {
    const { data } = await http.get('/tickets/departments')
    departments.value = data
  } catch {
    // ignore
  }
})
</script>
