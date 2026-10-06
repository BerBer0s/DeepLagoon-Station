const path = require('path');
const { createViteConfig } = require('./vite.base.config.cjs');
module.exports = (env) => {
  const config = createViteConfig({ entry: 'packages/tgui/index.js', bundleName: 'tgui', globalName: 'Tgui' })(env);
  config.build.outDir = path.resolve(__dirname, '../Resources/Web/DeepLagoon');
  return config;
};
