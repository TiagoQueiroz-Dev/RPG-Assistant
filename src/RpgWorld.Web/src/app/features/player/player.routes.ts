import { Routes } from '@angular/router';

export const PLAYER_ROUTES: Routes = [
  {
    path: '',
    title: 'Visão do Jogador · RPG Assistant',
    loadComponent: () => import('./player-shell').then((component) => component.PlayerShell),
  },
];
