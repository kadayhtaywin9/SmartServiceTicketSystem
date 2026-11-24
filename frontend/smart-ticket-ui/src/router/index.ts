import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import LoginView from '@/views/LoginView.vue'
import MainLayout from '@/layouts/MainLayout.vue'
import ServiceInboxView from '@/views/ServiceInboxView.vue'
import NewRequestView from '@/views/NewRequestView.vue'
import KnowledgeBaseView from '@/views/KnowledgeBaseView.vue'
import AdminReportsView from '@/views/AdminReportsView.vue'
import AuditLogsView from '@/views/AuditLogsView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'Login', component: LoginView },
    {
      path: '/',
      component: MainLayout,
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: '/inbox' },
        { path: 'inbox', name: 'ServiceInbox', component: ServiceInboxView },
        { path: 'new-request', name: 'NewRequest', component: NewRequestView },
        { path: 'knowledge-base', name: 'KnowledgeBase', component: KnowledgeBaseView },
        { path: 'team-reports', name: 'TeamReports', component: AdminReportsView },
        { path: 'audit-logs', name: 'AuditLogs', component: AuditLogsView }
      ]
    }
  ]
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return '/login'
  }
})

export default router
