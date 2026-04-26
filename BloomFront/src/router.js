import { createRouter, createWebHistory } from 'vue-router'
import Landing from './views/Landing.vue'

export default createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Landing },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})
