> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Sprint 1: Login Flow + Seeding + Nav

> **Status**: COMPLETE
> **Goal**: Log in as super admin or tenant user, see seed data, navigate with role-based visibility.

---

## Tasks

```
[x] 1.1  Login page — error display, loading state, remember-me checkbox
[x] 1.2  Login page — actually sign in via HttpContext cookie (Blazor SSR POST)
[x] 1.3  Logout — clear cookie, redirect to /account/login
[x] 1.4  NavMenu — load shop floor stages dynamically from DB
[x] 1.5  NavMenu — role-based visibility (operators see only assigned stages)
[x] 1.6  Redirect unauthenticated users to /account/login
[x] 1.7  Platform/Tenants — super admin can create new tenant (calls TenantService)
[x] 1.8  Platform/Users — super admin can create platform users
[x] 1.9  Verify end-to-end: login as superadmin → see platform pages
[x] 1.10 Verify end-to-end: login as tenant admin → see dashboard + all nav
```

---

## Acceptance Criteria

- `superadmin` / `admin123` → redirects to `/platform/tenants`
- `admin` / `admin123` → redirects to `/` (dashboard)
- Unauthenticated users see only the login page
- NavMenu shows shop floor stages from DB, not hardcoded
- Operators see only Dashboard + assigned stages + Part Tracker
- Managers/Admins see everything except Platform
- Super admin sees everything including Platform
- Logout clears auth cookie and redirects to login

## Technical Notes

- Blazor Server uses `HttpContext` for cookie auth — login must be an SSR page (not interactive)
- Or use a minimal API endpoint for login POST
- `TenantMiddleware` already reads claims and sets `ITenantContext`
- `TenantDbContextFactory` already falls back to in-memory DB when no tenant
- Seed data: super admin + demo tenant already created in `Program.cs`

## Files to Touch

- `Components/Pages/Account/Login.razor` — rewrite for real auth
- `Components/Pages/Account/Logout.razor` — rewrite for real auth
- `Components/Layout/NavMenu.razor` — dynamic stages + role filtering
- `Components/Pages/Platform/Tenants.razor` — wire to TenantService
- `Components/Pages/Platform/Users.razor` — wire to PlatformDbContext
- `Program.cs` — ensure auth middleware order is correct
- `Components/App.razor` — add `<AuthorizeRouteView>` if not present
