# TimeSheet Pro — Angular 21

Enterprise-grade Attendance & Timesheet Management System built with Angular 21. Inspired by Zoho People.

---

## 🚀 Quick Start

```bash
# 1. Install dependencies
npm install

# 2. Start development server
npm start
# App runs at: http://localhost:4200

# 3. Build for production
npm run build:prod
```

---

## ⚙️ Configuration

Edit `src/environments/environment.ts` to point to your backend:

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5117/api',    // ← your ASP.NET Core backend
  hubUrl: 'http://localhost:5117/notificationHub'  // ← SignalR hub
};
```

---

## 🔐 Demo Login Credentials

| Role     | Username  | Password     |
|----------|-----------|--------------|
| Admin    | admin     | Admin@123    |
| HR       | hruser    | Hr@12345     |
| Manager  | manager1  | Manager@123  |
| Employee | emp001    | Emp@12345    |
| Intern   | intern01  | Intern@123   |

---

## 📁 Project Structure

```
src/
├── app/
│   ├── components/
│   │   ├── login/                  # Auth pages
│   │   ├── register/
│   │   ├── navbar/                 # Top navigation bar
│   │   ├── sidebar/                # Collapsible dark sidebar
│   │   ├── spinner/                # Global loading overlay
│   │   ├── toast/                  # Notification toasts
│   │   ├── breadcrumb/             # Auto-updating breadcrumbs
│   │   ├── confirm-dialog/         # Reusable confirm modal
│   │   ├── admin-dashboard/        # Admin: Users, Projects, Timesheets, Leaves
│   │   ├── employee-dashboard/     # Employee: Attendance, Timesheet, Leave
│   │   ├── manager-dashboard/      # Manager: Approvals, Team, Projects
│   │   ├── hr-dashboard/           # HR: Employees, Payroll, Reports
│   │   └── intern-dashboard/       # Intern: Limited access
│   ├── services/
│   │   ├── auth.service.ts         # JWT auth with signals
│   │   ├── api.services.ts         # All backend API calls
│   │   ├── toast.service.ts        # Global toast notifications
│   │   ├── loading.service.ts      # HTTP loading state
│   │   ├── breadcrumb.service.ts   # Breadcrumb state
│   │   └── notification.service.ts # SignalR real-time notifications
│   ├── guards/
│   │   └── auth.guard.ts           # authGuard + roleGuard
│   ├── interceptors/
│   │   └── auth.interceptor.ts     # JWT injection + error handling
│   └── models/
│       └── index.ts                # All TypeScript interfaces
├── environments/
│   ├── environment.ts              # Development config
│   └── environment.prod.ts         # Production config
├── styles.css                      # Global Zoho-style design system
├── index.html
└── main.ts
```

---

## ✨ Features

### Authentication
- ✅ JWT-based login with signal-based state
- ✅ Active-only login — inactive accounts blocked with clear message
- ✅ New registrations show "Awaiting Admin Approval" screen
- ✅ Role-based route guards

### Admin Dashboard
- ✅ User management: Add, Edit Role, Activate/Deactivate, Delete
- ✅ Project management: Create, Delete
- ✅ View all timesheets with filter, sort, pagination
- ✅ Leave approval/rejection
- ✅ System settings (working hours, email)

### Employee Dashboard
- ✅ Real-time attendance timer (HH:MM:SS)
- ✅ Check-in / Check-out
- ✅ Full timesheet CRUD (Add, Edit, Delete pending ones)
- ✅ Leave application with type selection
- ✅ Profile card with stats

### Manager Dashboard
- ✅ Timesheet approval/rejection with confirm dialog
- ✅ Leave approval/rejection
- ✅ Team member view
- ✅ Project assignment
- ✅ Quick approvals widget on overview

### HR Dashboard
- ✅ Employee CRUD (Edit, Activate/Deactivate, Delete)
- ✅ Attendance records with date filter
- ✅ Leave management
- ✅ Payroll generation
- ✅ Summary reports

### Intern Dashboard
- ✅ Limited access (no approval actions)
- ✅ Timesheet submission
- ✅ Attendance check-in/out
- ✅ Leave application
- ✅ Task list view

### UI/UX
- ✅ Zoho People-inspired design (orange brand #E05B2B)
- ✅ Dark collapsible sidebar
- ✅ Animated toast notifications (success/error/warning/info)
- ✅ Breadcrumb navigation auto-updates on tab change
- ✅ Notification bell with live SignalR updates
- ✅ Confirm dialog replaces all window.confirm()
- ✅ Pagination on every table (numbered + first/last)
- ✅ Search filters + dropdown filters on every table
- ✅ Sortable column headers with ↑↓⇅ indicators
- ✅ Responsive design (mobile-first)
- ✅ Global spinner overlay during API calls

---

## 🛠 Tech Stack

| Layer       | Technology              |
|-------------|-------------------------|
| Framework   | Angular 21 (Standalone) |
| State       | Angular Signals         |
| HTTP        | RxJS Observables        |
| Real-time   | @microsoft/signalr      |
| Forms       | Angular Reactive Forms  |
| Templates   | @if / @for (new syntax) |
| Styling     | Pure CSS (no Bootstrap) |
| Build       | Angular CLI 21          |
