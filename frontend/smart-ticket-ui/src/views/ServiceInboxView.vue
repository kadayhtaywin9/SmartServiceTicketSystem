<template>
  <div>
    <v-snackbar v-model="snackbar.show" :color="snackbar.color" timeout="3000" location="top right" rounded="pill">
      {{ snackbar.text }}
    </v-snackbar>

    <v-alert
      v-for="(alert, idx) in slaAlerts"
      :key="idx"
      :type="alert.severity === 'Breached' ? 'error' : 'warning'"
      variant="tonal"
      density="compact"
      class="mb-4 liquid-glass-panel"
      closable
    >
      <strong>{{ alert.ticketCode }}:</strong> {{ alert.message }}
    </v-alert>

    <v-row class="ga-0">
      <!-- LEFT COLUMN: Clean Inbox List -->
      <v-col cols="12" lg="4" class="pr-lg-4 mb-4 mb-lg-0">
        <v-card class="liquid-glass-panel pa-4" style="min-height: 85vh">
          <div class="d-flex align-center justify-space-between mb-3">
            <h2 class="text-subtitle-1 font-weight-bold">
              {{ auth.isStaff ? i18n.t('inbox') : i18n.t('myRequests') }}
              <span class="text-caption text-medium-emphasis ml-1">({{ tickets.length }})</span>
            </h2>
            <v-btn icon size="x-small" variant="text" color="primary" :loading="loadingList" @click="loadTickets">
              <v-icon size="18">mdi-refresh</v-icon>
            </v-btn>
          </div>

          <v-text-field
            v-model="searchQuery"
            :placeholder="i18n.t('searchTickets')"
            prepend-inner-icon="mdi-magnify"
            density="compact"
            hide-details
            class="mb-3"
            @update:model-value="loadTickets"
          />

          <!-- Clean Pill Filter Bar -->
          <div class="d-flex flex-wrap ga-1 mb-4">
            <v-chip
              v-for="tab in quickFilters"
              :key="tab.value"
              size="x-small"
              :color="statusFilter === tab.value ? 'primary' : undefined"
              :variant="statusFilter === tab.value ? 'flat' : 'tonal'"
              class="cursor-pointer"
              @click="setStatusFilter(tab.value)"
            >
              {{ tab.title }}
            </v-chip>
          </div>

          <div class="d-flex flex-column ga-2" style="max-height: 68vh; overflow-y: auto">
            <div
              v-for="t in tickets"
              :key="t.id"
              class="ticket-row-item pa-3"
              :class="{ 'ticket-row-active': selectedTicket?.id === t.id }"
              @click="selectTicket(t.id)"
            >
              <div class="d-flex align-center justify-space-between mb-1">
                <div class="d-flex align-center ga-2">
                  <span class="priority-dot" :style="{ backgroundColor: priorityDotColor(t.priority) }"></span>
                  <span class="text-caption font-weight-bold text-primary">{{ t.ticketCode }}</span>
                </div>
                <span class="text-caption" :class="t.isSlaBreached ? 'text-error font-weight-bold' : 'text-medium-emphasis'">
                  {{ formatRemaining(t.resolutionDueAt, t.isSlaBreached) }}
                </span>
              </div>

              <div class="text-body-2 font-weight-medium text-truncate mb-1">{{ t.subject }}</div>

              <div class="d-flex align-center justify-space-between text-caption text-medium-emphasis">
                <span class="text-truncate">{{ t.departmentName }}</span>
                <span>{{ i18n.t('status_' + t.status) }}</span>
              </div>
            </div>

            <div v-if="!loadingList && tickets.length === 0" class="text-center text-body-2 text-medium-emphasis py-10">
              {{ i18n.t('noMatchingTickets') }}
            </div>
          </div>
        </v-card>
      </v-col>

      <!-- RIGHT COLUMN: Clean Tabbed Workspace -->
      <v-col cols="12" lg="8">
        <v-card v-if="selectedTicket" class="liquid-glass-panel pa-5 d-flex flex-column" style="min-height: 85vh">
          <!-- Minimal Header -->
          <div class="d-flex flex-wrap justify-space-between align-center ga-3 pb-3">
            <div style="min-width: 0; flex: 1">
              <div class="d-flex align-center ga-2 mb-1">
                <span class="text-caption font-weight-bold text-primary">{{ selectedTicket.ticketCode }}</span>
                <span class="text-caption text-medium-emphasis">· {{ selectedTicket.department.name }}</span>
                <v-chip v-if="activeViewers.length > 1" size="x-small" color="info" variant="tonal">
                  <v-icon start size="12">mdi-eye-outline</v-icon>{{ activeViewers.length }}
                </v-chip>
              </div>
              <h1 class="text-h6 font-weight-bold text-truncate">{{ selectedTicket.subject }}</h1>
            </div>

            <div class="d-flex align-center ga-2">
              <v-select
                v-if="auth.isStaff"
                v-model="statusModel"
                :items="statusSelectItems"
                density="compact"
                hide-details
                style="width: 175px"
                @update:model-value="updateStatus"
              />
              <v-chip v-else size="small" color="primary" variant="tonal">
                {{ i18n.t('status_' + selectedTicket.status) }}
              </v-chip>
            </div>
          </div>

          <v-tabs v-if="auth.isStaff" v-model="activeTab" color="primary" density="compact" class="mb-4">
            <v-tab value="messages" class="text-none">{{ i18n.t('tabMessages') }}</v-tab>
            <v-tab value="details" class="text-none">{{ i18n.t('tabDetails') }}</v-tab>
          </v-tabs>
          <v-divider v-else class="mb-4" />

          <!-- TAB 1: CLEAN CONVERSATION -->
          <div v-if="!auth.isStaff || activeTab === 'messages'" class="d-flex flex-column flex-grow-1">
            <!-- Attachments Pill Strip (Only shown if files exist) -->
            <div v-if="selectedTicket.attachments?.length" class="d-flex flex-wrap align-center ga-2 mb-3">
              <v-chip
                v-for="att in selectedTicket.attachments"
                :key="att.id"
                size="small"
                color="primary"
                variant="tonal"
                prepend-icon="mdi-paperclip"
                class="cursor-pointer"
                @click="downloadAttachment(att)"
              >
                {{ att.fileName }}
              </v-chip>
            </div>

            <div ref="threadContainer" class="d-flex flex-column ga-3 mb-4 pr-1 flex-grow-1" style="max-height: 48vh; overflow-y: auto">
              <div
                v-for="msg in selectedTicket.messages"
                :key="msg.id"
                :class="msg.isInternal ? 'internal-bubble' : 'public-bubble'"
                class="pa-4 rounded-xl"
              >
                <div class="d-flex justify-space-between align-center mb-1">
                  <div class="d-flex align-center ga-2">
                    <span class="text-body-2 font-weight-bold">{{ msg.senderName }}</span>
                    <v-chip v-if="msg.isInternal" size="x-small" color="warning" variant="tonal">
                      {{ i18n.t('internalNote') }}
                    </v-chip>
                    <v-chip v-if="msg.containsMaskedPii" size="x-small" color="error" variant="tonal">
                      {{ i18n.t('piiProtected') }}
                    </v-chip>
                  </div>
                  <span class="text-caption text-medium-emphasis">{{ new Date(msg.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}</span>
                </div>
                <div class="text-body-2" style="white-space: pre-line">{{ msg.body }}</div>
              </div>

              <div v-if="selectedTicket.messages.length === 0" class="pa-4 rounded-xl public-bubble text-body-2">
                {{ selectedTicket.description }}
              </div>
            </div>

            <div v-if="typingAgent" class="text-caption text-primary mb-2">
              {{ typingAgent }} {{ i18n.t('isTyping') }}
            </div>

            <!-- EndUser CSAT Rating -->
            <div
              v-if="(selectedTicket.status === 'Resolved' || selectedTicket.status === 'Closed') && !auth.isStaff"
              class="pa-4 mb-4 rounded-xl ai-thinking-box"
            >
              <div v-if="selectedTicket.csatSurvey" class="d-flex align-center ga-2 text-body-2">
                <v-icon color="warning" size="18">mdi-star</v-icon>
                <span>{{ i18n.t('csatThanks') }} <strong>{{ selectedTicket.csatSurvey.rating }} / 5</strong></span>
              </div>
              <div v-else class="d-flex flex-wrap align-center ga-3">
                <span class="text-body-2 font-weight-medium">{{ i18n.t('csatPrompt') }}</span>
                <v-rating v-model="csatRating" color="warning" density="compact" hover />
                <v-text-field
                  v-model="csatFeedback"
                  :placeholder="i18n.t('optionalFeedback')"
                  density="compact"
                  hide-details
                  style="max-width: 240px"
                />
                <v-btn size="small" color="primary" :loading="submittingCsat" class="text-none" @click="submitCsat">
                  {{ i18n.t('submitRating') }}
                </v-btn>
              </div>
            </div>

            <!-- Hidden File Input + Clean Minimal Reply Bar -->
            <input ref="fileInputRef" type="file" style="display: none" @change="handleFileUpload" />

            <div class="mt-auto">
              <v-textarea
                v-model="replyText"
                :placeholder="isInternalNote ? i18n.t('internalPlaceholder') : i18n.t('replyPlaceholder')"
                rows="2"
                auto-grow
                max-rows="5"
                hide-details
                class="mb-2"
                @input="onTyping"
                @keydown.ctrl.enter="postMessage"
              />

              <div class="d-flex flex-wrap align-center justify-space-between ga-2">
                <div class="d-flex align-center ga-2">
                  <v-btn
                    v-if="auth.isStaff"
                    size="small"
                    :color="isInternalNote ? 'warning' : 'primary'"
                    :variant="isInternalNote ? 'flat' : 'tonal'"
                    :prepend-icon="isInternalNote ? 'mdi-lock' : 'mdi-earth'"
                    class="text-none"
                    @click="isInternalNote = !isInternalNote"
                  >
                    {{ isInternalNote ? i18n.t('internalNote') : i18n.t('publicReply') }}
                  </v-btn>

                  <v-btn
                    size="small"
                    color="primary"
                    variant="text"
                    prepend-icon="mdi-paperclip"
                    :loading="uploadingFile"
                    class="text-none"
                    @click="fileInputRef?.click()"
                  >
                    {{ i18n.t('attachFile') }}
                  </v-btn>

                  <v-btn
                    v-if="auth.isStaff && selectedTicket.aiInsights?.suggestedReply"
                    size="small"
                    color="primary"
                    variant="text"
                    prepend-icon="mdi-sparkles"
                    class="text-none"
                    @click="useAiDraft"
                  >
                    {{ i18n.t('aiDraftBtn') }}
                  </v-btn>
                </div>

                <v-btn
                  :color="isInternalNote ? 'warning' : 'primary'"
                  prepend-icon="mdi-send"
                  size="small"
                  :loading="sending"
                  :disabled="!replyText.trim()"
                  class="cyan-glow text-none font-weight-bold px-5"
                  @click="postMessage"
                >
                  {{ isInternalNote ? i18n.t('saveInternalNote') : i18n.t('sendReply') }}
                </v-btn>
              </div>
            </div>
          </div>

          <!-- TAB 2: DETAILS & AI (Uncluttered Properties) -->
          <div v-else class="d-flex flex-column ga-4">
            <v-row>
              <v-col cols="12" sm="4">
                <v-select
                  v-model="departmentModel"
                  :items="departments"
                  item-title="name"
                  item-value="id"
                  :label="i18n.t('department')"
                  density="comfortable"
                  hide-details
                  @update:model-value="transferDepartment"
                />
              </v-col>
              <v-col cols="12" sm="4">
                <v-select
                  v-model="priorityModel"
                  :items="prioritySelectItems"
                  :label="i18n.t('priority')"
                  density="comfortable"
                  hide-details
                  @update:model-value="updatePriority"
                />
              </v-col>
              <v-col cols="12" sm="4">
                <v-select
                  v-model="assigneeModel"
                  :items="assignableAgents"
                  item-title="fullName"
                  item-value="id"
                  :label="i18n.t('assignee')"
                  density="comfortable"
                  hide-details
                  clearable
                  @update:model-value="updateAssignee"
                />
              </v-col>
            </v-row>

            <!-- AI Reasoning Card -->
            <div v-if="selectedTicket.aiInsights" class="ai-thinking-box pa-4 rounded-xl">
              <div class="d-flex align-center justify-space-between mb-2">
                <div class="d-flex align-center ga-2">
                  <v-icon color="primary" size="18">mdi-sparkles</v-icon>
                  <span class="text-body-2 font-weight-bold">{{ i18n.t('aiAssistantTitle') }}</span>
                </div>
                <v-chip size="x-small" color="primary" variant="tonal">
                  {{ i18n.t('sentiment_' + selectedTicket.sentimentLabel) }} · {{ Math.round(selectedTicket.sentimentScore * 100) }}%
                </v-chip>
              </div>
              <div class="text-caption mb-3" style="white-space: pre-line">{{ selectedTicket.aiInsights.thinking }}</div>
              <v-btn size="x-small" color="primary" variant="tonal" class="text-none" @click="useAiDraftAndSwitch">
                {{ i18n.t('useSuggestedReply') }}
              </v-btn>
            </div>

            <!-- Parent-Child Incident Link -->
            <div class="public-bubble pa-4 rounded-xl">
              <div class="text-caption font-weight-bold mb-2">{{ i18n.t('incidentLink') }}</div>
              <div class="d-flex flex-wrap align-center ga-2 mb-3">
                <v-chip
                  v-if="selectedTicket.parentTicket"
                  size="small"
                  color="info"
                  variant="tonal"
                  @click="selectTicket(selectedTicket.parentTicket.id)"
                >
                  {{ i18n.t('masterLabel') }}: {{ selectedTicket.parentTicket.ticketCode }}
                </v-chip>
                <v-chip
                  v-for="c in selectedTicket.childTickets"
                  :key="c.id"
                  size="small"
                  color="primary"
                  variant="tonal"
                  @click="selectTicket(c.id)"
                >
                  {{ i18n.t('childLabel') }} {{ c.ticketCode }}
                </v-chip>
                <span v-if="!selectedTicket.parentTicket && !selectedTicket.childTickets?.length" class="text-caption text-medium-emphasis">
                  {{ i18n.t('standaloneTicket') }}
                </span>
              </div>
              <div class="d-flex ga-2" style="max-width: 260px">
                <v-text-field
                  v-model="parentIdInput"
                  :placeholder="i18n.t('parentIdPlaceholder')"
                  type="number"
                  density="compact"
                  hide-details
                />
                <v-btn size="small" color="primary" variant="tonal" class="text-none" @click="linkParent">
                  {{ i18n.t('link') }}
                </v-btn>
              </div>
            </div>

            <!-- Requester & Device Info -->
            <div class="public-bubble pa-4 rounded-xl text-body-2">
              <div class="mb-1"><strong>{{ i18n.t('requester') }}:</strong> {{ selectedTicket.requester.fullName }} ({{ selectedTicket.requester.email }})</div>
              <div v-if="selectedTicket.requester.assets?.length">
                <strong>{{ i18n.t('device') }}:</strong> {{ selectedTicket.requester.assets[0].deviceModel }} · {{ selectedTicket.requester.assets[0].assetTag }}
              </div>
            </div>
          </div>
        </v-card>

        <v-card v-else class="liquid-glass-panel pa-10 d-flex flex-column align-center justify-center text-center" style="min-height: 85vh">
          <v-icon size="48" color="primary" class="mb-3">mdi-inbox-outline</v-icon>
          <div class="text-subtitle-1 font-weight-bold">{{ i18n.t('selectTicketTitle') }}</div>
          <p class="text-body-2 text-medium-emphasis mt-1">{{ i18n.t('selectTicketDesc') }}</p>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted } from 'vue'
import http from '@/api/http'
import { useAuthStore } from '@/stores/authStore'
import { useI18nStore } from '@/stores/i18nStore'
import { useTicketHub } from '@/composables/useTicketHub'

const auth = useAuthStore()
const i18n = useI18nStore()
const tickets = ref<any[]>([])
const departments = ref<any[]>([])
const assignableAgents = ref<any[]>([])
const selectedTicket = ref<any>(null)
const threadContainer = ref<HTMLElement | null>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)
const activeTab = ref('messages')

const loadingList = ref(false)
const searchQuery = ref('')
const statusFilter = ref('')

const statusModel = ref('Open')
const priorityModel = ref('Medium')
const departmentModel = ref<number | null>(null)
const assigneeModel = ref<number | null>(null)
const parentIdInput = ref('')
const isInternalNote = ref(false)
const replyText = ref('')
const sending = ref(false)
const uploadingFile = ref(false)

const csatRating = ref(5)
const csatFeedback = ref('')
const submittingCsat = ref(false)

const quickFilters = computed(() => [
  { title: i18n.t('filterAll'), value: '' },
  { title: i18n.t('status_Open'), value: 'Open' },
  { title: i18n.t('status_In_Progress'), value: 'In_Progress' },
  { title: i18n.t('status_Resolved'), value: 'Resolved' }
])

const statusSelectItems = computed(() => [
  { title: i18n.t('status_Open'), value: 'Open' },
  { title: i18n.t('status_In_Progress'), value: 'In_Progress' },
  { title: i18n.t('status_Pending_Customer'), value: 'Pending_Customer' },
  { title: i18n.t('status_Resolved'), value: 'Resolved' },
  { title: i18n.t('status_Closed'), value: 'Closed' }
])

const prioritySelectItems = computed(() => [
  { title: i18n.t('priority_Low'), value: 'Low' },
  { title: i18n.t('priority_Medium'), value: 'Medium' },
  { title: i18n.t('priority_High'), value: 'High' },
  { title: i18n.t('priority_Critical'), value: 'Critical' }
])

const snackbar = ref({ show: false, text: '', color: 'success' })
const notify = (text: string, color: 'success' | 'error' | 'info' = 'success') => {
  snackbar.value = { show: true, text, color }
}

const { activeViewers, typingAgent, slaAlerts, connect, joinTicketRoom, sendTyping } = useTicketHub()

const priorityDotColor = (p: string) => {
  if (p === 'Critical') return '#E11D48'
  if (p === 'High') return '#D97706'
  if (p === 'Medium') return '#0891B2'
  return '#10B981'
}

const scrollToBottom = async () => {
  await nextTick()
  if (threadContainer.value) {
    threadContainer.value.scrollTop = threadContainer.value.scrollHeight
  }
}

const setStatusFilter = async (val: string) => {
  statusFilter.value = val
  await loadTickets()
}

const loadMeta = async () => {
  try {
    const deptRes = await http.get('/tickets/departments')
    departments.value = deptRes.data
    if (auth.isStaff) {
      const agentRes = await http.get('/tickets/agents')
      assignableAgents.value = agentRes.data
    }
  } catch {
    // ignore
  }
}

const loadTickets = async () => {
  loadingList.value = true
  try {
    const { data } = await http.get('/tickets', {
      params: {
        q: searchQuery.value || undefined,
        status: statusFilter.value || undefined
      }
    })
    tickets.value = data
    if (!selectedTicket.value && data.length > 0) {
      await selectTicket(data[0].id)
    }
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errLoadTickets'), 'error')
  } finally {
    loadingList.value = false
  }
}

const selectTicket = async (id: number) => {
  try {
    const { data } = await http.get(`/tickets/${id}`)
    selectedTicket.value = data
    statusModel.value = data.status
    priorityModel.value = data.priority
    departmentModel.value = data.department?.id ?? null
    assigneeModel.value = data.assignee?.id ?? null
    await joinTicketRoom(id, auth.session?.fullName || 'User')
    await scrollToBottom()
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errLoadDetail'), 'error')
  }
}

const postMessage = async () => {
  if (!replyText.value.trim() || !selectedTicket.value) return
  sending.value = true
  try {
    const { data } = await http.post(`/tickets/${selectedTicket.value.id}/messages`, {
      body: replyText.value,
      isInternal: auth.isStaff ? isInternalNote.value : false
    })
    replyText.value = ''
    await sendTyping(selectedTicket.value.id, auth.session?.fullName || 'User', false)

    if (!selectedTicket.value.messages.some((m: any) => m.id === data.id)) {
      selectedTicket.value.messages.push(data)
    }
    await scrollToBottom()
    notify(data.isInternal ? i18n.t('msgInternalSaved') : i18n.t('msgReplySent'))
    await loadTickets()
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errSendMsg'), 'error')
  } finally {
    sending.value = false
  }
}

const handleFileUpload = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !selectedTicket.value) return

  uploadingFile.value = true
  try {
    const formData = new FormData()
    formData.append('file', file)
    await http.post(`/tickets/${selectedTicket.value.id}/attachments`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    notify(i18n.t('fileUploaded'))
    await selectTicket(selectedTicket.value.id)
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errUpload'), 'error')
  } finally {
    uploadingFile.value = false
    input.value = ''
  }
}

const downloadAttachment = async (att: any) => {
  try {
    const res = await http.get(`/tickets/attachments/${att.id}`, { responseType: 'blob' })
    const url = window.URL.createObjectURL(new Blob([res.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', att.fileName)
    document.body.appendChild(link)
    link.click()
    link.remove()
  } catch {
    notify('Unable to download attachment.', 'error')
  }
}

const updateStatus = async (newStatus: string) => {
  if (!selectedTicket.value || newStatus === selectedTicket.value.status) return
  try {
    await http.patch(`/tickets/${selectedTicket.value.id}/status`, {
      status: newStatus,
      resolutionNote: 'Resolved via Service Desk.'
    })
    notify(i18n.t('msgStatusUpdated'))
    await selectTicket(selectedTicket.value.id)
    await loadTickets()
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errUpdateStatus'), 'error')
  }
}

const transferDepartment = async (newDeptId: number) => {
  if (!selectedTicket.value || newDeptId === selectedTicket.value.department?.id) return
  try {
    await http.patch(`/tickets/${selectedTicket.value.id}/department`, { departmentId: newDeptId })
    notify(i18n.t('msgDeptTransferred'))
    await selectTicket(selectedTicket.value.id)
    await loadTickets()
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errDeptTransfer'), 'error')
  }
}

const updatePriority = async (newPriority: string) => {
  if (!selectedTicket.value || newPriority === selectedTicket.value.priority) return
  try {
    await http.patch(`/tickets/${selectedTicket.value.id}/priority`, { priority: newPriority })
    notify(i18n.t('msgPriorityUpdated'))
    await selectTicket(selectedTicket.value.id)
    await loadTickets()
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errUpdatePriority'), 'error')
  }
}

const updateAssignee = async (newAssigneeId: number | null) => {
  if (!selectedTicket.value) return
  try {
    await http.patch(`/tickets/${selectedTicket.value.id}/assign`, { assigneeId: newAssigneeId })
    notify(i18n.t('msgAssigneeUpdated'))
    await selectTicket(selectedTicket.value.id)
    await loadTickets()
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errAssign'), 'error')
  }
}

const linkParent = async () => {
  if (!parentIdInput.value || !selectedTicket.value) return
  try {
    await http.post(`/tickets/${selectedTicket.value.id}/link-parent`, {
      parentTicketId: Number(parentIdInput.value)
    })
    parentIdInput.value = ''
    notify(i18n.t('msgLinkedParent'))
    await selectTicket(selectedTicket.value.id)
    await loadTickets()
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errInvalidParent'), 'error')
  }
}

const submitCsat = async () => {
  if (!selectedTicket.value) return
  submittingCsat.value = true
  try {
    await http.post(`/tickets/${selectedTicket.value.id}/csat`, {
      rating: csatRating.value,
      feedback: csatFeedback.value
    })
    notify(i18n.t('msgCsatThanks'))
    await selectTicket(selectedTicket.value.id)
  } catch (err: any) {
    notify(err.response?.data?.message || i18n.t('errCsat'), 'error')
  } finally {
    submittingCsat.value = false
  }
}

const useAiDraft = () => {
  isInternalNote.value = false
  replyText.value = selectedTicket.value.aiInsights.suggestedReply
}

const useAiDraftAndSwitch = () => {
  useAiDraft()
  activeTab.value = 'messages'
}

const onTyping = () => {
  if (selectedTicket.value) {
    sendTyping(selectedTicket.value.id, auth.session?.fullName || 'User', replyText.value.length > 0)
  }
}

const formatRemaining = (dueIso: string, breached: boolean) => {
  if (breached) return i18n.t('slaBreached')
  const diffMins = Math.round((new Date(dueIso).getTime() - Date.now()) / 60000)
  if (diffMins <= 0) return i18n.t('dueNow')
  if (diffMins < 60) return `${diffMins}m`
  return `${Math.floor(diffMins / 60)}h ${diffMins % 60}m`
}

onMounted(async () => {
  await connect(
    async (msg) => {
      if (selectedTicket.value && msg.ticketId === selectedTicket.value.id) {
        if (!selectedTicket.value.messages.some((m: any) => m.id === msg.id)) {
          selectedTicket.value.messages.push(msg)
          await scrollToBottom()
        }
      }
    },
    async () => {
      await loadTickets()
      if (selectedTicket.value) {
        await selectTicket(selectedTicket.value.id)
      }
    }
  )
  await Promise.all([loadMeta(), loadTickets()])
})
</script>
