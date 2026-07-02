import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { Role } from '../core/models/api.models';

interface NavItem {
  label: string;
  path: string;
  icon: 'dashboard' | 'patients' | 'appointments' | 'staff';
  roles: Role[];
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: 'dashboard', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Patients', path: '/patients', icon: 'patients', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Appointments', path: '/appointments', icon: 'appointments', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Staff', path: '/staff', icon: 'staff', roles: ['Admin'] },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly auth = inject(AuthService);

  readonly navItems = computed(() => {
    const role = this.auth.role();
    return NAV_ITEMS.filter((item) => role !== null && item.roles.includes(role));
  });

  readonly initials = computed(() =>
    this.auth
      .fullName()
      .split(' ')
      .map((part) => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );

  logout(): void {
    this.auth.logout();
  }
}
