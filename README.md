# STMS v6 — Enterprise Cyan Service Desk (Software Dev · IT & Network · HR · Finance · Security)

Production-ready **Smart Service Ticket Management System** designed for multi-department technology companies (**Software Development & QA**, **IT & Network Infrastructure**, **Human Resources**, **Finance & Administration**, and **Cybersecurity & Compliance**).

Built with **C# (.NET 8 Web API + SignalR + EF Core)**, **Vue 3 + Vuetify 3**, and **MySQL 8.0+**, styled with a clean **Cyan Apple Liquid Glass (Light & Dark)** UI and full **English / Myanmar (`EN / မြန်မာ`)** localization.

## What's New in v6 (Production & Company-Ready)
- **Cyan Apple Liquid Glass Theme:** Clean Frost-Cyan Light Mode (default) and Obsidian-Teal Dark Mode.
- **Multi-Department Company Structure:** Pre-configured departments, AI auto-routing keywords, agent skills, and Knowledge Base SOPs for **Software Development**, **IT & Network**, **HR**, **Finance**, and **Cybersecurity**.
- **File Attachments:** Upload and download screenshots, error logs, HR forms, and invoices directly inside ticket conversations (`POST /api/tickets/{id}/attachments`).
- **Department Transfer:** Seamlessly transfer tickets between departments (e.g., IT -> Software Dev or HR -> Finance) with immutable audit logging.
- **Team, Onboarding & Operations (`Team & Reports`):**
  - Department workload & SLA compliance overview.
  - One-click **Export CSV** for management reporting.
  - **Employee & Agent Onboarding/Offboarding:** Create new users with PBKDF2-SHA256 hashed passwords and toggle active status.

## Quick Setup

### 1. Import MySQL Database (Required for v6 departments & `ticket_attachments`)
```powershell
cmd /c "mysql -u root -p < database\schema_and_seed.sql"
```
*(Update `ConnectionStrings:DefaultConnection` in `backend\SmartTicketApi\appsettings.json` with your MySQL password.)*

### 2. Run C# (.NET 8) Backend
```powershell
cd backend\SmartTicketApi
dotnet restore
dotnet run --environment Development
```
* API: `http://localhost:5000` | Swagger: `http://localhost:5000/swagger`

### 3. Run Vue 3 + Vuetify 3 Frontend
```powershell
cd frontend\smart-ticket-ui
npm install
npm run dev
```
* App: `http://localhost:5173`

### Demo Company Accounts
| Role | Department | Email | Password |
| :--- | :--- | :--- | :--- |
| **SuperAdmin** | Global / IT | `admin@company.local` | `Admin@123` |
| **DepartmentLead** | IT & Network | `lead@company.local` | `Lead@123` |
| **SupportAgent** | Software Dev & QA | `dev@company.local` | `Dev@123` |
| **SupportAgent** | IT & Network | `it@company.local` | `It@123` |
| **SupportAgent** | Human Resources | `hr@company.local` | `Hr@123` |
| **EndUser** | Employee | `employee@company.local` | `User@123` |
