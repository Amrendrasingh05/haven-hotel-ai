
import { Routes } from '@angular/router';
import { Admin } from './admin/admin';
import { AdminLogin } from './admin-login/admin-login';
import { Guest } from './guest/guest';
import { adminAuthGuard } from './admin-auth-guard';
import { Home } from './home/home';


export const routes: Routes = [
  {
    path: '',
    component: Home,
    pathMatch: 'full'
  },

  { path: 'admin-login', component: AdminLogin },
  {
    path: 'admin',
    component: Admin,
    canActivate: [adminAuthGuard]
  },
  { path: 'guest', component: Guest },
  { path: '', redirectTo: 'guest', pathMatch: 'full' },
  { path: '**', redirectTo: 'guest' }
];
