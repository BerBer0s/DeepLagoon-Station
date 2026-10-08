(() => {
  if (window.__deepLagoonGuideTree || !(location.pathname==='/ru/in_game' || location.pathname.startsWith('/ru/in_game/'))) return;
  window.__deepLagoonGuideTree = true;
  const panel = document.createElement('aside');
  panel.style.cssText = 'position:fixed;left:0;top:0;bottom:0;width:240px;overflow:auto;background:#171c24;color:#ddd;padding:14px;z-index:10000;font:14px Arial';
  document.body.style.marginLeft = '270px';
  const heading = document.createElement('h3'); heading.textContent = 'Игровая справка'; panel.append(heading);
  const reload = document.createElement('button'); reload.textContent = 'Обновить дерево'; panel.append(reload);
  const tree = document.createElement('div'); panel.append(tree); document.body.append(panel);
  const load = async () => {
    tree.textContent = 'Загрузка…';
    try {
      const response = await fetch('/graphql', {method:'POST',credentials:'omit',headers:{'Content-Type':'application/json'},body:JSON.stringify({query:'{ pages { tree(mode: ALL, locale: "ru") { id path title isFolder parent } } }'})});
      if (!response.ok) throw Error('HTTP '+response.status);
      const result = await response.json();
      if (!Array.isArray(result.data?.pages?.tree)) throw Error('Дерево страниц недоступно');
      const pages = result.data.pages.tree.filter(p => p.path==='in_game' || p.path.startsWith('in_game/'));
      tree.replaceChildren();
      if (!pages.length) tree.textContent = 'В разделе /in_game/ пока нет опубликованных статей.';
      const root = {children:new Map()};
      for (const page of pages) {
        let node=root;
        for (const segment of page.path.split('/').slice(1)) {
          if (!node.children.has(segment)) node.children.set(segment,{children:new Map(),title:segment});
          node=node.children.get(segment);
        }
        node.title=page.title; if (!page.isFolder) node.path=page.path;
      }
      const render = (node,parent) => {
        for (const child of [...node.children.values()].sort((a,b)=>a.title.localeCompare(b.title,'ru'))) {
          let container=parent;
          if (child.children.size) {const details=document.createElement('details');details.open=true;const summary=document.createElement('summary');summary.textContent=child.title;details.append(summary);parent.append(details);container=details;}
          if (child.path) {const link=document.createElement('a');link.textContent=child.title;link.href='/ru/'+child.path;link.style.cssText='display:block;color:#abc6ec;padding:6px 4px;overflow-wrap:anywhere';container.append(link);}
          render(child,container);
        }
      };
      if (root.path) {const link=document.createElement('a');link.textContent=root.title;link.href='/ru/'+root.path;link.style.cssText='display:block;color:#abc6ec;padding:6px 4px';tree.append(link);}
      render(root,tree);
    } catch (_) {tree.textContent='Не удалось загрузить дерево. Нажмите «Обновить дерево».';}
  };
  reload.onclick=load;load();
})();
