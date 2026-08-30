import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'player',
  },
  {
    path: 'player',
    loadChildren: () =>
      import('./features/player/player.routes').then((feature) => feature.PLAYER_ROUTES),
  },
  {
    path: 'gm',
    loadChildren: () =>
      import('./features/game-master/game-master.routes').then(
        (feature) => feature.GAME_MASTER_ROUTES,
      ),
  },
  {
    path: '**',
    redirectTo: 'player',
  },
];
