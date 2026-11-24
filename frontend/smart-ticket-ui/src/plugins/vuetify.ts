import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'
import { createVuetify, type ThemeDefinition } from 'vuetify'

const liquidGlassLight: ThemeDefinition = {
  dark: false,
  colors: {
    background: '#F0FAFC',
    surface: '#FFFFFF',
    'surface-variant': '#E0F5F9',
    primary: '#0891B2',      // Crisp Enterprise Cyan
    secondary: '#06B6D4',    // Bright Aqua Cyan
    accent: '#0E7490',
    success: '#10B981',
    warning: '#D97706',
    error: '#E11D48',
    info: '#0284C7',
    'on-background': '#0B1D24',
    'on-surface': '#0F2730'
  }
}

const liquidGlassDark: ThemeDefinition = {
  dark: true,
  colors: {
    background: '#050C0F',
    surface: '#0B1920',
    'surface-variant': '#122833',
    primary: '#22D3EE',      // Luminous Electric Cyan
    secondary: '#67E8F9',    // Soft Ice Cyan
    accent: '#06B6D4',
    success: '#34D399',
    warning: '#FBBF24',
    error: '#FB7185',
    info: '#38BDF8',
    'on-background': '#ECFEFF',
    'on-surface': '#E0F9FC'
  }
}

export default createVuetify({
  theme: {
    defaultTheme: 'liquidGlassLight',
    themes: {
      liquidGlassLight,
      liquidGlassDark
    }
  },
  defaults: {
    VCard: {
      rounded: 'xl',
      elevation: 0
    },
    VBtn: {
      rounded: 'pill',
      elevation: 0
    },
    VTextField: {
      variant: 'outlined',
      density: 'comfortable',
      rounded: 'lg'
    },
    VSelect: {
      variant: 'outlined',
      density: 'comfortable',
      rounded: 'lg'
    },
    VTextarea: {
      variant: 'outlined',
      rounded: 'lg'
    }
  }
})
