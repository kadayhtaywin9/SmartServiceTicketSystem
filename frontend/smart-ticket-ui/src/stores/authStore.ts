import { defineStore } from 'pinia'
import http from '@/api/http'

interface UserSession {
  token: string
  userId: number
  fullName: string
  email: string
  role: string
  permissions: string[]
}

export const useAuthStore = defineStore('auth', {
  state: (): { session: UserSession | null } => ({
    session: JSON.parse(localStorage.getItem('stms_session') || 'null')
  }),
  getters: {
    isAuthenticated: (state) => !!state.session?.token,
    isStaff: (state) => ['SuperAdmin', 'DepartmentLead', 'SupportAgent'].includes(state.session?.role || ''),
    hasPermission: (state) => (perm: string) => state.session?.permissions.includes(perm) || false
  },
  actions: {
    async login(email: string, password: string) {
      const { data } = await http.post<UserSession>('/auth/login', { email, password })
      this.session = data
      localStorage.setItem('stms_token', data.token)
      localStorage.setItem('stms_session', JSON.stringify(data))
    },
    logout() {
      this.session = null
      localStorage.removeItem('stms_token')
      localStorage.removeItem('stms_session')
    }
  }
})
