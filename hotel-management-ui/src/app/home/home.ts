
import { Component } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home {
  constructor(private readonly router: Router) {}

  goToAdmin(): void {
    this.router.navigate(['/admin-login']);
  }

  goToGuest(): void {
    this.router.navigate(['/guest']);
  }
}
