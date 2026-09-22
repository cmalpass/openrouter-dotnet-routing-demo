# OpenRouter routing policies in .NET 10

This companion application demonstrates how to keep AI model routing, fallback, privacy, and unit-price controls behind a small ASP.NET Core gateway. It accompanies the forthcoming article, *OpenRouter in .NET: Multi-Model Routing, Fallbacks, and Cost Controls*. The article link will be added when it is published.

The project intentionally separates two integration styles:

- `CompatibleChatClientFactory` points the official OpenAI .NET client at OpenRouter and exposes `Microsoft.Extensions.AI.IChatClient` for portable chat operations.
- `OpenRouterHttpGateway` uses explicit JSON contracts for OpenRouter-only fields such as `models`, `provider.sort`, `max_price`, `data_collection`, and `zdr`.
- The `free` policy routes to an OpenRouter `:free` model with a zero price ceiling for live smoke tests and low-stakes development calls.

API credentials never enter a browser or request body. Live mode reads `OPENROUTER_API_KEY` and the separate demo access key from server-side environment variables.

## Prerequisites

- .NET 10 SDK

The first restore needs access to NuGet. After restore, the default first run and test suite require no API key, account, model download, OpenRouter access, or paid inference.

## First run

From the repository root:

```bash
dotnet restore OpenRouterRoutingDemo.sln
dotnet run --project src/OpenRouterRoutingDemo.Api/OpenRouterRoutingDemo.Api.csproj --urls http://127.0.0.1:5088
```

In another terminal:

```bash
curl -fsS http://127.0.0.1:5088/

curl -fsS http://127.0.0.1:5088/api/policies

curl -fsS http://127.0.0.1:5088/api/chat/economy \
  -H 'Content-Type: application/json' \
  --data '{"prompt":"Explain why explicit model routing policies matter."}'
```

The default response identifies `mode` as `simulated`. Chat calls return deterministic, zero-cost responses so the complete application works immediately.

Model discovery is an explicit network operation against OpenRouter's public catalogue and does not require an API key:

```bash
curl -fsS \
  'http://127.0.0.1:5088/api/models?requiresTools=true&sort=pricing-low-to-high&take=10'
```

## Live OpenRouter mode

Create an OpenRouter API key and a separate, high-entropy demo access key. Start the server with live mode explicitly enabled:

```bash
OPENROUTER_API_KEY='your-key' \
OPENROUTER_DEMO_ACCESS_KEY='a-separate-high-entropy-access-key' \
OpenRouter__UseLiveApi=true \
dotnet run --project src/OpenRouterRoutingDemo.Api/OpenRouterRoutingDemo.Api.csproj --urls http://127.0.0.1:5088
```

Then call either integration path:

```bash
# OpenRouter-specific named routing policy
curl -fsS http://127.0.0.1:5088/api/chat/resilient-private \
  -H 'X-Demo-Access-Key: a-separate-high-entropy-access-key' \
  -H 'Content-Type: application/json' \
  --data '{"prompt":"Summarize the tradeoffs of multi-provider routing."}'

# Zero-price development and smoke-test policy
curl -fsS http://127.0.0.1:5088/api/chat/free \
  -H 'X-Demo-Access-Key: a-separate-high-entropy-access-key' \
  -H 'Content-Type: application/json' \
  --data '{"prompt":"Explain the purpose of a routing policy in one paragraph."}'

# Portable OpenAI-compatible IChatClient path
curl -fsS http://127.0.0.1:5088/api/chat-compatible \
  -H 'X-Demo-Access-Key: a-separate-high-entropy-access-key' \
  -H 'Content-Type: application/json' \
  --data '{"prompt":"Explain the adapter pattern in one paragraph."}'
```

Override the compatibility-path model for the current shell without changing source:

```bash
export OpenRouter__CompatibleModel='provider/model-slug'
```

The `free` policy defaults to `inclusionai/ling-3.0-flash-fin:free`. You can select another currently listed OpenRouter free variant without changing source:

```bash
export OpenRouter__FreeModel='provider/model:free'
```

Free variants have no inference charge, but they are not unlimited, guaranteed-available, or automatically private. OpenRouter documents separate rate limits and availability for free variants. Account-wide provider allowlists and guardrails can remove every free endpoint from consideration, which surfaces as a 404 "no endpoints available" response. Validate the selected model and account policy in the [Models API](https://openrouter.ai/docs/guides/overview/models) before a live run.

To opt into OpenRouter app attribution, configure both values before starting the API:

```bash
export OpenRouter__ApplicationUrl='https://your-app.example'
export OpenRouter__ApplicationTitle='Your application name'
```

These headers identify an application to OpenRouter; they do not authenticate the request.

The live endpoints require `X-Demo-Access-Key` and apply a per-IP fixed-window limit of 60 requests per minute. This is a safety boundary for the sample, not a complete production identity or quota system. A public deployment should use the application's normal authentication and authorization scheme, per-user quotas, and an account-level budget.

The named `/api/chat/{policyName}` endpoint applies the routing policy shown in this repository. The `/api/chat-compatible` endpoint demonstrates the portable `IChatClient` path and does not apply OpenRouter-specific routing, privacy, fallback, or price controls. Keep that distinction clear when adapting this sample.

Model availability, prices, and capabilities change. Before enabling a policy in production, validate its configured model IDs with the [OpenRouter Models API](https://openrouter.ai/docs/guides/overview/models).

## Routing policies

`RoutingPolicyCatalog` contains three examples:

- `economy` sorts eligible providers by price, denies provider data collection, and applies a maximum prompt/completion price.
- `resilient-private` uses ordered model fallback, prefers throughput, denies data collection, and requires Zero Data Retention endpoints.
- `free` uses a configurable `:free` model and a zero price ceiling for live validation without paid inference.

These controls are not interchangeable. ZDR prevents an eligible inference provider from retaining the request, but it does not keep data inside your own network or govern your application's logs.

`max_price` caps the eligible provider's price per token. It is not an account-level, daily, tenant, or request-total budget. Add those limits in the application or billing platform that owns them.

## Tests

```bash
dotnet build OpenRouterRoutingDemo.sln --configuration Release
dotnet test OpenRouterRoutingDemo.sln --configuration Release --no-build
```

The 25-test suite contains 11 core unit tests and 14 API integration tests. The core tests verify policy selection, validation, JSON field names, privacy controls, fallback model order, the configurable free policy, and the simulated provider. API tests exercise the running HTTP pipeline through `WebApplicationFactory<Program>`, the typed OpenRouter request/response boundary, live-mode access control and rate limiting, and public model-catalogue mapping through fake upstream handlers.

The test suite never contacts OpenRouter and never consumes credits.

## Authoritative references

- [OpenAI .NET SDK: custom base URL and API key](https://github.com/openai/openai-dotnet#using-a-custom-base-url-and-api-key)
- [Microsoft.Extensions.AI libraries](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- [OpenRouter provider routing](https://openrouter.ai/docs/guides/routing/provider-selection)
- [OpenRouter free model variants](https://openrouter.ai/docs/guides/routing/model-variants/free)
- [OpenRouter model fallbacks](https://openrouter.ai/docs/guides/routing/model-fallbacks)
- [OpenRouter router metadata](https://openrouter.ai/docs/guides/features/router-metadata)
- [OpenRouter usage accounting](https://openrouter.ai/docs/cookbook/administration/usage-accounting)
- [OpenRouter Zero Data Retention](https://openrouter.ai/docs/guides/features/zdr)

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
