using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace smart_handler
{
  /// <summary>
  /// HTTP Message handler for FHIR clients to add authorization headers
  /// </summary>
  public class FhirMessageHandler : DelegatingHandler
  {
    private string _accessToken;

    /// <summary>
    /// Constructor with access token
    /// </summary>
    /// <param name="accessToken"></param>
    public FhirMessageHandler(string accessToken)
    : base()
    {
      _accessToken = accessToken;
      InnerHandler = new HttpClientHandler();
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      request.Headers.Add("Authorization", $"Bearer {_accessToken}");
      return base.Send(request, cancellationToken);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      request.Headers.Add("Authorization", $"Bearer {_accessToken}");
      return base.SendAsync(request, cancellationToken);
    }
  }
}