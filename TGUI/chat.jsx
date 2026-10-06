// BlueMoon chat renderer, tabs, search, highlights and appearance settings.
// Game input and permission checks remain in SS14's ChatInputBox/ChatUIController.
import './packages/tgui-panel/styles/main.scss';
import './packages/tgui-panel/styles/themes/light.scss';
import './chat.scss';
import { combineReducers, useSelector } from 'common/redux';
import { useState } from 'react';
import { Button, Section, Stack } from 'tgui/components';
import { Pane } from 'tgui/layouts';
import { createRenderer } from 'tgui/renderer';
import { configureStore } from 'tgui/store';
import { ChatPanel, ChatSearchBar, ChatTabs, chatMiddleware, chatReducer } from 'tgui-panel/chat';
import { chatRenderer } from 'tgui-panel/chat/renderer';
import { selectChat } from 'tgui-panel/chat/selectors';
import { settingsMiddleware, settingsReducer, SettingsPanel, useSettings } from 'tgui-panel/settings';
import { EmoteBar, emoteReducer } from './EmoteBar';

const store = configureStore({
  reducer: combineReducers({ chat: chatReducer, settings: settingsReducer, emotes: emoteReducer }),
  middleware: { pre: [chatMiddleware, settingsMiddleware] },
});
const Panel = () => {
  const settings = useSettings();
  const chat = useSelector(selectChat);
  const [searchOpen, setSearchOpen] = useState(false);
  return (
    <Pane theme={settings.theme === 'default' ? 'light' : settings.theme}>
      <Stack fill vertical>
        <Stack.Item><Section fitted><Stack align="center">
          <Stack.Item grow><ChatTabs /></Stack.Item>
          <Stack.Item><Button icon="search" onClick={() => setSearchOpen(!searchOpen)} /></Stack.Item>
          <Stack.Item><Button icon="cog" onClick={() => settings.toggle()} /></Stack.Item>
        </Stack></Section></Stack.Item>
        {searchOpen && <Stack.Item><ChatSearchBar /></Stack.Item>}
        {settings.visible && <Stack.Item><SettingsPanel /></Stack.Item>}
        <Stack.Item><EmoteBar /></Stack.Item>
        <Stack.Item grow><Section fill fitted position="relative">
          <Pane.Content scrollable><ChatPanel lineHeight={settings.lineHeight} /></Pane.Content>
        </Section></Stack.Item>
        <Stack.Item><Button fluid onClick={() => chatRenderer.scrollToBottom?.()}>↓</Button></Stack.Item>
      </Stack>
    </Pane>
  );
};
const render = createRenderer(() => <Panel />);
const receive = message => {
  const parsed = Byond.parseJson(message);
  if (parsed.type === 'deeplagoon/chat-reset') {
    // A full reload clears both messages and renderer state, including erased messages.
    window.location.reload();
    return;
  }
  store.dispatch(parsed);
};
window.update = receive;
store.subscribe(render);
store.dispatch({ type: 'update', payload: { config: {
  interface: 'Chat', status: 2, window: { key: 'deeplagoon-chat', fancy: false },
}, data: {} } });
while (window.__updateQueue__.length) receive(window.__updateQueue__.shift());
render();
