import { Routes } from '@angular/router';

export const GAME_MASTER_ROUTES: Routes = [
  {
    path: '',
    title: 'Visão do Mestre · RPG Assistant',
    loadComponent: () =>
      import('./game-master-shell').then((component) => component.GameMasterShell),
  },
];
