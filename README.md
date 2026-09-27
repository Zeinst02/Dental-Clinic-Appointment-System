# Dental Clinic Appointment System 🦷

A full-stack dental clinic appointment management system built with ASP.NET Core Web API, SQL Server, Entity Framework Core, JWT Authentication, and React.

## 📌 Project Overview

This project is designed to manage dental clinic appointments and provide different features based on the user's role.

The system supports three main roles:

- Admin
- Doctor
- Patient

Each role has access to specific features and operations.

## ✨ Features

### 🔐 Authentication & Authorization

- User login with JWT authentication
- Secure password hashing
- Role-based authorization
- Protected API endpoints
- Different permissions for Admin, Doctor, and Patient

### 👨‍⚕️ Doctor Management

- View doctors
- Create doctors
- Assign medical specialties
- Doctor profile
- Doctor availability management
- Prevent overlapping availability schedules

### 🧑‍🦰 Patient Management

- Patient registration
- Patient profile
- View personal appointments
- Book appointments

### 📅 Appointment Management

- Check appointment availability
- Book appointments
- 30-minute appointment duration
- Prevent double booking
- Appointment confirmation
- Cancel appointments
- Complete appointments
- Validate doctor working hours

### 👑 Admin Management

- Manage users
- Create doctors and patients
- View appointments
- Manage doctor specialties
- Manage doctor availability

## 🛠️ Technologies

### Backend

- C#
- ASP.NET Core Web API
- .NET 8
- Entity Framework Core
- SQL Server
- JWT Authentication
- Swagger / OpenAPI

### Frontend

- React
- TypeScript
- Vite
- CSS

### Tools

- Visual Studio
- Visual Studio Code
- Git
- GitHub

## 🏗️ Project Structure

- Controllers
- DTOs
- Data
- Migrations
- Models
- Services
- Properties
- Program.cs
- appsettings.json
- ClinicAppointmentSystem.csproj
- ClinicAppointmentSystem.sln

## 📸 Screenshots

### Login

<img src="Screenshots/login.png" alt="Login" width="800">

### Admin Dashboard

<img src="Screenshots/admin-dashboard.png" alt="Admin Dashboard" width="800">

### Doctor Dashboard

<img src="Screenshots/doctor-dashboard.png" alt="Doctor Dashboard" width="800">

### Patient Dashboard

<img src="Screenshots/patient-dashboard.png" alt="Patient Dashboard" width="800">

### User Management

<img src="Screenshots/management-user.png" alt="User Management" width="800">

### User Management - Details

<img src="Screenshots/management-user2.png" alt="User Management Details" width="800">

### Doctor Appointments

<img src="Screenshots/doctor-appointment.png" alt="Doctor Appointments" width="800">