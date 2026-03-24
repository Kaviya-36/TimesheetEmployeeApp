import { Component, inject, signal, Input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { UserRole } from '../../models';

interface NavItem {
  label:    string;
  icon:     string;
  route:    string;
  roles:    UserRole[];
  children?: { label: string; tab: string }[];
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css'
})
export class SidebarComponent {
  readonly auth      = inject(AuthService);
  readonly collapsed = signal(false);

  private readonly allNav: NavItem[] = [
    { label: 'Dashboard',    icon: '⊞',  route: '/admin',    roles: ['Admin'] },
    { label: 'HR Portal',    icon: '⊞',  route: '/hr',       roles: ['HR'] },
    { label: 'Team View',    icon: '⊞',  route: '/manager',  roles: ['Manager'] },
    { label: 'My Space',     icon: '⊞',  route: '/employee', roles: ['Employee', 'Mentor'] },
    { label: 'Intern Hub',   icon: '⊞',  route: '/intern',   roles: ['Intern'] },
  ];

  readonly menuSections: { section: string; items: NavItem[] }[] = [
    {
      section: 'MAIN',
      items: [
        { label: 'Dashboard',  icon: '▦',  route: '/admin',    roles: ['Admin'] },
        { label: 'HR Portal',  icon: '▦',  route: '/hr',       roles: ['HR'] },
        { label: 'Team View',  icon: '▦',  route: '/manager',  roles: ['Manager'] },
        { label: 'My Space',   icon: '▦',  route: '/employee', roles: ['Employee', 'Mentor'] },
        { label: 'Intern Hub', icon: '▦',  route: '/intern',   roles: ['Intern'] },
      ]
    }
  ];

  visibleNav(): NavItem[] {
    const role = this.auth.currentRole() ?? ('' as UserRole);
    return this.allNav.filter(n => n.roles.includes(role));
  }
}
