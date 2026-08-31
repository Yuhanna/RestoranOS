# Local development stack

SQL Server for Restaurant OS API and MVC web.

```bash
docker compose -f infra/docker/docker-compose.dev.yml up -d
```

Default connection (matches `appsettings.Development.json` when using local SQL):

- Server: `localhost,1433`
- Database: `RestaurantOs`
- User: `sa`
- Password: `YourStrong!Passw0rd`

Stop:

```bash
docker compose -f infra/docker/docker-compose.dev.yml down
```
