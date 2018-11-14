import Vue from "vue"

import app from "./app"
import router from "./router"
import store from "./store"

import upperFirst from "lodash/upperFirst"
import camelCase from "lodash/camelCase"
import axios from "axios"

Array.prototype.last = function() {
  return this[this.length - 1]
}

const resolveUrl = (url, payload) => {
  const path = url.indexOf("/") > 0 ? url.split("/") : [url]
  const msg = data => {
    const d = data.shift()
    return {
      type: "forward",
      forward: d.split(":")[0],
      index: Number(d.split(":")[1]),
      message: {
        object: "message",
        recipient: d.split(":")[0],
        data: data.length ? msg(data) : payload,
      },
    }
  }
  return {
    object: "message",
    recipient: path.shift(),
    data: path.length ? msg(path) : payload,
  }
}

const requireComponent = require.context("./components", true, /\w+\.(vue)$/)

requireComponent.keys().forEach(fileName => {
  const componentConfig = requireComponent(fileName)
  const componentName = upperFirst(
    camelCase(
      fileName
        .replace(/^\.\/(.*)\.\w+$/, "$1")
        .split("/")
        .last(),
    ),
  )
  Vue.component(componentName, componentConfig.default || componentConfig)
})

if (!process.env.IS_WEB) Vue.use(require("vue-electron"))
Vue.config.productionTip = false

const imp = ["Field", "Switch", "Menu", "List", "Button"].forEach(e =>
  Vue.use(require("vue-material/dist/components")["Md" + e]),
)
import "vue-material/dist/vue-material.min.css"

Vue.axios = Vue.prototype.axios = axios
Vue.api = Vue.prototype.api = (url, payload) =>
  new Promise((res, rej) =>
    axios
      .post(`http://localhost:1548/api`, resolveUrl(url, payload))
      .then(res)
      .catch(rej),
  )
/* eslint-disable no-new */
new Vue({
  components: { app },
  router,
  store,
  template: "<app/>",
}).$mount("#app")
