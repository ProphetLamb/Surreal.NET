![Build Status](https://github.com/ProphetLamb/Surreal.Net/actions/workflows/build.yml/badge.svg)
[![CodeFactor](https://www.codefactor.io/repository/github/prophetlamb/surreal.net/badge)](https://www.codefactor.io/repository/github/prophetlamb/surreal.net)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/315508e8f6bf4829ab7d5a0467b0c693)](https://www.codacy.com/gh/ProphetLamb/Surreal.Net/dashboard?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=ProphetLamb/Surreal.Net&amp;utm_campaign=Badge_Grade)
[![codecov](https://codecov.io/gh/ProphetLamb/Surreal.Net/branch/master/graph/badge.svg?token=fcndEq1d3w)](https://codecov.io/gh/ProphetLamb/Surreal.Net)
[![All Contributors](https://img.shields.io/badge/all_contributors-5-orange.svg?style=flat-square)](#contributors-)

<!-- PROJECT LOGO -->

  <br />
    <p align="center">
    <img src="img/icon.png" alt="Logo" width="130" height="130">
  </a>
  <h1 align="center">Surreal .NET</h1>
  <p align="center">
    Database driver for SurrealDB available for REST and RPC sessions.
  </p>

<p align="center">
  (unofficial)
</p>

## Table of contents

- [Table of contents](#table-of-contents)
- [About](#about)
	- [Primary NuGet Packages](#primary-nuget-packages)
	- [Documentation](#documentation)
- [Quick-start](#quick-start)
- [Coverage](#coverage)
- [Contributing](#contributing)
- [Contributors ✨](#contributors-)

## About

Surreal .NET is a database driver for [SurrealDB](https://surrealdb.com). The connector can access the database via JSON-RPC as well as REST.

### Primary NuGet Packages

| Name                           | Description                                                                                                                | Nuget                                                                                                                                      |
| ------------------------------ | -------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `SurrealDB.Driver.Rpc`         | Websocket RPC based database driver for SurrealDB                                                                          | [![NuGet Badge](https://buildstats.info/nuget/SurrealDB.Driver.Rpc)](https://www.nuget.org/packages/SurrealDB.Driver.Rpc/)                 |
| `SurrealDB.Driver.Rest`        | REST based database driver for SurrealDB.                                                                                  | [![NuGet Badge](https://buildstats.info/nuget/SurrealDB.Driver.Rpc)](https://www.nuget.org/packages/SurrealDB.Driver.Rest/)                |
| `SurrealDB.Extensions.Service` | Service integration into the [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/?view=aspnetcore-6.0) ecosystem. | [![NuGet Badge](https://buildstats.info/nuget/SurrealDB.Extensions.Service)](https://www.nuget.org/packages/SurrealDB.Extensions.Service/) |

### Documentation

The API Documentation is available [**here**](https://surreal-net.github.io/Surreal.Net/)

## Quick-start

Firstly install [SurrealDB](https://surrealdb.com) on your system. See the [installation instructions](https://surrealdb.com/install):
```bash
# Brew
brew install surrealdb/tap/surreal
# Linux
curl -sSf https://install.surrealdb.com | sh
# Windows - system
choco install surreal --pre
# Windows - user
iwr https://windows.surrealdb.com -useb | iex
```

While Surreal .NET can be registered as a [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/?view=aspnetcore-6.0) service for usage in a [web API](https://github.com/ProphetLamb/Surreal.Net/blob/master/examples/MinimalApi/Controllers/WeatherForecastController.cs), the library can also be included in a [console app](https://github.com/ProphetLamb/Surreal.Net/blob/master/examples/ConsoleRpc/Program.cs).

I highly recommend taking a looksie at the [examples](https://github.com/ProphetLamb/Surreal.Net/tree/master/examples), but for now let's review a basic console app with the RPC library.

```xml
<PackageReference Include="SurrealDB.Driver.Rest" Version="1.0.6" />
```

```csharp
using SurrealDB.Configuration;
using SurrealDB.Driver.Rpc;

// Signin as a namespace, database, or root user;
using DatabaseRpc db = new(Config.Create()
   .WithEndpoint("127.0.0.1:8000")
   .WithDatabase("test")
   .WithNamespace("test")
   .WithBasicAuth("root", "root")
   .WithRpc()
   .Build());

await db.Open();

// Create a new person with a random id
var created = await db.Create("person", new {
    title = "Founder & CEO",
    name = new {
        first = "Tobie",
        last = "Morgan Hitchcock"
    },
    marketing = true,
    identifier = Random.Shared.Next().ToString("x")
});

// Update a person record with a specific id
var updated = await db.Change("person:jamie", new {
    marketing = true,
});

// Select all people records
var poeple = await db.Select("person");

// Perform a custom advanced query
var groups = await db.Query(
    "SELECT marketing, count() FROM type::table($tb) GROUP BY markering",
    new Dictionary<string, object?> {
        ["tb"] = "person"
    }
);

```

## Coverage

[![codecov](https://codecov.io/gh/ProphetLamb/Surreal.Net/branch/master/graphs/sunburst.svg?token=fcndEq1d3w)](https://codecov.io/gh/ProphetLamb/Surreal.Net)

## Contributing

Contributions are what make the open source community such an amazing place to be learn, inspire, and create. Any contributions you make are **greatly appreciated**.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Contributors ✨

Thanks goes to these wonderful people ([emoji key](https://allcontributors.org/docs/en/emoji-key)):

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center"><a href="https://github.com/ProphetLamb"><img src="https://avatars.githubusercontent.com/u/19748542?v=4?s=100" width="100px;" alt=""/><br /><sub><b>ProphetLamb</b></sub></a><br /><a href="https://github.com/ProphetLamb/Surreal.Net/commits?author=ProphetLamb" title="Code">💻</a></td>
      <td align="center"><a href="https://github.com/StephenGilboy"><img src="https://avatars.githubusercontent.com/u/827735?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Stephen Gilboy</b></sub></a><br /><a href="https://github.com/ProphetLamb/Surreal.Net/commits?author=StephenGilboy" title="Code">💻</a></td>
      <td align="center"><a href="https://antoniosbarotsis.github.io/"><img src="https://avatars.githubusercontent.com/u/50240570?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Tony</b></sub></a><br /><a href="https://github.com/ProphetLamb/Surreal.Net/commits?author=AntoniosBarotsis" title="Code">💻</a> <a href="https://github.com/ProphetLamb/Surreal.Net/commits?author=AntoniosBarotsis" title="Documentation">📖</a></td>
      <td align="center"><a href="https://github.com/Du-z"><img src="https://avatars.githubusercontent.com/u/16366766?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Brian Duhs</b></sub></a><br /><a href="https://github.com/ProphetLamb/Surreal.Net/commits?author=Du-z" title="Tests">⚠️</a> <a href="https://github.com/ProphetLamb/Surreal.Net/commits?author=Du-z" title="Code">💻</a> <a href="https://github.com/ProphetLamb/Surreal.Net/issues?q=author%3ADu-z" title="Bug reports">🐛</a> <a href="#ideas-Du-z" title="Ideas, Planning, & Feedback">🤔</a></td>
      <td align="center"><a href="http://siphalor.de/"><img src="https://avatars.githubusercontent.com/u/24505659?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Siphalor</b></sub></a><br /><a href="https://github.com/ProphetLamb/Surreal.Net/commits?author=Siphalor" title="Documentation">📖</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind welcome!
