import { useBackend } from '../backend';
import { Button, Section } from '../components';
import { Window } from '../layouts';

export const DeepLagoonDemo = () => {
  const { act, data } = useBackend<{ count: number }>();
  return (
    <Window title="DeepLagoon TGUI">
      <Window.Content>
        <Section title="Server counter">
          <p>Value received from the server: {data.count}</p>
          <Button onClick={() => act('increment')}>Increment</Button>
          <Button onClick={() => act('reset')}>Reset</Button>
        </Section>
      </Window.Content>
    </Window>
  );
};
