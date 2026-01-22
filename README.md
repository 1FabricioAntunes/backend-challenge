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

> **Note on Project Scope**: This project is intentionally designed as a demonstration of knowledge that exceeds the minimum requirements of the challenge. While in day-to-day work I prefer simpler solutions that fit the specific needs, here I sought to showcase a broader range of technologies and architectural patterns that could address more demanding production scenarios. The goal is to demonstrate familiarity with modern cloud-native practices, comprehensive testing strategies, and production-ready observability—even if a simpler approach would suffice for the core requirements.

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

2. **Start services**:

   **Development Mode** (with LocalStack, Prometheus, Grafana):

   ```bash
   docker-compose --profile dev-only up -d --build
   # or use the deploy script
   ./deploy.sh dev
   ```

   **Production Mode** (real AWS services only):

   ```bash
   docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
   # or use the deploy script
   ./deploy.sh prod
   ```

3. **Access the application**:
   - Frontend: <http://localhost:3000>
   - Backend API: <http://localhost:5000>
   - Swagger/OpenAPI: <http://localhost:5000/swagger>
   - **Development tools** (dev mode only):
     - Prometheus: <http://localhost:9090>
     - Grafana: <http://localhost:3001> (admin/admin)
     - LocalStack: <http://localhost:4566>

4. **View logs**:

   ```bash
   docker-compose logs -f api
   ```

5. **Stop services**:

   ```bash
   docker-compose down
   ```

For detailed local development setup without Docker, see [Development Guide](docs/development-guide.md).

For production deployment configuration and AWS setup, see [Production Deployment Guide](docs/production-deployment.md).

## API Reference

The system exposes three main endpoints:

- **POST** `/api/files/v1` — Upload CNAB file for processing
- **GET** `/api/transactions/v1` — Query transactions (supports filtering by store and date)
- **GET** `/api/stores/v1` — List stores with balances

For detailed request/response schemas and interactive testing, visit the Swagger UI at <http://localhost:5000/swagger>.

See [Backend Documentation](docs/backend.md) for comprehensive API details.

## Documentation

Comprehensive documentation organized by audience:

### For Business Stakeholders

- **[Business Rules](docs/business-rules.md)** — CNAB file format, transaction types, processing rules

### For Developers

- **[Architecture](docs/architecture.md)** — System design, components, and interactions
  - See [**Project Structure**](docs/architecture.md#project-structure) for the complete folder layout
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
