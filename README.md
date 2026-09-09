# Campus Study Hub

Campus Study Hub is a web application that helps university students organize their academic work in one place. It brings together subjects, class schedules, deadlines, shared learning materials, and study groups, with administration tools for managing users and reported content.

## 2. Problem and Objectives

Students often manage course information across separate chats, document folders, and calendars. This makes it difficult to track deadlines, find learning resources, and coordinate group study.

The project aims to:

- Centralize subjects, schedules, deadlines, and learning materials.
- Help students monitor their workload and study progress.
- Make it easier to share resources and organize study groups within a class.
- Provide controlled access to class content and tools for content moderation.
- Offer a responsive interface for desktop, tablet, and mobile devices.

## 3. Features

- **Authentication and profiles:** Google sign-in, cookie-based sessions, and student profiles with university, faculty or major, and cohort information. Demo accounts are available in Development mode.
- **Dashboard:** An overview of subjects, upcoming deadlines, completed tasks, study hours, recent documents, and study groups.
- **Subject management:** Create and update subjects, archive classes, and join classes using invitation codes.
- **Schedules and deadlines:** Manage study activities, weekly recurring schedules, and deadlines, with filters by subject and day, week, or month.
- **Learning materials:** Share links or upload PDF, TXT, DOCX, and PPTX files up to 10 MB; search, filter, bookmark, and report materials; track views and downloads.
- **Study groups:** Create group posts, request membership, approve or reject requests, and enforce member limits.
- **Administration:** View statistics, search users, suspend or restore accounts, and hide or restore reported content.
- **Activity tracking:** Record key user actions and expose a `/health` endpoint.

## 4. Technologies Used

| Technology | Purpose |
| --- | --- |
| C# and .NET 8 | Application language and runtime |
| ASP.NET Core | Web hosting, authentication, authorization, and HTTP endpoints |
| Blazor and Razor components | User interface with Interactive Server rendering |
| Entity Framework Core 8 | Data access and database operations |
| SQLite | Local database storage |
| Google OAuth and cookies | Google sign-in and session management |
| HTML and CSS | Responsive page layout and styling |
| GitHub Actions | Automated build and application checks |

The solution also includes a Blazor WebAssembly client project referenced by the web application. The current application configures Interactive Server rendering.

## 5. Installation and Running the Project

### Prerequisites

- .NET 8 SDK installed.
- A local copy of this repository.
- Google OAuth credentials only if you want to use Google sign-in.

### Run locally

Open a terminal in the repository root and run:

```powershell
dotnet restore CampusStudyHub/CampusStudyHub/CampusStudyHub.csproj
dotnet run --project CampusStudyHub/CampusStudyHub/CampusStudyHub.csproj --launch-profile http
```

Open `http://localhost:5260` in your browser. Visit `/login` and select a demo account to explore the application without configuring Google sign-in.

The `http` launch profile uses the Development environment. Demo access is controlled by `Demo:Enabled` in `CampusStudyHub/CampusStudyHub/appsettings.Development.json`.

The application creates its SQLite database automatically at `CampusStudyHub/CampusStudyHub/App_Data/campus.db`. Uploaded files are stored in the `App_Data/uploads` directory. No separate database server is required.

### Configure Google sign-in (optional)

Create a Google OAuth client of type **Web application** and register `http://localhost:5260/signin-google` as an authorized redirect URI for the local launch profile. If you use a different address, register that application's base URL followed by `/signin-google`.

Store the credentials using the web project's existing User Secrets configuration:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID" --project CampusStudyHub/CampusStudyHub/CampusStudyHub.csproj
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET" --project CampusStudyHub/CampusStudyHub/CampusStudyHub.csproj
```

Optionally, configure an administrator email and restrict sign-in to a university email domain:

```powershell
dotnet user-secrets set "Authentication:AdminEmails:0" "admin@your-school.edu" --project CampusStudyHub/CampusStudyHub/CampusStudyHub.csproj
dotnet user-secrets set "Authentication:AllowedDomains:0" "your-school.edu" --project CampusStudyHub/CampusStudyHub/CampusStudyHub.csproj
```

Restart the application after changing authentication settings.

### Run the application checks

```powershell
dotnet run --project tests/CampusStudyHub.Checks/CampusStudyHub.Checks.csproj
```

## 6. Project Structure

```text
CampusStudyHub/
|-- CampusStudyHub.slnx                  # Solution file
|-- README.md                           # Project documentation
|-- .github/
|   `-- workflows/ci.yml                 # Automated build and checks
|-- CampusStudyHub/
|   |-- CampusStudyHub/                  # Main ASP.NET Core web application
|   |   |-- Components/
|   |   |   |-- Layout/                  # Shared application layout
|   |   |   |-- Pages/                   # Landing, login, study hub, and legal pages
|   |   |   |-- App.razor                # Root component
|   |   |   `-- Routes.razor             # Routing
|   |   |-- Data/HubDb.cs                # Database context and entities
|   |   |-- Services/HubService.cs       # Business logic and access checks
|   |   |-- Properties/launchSettings.json
|   |   |-- wwwroot/                     # Styles and static assets
|   |   |-- App_Data/                    # Database and uploads (created at runtime)
|   |   |-- Program.cs                  # Services, authentication, and endpoints
|   |   |-- appsettings.json            # Default configuration
|   |   `-- appsettings.Development.json # Development configuration
|   `-- CampusStudyHub.Client/          # Referenced Blazor WebAssembly project
|       |-- Program.cs                  # Client entry point
|       `-- wwwroot/                    # Client configuration and static assets
`-- tests/
    `-- CampusStudyHub.Checks/           # Executable application checks
```
