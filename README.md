# InfluencerMatch

This repository contains a full-stack SaaS application for matching influencers with brand campaigns.

## Backend (ASP.NET Core Web API)

### Prerequisites
- .NET 10 SDK
- PostgreSQL server running locally (used connection string in `appsettings.json`)

### Running the API
1. Open a terminal in `backend/InfluencerMatch.API`.
2. Update the connection string in `appsettings.json` if necessary.
3. Apply migrations (first run only or when models change):
   ```bash
   dotnet ef database update --project ../InfluencerMatch.Infrastructure --startup-project .
   ```
4. Run the API:
   ```bash
   dotnet run
   ```
5. The API will start on a dynamic port (e.g. `https://localhost:60587`). Swagger UI will be available at `/swagger`.

### Database
The project uses EF Core with code-first migrations and a PostgreSQL provider. Delete and recreate migrations if you switch databases.

## Frontend (Vue 3 + Vite)

### Prerequisites
- Node.js 18+ / npm
- Ensure backend API is running first.

### Configuration
The frontend reads the API base URL from an environment variable `VITE_API_URL`. If not set it defaults to `https://localhost:60587/api`.

Create a `.env` file in `frontend/influencer-match` with contents similar to:
```
VITE_API_URL=https://localhost:60587/api
```

### Running
1. Install dependencies:
   ```bash
   cd frontend/influencer-match
   npm install
   ```
2. Start development server:
   ```bash
   npm run dev
   ```
3. Open `http://localhost:3000` in your browser.

## Features
- User registration/login with JWT authentication
- Role-based UI for influencers and brands
- CRUD operations for influencer profiles and brand campaigns (brands can edit their campaigns)
- Campaign browsing for influencers and creator browsing for brands
- Detailed influencer profile pages and campaign listing pages
- Matching algorithm returns ranked influencer suggestions; results are viewable by both sides
- Responsive Bootstrap 5 UI with multiple views and routing
- Reactive authentication state allows UI to update immediately on login/logout

## Notes
- Adjust ports or URLs via environment variables as needed.
- A production deployment would require proper CORS, HTTPS certificates, and secure secrets.

## Docs
- SMTP email setup for lifecycle notifications: `docs/email-setup.md`

Enjoy building your InfluencerMatch application!
