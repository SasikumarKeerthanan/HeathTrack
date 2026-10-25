# 🏥 HealthTrack – Health and Fitness Management System

### ASP.NET MVC 5 | C# | Entity Framework 6 | SQL Server | Bootstrap

HealthTrack is a web-based health and fitness management system developed using ASP.NET MVC 5 and C#. The application provides a centralised platform for managing health-related activities, fitness progress, trainer interactions and administrative operations.

The system supports three user roles: **Administrator, Trainer and Client**, with role-based authentication and authorisation to ensure secure access to relevant functionalities.

## 📌 Project Overview

HealthTrack aims to improve the efficiency of health and fitness management by replacing fragmented manual processes with a structured digital platform.

The application enables clients to manage personal health information and monitor fitness activities, trainers to support client fitness management, and administrators to manage users and monitor system operations.

### 🎯 Project Objectives

- Develop a secure web-based health and fitness management system.
- Implement role-based authentication and authorisation.
- Provide dedicated dashboards for administrators, trainers and clients.
- Enable clients to monitor health and fitness activities.
- Support trainer and client interactions.
- Manage appointments and support requests.
- Provide graphical dashboards and statistical summaries.
- Maintain data integrity through validation and relational database management.

## 🛠️ Technology Stack

| Category             | Technology                   |
| -------------------- | ---------------------------- |
| Programming Language | C#                           |
| Framework            | ASP.NET MVC 5                |
| .NET Framework       | .NET Framework 4.x           |
| ORM                  | Entity Framework 6           |
| Database             | Microsoft SQL Server         |
| Authentication       | ASP.NET Identity             |
| Frontend             | HTML5, CSS3, JavaScript      |
| UI Framework         | Bootstrap                    |
| Architecture         | Model–View–Controller (MVC)  |
| Development IDE      | Visual Studio                |
| Database Management  | SQL Server Management Studio |
| Version Control      | Git and GitHub               |

## 👥 User Roles

### 👨‍💼 Administrator

The administrator manages the overall application and maintains operational control.

**Main functionalities:**

- Secure administrator login.
- View administrative dashboard.
- Monitor total clients and trainers.
- Manage registered user accounts.
- Create administrator accounts.
- Create trainer accounts.
- View appointment statistics.
- Monitor support tickets.
- Manage user roles and account access.
- Maintain administrative audit logs.

### 🏋️ Trainer

The trainer supports clients in managing fitness-related activities.

**Main functionalities:**

- Secure trainer login.
- Access the trainer dashboard.
- View relevant client information.
- Manage assigned fitness-related activities.
- Support appointment management.
- Monitor client fitness progress.

### 👤 Client

The client manages personal health information and fitness activities.

**Main functionalities:**

- Register and log in securely.
- Access the personal dashboard.
- Manage personal profile information.
- Record health-related measurements.
- Track daily water consumption.
- Monitor fitness progress.
- Manage appointment-related activities.
- Access relevant trainer information.
- Submit support requests.

## 🏗️ System Architecture

HealthTrack follows the **Model–View–Controller (MVC)** architectural pattern.

```text
                  USER
                   |
                   v
              WEB BROWSER
                   |
                   v
          ASP.NET MVC APPLICATION
                   |
       +-----------+-----------+
       |           |           |
       v           v           v
     MODEL        VIEW      CONTROLLER
       |                       |
       +-----------+-----------+
                   |
                   v
            BUSINESS LOGIC
                   |
                   v
           ENTITY FRAMEWORK 6
                   |
                   v
             SQL SERVER
                   |
                   v
            HealthTrackDB
```

### Architecture Components

**Model:** Represents application data, database entities and validation rules.

**View:** Provides user interfaces through Razor views, HTML, CSS, JavaScript and Bootstrap.

**Controller:** Processes HTTP requests, executes application logic and returns appropriate views.

**Entity Framework:** Supports database operations through object-relational mapping.

**SQL Server:** Stores user information, health records, appointments and other application data.

## 🔐 Authentication and Security

HealthTrack uses ASP.NET Identity for authentication and role-based authorisation.

The application supports three roles:

```text
Administrator
Trainer
Client
```

Security features include:

- Password-based authentication.
- Role-based access control.
- Authorisation of protected controller actions.
- Server-side input validation.
- Anti-forgery token protection for applicable forms.
- Account management using ASP.NET Identity.
- Administrative activity logging.

## 📊 Dashboard Features

The application provides role-specific dashboards.

### Administrator Dashboard

Displays key operational statistics:

- Total registered clients.
- Total registered trainers.
- Pending appointments.
- Open support tickets.

### Trainer Dashboard

Provides access to trainer-related information and relevant client management activities.

### Client Dashboard

Provides access to personal health tracking and fitness management functionalities.

## 💾 Database

**Database Name:** `HealthTrackDB`

**Database Management System:** Microsoft SQL Server

**ORM:** Entity Framework 6 – Database First

The application uses a relational database structure with primary and foreign key relationships.

ASP.NET Identity manages authentication data, while application-specific entities maintain health and fitness information.

### Database Contexts

```csharp
HealthTrackDBEntities
ApplicationDbContext
```

- `HealthTrackDBEntities` manages application database entities.
- `ApplicationDbContext` manages ASP.NET Identity-related operations.

## 📁 Project Structure

```text
HealthTrack/
│
├── App_Start/
│   ├── BundleConfig.cs
│   ├── FilterConfig.cs
│   ├── IdentityConfig.cs
│   ├── RouteConfig.cs
│   └── Startup.Auth.cs
│
├── Content/
│
├── Controllers/
│   ├── AccountController.cs
│   ├── AdminController.cs
│   └── ...
│
├── Models/
│   ├── IdentityModels.cs
│   └── ...
│
├── Scripts/
│
├── Views/
│   ├── Account/
│   ├── Admin/
│   ├── Shared/
│   └── ...
│
├── Global.asax
├── Startup.cs
├── Web.config
├── packages.config
└── README.md
```

*The directory structure illustrates the main application components. Additional files and directories depend on the implementation.*

## ⚙️ Installation and Setup

### Prerequisites

Before running HealthTrack, install:

1. Visual Studio with ASP.NET and web development support.
2. .NET Framework compatible with the project.
3. Microsoft SQL Server.
4. SQL Server Management Studio (SSMS).
5. Git.

### Step 1: Clone the Repository

```bash
git clone <repository-url>
```

### Step 2: Open the Project

Open the solution file in Visual Studio.

```text
HealthTrack.sln
```

### Step 3: Restore NuGet Packages

In Visual Studio:

```text
Tools → NuGet Package Manager
      → Package Manager Console
```

Restore the packages required by the solution.

```powershell
Update-Package -Reinstall
```

### Step 4: Configure SQL Server

Create or restore the application database:

```sql
CREATE DATABASE HealthTrackDB;
```

**Important:** Creating an empty database is insufficient. The required database schema, tables, relationships and application data must also be created or restored.

### Step 5: Configure Database Connections

Update the connection strings in `Web.config` to match the local SQL Server instance.

Configure both the application database connection and the ASP.NET Identity database connection.

### Step 6: Build the Application

In Visual Studio:

```text
Build → Rebuild Solution
```

Ensure that the application builds successfully.

### Step 7: Run the Application

Start the application using IIS Express.

```text
Ctrl + F5
```

The application opens in the default web browser.

### Step 8: Initialise User Roles

Ensure the database seeding process has been configured and executed to create the required roles and initial administrator account.

**Security Note:** Do not publish real administrator passwords, database credentials or other sensitive configuration values in the GitHub repository.

## 🧪 Software Testing

HealthTrack uses functional testing to verify important application operations.

| Test Case ID | Test Scenario                       |
| ------------ | ----------------------------------- |
| FT-001       | Verify application functionality    |
| FT-002       | Verify application functionality    |
| FT-003       | Verify application functionality    |
| FT-004       | Verify application functionality    |
| FT-005       | Verify client water intake tracking |

Testing activities cover:

- Authentication and authorisation.
- User input validation.
- Database operations.
- Role-based access.
- Dashboard functionality.
- Health tracking operations.
- Error handling.
- User interface navigation.

## 💡 Software Engineering Principles

The development of HealthTrack incorporates established software engineering principles.

### Object-Oriented Programming

The application uses C# classes, objects and appropriate object-oriented design principles.

### MVC Architecture

Separation of models, views and controllers improves maintainability and code organisation.

### Data Validation

Input validation helps prevent invalid information from being stored in the database.

### Exception Handling

Exception handling supports the controlled management of application errors.

### Database Integrity

Primary keys, foreign keys and relational constraints support consistency across stored records.

### User Interface Design

Bootstrap and responsive design principles support usability across different screen sizes.

## 🚀 Future Enhancements

Potential future improvements include:

- Mobile application integration.
- AI-based personalised fitness recommendations.
- Advanced health analytics.
- Automated appointment reminders.
- Integration with wearable fitness devices.
- Expanded progress visualisation.
- Enhanced notification management.

## 👨‍💻 Development Team

| Team Member    |
| -------------- |
| S. Keerthanan  |
| S. Kithurshan  |
| M. M. Zainab   |
| P. Shangethana |

## 🎓 Academic Information

**Project Name:** HealthTrack – Health and Fitness Management System

**Project Type:** Academic Software Development Project

**Development Platform:** ASP.NET MVC 5

**Institution:** ESOFT Metro Campus

## 📄 Disclaimer

This application was developed for academic and educational purposes.

HealthTrack is intended to demonstrate software engineering concepts, database integration, authentication, role-based authorisation and web application development.

The application is not intended to replace professional medical advice, diagnosis or treatment.

---

**HealthTrack – Supporting Health and Fitness Management Through Technology.**
