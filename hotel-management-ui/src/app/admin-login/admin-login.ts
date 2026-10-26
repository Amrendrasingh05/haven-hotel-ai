
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

@Component({
  selector: 'app-admin-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-login.html',
  styleUrl: './admin-login.scss'
})
export class AdminLogin {
  private readonly apiUrl = 'https://haven-hotel-api.onrender.com/api';

  username = 'admin';
  password = '';
  loading = false;
  errorMessage = '';

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router
  ) {}

  login(): void {
    this.errorMessage = '';

    if (!this.username.trim() || !this.password) {
      this.errorMessage = 'Enter your username and password.';
      return;
    }

    this.loading = true;

    this.http.post<{
      token: string;
      username: string;
      expiresAtUtc: string;
    }>(`${this.apiUrl}/admin-auth/login`, {
      username: this.username.trim(),
      password: this.password
    }).subscribe({
      next: (response) => {
        sessionStorage.setItem('havenAdminSessionToken', response.token);
        sessionStorage.setItem('havenAdminUsername', response.username);
        this.password = '';
        this.loading = false;
        void this.router.navigate(['/admin']);
      },
      error: (error) => {
        this.errorMessage =
          error.error?.message ?? 'Login failed. Please try again.';
        this.loading = false;
      }
    });
  }
}
