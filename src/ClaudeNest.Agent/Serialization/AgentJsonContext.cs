using System.Text.Json.Serialization;
using ClaudeNest.Agent.Config;
using ClaudeNest.Shared.Enums;
using ClaudeNest.Shared.Messages;

namespace ClaudeNest.Agent.Serialization;

[JsonSerializable(typeof(DirectoryListingRequest))]
[JsonSerializable(typeof(DirectoryListingResponse))]
[JsonSerializable(typeof(SessionStatusUpdate))]
[JsonSerializable(typeof(StartSessionRequest))]
[JsonSerializable(typeof(StopSessionRequest))]
[JsonSerializable(typeof(AgentInfo))]
[JsonSerializable(typeof(AgentRegistrationResult))]
[JsonSerializable(typeof(List<SessionStatusUpdate>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(SessionState))]
[JsonSerializable(typeof(NestConfig))]
[JsonSerializable(typeof(AgentCredentials))]
[JsonSerializable(typeof(PairingExchangeRequest))]
[JsonSerializable(typeof(PairingExchangeResponse))]
[JsonSerializable(typeof(DeregisterCommand))]
// Primitives used by SignalR method arguments
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
internal partial class AgentJsonContext : JsonSerializerContext;
