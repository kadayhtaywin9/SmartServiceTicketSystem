<template>
  <div>
    <v-snackbar v-model="snackbar.show" :color="snackbar.color" timeout="3000" location="top right" rounded="pill">
      {{ snackbar.text }}
    </v-snackbar>

    <div class="d-flex flex-wrap justify-space-between align-center ga-3 mb-6">
      <h1 class="text-h5 font-weight-bold">{{ i18n.t('teamReports') }}</h1>

      <div class="d-flex ga-2">
        <v-btn
          variant="tonal"
          color="primary"
          prepend-icon="mdi-download"
          class="text-none"
          @click="exportCsv"
        >
          {{ i18n.t('exportCsv') }}
        </v-btn>
        <v-btn
          v-if="auth.session?.role === 'SuperAdmin'"
          color="primary"
          prepend-icon="mdi-account-plus-outline"
          class="cyan-glow text-none"
          @click="dialog = true"
        >
          {{ i18n.t('addMember') }}
        </v-btn>
      </div>
    </div>

    <!-- Compact Summary Strip -->
    <v-row class="mb-5">
      <v-col cols="6" md="3">
        <v-card class="liquid-glass-panel pa-4">
          <div class="text-caption text-medium-emphasis">{{ i18n.t('totalTickets') }}</div>
          <div class="text-h4 font-weight-bold text-primary mt-1">{{ overview.total }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card class="liquid-glass-panel pa-4">
          <div class="text-caption text-medium-emphasis">{{ i18n.t('activeTickets') }}</div>
          <div class="text-h4 font-weight-bold mt-1">{{ overview.active }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card class="liquid-glass-panel pa-4">
          <div class="text-caption text-medium-emphasis">{{ i18n.t('resolvedTickets') }}</div>
          <div class="text-h4 font-weight-bold text-success mt-1">{{ overview.resolved }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card class="liquid-glass-panel pa-4">
          <div class="text-caption text-medium-emphasis">{{ i18n.t('slaCompliance') }}</div>
          <div class="text-h4 font-weight-bold text-primary mt-1">{{ overview.slaComplianceRate }}%</div>
        </v-card>
      </v-col>
    </v-row>

    <v-row>
      <!-- Department Workload -->
      <v-col cols="12" md="5">
        <v-card class="liquid-glass-panel pa-5 h-100">
          <h2 class="text-subtitle-1 font-weight-bold mb-4">{{ i18n.t('deptWorkload') }}</h2>
          <div v-for="d in overview.departments" :key="d.id" class="mb-4">
            <div class="d-flex justify-space-between text-body-2 mb-1">
              <span class="font-weight-medium">{{ d.name }}</span>
              <span class="text-caption text-medium-emphasis">{{ d.open }} {{ i18n.t('openCount') }} / {{ d.total }}</span>
            </div>
            <v-progress-linear
              :model-value="d.total > 0 ? (d.resolved / d.total) * 100 : 100"
              color="primary"
              height="7"
              rounded
            />
          </div>
        </v-card>
      </v-col>

      <!-- Employee & Agent Directory -->
      <v-col cols="12" md="7">
        <v-card class="liquid-glass-panel pa-5 h-100">
          <h2 class="text-subtitle-1 font-weight-bold mb-4">{{ i18n.t('teamDirectory') }}</h2>
          <v-table class="bg-transparent" density="comfortable">
            <thead>
              <tr>
                <th>{{ i18n.t('fullNameLabel') }}</th>
                <th>{{ i18n.t('roleLabel') }}</th>
                <th>{{ i18n.t('department') }}</th>
                <th>{{ i18n.t('status') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="u in users" :key="u.id">
                <td>
                  <div class="text-body-2 font-weight-medium">{{ u.fullName }}</div>
                  <div class="text-caption text-medium-emphasis">{{ u.email }}</div>
                </td>
                <td>
                  <v-chip size="x-small" color="primary" variant="tonal">{{ u.roleName }}</v-chip>
                </td>
                <td class="text-caption">{{ u.departmentName }}</td>
                <td>
                  <v-chip
                    size="x-small"
                    :color="u.isActive ? 'success' : 'error'"
                    variant="tonal"
                    class="cursor-pointer"
                    @click="toggleUser(u)"
                  >
                    {{ u.isActive ? i18n.t('activeLabel') : i18n.t('inactiveLabel') }}
                  </v-chip>
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card>
      </v-col>
    </v-row>

    <!-- Onboard New Employee / Agent Modal -->
    <v-dialog v-model="dialog" max-width="480">
      <v-card class="liquid-glass-panel pa-6">
        <h2 class="text-h6 font-weight-bold mb-4">{{ i18n.t('onboardTitle') }}</h2>
        <v-text-field v-model="form.fullName" :label="i18n.t('fullNameLabel')" class="mb-2" />
        <v-text-field v-model="form.email" :label="i18n.t('workEmail')" class="mb-2" />
        <v-text-field v-model="form.password" :label="i18n.t('password')" type="password" class="mb-2" />
        <v-select v-model="form.roleId" :items="roles" item-title="name" item-value="id" :label="i18n.t('roleLabel')" class="mb-2" />
        <v-select v-model="form.departmentId" :items="departments" item-title="name" item-value="id" :label="i18n.t('department')" clearable class="mb-2" />
        <v-text-field v-model="form.skills" :label="i18n.t('skillsLabel')" class="mb-4" />
        <div class="d-flex justify-end ga-2">
          <v-btn variant="text" class="text-none" @click="dialog = false">{{ i18n.t('cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" class="cyan-glow text-none" @click="createUser">
            {{ i18n.t('createAccount') }}
          </v-btn>
        </div>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import http from '@/api/http'
import { useAuthStore } from '@/stores/authStore'
import { useI18nStore } from '@/stores/i18nStore'

const auth = useAuthStore()
const i18n = useI18nStore()

const overview = ref<any>({ total: 0, active: 0, resolved: 0, breached: 0, slaComplianceRate: 100, departments: [] })
const users = ref<any[]>([])
const roles = ref<any[]>([])
const departments = ref<any[]>([])
const dialog = ref(false)
const saving = ref(false)
const snackbar = ref({ show: false, text: '', color: 'success' })

const form = ref({
  fullName: '',
  email: '',
  password: '',
  roleId: 3,
  departmentId: null as number | null,
  skills: ''
})

const notify = (text: string, color: 'success' | 'error' = 'success') => {
  snackbar.value = { show: true, text, color }
}

const loadAll = async () => {
  try {
    const [ov, us, rl, dp] = await Promise.all([
      http.get('/admin/overview'),
      http.get('/admin/users'),
      http.get('/admin/roles'),
      http.get('/tickets/departments')
    ])
    overview.value = ov.data
    users.value = us.data
    roles.value = rl.data
    departments.value = dp.data
  } catch {
    // ignore
  }
}

const createUser = async () => {
  saving.value = true
  try {
    await http.post('/admin/users', form.value)
    dialog.value = false
    form.value = { fullName: '', email: '', password: '', roleId: 3, departmentId: null, skills: '' }
    notify(i18n.t('userCreated'))
    await loadAll()
  } catch (err: any) {
    notify(err.response?.data?.message || 'Failed to create user.', 'error')
  } finally {
    saving.value = false
  }
}

const toggleUser = async (u: any) => {
  if (auth.session?.role !== 'SuperAdmin') return
  try {
    await http.patch(`/admin/users/${u.id}/toggle-active`)
    await loadAll()
  } catch (err: any) {
    notify(err.response?.data?.message || 'Cannot update user.', 'error')
  }
}

const exportCsv = async () => {
  try {
    const res = await http.get('/tickets/export-csv', { responseType: 'blob' })
    const url = window.URL.createObjectURL(new Blob([res.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', 'stms-tickets-report.csv')
    document.body.appendChild(link)
    link.click()
    link.remove()
  } catch {
    notify('Failed to export CSV.', 'error')
  }
}

onMounted(loadAll)
</script>
