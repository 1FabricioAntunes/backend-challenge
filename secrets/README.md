# Docker Secrets Directory

This directory contains sensitive configuration values used by Docker Compose services.

## Setup

Create a file named `db_password.txt` containing the PostgreSQL password:

```bash
echo "your_secure_password_here" > secrets/db_password.txt
```

**Important**: Never commit actual secrets to version control. The `.gitignore` file in this directory prevents secrets from being tracked.

## Usage

Docker Compose automatically mounts these secret files into containers at `/run/secrets/`. Services can read the secrets without exposing them in environment variables or command-line arguments.

### Example: PostgreSQL Password

The `db` service uses `POSTGRES_PASSWORD_FILE` to read the password from `/run/secrets/db_password` instead of using `POSTGRES_PASSWORD` environment variable.

## Security Best Practices

1. **Development**: Use simple passwords in local `secrets/` files (already gitignored)
2. **Production**: Use external secret management systems (AWS Secrets Manager, HashiCorp Vault, etc.)
3. **File Permissions**: Restrict access to secret files:

   ```bash
   chmod 600 secrets/db_password.txt
   ```

4. **Rotation**: Rotate secrets regularly and update dependent services

## Available Secrets

- `db_password.txt` - PostgreSQL database password

## Adding New Secrets

1. Create the secret file in this directory
2. Add reference in `docker-compose.yml` under the `secrets:` top-level key
3. Mount the secret in the target service using the `secrets:` service-level key
4. Update this README with usage documentation
