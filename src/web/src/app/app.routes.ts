import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';

export const routes: Routes = [
	{
		path: '',
		pathMatch: 'full',
		redirectTo: 'showroom'
	},
	{
		path: 'login',
		canActivate: [guestGuard],
		loadComponent: () => import('./auth/login/login').then((m) => m.Login)
	},
	{
		path: 'register',
		canActivate: [guestGuard],
		loadComponent: () => import('./auth/register/register').then((m) => m.Register)
	},
	{
		path: 'showroom',
		canActivate: [authGuard],
		loadComponent: () => import('./views/device/device').then((m) => m.Device)
	},
	{
		path: 'management',
		canActivate: [authGuard],
		loadComponent: () => import('./management/management/management').then((m) => m.Management),
		children: [
			{
				path: '',
				pathMatch: 'full',
				redirectTo: 'devices'
			},
			{
				path: 'devices',
				loadComponent: () => import('./management/device/device').then((m) => m.Device)
			}
		]
	},
	{
		path: 'analytics/production',
		canActivate: [authGuard],
		loadComponent: () => import('./views/analytics/production/production').then((m) => m.ProductionAnalyticsComponent)
	},
	{
		path: 'analytics/device/:id',
		canActivate: [authGuard],
		loadComponent: () => import('./views/analytics/device-detail/device-detail').then((m) => m.DeviceAnalyticsDetailComponent)
	},
	{
		path: '**',
		redirectTo: 'showroom'
	}
];
