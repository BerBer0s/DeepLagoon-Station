import react from '@vitejs/plugin-react';
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const { createViteConfig } = createRequire(import.meta.url)('./vite.base.config.cjs');
const resources = fileURLToPath(new URL('../Resources/Web/DeepLagoon/', import.meta.url));

const packagedAssets = {
  name: 'deeplagoon-packaged-assets',
  configureServer(server) {
    server.middlewares.use((req, res, next) => {
      const pathname = new URL(req.url, 'http://localhost').pathname;
      if (pathname !== '/bridge.js' && !pathname.startsWith('/font-awesome/')) return next();
      const file = path.resolve(resources, '.' + decodeURIComponent(pathname));
      if (!file.startsWith(resources) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
        res.statusCode = 404;
        res.end();
        return;
      }
      res.setHeader('Content-Type', { '.js': 'text/javascript', '.css': 'text/css',
        '.ttf': 'font/ttf', '.woff2': 'font/woff2' }[path.extname(file)] || 'application/octet-stream');
      fs.createReadStream(file).pipe(res);
    });
  },
};

export default ({ mode }) => {
  const config = createViteConfig({ entry: 'packages/tgui/index.js', bundleName: 'tgui', globalName: 'Tgui' })({ mode });
  config.plugins.push(react({ include: /\.[jt]sx?$/ }), packagedAssets);
  // A publicDir containing production interface.html shadows Vite's dev HTML.
  config.publicDir = false;
  config.server = {
    host: '127.0.0.1', port: 5173, strictPort: true,
    // Reliable for Windows checkouts on mounted/old disks as well as local NTFS.
    watch: { usePolling: true, interval: 150 },
  };
  config.optimizeDeps = {
    entries: ['interface.html', 'chat.html'],
    include: ['tgui-dev-server/link/client.cjs'],
    esbuildOptions: { loader: { '.js': 'jsx' } },
  };
  return config;
};
