<template>
  <div>
    <div class="d-flex flex-wrap justify-space-between align-center ga-3 mb-6">
      <h1 class="text-h5 font-weight-bold">{{ i18n.t('kbTitle') }}</h1>

      <div class="d-flex ga-2">
        <v-text-field
          v-model="search"
          :placeholder="i18n.t('searchArticles')"
          prepend-inner-icon="mdi-magnify"
          density="compact"
          hide-details
          style="width: 240px"
          @update:model-value="loadArticles"
        />
        <v-btn
          v-if="auth.isStaff"
          color="primary"
          prepend-icon="mdi-plus"
          class="cyan-glow text-none"
          @click="dialog = true"
        >
          {{ i18n.t('newArticle') }}
        </v-btn>
      </div>
    </div>

    <v-row>
      <v-col v-for="art in articles" :key="art.id" cols="12" md="4">
        <v-card class="liquid-glass-panel pa-5 h-100 d-flex flex-column cursor-pointer" @click="selectedArticle = art">
          <v-chip size="x-small" color="primary" variant="tonal" class="align-self-start mb-3">{{ art.category }}</v-chip>
          <h3 class="text-subtitle-1 font-weight-bold mb-2">{{ art.title }}</h3>
          <p class="text-body-2 text-medium-emphasis flex-grow-1" style="display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden">
            {{ art.content }}
          </p>
          <div class="text-caption text-primary font-weight-medium mt-3">{{ i18n.t('readGuide') }} →</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Reader Dialog -->
    <v-dialog v-model="readerOpen" max-width="560">
      <v-card v-if="selectedArticle" class="liquid-glass-panel pa-6">
        <v-chip size="x-small" color="primary" variant="tonal" class="align-self-start mb-2">{{ selectedArticle.category }}</v-chip>
        <h2 class="text-h6 font-weight-bold mb-4">{{ selectedArticle.title }}</h2>
        <div class="text-body-2 mb-6" style="white-space: pre-line">{{ selectedArticle.content }}</div>
        <div class="d-flex justify-end">
          <v-btn color="primary" variant="tonal" class="text-none" @click="selectedArticle = null">{{ i18n.t('close') }}</v-btn>
        </div>
      </v-card>
    </v-dialog>

    <!-- Create KB Article Dialog -->
    <v-dialog v-model="dialog" max-width="520">
      <v-card class="liquid-glass-panel pa-6">
        <h2 class="text-h6 font-weight-bold mb-4">{{ i18n.t('publishKbTitle') }}</h2>
        <v-text-field v-model="form.category" :label="i18n.t('categoryLabel')" class="mb-2" />
        <v-text-field v-model="form.title" :label="i18n.t('articleTitleLabel')" class="mb-2" />
        <v-text-field v-model="form.tags" :label="i18n.t('triggerKeywordsLabel')" class="mb-2" />
        <v-textarea v-model="form.content" :label="i18n.t('instructionsLabel')" rows="4" class="mb-4" />
        <div class="d-flex justify-end ga-2">
          <v-btn variant="text" class="text-none" @click="dialog = false">{{ i18n.t('cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" class="text-none" @click="createArticle">{{ i18n.t('publish') }}</v-btn>
        </div>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import http from '@/api/http'
import { useAuthStore } from '@/stores/authStore'
import { useI18nStore } from '@/stores/i18nStore'

const auth = useAuthStore()
const i18n = useI18nStore()
const articles = ref<any[]>([])
const selectedArticle = ref<any>(null)
const readerOpen = computed({
  get: () => !!selectedArticle.value,
  set: (v) => { if (!v) selectedArticle.value = null }
})
const search = ref('')
const dialog = ref(false)
const saving = ref(false)
const form = ref({ category: '', title: '', tags: '', content: '' })

const loadArticles = async () => {
  try {
    const { data } = await http.get('/knowledgebase', { params: { q: search.value || undefined } })
    articles.value = data
  } catch {
    articles.value = []
  }
}

const createArticle = async () => {
  if (!form.value.title.trim()) return
  saving.value = true
  try {
    await http.post('/knowledgebase', form.value)
    dialog.value = false
    form.value = { category: '', title: '', tags: '', content: '' }
    await loadArticles()
  } finally {
    saving.value = false
  }
}

onMounted(loadArticles)
</script>
