SELECT 'CREATE DATABASE keycloak'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak')\gexec

SELECT 'CREATE DATABASE campaigns_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'campaigns_db')\gexec

SELECT 'CREATE DATABASE social_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'social_db')\gexec

SELECT 'CREATE DATABASE notifications_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'notifications_db')\gexec
