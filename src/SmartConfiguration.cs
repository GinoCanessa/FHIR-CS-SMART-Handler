using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace smart_handler
{
  /// <summary>
  /// Class to deserialize SMART well-known configuration
  /// </summary>
  public class SmartConfiguration
  {
    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint {get;set;}

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint {get;set;}

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public List<string> TokenEndpointAuthMethods {get;set;}

    [JsonPropertyName("registration_endpoint")]
    public string RegistrationEndpoint {get;set;}

    [JsonPropertyName("scopes_supported")]
    public List<string> SupportedScopes {get;set;}

    [JsonPropertyName("response_types_supported")]
    public List<string> SupportedResponseTypes {get;set;}

    [JsonPropertyName("management_endpoint")]
    public string ManagementEndpoint {get;set;}

    [JsonPropertyName("introspection_endpoint")]
    public string IntrospectionEndpoint {get;set;}

    [JsonPropertyName("revocation_endpoint")]
    public string RecovationEndpoint {get;set;}

    [JsonPropertyName("capabilities")]
    public List<string> Capabilities {get;set;}
  }
}