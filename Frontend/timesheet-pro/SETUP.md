# TimeSheet Pro — Setup Guide

## Prerequisites
- Node.js 18+ (check: `node --version`)
- npm 9+  (check: `npm --version`)
- Angular CLI 21 (will be installed locally via devDependencies)

---

## 1. Install Dependencies

```bash
npm install
```

This installs Angular 21, RxJS, and all dev tools.

---

## 2. Install SignalR (Optional — for real-time notifications)

```bash
npm install @microsoft/signalr
```

The app works without it — notifications fall back to local push only.

---

## 3. Configure Backend URL

Edit `src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiUrl:  'http://localhost:5117/api',   // ← Your ASP.NET Core backend
  hubUrl:  'http://localhost:5117/notificationHub'
};
```

---

## 4. Run Development Server

```bash
npm start
# or
npx ng serve --port 4200
```

Open browser: **http://localhost:4200**

---

## 5. Build for Production

```bash
npm run build:prod
# Output: dist/timesheet-pro/
```

---

## Common Issues

### "Cannot find module '@microsoft/signalr'"
Run: `npm install @microsoft/signalr`

### CORS errors in browser
Add the Angular dev server to your backend CORS policy:
```csharp
// Program.cs
builder.Services.AddCors(options => {
    options.AddPolicy("Dev", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
app.UseCors("Dev");
```

### "NG0100: Expression Changed After Checked"
Set `changeDetection: ChangeDetectionStrategy.OnPush` — already set in `angular.json` schematics.

---

## Login Credentials (for testing)

| Role     | Username  | Password     |
|----------|-----------|--------------|
| Admin    | admin     | Admin@123    |
| HR       | hruser    | Hr@12345     |
| Manager  | manager1  | Manager@123  |
| Employee | emp001    | Emp@12345    |
| Intern   | intern01  | Intern@123   |
