# OpenRouter routing policies in .NET 10

This companion application demonstrates how to keep AI model routing, fallback, privacy, and spend controls behind a small ASP.NET Core gateway. It accompanies the article [OpenRouter in .NET: Multi-Model Routing, Fallbacks, and Cost Controls](https://chrismalpass.com/posts/openrouter-dotnet-routing/).

The project intentionally separates two integration styles:

- `CompatibleChatClientFactory` points the official OpenAI .NET client at OpenRouter and exposes `Microsoft.Extensions.AI.IChatClient` for portable chat operations.
- `OpenRouterHttpGateway` uses explicit JSON contracts for OpenRouter-only fields such as `models`, `provider.sort`, `max_price`, `data_collection`, and `zdr`.

API credentials never enter a browser or request body. Live mode reads the key from the server-side `OPENROUTER_API_KEY` environment variable.

## Prerequisites

- .NET 10 SDK

After NuGet restore, no API key, account, model download, or network connection is required for the default first run or test suite.

## First run

From the repository root:

```bash
dotnet restore OpenRouterRoutingDemo.sln
dotnet run --project src/OpenRouterRoutingDemo.Api/OpenRouterRoutingDemo.Api.csproj --urls http://127.0.0.1:5088
```

In another terminal:

```bash
curl -s http://127.0.0.1:5088/

curl -s http://127.0.0.1:5088/api/policies

curl -s http://127.0.0.1:5088/api/chat/economy \
  -H 'Content-Type: application/json' \
  --data '{"prompt":"Explain why explicit model routing policies matter."}'
```

The default response identifies `mode` as `simulated`. Chat calls return deterministic, zero-cost responses so the complete application works immediately.

Model discovery is an explicit network operation against OpenRouter's public catalogue and does not require an API key:

```bash
curl -s \
  'http://127.0.0.1:5088/api/models?requiresTools=true&sort=pricing-low-to-high&take=10'
```

## Live OpenRouter mode

Create an OpenRouter API key and start the server with live mode explicitly enabled:

```bash
OPENROUTER_API_KEY='your-key' \
OpenRouter__UseLiveApi=true \
dotnet run --project src/OpenRouterRoutingDemo.Api/OpenRouterRoutingDemo.Api.csproj --urls http://127.0.0.1:5088
```

Then call either integration path:

```bash
# OpenRouter-specific named routing policy
curl -s http://127.0.0.1:5088/api/chat/resilient-private \
  -H 'Content-Type: application/json' \
  --data '{"prompt":"Summarize the tradeoffs of multi-provider routing."}'

# Portable OpenAI-compatible IChatClient path
curl -s http://127.0.0.1:5088/api/chat-compatible \
  -H 'Content-Type: application/json' \
  --data '{"prompt":"Explain the adapter pattern in one paragraph."}'
```

Override the compatibility-path model without changing source:

```bash
OpenRouter__CompatibleModel='provider/model-slug'
```

Model availability, prices, and capabilities change. Before enabling a policy in production, validate its configured model IDs with the [OpenRouter Models API](https://openrouter.ai/docs/guides/overview/models).

## Routing policies

`RoutingPolicyCatalog` contains two dated examples:

- `economy` sorts eligible providers by price, denies provider data collection, and applies a maximum prompt/completion price.
- `resilient-private` uses ordered model fallback, prefers throughput, denies data collection, and requires Zero Data Retention endpoints.

These controls are not interchangeable. ZDR prevents an eligible inference provider from retaining the request, but it does not keep data inside your own network or govern your application's logs.

## Tests

```bash
dotnet build OpenRouterRoutingDemo.sln --configuration Release
dotnet test OpenRouterRoutingDemo.sln --configuration Release --no-build
```

The core tests verify policy selection, validation, JSON field names, privacy controls, fallback model order, and the simulated provider. API tests exercise the running HTTP pipeline through `WebApplicationFactory<Program>`, the typed OpenRouter request/response boundary, and public model-catalogue mapping through fake upstream handlers.

The test suite never contacts OpenRouter and never consumes credits.

## Repository layout

```text
src/
  OpenRouterRoutingDemo.Core/                  Routing contracts, policies, and simulator
  OpenRouterRoutingDemo.Api/                   Minimal API, live client, and IChatClient factory
tests/
  OpenRouterRoutingDemo.Core.Tests/            Deterministic unit tests
  OpenRouterRoutingDemo.Api.IntegrationTests/  In-process HTTP integration tests
```

## License

MIT licensed to Chris Malpass. See [LICENSE](LICENSE).
