# Bounded Context: User Management

## Overview
The `user-management` service acts as a stateless mediator and proxy to **Keycloak**. It handles identity provisioning and lifecycle management but does not maintain its own local persistence layer for identity data.

## Architectural Philosophy
In alignment with the "C4/C5 Refactor", this service implements a simplified clean architecture approach. Because it is essentially a proxy to Keycloak and has no local state or complex domain logic, it eschews a ceremonial `Domain` project. Instead, application logic is centralized in command/query handlers and shared orchestration services (like `UserCreationFlowHandler`).

## Ubiquitous Language

- **Operator**: An administrative user of the UMBRAL platform. Can be active or deactivated. Has an associated profile and permissions managed via Keycloak roles.
- **Participant**: A regular user interacting with the UMBRAL platform. Handled in Keycloak.
- **Keycloak**: The central Identity and Access Management (IAM) provider used as the source of truth for all users.

## Key Components

- **Application Layer (`UserManagement.Application`)**: Contains MediatR Command/Query handlers and the core DTOs (`OperatorDto`, `ParticipantDto`). It orchestrates calls to the Keycloak API.
- **Infrastructure Layer (`UserManagement.Infrastructure`)**: Contains the `KeycloakAdminApiClient` which implements the `IOperatorAdministrationPort`. All HTTP communication with Keycloak happens here.
- **UserCreationFlowHandler**: Centralizes the multi-step process of creating a user in Keycloak (validation, creation, role assignment, and rollback on failure).

## Guardrails
- **No Domain Entities**: Because this service is a proxy, DTOs (`OperatorDto`) act as the primary data structures across all layers. Do not create anemic `Domain` projects or entity models.
- **Statelessness**: This service MUST NOT rely on an underlying database like Entity Framework Core. All data is retrieved from and written directly to Keycloak.
