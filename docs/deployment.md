# Deployment — free stack (Neon + Render + Vercel)

Goal: a real URL for demos, ₹0/month. Every push to `main` redeploys.

```
Browser ──► Vercel (Angular, free)
               │  /api/* proxied by vercel.json (same-origin: no CORS pain)
               ▼
            Render (API container, free)
               ▼
            Neon (PostgreSQL, free)
```

## 1. Database — Neon (5 min)

1. [neon.tech](https://neon.tech) → sign up with GitHub → **New project** (`klivia`, region Singapore)
2. Copy the **connection string** (starts `postgresql://…`) and convert to .NET format:
   `Host=<host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require`

## 2. API — Render (10 min)

1. [render.com](https://render.com) → sign up with GitHub → **New → Web Service** → pick `clinic-saas`
2. Settings: Language **Docker**, Root Directory **backend**, Instance type **Free**
3. Environment variables:

| Key | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | the Neon string from step 1 |
| `JwtSettings__Secret` | fresh random 64 chars — never reuse dev's |
| `Database__MigrateOnStartup` | `true` |
| `Email__User` | `taveperz@gmail.com` |
| `Email__Password` | the Gmail app password |
| `Frontend__BaseUrl` | your Vercel URL (add after step 3) |
| `Cors__AllowedOrigins__0` | your Vercel URL (add after step 3) |

4. Deploy → note your URL, e.g. `https://klivia.onrender.com`

> Free-tier truth: Render sleeps after ~15 min idle; the first request takes
> ~30–60 s to wake. Fine for demos — before real customers, upgrade ($7/mo)
> or move to Azure.

## 3. Frontend — Vercel (5 min)

1. Edit `frontend/vercel.json` → replace `REPLACE-WITH-YOUR-RENDER-URL` with your Render URL → commit & push
2. [vercel.com](https://vercel.com) → sign up with GitHub → **Add New → Project** → pick `clinic-saas`
3. Settings: Root Directory **frontend**, Framework **Angular**,
   Output Directory **dist/clinic-frontend/browser**
4. Deploy → you get `https://<name>.vercel.app`
5. Go back to Render and fill `Frontend__BaseUrl` + `Cors__AllowedOrigins__0` with that URL

## 4. Smoke test the live site

Register a clinic → add staff (check the invite email arrives) → patient →
appointment → check-in → consult → download the prescription PDF.

## Later

- Custom domain (`klivia.in`): buy → add to Vercel → done (free SSL)
- When Render's cold starts hurt: paid instance or the planned Azure move
