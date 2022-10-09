# Terminologie

The document specifies common terms and disambiguations, used to refer to certain components and systems exclusively withing this project.

## Instance

Instance refers the the actively used instance of the database, and is represented using a IP-endpoint.

## Driver, and Connector

The term driver encompasses all functionality related to interacting with the instance, in short the entire project. Whereas connector refers to the component that enables communication with the instance using a specific application layer protocol, such as REST via HTTP, or JSON-RPC via websocket.

## Request, Response, and Result

A request is the beginning of a transaction with a database, usually a query of some sort.

While the request originates from the client, the response refers to the entirety of a transaction originating from the database, the transaction can encompass multiple results. A result is an isolated message from the database, the result can be one of the following types `Ok`, `Error`, `TransportError`, and lastly `Unknown`.

| Type             | Description                                                               |
| ---------------- | ------------------------------------------------------------------------- |
| `Ok`             | This result is successful and carries a set of data                       |
| `Error`          | This result of the request was not successful and does not carry data     |
| `TransportError` | The entire transaction could not be made or was rejected during transport |
| `Unknown`        | Not emitted by the driver, the default value for the result               |

## Namespace, Scope, and Client
| ! Concept                                                             |
| --------------------------------------------------------------------- |
| This is not implemented, but a guideline for planning future features |

The **namespace** is the lowest level component, in addition to the instance it defines the namespace within which all transactions exist. A client can be created from the namespace, but in order to manage multiple users and scopes, the **client manager** can be used.

Similar to the namespace the **scope** allows initialization of clients, but not more.

The **client** is a component whose state is defined by the database, and **authentication** to the database. Contrary to a connector, the client cannot interact with the database directly, this is disadvantageous for usage in scripts, but required for multi tenant/user separation in projects using DI, such as ASP.NET APIs.

## Database

Using a name a specific **database** can be obtained from the client. The allows the user access to tables by name.

Every **table** is strong typed. The type represents all relevant data inside the table.
Granular operation, such as updating a single field, can be represented by projecting the type using a forgetful functor.
