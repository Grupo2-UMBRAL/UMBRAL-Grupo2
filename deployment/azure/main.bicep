targetScope = 'resourceGroup'

@description('Azure region for all demo resources.')
param location string = resourceGroup().location

@description('Neon PostgreSQL host used by Keycloak and the demo services.')
param postgresHost string

@description('Neon PostgreSQL database used by Keycloak and the demo services.')
param postgresDatabase string

@description('Neon PostgreSQL user used by Keycloak and the demo services.')
param postgresUser string

@description('Short prefix used for resource names and tags.')
param resourcePrefix string = 'umbral-demo'

@description('Azure Container Apps environment name.')
param containerAppsEnvironmentName string = 'cae-umbral-demo'

@description('Globally unique Azure Container Registry name. Use lowercase letters and numbers only.')
param containerRegistryName string

@description('Public HTTPS base URL exposed by the Edge Proxy. Do not include a trailing slash.')
param publicBaseUrl string

@description('Immutable image tag pushed by CI, usually the git SHA.')
param imageTag string

@description('Set false for the first bootstrap deployment that only creates shared infrastructure and ACR.')
param deployContainerApps bool = true

@secure()
@description('Neon PostgreSQL password used by Keycloak and all demo services.')
param postgresPassword string

@secure()
@description('RabbitMQ default user password.')
param rabbitMqPassword string

@secure()
@description('Keycloak bootstrap admin password.')
param keycloakAdminPassword string

@secure()
@description('Password used by User Management to call Keycloak admin APIs.')
param userManagementKeycloakAdminPassword string

var tags = {
  project: 'umbral'
  workload: 'academic-demo'
}

var rabbitMqUser = 'umbral'
var keycloakRealm = 'umbral'
var keycloakAdminUser = 'admin'
var authAuthority = '${publicBaseUrl}/auth/realms/${keycloakRealm}'
var requireHttpsMetadata = 'true'
var registryLoginServer = '${containerRegistryName}.azurecr.io'

var imageNames = {
  rabbitmq: 'rabbitmq:3.13-management-alpine'
  keycloak: '${registryLoginServer}/umbral/keycloak:${imageTag}'
  userManagement: '${registryLoginServer}/umbral/user-management:${imageTag}'
  missionManagement: '${registryLoginServer}/umbral/mission-management:${imageTag}'
  sessionManagement: '${registryLoginServer}/umbral/session-management:${imageTag}'
  scoringMonitoring: '${registryLoginServer}/umbral/scoring-monitoring:${imageTag}'
  web: '${registryLoginServer}/umbral/web:${imageTag}'
  edgeProxy: '${registryLoginServer}/umbral/edge-proxy:${imageTag}'
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${resourcePrefix}-logs'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvironmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

var acrCredentials = registry.listCredentials()
var acrUsername = acrCredentials.username
var acrPassword = acrCredentials.passwords[0].value

var registryConfig = [
  {
    server: registry.properties.loginServer
    username: acrUsername
    passwordSecretRef: 'acr-password'
  }
]

resource rabbitmq 'Microsoft.App/containerApps@2024-03-01' = if (deployContainerApps) {
  name: 'rabbitmq'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: [
        {
          name: 'rabbitmq-password'
          value: rabbitMqPassword
        }
      ]
      ingress: {
        external: false
        targetPort: 5672
        exposedPort: 5672
        transport: 'tcp'
      }
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'rabbitmq'
          image: imageNames.rabbitmq
          env: [
            {
              name: 'RABBITMQ_DEFAULT_USER'
              value: rabbitMqUser
            }
            {
              name: 'RABBITMQ_DEFAULT_PASS'
              secretRef: 'rabbitmq-password'
            }
            {
              // Pinned so the node name survives revision changes, which rotate the hostname.
              name: 'RABBITMQ_NODENAME'
              value: 'rabbit@localhost'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

resource keycloak 'Microsoft.App/containerApps@2024-03-01' = if (deployContainerApps) {
  name: 'keycloak'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registryConfig
      secrets: [
        {
          name: 'acr-password'
          value: acrPassword
        }
        {
          name: 'keycloak-admin-password'
          value: keycloakAdminPassword
        }
        {
          name: 'postgres-password'
          value: postgresPassword
        }
      ]
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'keycloak'
          image: imageNames.keycloak
          command: [
            '/opt/keycloak/bin/kc.sh'
          ]
          args: [
            'start'
            '--import-realm'
            '--http-relative-path=/auth'
            '--hostname=${publicBaseUrl}/auth'
            '--hostname-backchannel-dynamic=true'
          ]
          env: [
            {
              name: 'KEYCLOAK_ADMIN'
              value: keycloakAdminUser
            }
            {
              name: 'KEYCLOAK_ADMIN_PASSWORD'
              secretRef: 'keycloak-admin-password'
            }
            {
              name: 'KC_DB'
              value: 'postgres'
            }
            {
              name: 'KC_DB_URL'
              value: 'jdbc:postgresql://${postgresHost}:5432/${postgresDatabase}?sslmode=require'
            }
            {
              name: 'KC_DB_USERNAME'
              value: postgresUser
            }
            {
              name: 'KC_DB_PASSWORD'
              secretRef: 'postgres-password'
            }
            {
              name: 'KC_HTTP_ENABLED'
              value: 'true'
            }
            {
              name: 'KC_HEALTH_ENABLED'
              value: 'true'
            }
            {
              name: 'KC_PROXY_HEADERS'
              value: 'xforwarded'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

resource userManagement 'Microsoft.App/containerApps@2024-03-01' = if (deployContainerApps) {
  name: 'user-management-service'
  location: location
  tags: tags
  dependsOn: [
    keycloak
  ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registryConfig
      secrets: [
        {
          name: 'acr-password'
          value: acrPassword
        }
        postgresConnectionSecrets[0]
        {
          name: 'keycloak-admin-password'
          value: userManagementKeycloakAdminPassword
        }
      ]
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'user-management-service'
          image: imageNames.userManagement
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__Postgres'
              secretRef: 'postgres-connection-user-management'
            }
            {
              name: 'Auth__Authority'
              value: authAuthority
            }
            {
              name: 'Auth__RequireHttpsMetadata'
              value: requireHttpsMetadata
            }
            {
              name: 'Auth__Audience'
              value: 'umbral-user-management-api'
            }
            {
              name: 'Persistence__ApplyMigrationsOnStartup'
              value: 'true'
            }
            {
              name: 'UserManagement__Keycloak__BaseUrl'
              value: 'http://keycloak/auth'
            }
            {
              name: 'UserManagement__Keycloak__Realm'
              value: keycloakRealm
            }
            {
              name: 'UserManagement__Keycloak__AdminRealm'
              value: 'master'
            }
            {
              name: 'UserManagement__Keycloak__AdminClientId'
              value: 'admin-cli'
            }
            {
              name: 'UserManagement__Keycloak__AdminUsername'
              value: keycloakAdminUser
            }
            {
              name: 'UserManagement__Keycloak__AdminPassword'
              secretRef: 'keycloak-admin-password'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

resource missionManagement 'Microsoft.App/containerApps@2024-03-01' = if (deployContainerApps) {
  name: 'mission-management-service'
  location: location
  tags: tags
  dependsOn: [
    keycloak
  ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registryConfig
      secrets: [
        {
          name: 'acr-password'
          value: acrPassword
        }
        postgresConnectionSecrets[1]
      ]
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'mission-management-service'
          image: imageNames.missionManagement
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__Postgres'
              secretRef: 'postgres-connection-mission-management'
            }
            {
              name: 'Auth__Authority'
              value: authAuthority
            }
            {
              name: 'Auth__RequireHttpsMetadata'
              value: requireHttpsMetadata
            }
            {
              name: 'Auth__Audience'
              value: 'umbral-mission-management-api'
            }
            {
              name: 'Persistence__ApplyMigrationsOnStartup'
              value: 'true'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

resource sessionManagement 'Microsoft.App/containerApps@2024-03-01' = if (deployContainerApps) {
  name: 'session-management-service'
  location: location
  tags: tags
  dependsOn: [
    rabbitmq
    keycloak
    scoringMonitoring
  ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registryConfig
      secrets: [
        {
          name: 'acr-password'
          value: acrPassword
        }
        postgresConnectionSecrets[2]
        {
          name: 'rabbitmq-password'
          value: rabbitMqPassword
        }
      ]
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'session-management-service'
          image: imageNames.sessionManagement
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__Postgres'
              secretRef: 'postgres-connection-session-management'
            }
            {
              name: 'Auth__Authority'
              value: authAuthority
            }
            {
              name: 'Auth__RequireHttpsMetadata'
              value: requireHttpsMetadata
            }
            {
              name: 'Auth__Audience'
              value: 'umbral-session-management-api'
            }
            {
              name: 'Persistence__ApplyMigrationsOnStartup'
              value: 'true'
            }
            {
              name: 'RabbitMQ__Host'
              value: 'rabbitmq'
            }
            {
              name: 'RabbitMQ__Port'
              value: '5672'
            }
            {
              name: 'RabbitMQ__User'
              value: rabbitMqUser
            }
            {
              name: 'RabbitMQ__Password'
              secretRef: 'rabbitmq-password'
            }
            {
              name: 'ScoringMonitoring__BaseUrl'
              value: 'http://scoring-monitoring-service/'
            }
            {
              name: 'SignalR__Enabled'
              value: 'true'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

resource scoringMonitoring 'Microsoft.App/containerApps@2024-03-01' = if (deployContainerApps) {
  name: 'scoring-monitoring-service'
  location: location
  tags: tags
  dependsOn: [
    rabbitmq
    keycloak
  ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registryConfig
      secrets: [
        {
          name: 'acr-password'
          value: acrPassword
        }
        postgresConnectionSecrets[3]
        {
          name: 'rabbitmq-password'
          value: rabbitMqPassword
        }
      ]
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'scoring-monitoring-service'
          image: imageNames.scoringMonitoring
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__Postgres'
              secretRef: 'postgres-connection-scoring-monitoring'
            }
            {
              name: 'Auth__Authority'
              value: authAuthority
            }
            {
              name: 'Auth__RequireHttpsMetadata'
              value: requireHttpsMetadata
            }
            {
              name: 'Auth__Audience'
              value: 'umbral-scoring-monitoring-api'
            }
            {
              name: 'Persistence__ApplyMigrationsOnStartup'
              value: 'true'
            }
            {
              name: 'RabbitMQ__Host'
              value: 'rabbitmq'
            }
            {
              name: 'RabbitMQ__Port'
              value: '5672'
            }
            {
              name: 'RabbitMQ__User'
              value: rabbitMqUser
            }
            {
              name: 'RabbitMQ__Password'
              secretRef: 'rabbitmq-password'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = if (deployContainerApps) {
  name: 'web'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registryConfig
      secrets: [
        {
          name: 'acr-password'
          value: acrPassword
        }
      ]
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'web'
          image: imageNames.web
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
    }
  }
}

resource edgeProxy 'Microsoft.App/containerApps@2024-03-01' = if (deployContainerApps) {
  name: 'edge-proxy'
  location: location
  tags: tags
  dependsOn: [
    userManagement
    missionManagement
    sessionManagement
    scoringMonitoring
    keycloak
    web
  ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registryConfig
      secrets: [
        {
          name: 'acr-password'
          value: acrPassword
        }
      ]
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'edge-proxy'
          image: imageNames.edgeProxy
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'AllowedOrigins__0'
              value: publicBaseUrl
            }
            {
              name: 'ReverseProxy__Clusters__user-management__Destinations__primary__Address'
              value: 'http://user-management-service/'
            }
            {
              name: 'ReverseProxy__Clusters__mission-management__Destinations__primary__Address'
              value: 'http://mission-management-service/'
            }
            {
              name: 'ReverseProxy__Clusters__session-management__Destinations__primary__Address'
              value: 'http://session-management-service/'
            }
            {
              name: 'ReverseProxy__Clusters__scoring-monitoring__Destinations__primary__Address'
              value: 'http://scoring-monitoring-service/'
            }
            {
              name: 'ReverseProxy__Clusters__keycloak__Destinations__primary__Address'
              value: 'http://keycloak/'
            }
            {
              name: 'ReverseProxy__Clusters__web__Destinations__primary__Address'
              value: 'http://web/'
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
    }
  }
}

var postgresConnectionSecrets = [
  {
    name: 'postgres-connection-user-management'
    value: 'Host=${postgresHost};Port=5432;Database=${postgresDatabase};Username=${postgresUser};Password=${postgresPassword};SSL Mode=Require;Search Path=user_management'
  }
  {
    name: 'postgres-connection-mission-management'
    value: 'Host=${postgresHost};Port=5432;Database=${postgresDatabase};Username=${postgresUser};Password=${postgresPassword};SSL Mode=Require;Search Path=mission_management'
  }
  {
    name: 'postgres-connection-session-management'
    value: 'Host=${postgresHost};Port=5432;Database=${postgresDatabase};Username=${postgresUser};Password=${postgresPassword};SSL Mode=Require;Search Path=session_management'
  }
  {
    name: 'postgres-connection-scoring-monitoring'
    value: 'Host=${postgresHost};Port=5432;Database=${postgresDatabase};Username=${postgresUser};Password=${postgresPassword};SSL Mode=Require;Search Path=scoring_monitoring'
  }
]

output acrLoginServer string = registry.properties.loginServer
output expectedPublicBaseUrl string = publicBaseUrl
