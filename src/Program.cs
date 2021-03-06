using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Win32;

namespace smart_handler
{
  class Program
  {
    private const string _clientId = "fhir_demo_id";
    private const string _defaultFhirServerUrl = "https://launch.smarthealthit.org/v/r4/sim/eyJoIjoiMSIsImUiOiJlZmI1ZDRjZS1kZmZjLTQ3ZGYtYWE2ZC0wNWQzNzJmZGI0MDcifQ/fhir/";

    private static string _authCode = string.Empty;
    private static string _clientState = string.Empty;

    private static string _tokenUrl = string.Empty;

    private static string _redirectUrl = string.Empty;

    private static string _fhirServerUrl = string.Empty;
    private const string _uriPrefix = "smartHandler";

    /// <summary>
    /// C# Application to perform FHIR SMART App Launch with a custom URI scheme
    /// </summary>
    /// <param name="configureUriScheme">Flag to ask the application to configure the URI scheme</param>
    /// <param name="fhirServerUrl">FHIR R4 endpoint URL</param>
    /// <param name="launchUrl">URL launched for SMART redirect</param>
    /// <returns></returns>
    public static int Main(
      bool configureUriScheme = false,
      string fhirServerUrl = "",
      string launchUrl = "")
    {
      if (configureUriScheme)
      {
        if (ConfigureUriScheme())
        {
          System.Console.WriteLine("Successfully registered URI scheme");
        }
        else
        {
          System.Console.WriteLine("Failed to configure the URI scheme");
          return -1;
        }

        return 0;
      }

      CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
      CancellationToken cancellationToken = cancellationTokenSource.Token;

      using (Mutex instanceMutex = new Mutex(true, _uriPrefix, out bool mutexWasCreated))
      {
        if (!mutexWasCreated)
        {
          // second instance of the application
          if (string.IsNullOrEmpty(launchUrl))
          {
            System.Console.WriteLine($"No launch url present");
          }
          else
          {
            System.Console.WriteLine($"Launched with url: {launchUrl}");
            WriteToNamedPipe(launchUrl);
          }

          return 0;
        }

        // first (main) instance of the application

        Task pipeReadTask = Task.Run(() => NamedPipeReader(cancellationToken), cancellationToken);

        StartSmartAppLaunch(fhirServerUrl);

        System.Console.WriteLine("Done, press enter to exit.");
        System.Console.ReadLine();
        cancellationTokenSource.Cancel();
      }

      return 0;
  }

    /// <summary>
    /// Start the FHIR SMART App Launch flow
    /// </summary>
    /// <param name="fhirServerUrl"></param>
    private static bool StartSmartAppLaunch(string fhirServerUrl)
    {
      if (string.IsNullOrEmpty(fhirServerUrl))
      {
        fhirServerUrl = _defaultFhirServerUrl;
      }

      System.Console.WriteLine($"  FHIR Server: {fhirServerUrl}");
      _fhirServerUrl = fhirServerUrl;

      if (!TryGetSmartUrls(fhirServerUrl, out string authorizeUrl, out string tokenUrl))
      {
          System.Console.WriteLine($"Failed to discover SMART URLs");
          return false;
      }

      System.Console.WriteLine($"Authorize URL: {authorizeUrl}");
      System.Console.WriteLine($"    Token URL: {tokenUrl}");
      _tokenUrl = tokenUrl;

      _redirectUrl = $"{_uriPrefix}://{Guid.NewGuid().ToString()}/";

      string url = 
          $"{authorizeUrl}" + 
          $"?response_type=code" + 
          $"&client_id={_clientId}" +
          $"&redirect_uri={HttpUtility.UrlEncode(_redirectUrl)}" +
          $"&scope={HttpUtility.UrlEncode("openid fhirUser profile launch/patient patient/*.read")}" +
          $"&state=local_state" +
          $"&aud={fhirServerUrl}";

      LaunchUrl(url);

      return true;
    }

    /// <summary>
    /// Get the SMART configuration URLs from .well-known/smart-configuration
    /// </summary>
    /// <param name="fhirServerUrl"></param>
    /// <param name="authorizeUrl"></param>
    /// <param name="tokenUrl"></param>
    /// <returns></returns>
    private static bool TryGetSmartUrls(string fhirServerUrl, out string authorizeUrl, out string tokenUrl)
    {
      authorizeUrl = string.Empty;
      tokenUrl = string.Empty;

      try
      {
        using (HttpClient client = new HttpClient())
        {
          Uri fhirServerUri = new Uri(fhirServerUrl);
          Uri requestUri = new Uri(fhirServerUri, ".well-known/smart-configuration");

          HttpResponseMessage response = client.GetAsync(requestUri).Result;

          if (response.IsSuccessStatusCode)
          {
            string json = response.Content.ReadAsStringAsync().Result;
            SmartConfiguration smartConfiguration = JsonSerializer.Deserialize<SmartConfiguration>(json);

            authorizeUrl = smartConfiguration.AuthorizationEndpoint;
            tokenUrl = smartConfiguration.TokenEndpoint;

            return true;
          }
        }
      }
      catch (Exception ex)
      {
        System.Console.WriteLine($"Failed to get SMART configuration: {ex.Message}");
      }

      return false;
    }

    /// <summary>
    /// Launch a URL in the user's default web browser.
    /// </summary>
    /// <param name="url"></param>
    /// <returns>true if successful, false otherwise</returns>
    public static bool LaunchUrl(string url)
    {
      try
      {
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
          FileName = url,
          UseShellExecute = true,
        };

        Process.Start(startInfo);
        return true;
      }
      catch (Exception)
      {
          // ignore
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        try
        {
          url = url.Replace("&", "^&");
          Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
          return true;
        }
        catch (Exception)
        {
            // ignore
        }
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        string[] allowedProgramsToRun = { "xdg-open", "gnome-open", "kfmclient" };

        foreach (string helper in allowedProgramsToRun)
        {
          try
          {
            Process.Start(helper, url);
            return true;
          }
          catch (Exception)
          {
            // ignore
          }
        }
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        try
        {
          Process.Start("open", url);
          return true;
        }
        catch (Exception)
        {
          // ignore
        }
      }

      System.Console.WriteLine($"Failed to launch URL");
      return false;
    }

  /// <summary>
  /// Task to create the listening end of a named pipe and read from it.
  /// </summary>
  /// <param name="cancellationToken"></param>
  private static void NamedPipeReader(CancellationToken cancellationToken)
  {
    System.Console.WriteLine($"Setting up pipe: {_uriPrefix}");

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        using (NamedPipeServerStream pipeServerStream = new NamedPipeServerStream(_uriPrefix, PipeDirection.In))
        {
          pipeServerStream.WaitForConnection();

          System.Console.WriteLine("Received pipe connection");

          byte[] buffer = new byte[1024 * 8];
          int bytesRead = pipeServerStream.Read(buffer, 0, buffer.Length);

          if (bytesRead < 1)
          {
            continue;
          }

          string readValue = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
          System.Console.WriteLine($"Received text: {readValue}");
          ParseRedirectUrl(readValue);
        }
      }
      catch (Exception ex)
      {
        System.Console.WriteLine($"Exception reading from pipe: {ex.Message}");
      }
    }
  }

  /// <summary>
  /// Parse the needed query parameters from a redirect URL and continue the app launch flow
  /// </summary>
  /// <param name="url"></param>
  private static void ParseRedirectUrl(string url)
  {
    if (string.IsNullOrEmpty(url))
    {
      return;
    }

    if (!url.StartsWith(_redirectUrl, StringComparison.OrdinalIgnoreCase))
    {
      System.Console.WriteLine($"Invalid request: {url}");
      return;
    }

    int queryIndex = url.IndexOf('?');
    if (queryIndex >= 0)
    {
      string code = string.Empty;
      string state = string.Empty;

      NameValueCollection queryParameters = HttpUtility.ParseQueryString(url.Substring(queryIndex));
      foreach (string key in queryParameters.AllKeys)
      {
        switch (key)
        {
          case "code":
            code = queryParameters.GetValues(key)[0];
            break;

          case "state":
            state = queryParameters.GetValues(key)[0];
            break;
        }
      }

      Task.Run(() => SetAuthCode(code, state));
    }
  }

  /// <summary>
  /// Set the authorization code and state
  /// </summary>
  /// <param name="code"></param>
  /// <param name="state"></param>
  public static async void SetAuthCode(string code, string state)
  {
    _authCode = code;
    _clientState = state;

    System.Console.WriteLine($"Code received: {code}");

    Dictionary<string, string> requestValues = new Dictionary<string, string>()
    {
      { "grant_type", "authorization_code" },
      { "code", code },
      { "redirect_uri", _redirectUrl },
      { "client_id", _clientId },
    };

    HttpRequestMessage request = new HttpRequestMessage()
    {
      Method = HttpMethod.Post,
      RequestUri = new Uri(_tokenUrl),
      Content = new FormUrlEncodedContent(requestValues),
    };

    HttpClient client = new HttpClient();

    HttpResponseMessage response = await client.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      System.Console.WriteLine($"Failed to exchange code for token!");
      throw new Exception($"Unauthorized: {response.StatusCode}");
    }

    string json = await response.Content.ReadAsStringAsync();

    System.Console.WriteLine($"----- Authorization Response -----");
    System.Console.WriteLine(json);
    System.Console.WriteLine($"----- Authorization Response -----");

    SmartResponse smartResponse = JsonSerializer.Deserialize<SmartResponse>(json);

    Task.Run(() => DoSomethingWithToken(smartResponse));
  }

  /// <summary>
  /// Use a SMART token with the FHIR Net API
  /// </summary>
  /// <param name="smartResponse"></param>
  public static void DoSomethingWithToken(SmartResponse smartResponse)
  {
    if (smartResponse == null)
    {
      throw new ArgumentNullException(nameof(smartResponse));
    }

    if (string.IsNullOrEmpty(smartResponse.AccessToken))
    {
      throw new ArgumentNullException("SMART Access Token is required!");
    }

    Hl7.Fhir.Rest.FhirClient fhirClient = new Hl7.Fhir.Rest.FhirClient(
      _fhirServerUrl,
      new Hl7.Fhir.Rest.FhirClientSettings()
      {
        PreferredFormat = Hl7.Fhir.Rest.ResourceFormat.Json,
        PreferredReturn = Hl7.Fhir.Rest.Prefer.ReturnRepresentation,
      },
      new FhirMessageHandler(smartResponse.AccessToken));

    Hl7.Fhir.Model.Patient patient = fhirClient.Read<Hl7.Fhir.Model.Patient>($"Patient/{smartResponse.PatientId}");

    System.Console.WriteLine($"Read back patient: {patient.Name[0].ToString()}");
  }

  /// <summary>
  /// Write a value to our URI Prefix named pipe.
  /// </summary>
  /// <param name="value"></param>
  private static void WriteToNamedPipe(string value)
  {
    using (NamedPipeClientStream pipeClientStream = new NamedPipeClientStream(".", _uriPrefix, PipeDirection.Out))
    {
      pipeClientStream.Connect();

      byte[] buffer = System.Text.Encoding.UTF8.GetBytes(value);
      pipeClientStream.Write(buffer, 0, buffer.Length);
    }
  }

  /// <summary>
  /// Configure a URI scheme for Windows, using the registry
  /// </summary>
  /// <returns>true for success</returns>
  private static bool ConfigureUriScheme()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      string assemblyLocation = Assembly.GetEntryAssembly().Location;

      if (Path.GetExtension(assemblyLocation).Equals(".dll", StringComparison.OrdinalIgnoreCase))
      {
        assemblyLocation = Path.ChangeExtension(assemblyLocation, ".exe");

        if (!File.Exists(assemblyLocation))
        {
          System.Console.WriteLine("Could not find executable, please package as an exe!");
          return false;
        }
      }

      try
      {
        using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(_uriPrefix))
        {
          key.SetValue(string.Empty, "URL:SMART Handler Redirect");
          key.SetValue("URL Protocol", string.Empty);

          using (RegistryKey shellKey = key.CreateSubKey("shell"))
          using (RegistryKey openKey = shellKey.CreateSubKey("open"))
          using (RegistryKey commandKey = openKey.CreateSubKey("command"))
          {
            commandKey.SetValue(string.Empty, $"\"{assemblyLocation}\" --launch-url \"%1\"");
          }
        }

        return true;
      }
      catch (UnauthorizedAccessException authEx)
      {
        System.Console.WriteLine($"Failed to register the URI scheme: {authEx.Message}");
      }
    }
    else
    {
      throw new NotImplementedException($"No URI configuration for this platform: {RuntimeInformation.OSDescription}");
    }

    return false;
    }
  }
}
