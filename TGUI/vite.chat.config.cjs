const path = require('path');
const { createViteConfig } = require('./vite.base.config.cjs');
module.exports = (env) => {
  const config = createViteConfig({ entry: 'chat.jsx', bundleName: 'chat', globalName: 'DeepLagoonChat' })(env);
  config.build.outDir = path.resolve(__dirname, '../Resources/Web/DeepLagoon');
  return config;
};
