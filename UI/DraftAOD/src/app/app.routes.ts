import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards/auth.guards';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent), title: 'Football Mayhem' },
  { path: 'about', loadComponent: () => import('./features/about/about.component').then((m) => m.AboutComponent), title: 'About | Football Mayhem' },
  { path: 'auction', canActivate: [authGuard], loadComponent: () => import('./features/auction/auction.component').then((m) => m.AuctionComponent), title: 'Live Auction | Football Mayhem' },
  { path: 'login', loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent), title: 'Sign in | Football Mayhem' },
  { path: 'register', loadComponent: () => import('./features/auth/register/register.component').then((m) => m.RegisterComponent), title: 'Create account | Football Mayhem' },
  { path: 'profile', canActivate: [authGuard], loadComponent: () => import('./features/profile/profile.component').then((m) => m.ProfileComponent), title: 'Profile | Football Mayhem' },
  { path: 'favorites', canActivate: [authGuard], loadComponent: () => import('./features/favorites/favorites.component').then((m) => m.FavoritesComponent), title: 'Favorites | Football Mayhem' },
  { path: 'search', canActivate: [authGuard], loadComponent: () => import('./features/search/search.component').then((m) => m.SearchComponent), title: 'Search | Football Mayhem' },
  { path: 'assistant', canActivate: [authGuard], loadComponent: () => import('./features/assistant/assistant.component').then((m) => m.AssistantComponent), title: 'Assistant | Football Mayhem' },
  { path: 'hall-of-fame', canActivate: [authGuard], loadComponent: () => import('./features/hall-of-fame/hall-of-fame.component').then((m) => m.HallOfFameComponent), title: 'Hall of Fame | Football Mayhem' },
  { path: 'chemistry', canActivate: [authGuard], loadComponent: () => import('./features/chemistry/chemistry.component').then((m) => m.ChemistryComponent), title: 'Chemistry | Football Mayhem' },
  { path: 'players/:id', canActivate: [authGuard], loadComponent: () => import('./features/player-profile/player-profile.component').then((m) => m.PlayerProfileComponent), title: 'Player profile | Football Mayhem' },
  { path: 'admin', canActivate: [authGuard, adminGuard], loadComponent: () => import('./features/admin/dashboard/admin-dashboard.component').then((m) => m.AdminDashboardComponent), title: 'Administration | Football Mayhem' },
  { path: 'admin/users', canActivate: [authGuard, adminGuard], loadComponent: () => import('./features/admin/users/admin-users.component').then((m) => m.AdminUsersComponent), title: 'User management | Football Mayhem' },
  { path: 'admin/players', canActivate: [authGuard, adminGuard], loadComponent: () => import('./features/admin/players/admin-players.component').then((m) => m.AdminPlayersComponent), title: 'Player management | Football Mayhem' },
  { path: 'admin/chemistry', canActivate: [authGuard, adminGuard], loadComponent: () => import('./features/admin/chemistry/admin-chemistry.component').then((m) => m.AdminChemistryComponent), title: 'Chemistry management | Football Mayhem' },
  { path: 'admin/activity', canActivate: [authGuard, adminGuard], loadComponent: () => import('./features/admin/activity/admin-activity.component').then((m) => m.AdminActivityComponent), title: 'System activity | Football Mayhem' },
  { path: 'unauthorized', loadComponent: () => import('./features/errors/unauthorized/unauthorized.component').then((m) => m.UnauthorizedComponent), title: 'Access restricted | Football Mayhem' },
  { path: '**', loadComponent: () => import('./features/errors/not-found/not-found.component').then((m) => m.NotFoundComponent), title: 'Page not found | Football Mayhem' },
];










