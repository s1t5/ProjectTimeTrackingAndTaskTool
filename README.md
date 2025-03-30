# 🕒 Project Time Tracking and Task Tool  

**A modern web-based time tracking solution with Kanban board for project management**  

[![Docker](https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white)](#)  
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](#)  
[![SQL Server](https://img.shields.io/badge/Microsoft_SQL_Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)](#)

## ✨ Key Features

### ⏱️ Time Tracking  
- Record project work from the respective employees 
- Project assignment with dropdown selection  
- Stopwatch functionality for precise time recording  
- Manual time adjustment available  

### 📋 Kanban Board  
- Project-specific boards  
- Customizable buckets (Backlog, Open, In Progress, Done)  
- Drag & Drop cards  
- Deadline and priority management 
- Task assignment 

### 📊 Reporting  
- Time period filtered overviews  
- Employee and project summaries  
- CSV export of all time records  

## 🖼️ Screenshots


## 🚀 Quick Start

### Prerequisites  
- [Docker](https://www.docker.com/products/docker-desktop)  
- [Docker Compose](https://docs.docker.com/compose/install/)  
- [.NET Core SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### 🛠️ Installation  

1. Clone the repository:  
```bash
git clone https://github.com/your-repo/time-tracking-system.git
cd time-tracking-system
```

2. Adapt the `docker-compose.yml` and store a secure `SA_PASSWORD`. This Password must also be set in the `initialization script line` as well as the `healthckeck test` line.  Beyond that the password must also be specified in the connection string found in the `appsettings.json` file. 

This password must now have been stored in a total of four locations.

3. Build and start containers:  
```bash
docker-compose up -d --build
```

4. Access the application:  
- Web Interface: http://localhost:80  


## 🐳 Docker Deployment  

| Service | Port | Description |  
|---------|------|-------------|  
| `app` | 80 → 80 | ASP.NET Core Application |  
| `db` | 1433 | SQL Server Database | 

### Environment Variables  

| Variable | Example Value | Description |  
|----------|--------------|-------------|  
| `ASPNETCORE_ENVIRONMENT` | Production | Runtime environment |  

## 🔄 Maintenance  

**Stop containers:**  
```bash 
docker-compose down
```

**Stop and remove volumes (Warning: Deletes data):**  
```bash
docker-compose down -v
```

## 🔐 Security Note  
- 🔒 Change default passwords in `docker-compose.yml`  
- 🔐 Always use HTTPS and authentication provided by a reverse proxy in production  
- 💾 Regular backups of `sql_data` volume recommended  

## 🤝 Contributing  
Contributions welcome! Please open an Issue or Pull Request.

## 🚀 **Roadmap**  
- Translation of the code and the database into English (the project was started in German at the beginning) 
- Improve deployment process

---

📄 *License: GNU GENERAL PUBLIC LICENSE Version 3 (see LICENSE file)*
