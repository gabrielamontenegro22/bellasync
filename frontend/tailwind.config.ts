// tailwind.config.ts — escrito en JavaScript puro (sin sintaxis TS)
// Razón: el loader de Tailwind a veces no parsea sintaxis TypeScript
// (`satisfies Config`, `import type`) y termina cargando el config default.
// Mantenemos el nombre .ts para que Tailwind lo encuentre primero.

/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx,js,jsx}'],
  theme: {
    extend: {
      colors: {
        cream: '#fbf8f2',
        brand: {
          50:  '#f1f8f6',
          100: '#dcefe9',
          200: '#b9ddd2',
          300: '#8ec5b6',
          400: '#5fa595',
          500: '#3f8a7a',
          600: '#2a7064',
          700: '#0f766e',
          800: '#0d5e57',
          900: '#0a4842',
        },
        gold: {
          50:  '#fbf7ee',
          100: '#f3ead0',
          200: '#e6d5a3',
          300: '#d6bc78',
          400: '#c9a86a',
          500: '#b39052',
          600: '#8f7341',
        },
        warm: {
          50:  '#faf8f5',
          100: '#f4f1ec',
          150: '#ece7df',
          200: '#e3ddd2',
          250: '#d8d1c4',
          300: '#cbc3b4',
          400: '#a89f8e',
          500: '#80796a',
          600: '#5f5a4f',
          700: '#46423a',
          800: '#2e2b25',
          900: '#1a1814',
        },
        terra: {
          100: '#f6e6e0',
          300: '#e2a89a',
          500: '#b96f5b',
        },
      },
      fontFamily: {
        sans:  ['Inter', 'ui-sans-serif', 'system-ui'],
        serif: ['"Cormorant Garamond"', 'Georgia', 'serif'],
        mono:  ['"JetBrains Mono"', 'ui-monospace', 'monospace'],
      },
      boxShadow: {
        soft:   '0 1px 2px rgba(46,43,37,0.04), 0 2px 6px rgba(46,43,37,0.04)',
        softer: '0 1px 2px rgba(46,43,37,0.03)',
        pop:    '0 12px 40px -16px rgba(46,43,37,0.25)',
        panel:  '-12px 0 32px -12px rgba(46,43,37,0.18)',
      },
      borderRadius: {
        lg: '10px',
        xl: '14px',
      },
      keyframes: {
        fadeIn:    { from: { opacity: '0', transform: 'translateY(6px)' },  to: { opacity: '1', transform: 'translateY(0)' } },
        stepIn:    { from: { opacity: '0', transform: 'translateX(12px)' }, to: { opacity: '1', transform: 'translateX(0)' } },
        slideIn:   { from: { transform: 'translateX(100%)' },               to: { transform: 'translateX(0)' } },
        pingSlow:  { '0%': { transform: 'scale(1)', opacity: '0.5' },       '80%, 100%': { transform: 'scale(1.6)', opacity: '0' } },
      },
      animation: {
        'fade':      'fadeIn   .25s ease both',
        'step':      'stepIn   .35s ease both',
        'slide':     'slideIn  .25s ease both',
        'ping-slow': 'pingSlow 1.8s cubic-bezier(0,0,.2,1) infinite',
      },
    },
  },
  plugins: [],
}
