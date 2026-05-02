# AliCraft Creations — 3D Lithophane Shop

A full ASP.NET Core 10 MVC e‑commerce site for handcrafted 3D lithophane frames and keychains.

Built for the Philippine market with cascading Province → City address pickers, GCash‑style payment (simulated), multi‑step order tracking, a real‑time admin ↔ customer chat, and a complete admin dashboard.

## Tech stack

- ASP.NET Core 10 MVC (Razor views)
- Entity Framework Core 10 + SQLite (auto‑created on first run)
- Cookie authentication (BCrypt password hashing)
- Plain CSS themed from the AliCraft logo palette (orange `#F5A623`, cyan `#22B6D2`, pink `#E91E63`, purple `#7A3FA5`, blue `#2196F3`)
- Chat via short‑interval JSON polling (no external dependencies)

## Run it

```powershell
dotnet run --urls http://localhost:5080
```

Then open [http://localhost:5080](http://localhost:5080). On first run the SQLite database `alicraft2.db` is created in the project folder and seeded automatically.

Uploads (avatars, product images, payment proofs, chat images, custom photos) are stored under `wwwroot/uploads/…`.

## Seeded accounts

| Role  | Email                  | Password  |
|-------|------------------------|-----------|
| Admin | `admin@alicraft.com`   | `Admin123!` |
| User  | `demo@alicraft.com`    | `Demo123!` |

The admin's security question is **"What is our shop name?"** with answer **"alicraft"** (for the Forgot Password demo).

Six lithophane products (3 frames + 3 keychains) are pre‑loaded.

## Features

### Public
- **Home** — hero, featured products, value props
- **About** — craft story, shipping info
- **Shop** — category filter (Frame / Keychain), live search
- **Product details** — custom note + photo upload per item

### Customer (authenticated)
- **Register / Login** with cascading PH Province → City dropdown
- **Forgot password** — 3‑step flow: email → security answer → new password
- **Profile** — edit address, change password, upload avatar
- **Cart** — per‑item quantity, custom photo + note, flat ₱79 shipping
- **Checkout** — GCash (reference # + optional screenshot) or Cash on Delivery
- **Orders** — history, filter by status, **printable receipt**
- **Tracking bar** — Pending → Processing → In Transit → Delivered
- **Cancel order** while still Pending / Processing
- **Buy again** — one‑click reorder of a past order into your cart
- **Chat with admin** — near real‑time (2.5s polling) with image attachments

### Admin
- **Dashboard** — product / user / order / revenue stats; pipeline + recent orders
- **Reports** — 14‑day revenue bar chart, top products, top customers, payment mix, status breakdown
- **Products CRUD** — create, edit, delete (auto‑archive if referenced by past orders); low‑stock badges
- **Orders** — filter by status, view full details, update status
- **Inbox** — unread counts, open any customer thread
- **Customers** — quick list with direct‑to‑chat action

### Shop UX polish
- Sort by newest / price ↑ / price ↓ / name
- Low‑stock indicators ("Only 3 left!", "Out of stock") on both customer cards and admin table
- Testimonials + gradient call‑to‑action on homepage

## Project structure

```
Alicraft2/
├── Program.cs                 # DI, cookie auth, EF Core, seeder
├── appsettings.json
├── Models/                    # User, Product, CartItem, Order, OrderItem, ChatMessage, ViewModels
├── Data/
│   ├── AppDbContext.cs
│   ├── DbInitializer.cs       # seeds admin + demo + 6 products
│   └── LocationData.cs        # PH Province → City map
├── Services/
│   └── CurrentUser.cs         # scoped auth helper for controllers + views
├── Controllers/               # Home, Account, Locations, Shop, Cart, Orders, Chat, Admin
├── Views/                     # themed Razor views
└── wwwroot/
    ├── css/site.css
    ├── js/site.js
    ├── images/                # logo + placeholder SVGs
    └── uploads/               # runtime-created
```

## API endpoints (JSON)

- `GET  /api/locations/provinces` → `["Metro Manila", "Cebu", ...]`
- `GET  /api/locations/cities?province={name}` → `["Manila", "Makati", ...]`
- `GET  /Chat/Poll?since={unix-ms}&userId={id}` → new chat messages (admin requires `userId`)

## Resetting the database

Stop the server, delete `alicraft2.db`, and restart — it will be re‑seeded.
