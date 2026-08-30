import { routes } from './app.routes';

describe('application routes', () => {
  it('keeps game master and player experiences in separate lazy routes', () => {
    const player = routes.find((route) => route.path === 'player');
    const gameMaster = routes.find((route) => route.path === 'gm');

    expect(player?.loadChildren).toBeTypeOf('function');
    expect(gameMaster?.loadChildren).toBeTypeOf('function');
    expect(player).not.toBe(gameMaster);
  });
});
