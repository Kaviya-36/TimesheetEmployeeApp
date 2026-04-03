import { Component, inject, signal } from '@angular/core';
import { AuthService } from '../../services/auth.service';
import { TabService } from '../../services/tab.service';
import { UserRole } from '../../models';

interface NavItem { label: string; icon: string; tab: string; }

const NAV_MAP: Record<string, NavItem[]> = {
  Admin: [
    { label: 'Overview',    icon: '📊', tab: 'overview'   },
    { label: 'Users',       icon: '👥', tab: 'users'      },
    { label: 'Projects',    icon: '🗂',  tab: 'projects'   },
    { label: 'Timesheets',  icon: '📋', tab: 'timesheets' },
    { label: 'Leaves',      icon: '🌴', tab: 'leaves'     },
    { label: 'Attendance',  icon: '⏰', tab: 'attendance' },
    { label: 'Audit Logs',  icon: '🔍', tab: 'auditlogs'  },
    { label: 'Settings',    icon: '⚙',  tab: 'settings'   },
    { label: 'Profile',     icon: '👤', tab: 'profile'    },
  ],
  HR: [
    { label: 'Employees',   icon: '👥', tab: 'employees'  },
    { label: 'Attendance',  icon: '📅', tab: 'attendance' },
    { label: 'Leaves',      icon: '🌴', tab: 'leaves'     },
    { label: 'Timesheets',  icon: '📋', tab: 'timesheets' },
    { label: 'Payroll',     icon: '💰', tab: 'payroll'    },
    { label: 'Reports',     icon: '📊', tab: 'reports'    },
    { label: 'Profile',     icon: '👤', tab: 'profile'    },
  ],
  Manager: [
    { label: 'Overview',      icon: '📊', tab: 'dashboard'   },
    { label: 'Timesheets',    icon: '📋', tab: 'timesheets'  },
    { label: 'My Timesheet',  icon: '🕐', tab: 'mytimesheet' },
    { label: 'Leaves',        icon: '🌴', tab: 'leaves'      },
    { label: 'My Team',       icon: '👥', tab: 'team'        },
    { label: 'Projects',      icon: '🗂',  tab: 'projects'    },
    { label: 'Attendance',    icon: '⏰', tab: 'attendance'  },
    { label: 'Profile',       icon: '👤', tab: 'profile'     },
  ],
  Employee: [
    { label: 'Dashboard',   icon: '📊', tab: 'dashboard'  },
    { label: 'Timesheets',  icon: '📋', tab: 'timesheet'  },
    { label: 'Attendance',  icon: '⏰', tab: 'attendance' },
    { label: 'Leave',       icon: '🌴', tab: 'leave'      },
    { label: 'Profile',     icon: '👤', tab: 'profile'    },
  ],
  Mentor: [
    { label: 'Interns',    icon: '🎓', tab: 'interns'  },
    { label: 'My Leave',   icon: '🌴', tab: 'leave'    },
    { label: 'Profile',    icon: '👤', tab: 'profile'  },
  ],
  Intern: [
    { label: 'Dashboard',   icon: '📊', tab: 'dashboard'  },
    { label: 'Attendance',  icon: '⏰', tab: 'attendance' },
    { label: 'Leave',       icon: '🌴', tab: 'leave'      },
    { label: 'Tasks',       icon: '📝', tab: 'tasks'      },
    { label: 'Profile',     icon: '👤', tab: 'profile'    },
  ],
};

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css'
})
export class SidebarComponent {
  readonly auth      = inject(AuthService);
  readonly tabSvc    = inject(TabService);
  readonly collapsed = signal(false);

  roleColor(): string { return '#2563EB'; }

  navItems(): NavItem[] {
    const role = this.auth.currentRole() ?? '';
    return NAV_MAP[role] ?? [];
  }

  select(tab: string) { this.tabSvc.setTab(tab); }
}
