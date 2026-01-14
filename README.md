# TransactionProcessor Backend Challenge

Cloud-native, serverless, and asynchronous system for processing CNAB files, persisting transactional data, and exposing query capabilities via APIs and a web frontend.

## Overview

This project demonstrates a production-ready backend solution built with modern architectural patterns and best practices. The system processes CNAB (Brazilian bank interchange) files asynchronously, guarantees transactional integrity, and provides a responsive user experience.

**Key Features:**
- Asynchronous file processing with SQS
- Transactional integrity (all-or-nothing per file)
- Clean Architecture with Domain-Driven Design
- Serverless-ready (AWS Lambda)
- Comprehensive observability and testing

**Tech Stack:**
- **Backend**: .NET 8, ASP.NET Core, FastEndpoints, EF Core, PostgreSQL
- **Frontend**: React 19, TypeScript, Vite
- **Infrastructure**: Docker, LocalStack, AWS (Lambda, S3, SQS, Cognito)
- **Testing**: xUnit, FluentAssertions, Testcontainers

## Quick Start

### Prerequisites

- Docker and Docker Compose
- (Optional) .NET 8 SDK, Node.js 22+, pnpm for local development

### Run with Docker Compose

1. **Clone and navigate to source**:
   ```bash
   git clone <repository-url>
   cd src
   ```

2. **Start all services**:
   ```bash
   docker-compose up --build
   ```

3. **Access the application**:
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:5000
   - Swagger/OpenAPI: http://localhost:5000/swagger

4. **Stop services**:
   ```bash
   docker-compose down
   ```

For detailed local development setup without Docker, see [Development Guide](docs/development-guide.md).

## API Reference

The system exposes three main endpoints:

- **POST** `/api/files/v1/upload` — Upload CNAB file for processing
- **GET** `/api/transactions/v1` — Query transactions (supports filtering by store and date)
- **GET** `/api/stores/v1` — List stores with balances

For detailed request/response schemas and interactive testing, visit the Swagger UI at http://localhost:5000/swagger.

See [Backend Documentation](docs/backend.md) for comprehensive API details.

## Documentation

Comprehensive documentation organized by audience:

### For Business Stakeholders
- **[Business Rules](docs/business-rules.md)** — CNAB file format, transaction types, processing rules

### For Developers
- **[Architecture](docs/architecture.md)** — System design, components, and interactions
- **[Development Guide](docs/development-guide.md)** — Local setup, debugging, and workflows
- **[Backend](docs/backend.md)** — API implementation and patterns
- **[Frontend](docs/frontend.md)** — UI implementation and state management
- **[Database](docs/database.md)** — Schema design and EF Core configuration
- **[Async Processing](docs/async-processing.md)** — SQS workflow and retry mechanisms

### For Technical Reviewers
- **[Security](docs/security.md)** — Authentication, authorization, OWASP compliance
- **[Observability](docs/observability.md)** — Logging, metrics, monitoring
- **[Testing Strategy](docs/testing-strategy.md)** — Unit, integration, E2E tests
- **[Decisions & Trade-offs](docs/decisions-and-tradeoffs.md)** — Architectural decisions and rationale

### Operations
- **[Deployment Guide](docs/deployment.md)** — Local and AWS production deployment
- **[Troubleshooting](docs/troubleshooting.md)** — Common issues and solutions

## Contributing

This is a challenge project. For questions or clarifications, consult the [Decisions and Trade-offs](docs/decisions-and-tradeoffs.md) document.

To contribute:
1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit your changes: `git commit -am 'Add feature'`
4. Push to the branch: `git push origin feature/your-feature`
5. Submit a pull request

## License

MIT License. See LICENSE file for details.

---

This project is part of the TransactionProcessor Backend Challenge and is provided for educational and evaluation purposes.

**Last Updated**: January 14, 2026  
**Version**: 1.0.0
