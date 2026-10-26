# Ceyvoria Tours — Backend Web API (.NET 9)

**High-Performance ASP.NET Core 9.0 REST API with Entity Framework Core & PostgreSQL.**

This repository powers the backend API for **Ceyvoria Tours**, a luxury travel platform for Sri Lanka.

---

## 🏛️ Architecture & Features

- **Clean Architecture Layers**:
  - `CeyvoriaTours.Domain`: Domain models (Tours, Destinations, Bookings, Enquiries, AppUser).
  - `CeyvoriaTours.Application`: DTOs, interfaces, validation, security abstractions.
  - `CeyvoriaTours.Infrastructure`: EF Core PostgreSQL (`CeyvoriaDb`), seed data, repository implementations.
  - `CeyvoriaTours.API`: Minimal API endpoints, JWT Bearer authentication, rate limiting, Swagger OpenAPI documentation.
- **Security & Auth**:
  - ASP.NET Core Identity password hashing (`PBKDF2`).
  - Cryptographically signed JSON Web Tokens (JWT) with HMAC-SHA256.
  - Granular fixed-window rate limiting on forms and auth endpoints.
  - CORS configuration for production & local development frontend origins.
- **Cloud & Container Ready**:
  - Production multi-stage `Dockerfile` included.
  - Dynamic `PORT` binding compatible with **Render**, **Railway**, **Fly.io**, **AWS**, or **Azure**.

---

## 🚀 Quick Start (Local Development)

### 1. Requirements
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/) or [Docker](https://www.docker.com/)

### 2. Start PostgreSQL via Docker
```bash
docker compose up -d
```
*Database runs on `localhost:5433` (or default `5432`).*

### 3. Configure Environment Variables
Copy `.env.example` to `.env` or set environment variables:
```bash
cp .env.example .env
```

Key environment variables:
```env
ASPNETCORE_ENVIRONMENT=Development
PORT=5050
ConnectionStrings__DefaultConnection=Host=localhost;Port=5433;Database=ceyvoria;Username=ceyvoria;Password=YOUR_PASSWORD
Jwt__Secret=YOUR_RANDOM_SECRET_KEY_AT_LEAST_48_CHARACTERS_LONG_FOR_SECURITY
Jwt__Issuer=CeyvoriaTours
Jwt__Audience=CeyvoriaToursWebsite
CORS_ALLOWED_ORIGINS=http://localhost:3000
```

### 4. Build and Run
```bash
dotnet restore
dotnet build
dotnet run --project CeyvoriaTours.API/CeyvoriaTours.API.csproj
```

The API will start at `http://localhost:5050`.
Access the interactive Swagger UI at **http://localhost:5050/swagger**.

---

## 🌐 Deploy to Cloud (Render / Railway / Docker)

### Deploy on Render:
1. Create a free **PostgreSQL Database** on Render (or [Neon](https://neon.tech) / [Supabase](https://supabase.com)).
2. Create a **New Web Service** pointing to this GitHub repository.
3. Select **Docker** environment (uses the included `Dockerfile`).
4. Set Environment Variables:
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `ConnectionStrings__DefaultConnection` = *Your hosted PostgreSQL connection string*
   - `Jwt__Secret` = *Secure 48+ character key*
   - `CORS_ALLOWED_ORIGINS` = `https://your-frontend-domain.vercel.app`
5. Deploy! Render will build the container and provide your live API URL.

---

## 📡 Core API Endpoints

| Method | Route | Description |
|---|---|---|
| `GET` | `/health` | API Healthcheck status |
| `GET` | `/api/tours` | List all published luxury tours |
| `GET` | `/api/tours/{slug}` | Get single tour with route & itinerary |
| `GET` | `/api/destinations` | List all 44 destinations |
| `GET` | `/api/experiences` | List curated experiences |
| `POST` | `/api/contact` | Submit guest enquiry |
| `POST` | `/api/custom-trips` | Submit tailor-made trip planner enquiry |
| `POST` | `/api/bookings` | Submit tour booking request |
| `POST` | `/api/auth/register` | Customer registration |
| `POST` | `/api/auth/login` | Customer / Admin JWT login |
| `GET` | `/api/auth/me` | Current authenticated user profile |
| `GET` | `/api/admin/*` | Admin-only dashboard & content management |
